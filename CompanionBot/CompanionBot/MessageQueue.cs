using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Collectors;
using Discord.Commands;
using Discord.WebSocket;
using Interactivity;
using Microsoft.Extensions.DependencyInjection;

namespace CompanionBot
{
    public class MessageQueue
    {
        private readonly IDiscordClient _client;
        private readonly Logger _logger;
        private static InteractivityService _inter;
        //private static Queue<CommandData> queue;
        private Dictionary<ulong, Queue<string>> queues;
        private Dictionary<ulong, Task> workers;
        private readonly GuildSettings _settings;
        private static bool working;
        public MessageQueue(DiscordSocketClient client, InteractivityService interactive, Logger logger, GuildSettings settings)
        {
            _client = client;
            _inter = interactive;
            _logger = logger;
            _settings = settings;
            queue = new Queue<CommandData>();
            working = false;
        }

        public Task Enqueue(ICommandContext context, List<string> commands)
        {
            if (queues.ContainsKey(context.Guild.Id))
            {
                queues[context.Guild.Id].Concat(commands);
            }
            else
            {
                queues.Add(context.Guild.Id, new Queue<string>(commands));
            }

            if (!workers.ContainsKey(context.Guild.Id))
            {
                workers.Add(context.Guild.Id, work(queues[context.Guild.Id]));
            }
            return Task.CompletedTask;
        }

        private async Task post()
        {
            working = true;
            RequestOptions options = RequestOptions.Default;
            options.RetryMode = RetryMode.RetryRatelimit;

            var typingOptions = RequestOptions.Default;
            typingOptions.RetryMode = RetryMode.AlwaysFail;

            while (queue.Count > 0)
            {
                CommandData current = queue.Dequeue();
                IDisposable typing = current.channel.EnterTypingState(typingOptions);
                var t = current.channel.SendMessageAsync(current.command, false, null, options);
                // wait for PokeNav to respond...
                var Result = await _inter.NextMessageAsync(x => x.Author.Id == 428187007965986826 && x.Channel.Id == current.channel.Id && x.Embeds.Count == 1 && (x.Content == "The following poi has been created for use in your community:" || x.Embeds.First().Title == "Error"), null, TimeSpan.FromSeconds(10));
                await t;
                if (Result.IsSuccess == false)
                {
                    await current.channel.SendMessageAsync("PokeNav did not respond in time, please try again by Hand!", false, null, options);
                    await _logger.Log(new LogMessage(LogSeverity.Info, this.GetType().Name, "PokeNav did not respond within 10 seconds."));
                }
                typing.Dispose();
            }
            working = false;
        }

        private async Task work(ulong guildId)
        {
            if (queues.TryGetValue(guildId, out Queue<string> queue))
            {
                RequestOptions options = RequestOptions.Default;
                options.RetryMode = RetryMode.RetryRatelimit;

                var typingOptions = RequestOptions.Default;
                typingOptions.RetryMode = RetryMode.AlwaysFail;

                var channel = await _client.GetChannelAsync(_settings[guildId].PNavChannel); // TODO handle channel not set yet!


                // TODO adapt to changes and delete unused things!
                while (queue.Count > 0)
                {
                    string current = queue.Dequeue();
                    IDisposable typing = chann
                    var t = channel.SendMessageAsync(current.command, false, null, options);
                    // wait for PokeNav to respond...
                    var Result = await _inter.NextMessageAsync(x => x.Author.Id == 428187007965986826 && x.Channel.Id == current.channel.Id && x.Embeds.Count == 1 && (x.Content == "The following poi has been created for use in your community:" || x.Embeds.First().Title == "Error"), null, TimeSpan.FromSeconds(10));
                    await t;
                    if (Result.IsSuccess == false)
                    {
                        await current.channel.SendMessageAsync("PokeNav did not respond in time, please try again by Hand!", false, null, options);
                        await _logger.Log(new LogMessage(LogSeverity.Info, this.GetType().Name, "PokeNav did not respond within 10 seconds."));
                    }
                    typing.Dispose();
                }
            }
        }
    }

    public struct CommandData
    {
        public string command;
        public ISocketMessageChannel channel;
    }
}
