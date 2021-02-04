using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading.Tasks;

namespace CompanionBot
{
    class Program
    {
        private DiscordSocketClient _client;
        private CommandService commandService;
        private CommandHandler handler;

        public static void Main(string[] args)
        => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            _client = new DiscordSocketClient();
            _client.Log += Log;
            BotConfig config = JsonConvert.DeserializeObject<BotConfig>(File.ReadAllText("config.json"));

            await _client.LoginAsync(TokenType.Bot, config.token);
            await _client.StartAsync();

            commandService = new CommandService(new CommandServiceConfig() { CaseSensitiveCommands = false, DefaultRunMode=RunMode.Async }) ;
            commandService.Log += Log;
            handler = new CommandHandler(_client, commandService, config.prefix);
            await handler.InstallCommandsAsync();

            // Block this task until the program is closed.
            await Task.Delay(-1);
        }

        private Task Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }
    }
}
