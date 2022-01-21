using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord.Interactions;
using Discord;
using Discord.WebSocket;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Fergun.Interactive;
using System.Linq;

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

        [SlashCommand("mod-channel", "Requests PokeNav's mod channel and saves it."), RequireBotPermission(ChannelPermission.ViewChannel)]
        public async Task SetModChannelAsync()
        {
            var T = RespondAsync($"<@{_config["pokeNavId"]}> show mod-channel");
            var result = await _interactive.NextMessageAsync((message) => message.Author.Id == ulong.Parse(_config["pokeNavId"]) && message.Channel.Id == Context.Channel.Id && message.MentionedChannels.Count == 1, null, TimeSpan.FromSeconds(10));
            await T;
            if (result.IsSuccess)
            {
                var channel = result.Value.MentionedChannels.First();
                var currentSettings = _settings[Context.Guild.Id];
                currentSettings.PNavChannel = channel.Id;
                _settings[Context.Guild.Id] = currentSettings;
                await FollowupAsync($"Moderation Channel successfully set to <#{channel.Id}>");
                await _logger.Log(new LogMessage(LogSeverity.Info, nameof(SetModChannelAsync), $"PokeNav Mod Channel set to #{channel.Name} ({channel.Id}) for Guild {Context.Guild.Name} ({Context.Guild.Id})."));
                ChannelPermissions modPerms = Context.Guild.GetUser(_client.CurrentUser.Id).GetPermissions(channel);
                if (!modPerms.SendMessages || !modPerms.ViewChannel || !modPerms.AddReactions || !modPerms.ReadMessageHistory)
                    await FollowupAsync(":warning: Attention! :warning: The bot is missing permissions in the PokeNav mod channel:" +
                        $"\n\t{(modPerms.ViewChannel ? ":white_check_mark:" : ":x:")} View Channel" +
                        $"\n\t{(modPerms.SendMessages ? ":white_check_mark:" : ":x:")} Send messages" +
                        $"\n\t{(modPerms.AddReactions ? ":white_check_mark:" : ":x:")} Add Reactions (Recommended but optional)" +
                        $"\n\t{(modPerms.ReadMessageHistory ? ":white_check_mark:" : ":x:")} View Message History (for ambiguous edits)" +
                        $"\nMake sure to grant the necessary permissions to the bot for <#{channel.Id}>.");
            }
            else
                await FollowupAsync($"Did not receive a Response from PokeNav in time!\nMake sure PokeNav is able to respond in the Channel where you execute the command!");
        }

        [SlashCommand("pause", "Pauses the currently running PokeNav POI import.")]
        public Task Pause()
        {
            // don't block gateway by returning the longer running pause task. Instead execute Task and return a completed one!
            _ = _queue.Pause(Context);
            return Task.CompletedTask;
        }

        [SlashCommand("resume", "Restarts the previously paused PokeNav POI import.")]
        public Task Resume()
        {
            // don't block gateway by returning the longer running resume task. Instead execute Task and return a completed one!
            _ = _queue.Resume(Context);
            return Task.CompletedTask;
        }

#if DEBUG 
        [ComponentInteraction("devdisconnect*"), RequireOwner]
#else
        [ComponentInteraction("disconnect*"), RequireOwner]
#endif
        public async Task ConfirmDisconnectAsync()
        {
            await RespondAsync("Disconnecting now. Goodbye!", ephemeral: true);
            await _client.LogoutAsync();
            await _client.StopAsync();
            Environment.Exit(0);
        }
    }

    [DontAutoRegister]
    public class ManualSlashModules : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly Logger _logger;
        public ManualSlashModules(Logger logger)
        {
            _logger = logger;
        }

        [SlashCommand("disconnect", "Disconnects the Bot from the gateway."), RequireOwner, DefaultPermission(false)]
        public async Task DisconnectAsync()
        {
            string prompt = "Are you really sure? The bot will need manual restart if you do this!";
#if DEBUG
            prompt += "\nThis is the test instance.";
#endif
            await _logger.Log(new LogMessage(LogSeverity.Warning, nameof(DisconnectAsync), "Disconnect requested by Interaction, waiting for confirmation..."));
            await RespondAsync(prompt, ephemeral: true, components: new ComponentBuilder()
                .WithButton("Yes i am!",
#if DEBUG
                "devdisconnect",
#else
                "disconnect",
#endif
                ButtonStyle.Danger, new Emoji("⚠"))
                .Build());
        }
    }
}
