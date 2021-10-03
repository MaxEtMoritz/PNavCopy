using Discord;
using Discord.Addons.CommandsExtension;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using Interactivity;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace CompanionBot
{
    public class General : ModuleBase<SocketCommandContext>
    {
        private readonly CommandService _commands;
        private readonly GuildSettings _settings;
        private readonly DiscordSocketClient _client;
        public General(CommandService commands, GuildSettings settings, DiscordSocketClient client)
        {
            _commands = commands;
            _settings = settings;
            _client = client;
        }

        /*[Command("repost"), Summary("Re-posts a message.")]
        public async Task RepostAsync([Remainder, Summary("The Message to repost.")] string message)
        {
            await ReplyAsync(message);
        }*/

        [Command("help"), Summary("Shows info on Commands."), RequireBotPermission(ChannelPermission.SendMessages), RequireBotPermission(ChannelPermission.EmbedLinks, ErrorMessage = "To display help, the bot needs the 'Embed Links' permission.")]
        public async Task ShowHelpAsync([Summary("The name of the Command you want help for."), Remainder] string commandName = null)
        {
            var embed = _commands.GetDefaultHelpEmbed(commandName, "" + _settings[Context.Guild.Id].Prefix);
            await ReplyAsync(embed: embed);
        }

        [Command("my-settings"), Alias("ms"), Summary("Shows your current Settings."), RequireBotPermission(ChannelPermission.SendMessages)]
        public Task ShowSettings()
        {
            ChannelPermissions? perms = Context.Guild.GetUser(_client.CurrentUser.Id)?.GetPermissions(Context.Channel as IGuildChannel);
            if (!perms.HasValue || perms.Value.SendMessages)
            {
                string response = "Your current Settings are:";
                Settings settings = _settings[Context.Guild.Id];
                response += $"\nPrefix:\t{settings.Prefix}";
                response += $"\nPokeNav Mod-Channel:\t{(settings.PNavChannel != null ? "<#" + settings.PNavChannel + ">" : "none")}";
                return ReplyAsync(response);
            }
            return Task.FromException(new Exception("Unable to send messages in the Channel!"));
        }
    }

    [Name("PoI Management")]
    public class PoIManagement : ModuleBase<SocketCommandContext>
    {
        private readonly MessageQueue _queue;
        private readonly Logger _logger;
        private readonly HttpClient _webClient;
        private readonly DiscordSocketClient _client;
        public PoIManagement(MessageQueue queue, Logger logger, HttpClient webClient, DiscordSocketClient socketClient)
        {
            _queue = queue;
            _logger = logger;
            _webClient = webClient;
            _client = socketClient;
        }

        [Command("createmultiple", RunMode = RunMode.Async), Alias("cm"), Summary("Receives data for multiple PoI from the IITC plugin and sends the data one by one for the PokeNav Bot."), RequireWebhook(Group = "Perm"), RequireOwner(Group = "Perm"), RequireAttachedJson]
        public async Task CreatePoIAsync() // Async because download could take time
        {
            ChannelPermissions perms = Context.Guild.GetUser(_client.CurrentUser.Id).GetPermissions(Context.Channel as IGuildChannel);
            string dataString = "";
            try
            {
                dataString = await _webClient.GetStringAsync(Context.Message.Attachments.First().Url);
            }
            catch (HttpRequestException e)
            {
                dataString = "[]";
                await _logger.Log(new LogMessage(LogSeverity.Error, nameof(CreatePoIAsync), $"Download of attached File failed: {e.Message}", e));
                if (perms.SendMessages)
                    await ReplyAsync($"Download of Attached File Failed: {e.Message}");
            }
            // Command Exception thrown!
            PortalData[] data = JsonConvert.DeserializeObject<PortalData[]>(dataString);

            List<string> commands = new List<string>();
            foreach (PortalData current in data)
            {
                commands.Add($"create poi {current.type} «{current.name}» {current.lat} {current.lng}{(current.isEx != null ? $" \"ex_eligible: {Convert.ToInt16((bool)current.isEx)}\"" : "")}");
            }
            await _queue.EnqueueCreate(Context, commands, perms);
        }

        [Command("pause"), Alias("p", "stop"), Summary("Pauses the Bulk Export. To start again, run the `resume` Command.")]
        public Task Pause()
        {
            _queue.Pause(Context); // Pausing can take time so this would block the gateway too long if the Pause Task is returned.
            return Task.CompletedTask;
        }

        [Command("resume"), Alias("r", "restart"), Summary("Resume the Bulk Export.")]
        public Task Resume()
        {
            return _queue.Resume(Context);
        }

        [Command("edit", RunMode = RunMode.Async), Alias("e"), Summary("Receives a list of Edits to make from the IITC Plugin, sends the PoI Info Command to obtain the PokeNav id and makes the Edit afterwards."), RequireWebhook(Group = "g"), RequireOwner(Group = "g"), RequireAttachedJson]
        public async Task Edit() // Async because Download can take time...
        {
            ChannelPermissions perms = Context.Guild.GetUser(_client.CurrentUser.Id).GetPermissions(Context.Channel as IGuildChannel);
            string dataString = "";
            try
            {
                dataString = await _webClient.GetStringAsync(Context.Message.Attachments.First().Url);
            }
            catch (HttpRequestException e)
            {
                dataString = "[]";
                await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Download of attached File failed: {e.Message}", e));
                if (perms.SendMessages)
                    await ReplyAsync($"Download of Attached File Failed: {e.Message}");
            }

            // Command Exception Thrown!
            List<EditData> data = JsonConvert.DeserializeObject<List<EditData>>(dataString);
            await _queue.EnqueueEdit(Context, data, perms);
        }
    }

    [Group("set"), Alias("s"), Name("Configuration"), Summary("Configure the Bot"), RequireUserPermission(GuildPermission.ManageGuild)]
    public class ConfigurationModule : ModuleBase<SocketCommandContext>
    {
        private readonly GuildSettings _settings;
        private readonly InteractivityService _interactive;
        private readonly DiscordSocketClient _client;
        private readonly Logger _logger;
        private readonly IConfiguration _config;
        private readonly string prefix;
        public ConfigurationModule(GuildSettings settings, InteractivityService inter, IConfiguration config, DiscordSocketClient client, Logger logger)
        {
            _settings = settings;
            _interactive = inter;
            _config = config;
            prefix = $"<@{config["pokeNavId"]}> ";
            _client = client;
            _logger = logger;
        }

        [Command("mod-channel", RunMode = RunMode.Async), Alias("mc"), Summary("Sets the PokeNav Moderation Channel for this Server by sending `show mod-channel`-Command to PokeNav.")]
        public async Task SetModChannel()
        {
            // TODO: let it be set by Command
            ChannelPermissions perms = Context.Guild.GetUser(_client.CurrentUser.Id).GetPermissions(Context.Channel as IGuildChannel);
            if (perms.SendMessages)
            {
                var T = ReplyAsync($"{prefix}show mod-channel");
                var result = await _interactive.NextMessageAsync((message) => message.Author.Id == ulong.Parse(_config["pokeNavId"]) && message.Channel.Id == Context.Channel.Id && message.MentionedChannels.Count == 1, null, TimeSpan.FromSeconds(10));
                await T;
                if (result.IsSuccess)
                {
                    var channel = result.Value.MentionedChannels.First();
                    var currentSettings = _settings[Context.Guild.Id];
                    currentSettings.PNavChannel = channel.Id;
                    _settings[Context.Guild.Id] = currentSettings;
                    await ReplyAsync($"Moderation Channel successfully set to <#{channel.Id}>");
                    await _logger.Log(new LogMessage(LogSeverity.Info, nameof(SetModChannel), $"PokeNav Mod Channel set to #{channel.Name} ({channel.Id}) for Guild {Context.Guild.Name} ({Context.Guild.Id})."));
                    ChannelPermissions modPerms = Context.Guild.GetUser(_client.CurrentUser.Id).GetPermissions(channel);
                    if (!modPerms.SendMessages || !modPerms.ViewChannel || !modPerms.AddReactions || !modPerms.ReadMessageHistory)
                        await ReplyAsync(":warning: Attention! :warning: The bot is missing permissions in the PokeNav mod channel:" +
                            $"\n\tView Channel: {(modPerms.ViewChannel ? ":white_check_mark:" : ":x:")}" +
                            $"\n\tSend Messages: {(modPerms.SendMessages ? ":white_check_mark:" : ":x:")}" +
                            $"\n\tAdd Reactions (Recommended but optional): {(modPerms.AddReactions ? ":white_check_mark:" : ":x:")}" +
                            $"\n\tView Message History (for ambiguous edits): {(modPerms.ReadMessageHistory ? ":white_check_mark:" : ":x:")}" +
                            $"\nMake sure to grant the necessary permissions to the bot for <#{channel.Id}>.");
                }
                else
                    await ReplyAsync($"Did not receive a Response from PokeNav in time!\nMake sure PokeNav is able to respond in the Channel where you execute the command!");
            }
        }

        [Command("prefix"), Alias("p"), Summary("Sets the Prefix for this Bot on the Server.")]
        public async Task SetPrefix([Summary("The new Prefix for the Bot")] char prefix)
        {
            ChannelPermissions perms = Context.Guild.GetUser(_client.CurrentUser.Id).GetPermissions(Context.Channel as IGuildChannel);
            Settings current = _settings[Context.Guild.Id];
            current.Prefix = prefix;
            _settings[Context.Guild.Id] = current;
            if (perms.SendMessages)
                await ReplyAsync($"Prefix successfully set to '{prefix}'.");
            await _logger.Log(new LogMessage(LogSeverity.Info, nameof(SetPrefix), $"Prefix for guild {Context.Guild.Name} ({Context.Guild.Id}) set to {prefix}."));
        }
    }
}
