using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord.Interactions;
using Discord;
using Discord.WebSocket;
using System.Diagnostics;

namespace CompanionBot
{
    public class TestModules : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly Logger _logger;
        private readonly DiscordSocketClient _client;
        public TestModules(Logger logger, DiscordSocketClient client)
        {
            _logger = logger;
            _client = client;
        }
        [SlashCommand("disconnect", "Disconnects the Bot from the gateway."), RequireOwner]
        public async Task DisconnectAsync()
        {
            await _logger.Log(new LogMessage(LogSeverity.Warning, nameof(DisconnectAsync), "Disconnect requested by Interaction, waiting for confirmation..."));
            await RespondAsync("Are you really sure? The bot will need manual restart if you do this!", ephemeral:true, components: new ComponentBuilder()
                .WithButton("Yes i am!", "disconnect", ButtonStyle.Danger, new Emoji("⚠"))
                .Build());
        }

        [ComponentInteraction("disconnect*"), RequireOwner]
        public async Task ConfirmDisconnectAsync()
        {
            await RespondAsync("Disconnecting now. Goodbye!", ephemeral: true);
            await _client.LogoutAsync();
            await _client.StopAsync();
            Environment.Exit(0);
        }
    }
}
