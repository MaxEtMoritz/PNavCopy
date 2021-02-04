using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace CompanionBot
{
    class Program
    {
        private DiscordSocketClient _client;
        private CommandService commandService;
        private CommandHandler handler;
        private IConfiguration _config;
        private IServiceProvider _services;

        public static void Main(string[] args)
        => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            var _builder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile(path: "config.json");
            _config = _builder.Build();
            _services = new ServiceCollection()
                .AddSingleton(_config)
                .AddSingleton<DiscordSocketClient>()
                .AddSingleton<CommandHandler>()
                .AddSingleton<CommandService>()
                .BuildServiceProvider();

            _client = _services.GetRequiredService<DiscordSocketClient>();
            _client.Log += Log;
            //BotConfig config = JsonConvert.DeserializeObject<BotConfig>(File.ReadAllText("config.json"));

            await _client.LoginAsync(TokenType.Bot, _config["token"]);
            await _client.StartAsync();
            commandService = _services.GetRequiredService<CommandService>();
            //commandService = new CommandService(new CommandServiceConfig() { CaseSensitiveCommands = false, DefaultRunMode = RunMode.Async });
            commandService.Log += Log;
            commandService.AddTypeReader<object>(new JsonTypeReader());
            handler = _services.GetRequiredService<CommandHandler>();
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
