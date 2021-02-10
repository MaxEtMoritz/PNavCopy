using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord.Addons.Collectors;

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
                        response += "__**" + command.Name + "**__\n";
                        if (command.Aliases.Count > 1)
                        {
                            response += "__Aliases:__ ";
                            foreach (string alias in command.Aliases)
                            {
                                if (alias != command.Name)
                                    response += alias + " ";
                            }
                            response += "\n";
                        }
                        response += "\t" + command.Summary;
                        response += $"Usage: `{_config["prefix"]}{command.Name}";
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

        [Command("createmultiple"), Summary("Receives data for multiple PoI from the IITC plugin and sends the data one by one for the PokeNav Bot.")]
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
                        commands.Add($"{prefix}create poi {type} «{current[1]}» {current[2]} {current[3]}");
                    }
                }
                await _queue.Enqueue(Context.Channel, commands);
            }
        }
    }
}
