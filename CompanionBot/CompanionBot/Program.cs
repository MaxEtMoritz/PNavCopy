using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Interactivity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bot;
using CompanionBot.API;

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
            => new Program().MainAsync(args).GetAwaiter().GetResult();


        public async Task MainAsync(string[] args)
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
                .BuildServiceProvider();

            _client = _services.GetRequiredService<DiscordSocketClient>();
            _client.Log += _services.GetRequiredService<Logger>().Log;

            await _client.LoginAsync(TokenType.Bot, _config["token"]);
            await _client.StartAsync();
            commandService = _services.GetRequiredService<CommandService>();
            commandService.Log += _services.GetRequiredService<Logger>().Log;
            commandService.AddTypeReader(typeof(List<string[]>), new JsonTypeReader<List<string[]>>());
            commandService.AddTypeReader(typeof(List<EditData>), new JsonTypeReader<List<EditData>>());
            handler = _services.GetRequiredService<CommandHandler>();
            await handler.InstallCommandsAsync();

            // create the API
            CreateHostBuilder(args, _services).Build().Run();

            // Block this task until the program is closed.
            await Task.Delay(-1);
        }

        public static IHostBuilder CreateHostBuilder(string[] args, IServiceProvider services) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                }).ConfigureServices((x) =>
                {
                    x.AddSingleton(services.GetRequiredService<DiscordSocketClient>())
                        .AddSingleton(services.GetRequiredService<MessageQueue>())
                        .AddSingleton(services.GetRequiredService<Logger>());
                });
    }
}
