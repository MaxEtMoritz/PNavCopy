using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Fergun.Interactive;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Globalization;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Timers;

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
        private static readonly Timer watchdog = new(TimeSpan.FromSeconds(30)) { AutoReset = false };
        internal static bool end = false;

        public static void Main() => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            _config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("config.json",false, true)
                .Build();

            CultureInfo.DefaultThreadCurrentCulture = new("en-US");
            CultureInfo.DefaultThreadCurrentUICulture = new("en-US");

            _services = new ServiceCollection()
                .AddSingleton(_config)
                .AddSingleton(new DiscordSocketClient(new DiscordSocketConfig()
                {
                    MessageCacheSize = 20,
                    GatewayIntents = GatewayIntents.GuildMessageReactions
                    | GatewayIntents.GuildMessages
                    | GatewayIntents.Guilds | GatewayIntents.MessageContent,
                    AlwaysDownloadDefaultStickers = false,
                    AlwaysResolveStickers = false,
#if DEBUG
                    LogGatewayIntentWarnings = true,
                    LogLevel = LogSeverity.Debug,
#endif
                }))
                .AddSingleton<InteractiveService>()
                .AddSingleton<MessageQueue>()
                .AddSingleton<CommandHandler>()
                .AddSingleton<CommandService>()
                .AddSingleton<GuildSettings>()
                .AddSingleton<Logger>()
                .AddSingleton<InteractionHandler>()
                .AddSingleton(new HttpClient() { Timeout = TimeSpan.FromSeconds(10) })
                .AddSingleton<InteractionService>(b => new(b.GetRequiredService<DiscordSocketClient>(), new InteractionServiceConfig()
                {
                    LocalizationManager = new ResxLocalizationManager("CompanionBot.Properties.SlashCommands", Assembly.GetExecutingAssembly(), new CultureInfo[] {
                        CultureInfo.GetCultureInfo("de"),
                        CultureInfo.GetCultureInfo("en-US")
                    })
                }))
                .BuildServiceProvider();
            watchdog.Elapsed += Watchdog_Elapsed;

            _client = _services.GetRequiredService<DiscordSocketClient>();
            _client.Log += _services.GetRequiredService<Logger>().Log;
            _client.LeftGuild += GuildLeft;
            _client.Disconnected += ClientDisconnected;
            _client.Connected += ClientConnected;

            _client.Ready += ClientReady;
            _services.GetRequiredService<InteractiveService>().Log += _services.GetRequiredService<Logger>().Log;

            await _client.LoginAsync(TokenType.Bot, _config["token"]);
            await _client.StartAsync();

            commandService = _services.GetRequiredService<CommandService>();
            commandService.Log += _services.GetRequiredService<Logger>().Log;
            handler = _services.GetRequiredService<CommandHandler>();
            await handler.InstallCommandsAsync();

            iHandler = _services.GetRequiredService<InteractionHandler>();
            await iHandler.InstallCommandsAsync();

            // logout and save state when process is terminated.
            void PosixHandler(PosixSignalContext ctx)
            {
                _services.GetRequiredService<Logger>().Log(new(LogSeverity.Info, nameof(PosixHandler), $"Received {Enum.GetName(typeof(PosixSignal), ctx.Signal)}, exiting...")).Wait();
                _services.GetRequiredService<MessageQueue>().SaveState().Wait();
                _client.Dispose();
                // don't know why preventing automatic termination is necessary, but otherwise the handler hangs the application...
                ctx.Cancel = true;
                Environment.Exit(0);
            }

            // store registrations, otherwise the handlers are cleared by GC after some time...
            PosixSignalRegistration[] regs = new[]{
                PosixSignalRegistration.Create(PosixSignal.SIGINT, PosixHandler),
                PosixSignalRegistration.Create(PosixSignal.SIGHUP, PosixHandler),
                PosixSignalRegistration.Create(PosixSignal.SIGTERM, PosixHandler),
                PosixSignalRegistration.Create(PosixSignal.SIGQUIT, PosixHandler)
            };

            // Block this task until the program is closed.
            await Task.Delay(-1);
        }

        private void Watchdog_Elapsed(object sender, ElapsedEventArgs e)
        {
            _services.GetRequiredService<MessageQueue>().SaveState().Wait();
            _services.GetRequiredService<Logger>().Log(new LogMessage(LogSeverity.Error, this.GetType().Name, "Gateway connection was not recovered in time. Exiting.")).Wait();
            Environment.Exit(-1);
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

        private async Task ClientDisconnected(Exception ex)
        {
            await _services.GetRequiredService<MessageQueue>().SaveState();
            if (ex.GetType().Name == "TaskCanceledException" && end)
            {
                // Bot was disconnected gracefully
                await _services.GetRequiredService<Logger>().Log(new LogMessage(LogSeverity.Info, this.GetType().Name, "Disconnected from gateway due to Command"));
                Environment.Exit(0);
            }
            else
            {
                await _services.GetRequiredService<Logger>().Log(new LogMessage(LogSeverity.Warning, this.GetType().Name, "Disconnected. Kill scheduled...", ex));
                watchdog.Start();
            }
        }

        private async Task ClientConnected()
        {
            if (watchdog.Enabled)
            {
                await _services.GetRequiredService<Logger>().Log(new LogMessage(LogSeverity.Info, this.GetType().Name, "Kill prevented, connection was re-established."));
                watchdog.Stop();
            }
        }
    }
}
