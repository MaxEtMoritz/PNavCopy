using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Collectors;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;

namespace CompanionBot
{
    public class MessageQueue
    {
        private readonly IDiscordClient _client;
        private static MessageCollector _collector;
        private static Queue<CommandData> queue;
        public MessageQueue(IDiscordClient client, MessageCollector collector)
        {
            _client = client;
            _collector = collector;
            queue = new Queue<CommandData>();
        }

        public Task Enqueue(ISocketMessageChannel channel, List<string> commands)
        {
            foreach (string command in commands)
            {
                queue.Enqueue(new CommandData { command = command, channel = channel });
            }
            if (Post.Status != TaskStatus.Running)
                Post.
            return Task.CompletedTask;
        }

        private async Task work() { }

        Task v = new Task(async()=>work);
        //TODO launch a new Task each time or fix somehow else

        private Task Post = new Task(async () =>
        {
            //IDisposable typing = Context.Channel.EnterTypingState();
            MatchOptions options = new MatchOptions
            {
                Timeout = TimeSpan.FromSeconds(10),
                ResetTimeoutOnAttempt = false
            };

            while (queue.Count > 0)
            {
                CommandData current = queue.Dequeue();

                await current.channel.SendMessageAsync(current.command);
                // wait for PokeNav to respond...

                MessageMatch match = await _collector.MatchAsync((SocketMessage msg, int index) => { return msg.Channel.Id == current.channel.Id && msg.Author.Id == 428187007965986826 && msg.Embeds.Count > 0; }, options);
                if (match == null)
                {
                    await current.channel.SendMessageAsync("PokeNav did not respond in time, please try again by Hand!");
                }
                else
                {
                    var result = match.Message;
                }
            }
        });
    }

    public struct CommandData
    {
        public string command;
        public ISocketMessageChannel channel;
    }
}
