using Discord.Commands;
using System;
using System.Threading.Tasks;

namespace CompanionBot
{
    public class GeneralModule : ModuleBase<SocketCommandContext>
    {
        private readonly CommandService _commands;
        public GeneralModule(CommandService commands)
        {
            _commands = commands;
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
                response += "__**Available Commands:**__\nType `help commandName` for more info on specific Commands.";
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
                        if(command.Aliases.Count > 1)
                        {
                            response += "__Aliases:__ ";
                            foreach (string alias in command.Aliases)
                            {
                                if(alias != command.Name)
                                    response += alias + " ";
                            }
                            response += "\n";
                        }
                        response += "\t" + command.Summary;
                        if(command.Parameters.Count > 0)
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
}
