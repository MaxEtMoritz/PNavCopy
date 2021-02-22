using Discord;
using Discord.Addons.Collectors;
using Discord.Commands;
using Discord.WebSocket;
using Interactivity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
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
            _client = new DiscordSocketClient();
            var inter = new InteractivityService(_client, TimeSpan.FromSeconds(10));
            _services = new ServiceCollection()
                .AddSingleton(_config)
                .AddSingleton(_client)
                .AddSingleton(inter)
                .AddSingleton(new MessageQueue(_client, inter))
                .AddSingleton<CommandHandler>()
                .AddSingleton<CommandService>()
                .AddSingleton<GuildSettings>()
                .BuildServiceProvider();
            //_client = _services.GetRequiredService<DiscordSocketClient>();
            _client.Log += Log;

            await _client.LoginAsync(TokenType.Bot, _config["token"]);
            await _client.StartAsync();
            commandService = _services.GetRequiredService<CommandService>();
            commandService.Log += Log;
            commandService.AddTypeReader(typeof(List<string[]>), new JsonTypeReader<List<string[]>>());
            commandService.AddTypeReader(typeof(List<EditData>), new JsonTypeReader<List<EditData>>());
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
