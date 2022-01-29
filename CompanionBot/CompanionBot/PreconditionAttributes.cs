using Discord.Commands;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CompanionBot
{
    internal class RequireWebhookAttribute : PreconditionAttribute
    {
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            if (context.User.IsWebhook)
            {
                return Task.FromResult(PreconditionResult.FromSuccess());
            }
            else
            {
                return Task.FromResult(PreconditionResult.FromError("Only a WebHook can run this command"));
            }
        }
    }

    internal class RequireAttachedJsonAttribute : PreconditionAttribute
    {
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            if(context.Message.Attachments.Count == 1 && context.Message.Attachments.First().Filename.EndsWith(".json"))
            {
                return Task.FromResult(PreconditionResult.FromSuccess());
            }
            return Task.FromResult(PreconditionResult.FromError("A single `.json` file needs to be attached to the message"));
        }
    }
}
