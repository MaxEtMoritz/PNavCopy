using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Interactivity;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CompanionBot
{
    public class GeneralModule : ModuleBase<SocketCommandContext>
    {
        private readonly CommandService _commands;
        private readonly IConfiguration _config;
        public GeneralModule(CommandService commands, IConfiguration config)
        {
            _commands = commands;
            _config = config;
        }

        [Command("repost"), Summary("Re-posts a message.")]
        public async Task RepostAsync([Remainder, Summary("The Message to repost.")] string message)
        {
            await ReplyAsync(message);
        }

        [Command("help"), Summary("Shows info on Commands.")]
        public async Task ShowHelpAsync([Summary("The name of the Command you want help for.")] string commandName = null)
        {
            //show a list of commands or specific Info on a command.
            string response = "";
            if (String.IsNullOrEmpty(commandName))
            {
                response += $"__**Available Commands:**__\nType `{_config["prefix"]}help commandName` for more info on specific Commands.";
                foreach (CommandInfo command in _commands.Commands)
                {
                    response += "\n__" + command.Name + "__\n\t" + command.Summary;
                }
            }
            else
            {
                SearchResult result = _commands.Search(commandName);
                if (result.IsSuccess && result.Error == null)
                {
                    foreach (var item in result.Commands)
                    {
                        CommandInfo command = item.Command;
                        response += "__**" + command.Name + "**__";
                        if (command.Aliases.Count > 1)
                        {
                            response += "\n__Aliases:__ ";
                            foreach (string alias in command.Aliases)
                            {
                                if (alias != command.Name)
                                    response += alias + " ";
                            }
                        }
                        response += "\n\t" + command.Summary;
                        response += $"\nUsage: `{_config["prefix"]}{command.Name}";
                        if (command.Parameters.Count > 0)
                        {
                            foreach (ParameterInfo arg in command.Parameters)
                            {
                                if (arg.IsOptional)
                                {
                                    response += $" [{arg.Name}]";
                                }
                                else
                                {
                                    response += " " + arg.Name;
                                }
                            }
                        }
                        response += "`";
                        if (command.Parameters.Count > 0)
                        {
                            response += "\n__Parameters:__";
                            foreach (ParameterInfo param in command.Parameters)
                            {
                                response += "\n";
                                if (param.IsRemainder)
                                    response += "[Remainder] ";
                                response += param.Type.Name + " **" + param.Name + "**";
                                if (param.IsOptional)
                                    response += " (optional)";
                                if (!String.IsNullOrEmpty(param.Summary))
                                {
                                    response += "\n\t" + param.Summary;
                                }
                            }
                        }
                    }
                }
                else if (result.Error != null)
                {
                    response = "Error: " + result.ErrorReason;
                }
                else
                {
                    response = "no command called \"" + commandName + "\" found.";
                }
            }
            await ReplyAsync(response);
        }
    }

    public class RepostModule : ModuleBase<SocketCommandContext>
    {
        private readonly MessageQueue _queue;
        public RepostModule(MessageQueue queue)
        {
            _queue = queue;
        }

        private enum LocationType
        {
            pokestop, gym
        }

        [Command("createmultiple"), Alias("cm"), Summary("Receives data for multiple PoI from the IITC plugin and sends the data one by one for the PokeNav Bot."), RequireWebhook]
        public async Task CreatePoIAsync([Remainder, Summary("The PoI data from the IITC plugin.")] List<string[]> data)
        {
            //order of params: type name lat lng (isEx)
            // first item must be PokeNav prefix

            if (data.Count == 0 || data[0].Length == 0 || String.IsNullOrEmpty(data[0][0]))
                await ReplyAsync("Bad Format!");
            else
            {
                char prefix = data[0][0][0];
                List<string> commands = new List<string>();
                for (int i = 1; i < data.Count; i++)
                {
                    string[] current = data[i];
                    if (current.Length < 4 || current.Length > 5)
                    {
                        await ReplyAsync("Bad Format!");
                    }
                    else
                    {
                        string type;
                        try
                        {
                            type = ((LocationType)Convert.ToInt16(current[0])).ToString();
                        }
                        catch (Exception)
                        {
                            await ReplyAsync("Bad Format!");
                            continue;
                        }
                        commands.Add($"{prefix}create poi {type} «{current[1]}» {current[2]} {current[3]}{(current.Length > 4 && current[4] == "1" ? " \"ex_eligible: 1\"" : "")}");
                    }
                }
                await _queue.Enqueue(Context.Channel, commands);
            }
        }

        [Command("edit"), Alias("e"), Summary("Receives a list of Edits to make from the IITC Plugin, sends the PoI Info Command to obtain the PokeNav id and makes the Edit afterwards."), RequireWebhook]
        public async Task EditAsync([Remainder, Summary("List of Edits to make, provided by the IITC Plugin.")] List<EditData> data)
        {

        }
    }

    [Group("set"), Alias("s"), Summary("Configure the Bot"), RequireUserPermission(GuildPermission.ManageGuild)]
    public class ConfigurationModule : ModuleBase<SocketCommandContext>
    {
        private readonly GuildSettings _settings;
        private readonly InteractivityService _interactive;
        public ConfigurationModule(GuildSettings settings, InteractivityService inter)
        {
            _settings = settings;
            _interactive = inter;
        }

        [Command("pokenav-prefix"), Alias("pp"), Summary("Set the PokeNav Prefix for this Server.")]
        public async Task SetPokeNavPrefix([Summary("The Prefix the PokeNav Bot uses on this Server")] char prefix)
        {
            Settings current = _settings[Context.Guild];
            current.PNavPrefix = prefix;
            _settings[Context.Guild] = current;
            await ReplyAsync($"PokeNav Prefix successfully set to '{prefix}'.");
        }

        [Command("mod-channel", RunMode = RunMode.Async), Alias("mc"), Summary("Sets the PokeNav Moderation Channel for this Server by sending ```show mod-channel```-Command to PokeNav.")]
        public async Task SetModChannel()
        {
            var T = ReplyAsync($"{_settings[Context.Guild].PNavPrefix}show mod-channel");
            var result = await _interactive.NextMessageAsync((message) =>
            {
                return message.Author.Id == 428187007965986826 && message.Channel.Id == Context.Channel.Id && message.MentionedChannels.Count == 1;
            });
            await T;
            if (result.IsSuccess)
            {
                var channel = result.Value.MentionedChannels.First();
                var currentSettings = _settings[Context.Guild];
                currentSettings.PNavChannel = channel.Id;
                _settings[Context.Guild] = currentSettings;
                await ReplyAsync($"Moderation Channel successfully set to <#{channel.Id}>");
            }
            else
            {
                await ReplyAsync($"Did not receive a Response from PokeNav in time!\nMake sure you have set the right PokeNav Prefix (run ```{_settings[Context.Guild].Prefix}set pokenav-prefix``` to change) and PokeNav is able to respond in this Channel!");
            }
        }

        [Command("prefix"), Alias("p"), Summary("Sets the Prefix for this Bot on the Server.")]
        public async Task SetPrefix([Summary("The new Prefix for the Bot")] char prefix)
        {
            Settings current = _settings[Context.Guild];
            current.Prefix = prefix;
            _settings[Context.Guild] = current;
            await ReplyAsync($"Prefix successfully set to '{prefix}'.");
        }
    }

    public struct EditData
    {
        // t: type, n: name, a: l**a**titude, o: l**o**ngitude, e: ex-eligibility (or edits on top-level)
        public Int16 t; // TODO check if it works to declare it directly as LocationData!
        public string n;
        public IDictionary<char, string> e;
    }
}
