using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Interactivity;

namespace CompanionBot
{
    public class MessageQueue
    {
        private readonly IDiscordClient _client;
        private readonly Logger _logger;
        private static InteractivityService _inter;
        private readonly Dictionary<ulong, CancellationTokenSource> tokens = new Dictionary<ulong, CancellationTokenSource>();
        private readonly Dictionary<ulong, Queue<string>> queues = new Dictionary<ulong, Queue<string>>();
        private readonly Dictionary<ulong, Task> workers = new Dictionary<ulong, Task>();
        private readonly GuildSettings _settings;

        public MessageQueue(DiscordSocketClient client, InteractivityService interactive, Logger logger, GuildSettings settings)
        {
            _client = client;
            _inter = interactive;
            _logger = logger;
            _settings = settings;
        }

        public Task Enqueue(ICommandContext context, List<string> commands)
        {
            if (queues.ContainsKey(context.Guild.Id))
            {
                var queue = queues[context.Guild.Id];
                foreach (string command in commands)
                {
                    queue.Enqueue(command);
                }
            }
            else
            {
                queues.Add(context.Guild.Id, new Queue<string>(commands));
            }

            if (!workers.ContainsKey(context.Guild.Id))
            {
                CancellationTokenSource source = new CancellationTokenSource();
                tokens[context.Guild.Id] = source;
                workers.Add(context.Guild.Id, Work(context.Guild.Id, context.Channel, source.Token));
            }
            return Task.CompletedTask;
        }

        public Task Pause(ICommandContext context)
        {
            if (workers.ContainsKey(context.Guild.Id))
            {
                if(tokens.TryGetValue(context.Guild.Id, out CancellationTokenSource token))
                {
                    token.Cancel(); // Disposing is done in the Task itself when it is canceled or completed.
                    context.Channel.SendMessageAsync("Successfully paused!");
                }
                else
                {
                    _logger.Log(new LogMessage(LogSeverity.Error, this.GetType().Name, $"Task running for Guild {context.Guild.Id}, but no Cancellation Token was present!"));
                    context.Channel.SendMessageAsync("Error while pausing!");
                }
            }
            else
            {
                _logger.Log(new LogMessage(LogSeverity.Info, this.GetType().Name, $"Pause failed in Guild {context.Guild.Id}: No Bulk Export was running."));
                context.Channel.SendMessageAsync("Nothing to Pause here, no Bulk Export running at the moment!");
            }
            return Task.CompletedTask;
        }

        public Task Resume(ICommandContext context)
        {
            if (!workers.ContainsKey(context.Guild.Id))
            {
                if (queues.ContainsKey(context.Guild.Id) && queues[context.Guild.Id].Any())
                {
                    CancellationTokenSource source = new CancellationTokenSource();
                    tokens[context.Guild.Id] = source;
                    workers.Add(context.Guild.Id, Work(context.Guild.Id, context.Channel, source.Token));
                }
                else
                {
                    context.Channel.SendMessageAsync("No Data to Export present!");
                    _logger.Log(new LogMessage(LogSeverity.Info, this.GetType().Name, $"Resume failed in Guild {context.Guild.Id}: No Data was present."));
                }
            }
            else
            {
                context.Channel.SendMessageAsync("Bulk Export is already running, no need to Resume!");
                _logger.Log(new LogMessage(LogSeverity.Info, this.GetType().Name, $"Resume failed in Guild {context.Guild.Id}: Bulk Export was already running."));
            }
            return Task.CompletedTask;
        }

        private async Task Work(ulong guildId, IMessageChannel invokedChannel, CancellationToken token)
        {
            if (queues.TryGetValue(guildId, out Queue<string> queue))
            {
                RequestOptions options = RequestOptions.Default;
                options.RetryMode = RetryMode.RetryRatelimit;

                var typingOptions = RequestOptions.Default;
                typingOptions.RetryMode = RetryMode.AlwaysFail;

                if (_settings[guildId].PNavChannel != null) {
                    IMessageChannel channel = await _client.GetChannelAsync((ulong)_settings[guildId].PNavChannel) as IMessageChannel;

                    if(channel == null)
                    {
                        await _logger.Log(new LogMessage(LogSeverity.Error, this.GetType().Name, $"Mod-Channel was no Message Channel for Guild {guildId}!"));
                        await invokedChannel.SendMessageAsync($"There was a problem with the mod-channel! try to run `{_settings[guildId].Prefix}set mod-channel` and then `{_settings[guildId].Prefix}resume` to try again!");
                        return;
                    }

                    while (queue.Count > 0)
                    {
                        if (token.IsCancellationRequested)
                        {
                            workers.Remove(guildId);
                            tokens.Remove(guildId, out CancellationTokenSource s);
                            s.Dispose();
                            return;
                        }
                        string current = queue.Dequeue();
                        IDisposable typing = channel.EnterTypingState(typingOptions);
                        var t = channel.SendMessageAsync(current, false, null, options);
                        // wait for PokeNav to respond...
                        var Result = await _inter.NextMessageAsync(x => x.Author.Id == 428187007965986826 && x.Channel.Id == channel.Id && x.Embeds.Count == 1 && (x.Content == "The following poi has been created for use in your community:" || x.Embeds.First().Title == "Error"), null, TimeSpan.FromSeconds(10));
                        await t;
                        if (Result.IsSuccess == false)
                        {
                            await channel.SendMessageAsync("PokeNav did not respond in time, please try again by Hand!", false, null, options);
                            await _logger.Log(new LogMessage(LogSeverity.Info, this.GetType().Name, $"PokeNav did not respond within 10 seconds in Guild {guildId}."));
                        }
                        typing.Dispose();
                    }
                }
                else
                {
                    await invokedChannel.SendMessageAsync($"PokeNav Moderation Channel not set yet! Run `{_settings[guildId].Prefix}set mod-channel` to set it, then run `{_settings[guildId].Prefix}resume` to create the PoI!");
                    await _logger.Log(new LogMessage(LogSeverity.Info, this.GetType().Name, $"Execution failed in Guild {guildId}: Mod-Channel was not set!"));
                }
                workers.Remove(guildId);
                tokens.Remove(guildId, out CancellationTokenSource source);
                source.Dispose();
            }
        }
    }
}
