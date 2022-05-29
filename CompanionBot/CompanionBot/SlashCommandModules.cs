using System;
using System.Threading.Tasks;
using Discord.Interactions;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Fergun.Interactive;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using System.Text;

namespace CompanionBot
{
    public class SlashModules : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly Logger _logger;
        private readonly DiscordSocketClient _client;
        private readonly IConfiguration _config;
        private readonly InteractiveService _interactive;
        private readonly GuildSettings _settings;
        private readonly MessageQueue _queue;
        public SlashModules(Logger logger, DiscordSocketClient client, IConfiguration config, InteractiveService interactive, GuildSettings settings, MessageQueue queue)
        {
            _logger = logger;
            _client = client;
            _config = config;
            _interactive = interactive;
            _settings = settings;
            _queue = queue;
        }

        public override void BeforeExecute(ICommandInfo cmd)
        {
            CultureInfo.CurrentCulture = new(Context.Interaction.UserLocale);
            CultureInfo.CurrentUICulture = new(Context.Interaction.UserLocale);
        }

        [SlashCommand("mod-channel", "Requests PokeNav's mod channel and saves it."), RequireBotPermission(ChannelPermission.ViewChannel), EnabledInDm(false)]
        public async Task SetModChannelAsync()
        {
            Task T = RespondAsync($"<@{_config["pokeNavId"]}> show mod-channel");
            var result = await _interactive.NextMessageAsync((message) => message.Author.Id == ulong.Parse(_config["pokeNavId"]) && message.Channel.Id == Context.Channel.Id && message.MentionedChannels.Count == 1, null, TimeSpan.FromSeconds(10));
            await T;
            if (result.IsSuccess)
            {
                var channel = result.Value.MentionedChannels.First();
                Settings currentSettings = _settings[Context.Guild.Id];
                currentSettings.PNavChannel = channel.Id;
                _settings[Context.Guild.Id] = currentSettings;
                await FollowupAsync(String.Format(Properties.Resources.modChannelSuccess, channel.Id));
                await _logger.Log(new LogMessage(LogSeverity.Info, nameof(SetModChannelAsync), $"PokeNav Mod Channel set to #{channel.Name} ({channel.Id}) for Guild {Context.Guild.Name} ({Context.Guild.Id})."));
                ChannelPermissions modPerms = Context.Guild.GetUser(_client.CurrentUser.Id).GetPermissions(channel);
                if (!modPerms.SendMessages || !modPerms.ViewChannel || !modPerms.AddReactions || !modPerms.ReadMessageHistory)
                    await FollowupAsync(String.Format(
                        Properties.Resources.promptMissingPerms,
                        modPerms.ViewChannel ? "✅" : "❌",
                        modPerms.SendMessages ? "✅" : "❌",
                        modPerms.AddReactions ? "✅" : "❌",
                        modPerms.ReadMessageHistory ? "✅" : "❌",
                        channel.Id
                    ));
            }
            else
                await FollowupAsync(Properties.Resources.modChannelFail);
        }

        [SlashCommand("pause", "Pauses the currently running PokeNav POI import."), EnabledInDm(false)]
        public Task Pause()
        {
            Console.WriteLine(CultureInfo.CurrentCulture.Name);
            return _queue.Pause(Context);
        }

        [SlashCommand("resume", "Restarts the previously paused PokeNav POI import."), EnabledInDm(false)]
        public Task Resume()
        {
            return _queue.Resume(Context);
        }

#if DEBUG 
        [ComponentInteraction("devdisconnect*"), RequireOwner]
#else
        [ComponentInteraction("disconnect*"), RequireOwner]
#endif
        public async Task ConfirmDisconnectAsync()
        {
            await RespondAsync("Saving state...", ephemeral: true);
            await _queue.SaveState();
            await FollowupAsync(Properties.Resources.goodbye, ephemeral: true);
            await _client.LogoutAsync();
            await _client.StopAsync();
            Environment.Exit(0);
        }
    }

    [DontAutoRegister]
    public class ManualSlashModules : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly Logger _logger;
        private readonly MessageQueue _queue;
        private readonly DiscordSocketClient _client;
        public ManualSlashModules(Logger logger, MessageQueue queue, DiscordSocketClient client)
        {
            _logger = logger;
            _queue = queue;
            _client = client;
        }

        public override void BeforeExecute(ICommandInfo cmd)
        {
            CultureInfo.CurrentCulture = new(Context.Interaction.UserLocale);
            CultureInfo.CurrentUICulture = new(Context.Interaction.UserLocale);
        }

        [SlashCommand("disconnect", "Disconnects the Bot from the gateway."), RequireOwner]
        public async Task DisconnectAsync()
        {
            string prompt = Properties.Resources.sure;
#if DEBUG
            prompt += Properties.Resources.testInstance;
#endif
            await _logger.Log(new LogMessage(LogSeverity.Warning, nameof(DisconnectAsync), "Disconnect requested by Interaction, waiting for confirmation..."));
            await RespondAsync(prompt, ephemeral: true, components: new ComponentBuilder()
                .WithButton(Properties.Resources.yesIAm,
#if DEBUG
                "devdisconnect",
#else
                "disconnect",
#endif
                ButtonStyle.Danger, new Emoji("⚠"))
                .Build());
        }

        [SlashCommand("status", "shows the current bot status"), RequireOwner]
        public async Task BotStatus()
        {
            var status = _queue.GetState();
            // embed fields have max length of 1024 chars (https://discord.com/developers/docs/resources/channel#embed-object-embed-limits)
            StringBuilder servers = new(100, 1024);
            StringBuilder creations = new(100, 1024);
            StringBuilder edits = new(100, 1024);
            foreach (var state in status)
            {
                try
                {
                    servers.AppendLine(state.server.ToString());
                    creations.AppendLine(state.creations.ToString());
                    edits.AppendLine(state.edits.ToString());
                }
                catch (ArgumentOutOfRangeException)
                {
                    break;
                }
            }
            List<EmbedFieldBuilder> fields = new()
            {
                new() { IsInline = true, Name = Properties.Resources.guildId, Value = String.IsNullOrEmpty(servers.ToString()) ? "---" : servers.ToString() },
                new() { IsInline = true, Name = Properties.Resources.numCreations, Value = String.IsNullOrEmpty(creations.ToString()) ? "---" : creations.ToString() },
                new() { IsInline = true, Name = Properties.Resources.numEdits, Value = String.IsNullOrEmpty(edits.ToString()) ? "---" : edits.ToString() }
            };
            EmbedBuilder embed = new EmbedBuilder()
                .WithTitle(Properties.Resources.currentBotState)
                .WithDescription(String.Format(Properties.Resources.currentlyXServers, _client.Guilds.Count))
                .WithCurrentTimestamp()
                .WithFooter(Properties.Resources.dataByBot)
                .WithAuthor(_client.CurrentUser)
                .WithFields(fields);
            await RespondAsync(embed: embed.Build(), ephemeral: true);
        }
    }
}
