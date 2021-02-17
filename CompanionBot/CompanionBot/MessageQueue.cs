using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Collectors;
using Discord.WebSocket;
using Interactivity;

namespace CompanionBot
{
    public class MessageQueue
    {
        private readonly IDiscordClient _client;
        private static InteractivityService _inter;
        private static Queue<CommandData> queue;
        private static bool working;
        public MessageQueue(IDiscordClient client, InteractivityService service)
        {
            _client = client;
            _inter = service;
            queue = new Queue<CommandData>();
            working = false;
        }

        public Task Enqueue(ISocketMessageChannel channel, List<string> commands)
        {
            foreach (string command in commands)
            {
                queue.Enqueue(new CommandData { command = command, channel = channel });
            }
            
            
            if (!working)
            {
                post();
            }
            return Task.CompletedTask;
        }

        private async Task post()
        {
            working = true;

            while (queue.Count > 0)
            {
                CommandData current = queue.Dequeue();
                IDisposable typing = current.channel.EnterTypingState();
                current.channel.SendMessageAsync(current.command);
                // wait for PokeNav to respond...
                var Result = await _inter.NextMessageAsync(x => x.Author.Id == 428187007965986826 && x.Channel.Id == current.channel.Id && x.Embeds.Count > 0);
                if (Result.IsSuccess == false)
                {
                    await current.channel.SendMessageAsync("PokeNav did not respond in time, please try again by Hand!");
                }
                typing.Dispose();
            }
            working = false;
        }
    }

    public struct CommandData
    {
        public string command;
        public ISocketMessageChannel channel;
    }
}
