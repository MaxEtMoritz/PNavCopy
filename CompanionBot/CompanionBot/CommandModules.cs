using Discord.Commands;
using System;
using System.Threading.Tasks;

namespace CompanionBot
{
    public class GeneralModule : ModuleBase<SocketCommandContext>
    {
        [Command("Repost")]
        [Summary("Re-posts a message.")]
        public Task RepostAsync([Remainder] [Summary("The Message to repost")] string message)
        {
            return ReplyAsync(message);
        }

        [Command("help")]
        [Summary("Shows info on Commands.")]
        public async Task ShowHelpAsync([Summary("")] string commandName = null)
        {
            //show a list of commands or specific Info on a command.
        }
    }
}
