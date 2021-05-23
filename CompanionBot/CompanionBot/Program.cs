using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Interactivity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Net.Http;
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
            _config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile(path: "config.json")
                .Build();

            _services = new ServiceCollection()
                .AddSingleton(_config)
                .AddSingleton<DiscordSocketClient>()
                .AddSingleton<InteractivityService>()
                .AddSingleton<MessageQueue>()
                .AddSingleton<CommandHandler>()
                .AddSingleton<CommandService>()
                .AddSingleton<GuildSettings>()
                .AddSingleton<Logger>()
                .AddSingleton(new HttpClient() {Timeout=TimeSpan.FromSeconds(10)})
                .BuildServiceProvider();

            _client = _services.GetRequiredService<DiscordSocketClient>();
            _client.Log += _services.GetRequiredService<Logger>().Log;

            await _client.LoginAsync(TokenType.Bot, _config["token"]);
            await _client.StartAsync();
            commandService = _services.GetRequiredService<CommandService>();
            commandService.Log += _services.GetRequiredService<Logger>().Log;
            handler = _services.GetRequiredService<CommandHandler>();
            await handler.InstallCommandsAsync();

            // Block this task until the program is closed.
            await Task.Delay(-1);
        }
    }
}
