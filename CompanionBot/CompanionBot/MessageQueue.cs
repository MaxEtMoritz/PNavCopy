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
        private readonly Dictionary<ulong, Queue<string>> createQueues = new Dictionary<ulong, Queue<string>>();
        private readonly Dictionary<ulong, Queue<EditData>> editQueues = new Dictionary<ulong, Queue<EditData>>();
        private readonly Dictionary<ulong, Task> workers = new Dictionary<ulong, Task>();
        private readonly GuildSettings _settings;

        public MessageQueue(DiscordSocketClient client, InteractivityService interactive, Logger logger, GuildSettings settings)
        {
            _client = client;
            _inter = interactive;
            _logger = logger;
            _settings = settings;
        }

        public Task EnqueueCreate(ICommandContext context, List<string> commands)
        {
            if (createQueues.ContainsKey(context.Guild.Id))
            {
                var queue = createQueues[context.Guild.Id];
                foreach (string command in commands)
                {
                    queue.Enqueue(command);
                }
            }
            else
            {
                createQueues.Add(context.Guild.Id, new Queue<string>(commands));
            }

            if (!workers.ContainsKey(context.Guild.Id) || workers[context.Guild.Id].IsCompleted)
            {
                CancellationTokenSource source = new CancellationTokenSource();
                tokens[context.Guild.Id] = source;
                workers.Add(context.Guild.Id, Work(context.Guild.Id, context.Channel, source.Token));
            }
            return Task.CompletedTask;
        }

        public Task EnqueueEdit(ICommandContext context, List<EditData> edits)
        {
            if (editQueues.ContainsKey(context.Guild.Id))
            {
                var queue = editQueues[context.Guild.Id];
                foreach (EditData data in edits)
                {
                    queue.Enqueue(data);
                }
            }
            else
            {
                editQueues.Add(context.Guild.Id, new Queue<EditData>(edits));
            }
            if (!workers.ContainsKey(context.Guild.Id) || workers[context.Guild.Id].IsCompleted)
            {
                CancellationTokenSource source = new CancellationTokenSource();
                tokens[context.Guild.Id] = source;
                workers[context.Guild.Id] = Edit(context.Guild.Id, context.Channel, source.Token);
            }
            return Task.CompletedTask;
        }

        public Task Pause(ICommandContext context)
        {
            if (workers.ContainsKey(context.Guild.Id) && !workers[context.Guild.Id].IsCompleted)
            {
                if (tokens.TryGetValue(context.Guild.Id, out CancellationTokenSource token))
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
            if (!workers.ContainsKey(context.Guild.Id) || workers[context.Guild.Id].IsCompleted)
            {
                if (createQueues.ContainsKey(context.Guild.Id) && createQueues[context.Guild.Id].Any())
                {
                    CancellationTokenSource source = new CancellationTokenSource();
                    tokens[context.Guild.Id] = source;
                    workers.Add(context.Guild.Id, Work(context.Guild.Id, context.Channel, source.Token));
                }
                else if (editQueues.ContainsKey(context.Guild.Id)&&editQueues[context.Guild.Id].Any())
                {
                    CancellationTokenSource source = new CancellationTokenSource();
                    tokens[context.Guild.Id] = source;
                    workers.Add(context.Guild.Id, Edit(context.Guild.Id, context.Channel, source.Token));
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
            if (createQueues.TryGetValue(guildId, out Queue<string> queue))
            {
                RequestOptions options = RequestOptions.Default;
                options.RetryMode = RetryMode.RetryRatelimit;

                var typingOptions = RequestOptions.Default;
                typingOptions.RetryMode = RetryMode.AlwaysFail;

                if (_settings[guildId].PNavChannel != null)
                {
                    IMessageChannel channel = await _client.GetChannelAsync((ulong)_settings[guildId].PNavChannel) as IMessageChannel;

                    if (channel == null)
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
            }

            if (editQueues.ContainsKey(guildId) && editQueues[guildId].Any())
            {
                workers[guildId] = Edit(guildId, invokedChannel, token); // proceed with edits after creation is complete if there are any.
            }
            else
            {
                workers.Remove(guildId);
                tokens.Remove(guildId, out CancellationTokenSource source);
                source.Dispose();
            }
        }

        private async Task Edit(ulong guildId, IMessageChannel invokedChannel, CancellationToken token)
        {
            if (editQueues.TryGetValue(guildId, out var queue))
            {
                if (_settings[guildId].PNavChannel != null)
                {
                    IMessageChannel channel = await _client.GetChannelAsync((ulong)_settings[guildId].PNavChannel) as IMessageChannel;

                    if (channel == null)
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
                        EditData current = queue.Dequeue();
                        string type = (current.t <= 0 ?"stop":"gym");
                        var t = channel.SendMessageAsync($"{_settings[guildId].PNavPrefix}{type}-info {current.n}");
                        var result = await _inter.NextMessageAsync((msg) => msg.Author.Id == 428187007965986826 && msg.Channel.Id == channel.Id && msg.Embeds.Count == 1 && (msg.Embeds.First().Title.Equals(current.n,StringComparison.OrdinalIgnoreCase) || msg.Embeds.First().Title == "Error"), timeout: TimeSpan.FromSeconds(10));
                        await t;
                        if (result.IsSuccess)
                        {
                            Embed embed = result.Value.Embeds.First();
                            if (embed.Title == "Error")
                            {
                                await channel.SendMessageAsync("Edit Failed! PoI not found!");
                                continue;
                            }
                            else if(embed.Title== "Select Location")
                            {
                                // TODO handle the Location Select Dialog or skip this edit!

                                // Possibilities to decide: case sensitivity of the name, Coordinates (would require additional data from the script, google maps link on the names), presence of quotes or ticks in the name, underscore or space, maybe more...

                                // or let the User decide and react like a user did.
                            }
                            string text = embed.Footer.Value.Text.Split('\u25AB')[2];
                            if(!uint.TryParse(text.Substring(2),out uint id))
                            {
                                await channel.SendMessageAsync("Error: Parsing of Location ID failed!");
                                await _logger.Log(new LogMessage(LogSeverity.Error, this.GetType().Name, $"Parsing of location Id failed in Guild {guildId}! Embed had the Footer {text}!"));
                                continue;
                            }
                            string editString = $"{_settings[guildId].PNavPrefix}update poi {id}";
                            foreach (var item in current.e)
                            {
                                string newEdit = " \"";
                                switch (item.Key)
                                {
                                    case 'n':
                                        newEdit += $"name: {item.Value}\"";
                                        break;
                                    case 't':
                                        newEdit += "type: ";
                                        if(!Enum.TryParse(item.Value, out LocationType locationType))
                                        {
                                            await channel.SendMessageAsync($"Error: Unknown Location Type number {item.Value}");
                                            await _logger.Log(new LogMessage(LogSeverity.Info, this.GetType().Name, $"unknown Location Type Number in Guild {guildId}: {item.Value}"));
                                        }
                                        newEdit += locationType + "\"";
                                        break;
                                    case 'a':
                                        newEdit += $"latitude: {item.Value}\"";
                                        break;
                                    case 'o':
                                        newEdit += $"longitude: {item.Value}\"";
                                        break;
                                    case 'e':
                                        newEdit += $"ex_eligible: {item.Value}\"";
                                        break;
                                    default:
                                        await _logger.Log(new LogMessage(LogSeverity.Error, this.GetType().Name, $"Unknown Edits Key in Guild {guildId}: '{item.Key}' (Value {item.Value})!"));
                                        await channel.SendMessageAsync($"Error: Unknown Edit Key '{item.Key}'!");
                                        continue;
                                }
                                editString += newEdit;
                            }
                            t = channel.SendMessageAsync(editString);
                            result = await _inter.NextMessageAsync((msg) => msg.Author.Id == 428187007965986826 && msg.Channel.Id == channel.Id && msg.Embeds.Count == 1, timeout: TimeSpan.FromSeconds(10));
                            await t;
                            if (!result.IsSuccess)
                            {
                                //no response in timeout!
                                await channel.SendMessageAsync($"PokeNav did not respond in time! maybe you have not set the right PokeNav Prefix? run `{_settings[guildId].Prefix}set pokenav-prefix` to correct it, then `{_settings[guildId].Prefix}resume`!");
                                await _logger.Log(new LogMessage(LogSeverity.Info, this.GetType().Name, $"PokeNav did not respond in guild {guildId}!"));
                                return;
                            }
                        }
                        else
                        {
                            // no response in timeout!
                            await channel.SendMessageAsync($"PokeNav did not respond in time! maybe you have not set the right PokeNav Prefix? run `{_settings[guildId].Prefix}set pokenav-prefix` to correct it, then `{_settings[guildId].Prefix}resume`!");
                            await _logger.Log(new LogMessage(LogSeverity.Info, this.GetType().Name, $"PokeNav did not respond in guild {guildId}!"));
                            return;
                        }
                    }
                }
                else
                {
                    await invokedChannel.SendMessageAsync($"PokeNav Moderation Channel not set yet! Run `{_settings[guildId].Prefix}set mod-channel` to set it, then run `{_settings[guildId].Prefix}resume` to create the PoI!");
                    await _logger.Log(new LogMessage(LogSeverity.Info, this.GetType().Name, $"Execution failed in Guild {guildId}: Mod-Channel was not set!"));
                }
            }

            if (createQueues.ContainsKey(guildId) && createQueues[guildId].Any())
            {
                workers[guildId] = Work(guildId, invokedChannel, token); // proceed with creation after edits are complete if there is anything to create.
            }
            else
            {
                workers.Remove(guildId);
                tokens.Remove(guildId, out CancellationTokenSource source);
                source.Dispose();
            }
        }
    }
}
