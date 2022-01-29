using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Fergun.Interactive;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Resources;
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
        private InteractionHandler iHandler;

        public static void Main()
        => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            _config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("config.json")
                .Build();

            _services = new ServiceCollection()
                .AddSingleton(_config)
                .AddSingleton(new DiscordSocketClient(new DiscordSocketConfig() {
                    MessageCacheSize = 20,
                    GatewayIntents = GatewayIntents.GuildMessageReactions
                    | GatewayIntents.GuildMessages
                    | GatewayIntents.Guilds,
                    AlwaysDownloadDefaultStickers = false,
                    AlwaysResolveStickers = false,
#if DEBUG
                    LogGatewayIntentWarnings = true,
                    LogLevel = LogSeverity.Debug
#endif
                }))
                .AddSingleton<InteractiveService>()
                .AddSingleton<MessageQueue>()
                .AddSingleton<CommandHandler>()
                .AddSingleton<CommandService>()
                .AddSingleton<GuildSettings>()
                .AddSingleton<Logger>()
                .AddSingleton<InteractionHandler>()
                .AddSingleton(new HttpClient() {Timeout=TimeSpan.FromSeconds(10)})
                .AddSingleton(x=>new InteractionService(x.GetRequiredService<DiscordSocketClient>()))
                .BuildServiceProvider();

            _client = _services.GetRequiredService<DiscordSocketClient>();
            _client.Log += _services.GetRequiredService<Logger>().Log;
            _client.LeftGuild += GuildLeft;

            _client.Ready += ClientReady;
            Console.WriteLine(_services.GetRequiredService<InteractionService>().RestClient);

            await _client.LoginAsync(TokenType.Bot, _config["token"]);
            await _client.StartAsync();
            commandService = _services.GetRequiredService<CommandService>();
            commandService.Log += _services.GetRequiredService<Logger>().Log;
            handler = _services.GetRequiredService<CommandHandler>();
            await handler.InstallCommandsAsync();

            iHandler = _services.GetRequiredService<InteractionHandler>();
            await iHandler.InstallCommandsAsync();

            // Block this task until the program is closed.
            await Task.Delay(-1);
        }

        private async Task ClientReady()
        {
#if DEBUG
            await iHandler.RegisterSlashCommandsForTestGuild();
#else
            await iHandler.RegisterSlashCommandsGlobally();
#endif
        }

        private Task GuildLeft(SocketGuild guild)
        {
            Logger logger = _services.GetRequiredService<Logger>();
            logger.Log(new LogMessage(LogSeverity.Info, this.GetType().Name, $"The Bot was removed from Guild {guild.Name} ({guild.Id})."));
            _services.GetRequiredService<GuildSettings>().DeleteSettings(guild.Id);
            return Task.CompletedTask;
        }
    }
}
