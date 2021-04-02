using Discord;
using Discord.Addons.CommandsExtension;
using Discord.Commands;
using Discord.WebSocket;
using Interactivity;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace CompanionBot
{
    public class General : ModuleBase<SocketCommandContext>
    {
        private readonly CommandService _commands;
        private readonly GuildSettings _settings;
        public General(CommandService commands, GuildSettings settings)
        {
            _commands = commands;
            _settings = settings;
        }

        [Command("repost"), Summary("Re-posts a message.")]
        public async Task RepostAsync([Remainder, Summary("The Message to repost.")] string message)
        {
            await ReplyAsync(message);
        }

        [Command("help"), Summary("Shows info on Commands.")]
        public async Task ShowHelpAsync([Summary("The name of the Command you want help for."), Remainder] string commandName = null)
        {
            var embed = _commands.GetDefaultHelpEmbed(commandName, "" + _settings[Context.Guild.Id].Prefix);
            await ReplyAsync(embed: embed);
        }

        [Command("my-settings"), Alias("ms"), Summary("Shows your current Settings.")]
        public Task ShowSettings()
        {
            string response = "Your current Settings are:";
            Settings settings = _settings[Context.Guild.Id];
            response += $"\nPrefix:\t{settings.Prefix}";
            response += $"\nPokeNav Mod-Channel:\t{(settings.PNavChannel != null ? "<#" + settings.PNavChannel + ">" : "none")}";
            return ReplyAsync(response);
        }
    }

    [Name("PoI Management")]
    public class PoIManagement : ModuleBase<SocketCommandContext>
    {
        private readonly MessageQueue _queue;
        private readonly Logger _logger;
        public PoIManagement(MessageQueue queue, Logger logger)
        {
            _queue = queue;
            _logger = logger;
        }

        [Command("createmultiple", RunMode = RunMode.Async), Alias("cm"), Summary("Receives data for multiple PoI from the IITC plugin and sends the data one by one for the PokeNav Bot."), RequireWebhook(Group = "Perm"), RequireOwner(Group = "Perm"), RequireAttachedJson]
        public async Task CreatePoIAsync() // Async because download could take time
        {
            string dataString = "";
            using (var client = new WebClient())
            {
                client.Encoding = Encoding.UTF8;
                try
                {
                    dataString = await client.DownloadStringTaskAsync(Context.Message.Attachments.First().Url);
                }
                catch (WebException e)
                {
                    dataString = "[]";
                    await _logger.Log(new LogMessage(LogSeverity.Error, nameof(CreatePoIAsync), $"Download of attached File failed: {e.Response}", e));
                    await ReplyAsync($"Download of Attached File Failed: {e.Response}");
                }
            }

            // Command Exception thrown!
            PortalData[] data = JsonConvert.DeserializeObject<PortalData[]>(dataString);

            List<string> commands = new List<string>();
            foreach (PortalData current in data)
            {
                commands.Add($"create poi {current.type} «{current.name}» {current.lat} {current.lng}{(current.isEx != null ? $" \"ex_eligible: {Convert.ToInt16((bool)current.isEx)}\"" : "")}");
            }
            await _queue.EnqueueCreate(Context, commands);
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
            string dataString = "";
            using (var client = new WebClient())
            {
                client.Encoding = Encoding.UTF8;
                try
                {
                    dataString = await client.DownloadStringTaskAsync(Context.Message.Attachments.First().Url);
                }
                catch (WebException e)
                {
                    dataString = "[]";
                    await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Download of attached File failed: {e.Response}", e));
                    await ReplyAsync($"Download of Attached File Failed: {e.Response}");
                }
            }

            // Command Exception Thrown!
            List<EditData> data = JsonConvert.DeserializeObject<List<EditData>>(dataString);
            await _queue.EnqueueEdit(Context, data);
        }
    }

    [Group("set"), Alias("s"), Name("Configuration"), Summary("Configure the Bot"), RequireUserPermission(GuildPermission.ManageGuild)]
    public class ConfigurationModule : ModuleBase<SocketCommandContext>
    {
        private readonly GuildSettings _settings;
        private readonly InteractivityService _interactive;
        private readonly string prefix;
        public ConfigurationModule(GuildSettings settings, InteractivityService inter, IConfiguration config)
        {
            _settings = settings;
            _interactive = inter;
            prefix = $"<@{config["pokeNavId"]}> ";
        }

        [Command("mod-channel", RunMode = RunMode.Async), Alias("mc"), Summary("Sets the PokeNav Moderation Channel for this Server by sending `show mod-channel`-Command to PokeNav.")]
        public async Task SetModChannel()
        {
            var T = ReplyAsync($"{prefix}show mod-channel");
            var result = await _interactive.NextMessageAsync((message) =>
            {
                return message.Author.Id == 428187007965986826 && message.Channel.Id == Context.Channel.Id && message.MentionedChannels.Count == 1;
            }, null, TimeSpan.FromSeconds(10));
            await T;
            if (result.IsSuccess)
            {
                var channel = result.Value.MentionedChannels.First();
                var currentSettings = _settings[Context.Guild.Id];
                currentSettings.PNavChannel = channel.Id;
                _settings[Context.Guild.Id] = currentSettings;
                await ReplyAsync($"Moderation Channel successfully set to <#{channel.Id}>");
            }
            else
            {
                await ReplyAsync($"Did not receive a Response from PokeNav in time!\nMake sure PokeNav is able to respond in this Channel!");
            }
        }

        [Command("prefix"), Alias("p"), Summary("Sets the Prefix for this Bot on the Server.")]
        public async Task SetPrefix([Summary("The new Prefix for the Bot")] char prefix)
        {
            Settings current = _settings[Context.Guild.Id];
            current.Prefix = prefix;
            _settings[Context.Guild.Id] = current;
            await ReplyAsync($"Prefix successfully set to '{prefix}'.");
        }
    }
}
