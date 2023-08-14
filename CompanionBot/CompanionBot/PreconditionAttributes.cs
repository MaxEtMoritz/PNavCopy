using Discord;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using C = Discord.Commands;
using I = Discord.Interactions;

namespace CompanionBot
{
    internal class RequireWebhookAttribute : C.PreconditionAttribute
    {
        public override Task<C.PreconditionResult> CheckPermissionsAsync(C.ICommandContext context, C.CommandInfo command, IServiceProvider services)
        {
            if (context.User.IsWebhook)
            {
                return Task.FromResult(C.PreconditionResult.FromSuccess());
            }
            else
            {
                return Task.FromResult(C.PreconditionResult.FromError("Only a WebHook can run this command"));
            }
        }
    }

    internal class RequireAttachedJsonAttribute : C.PreconditionAttribute
    {
        public override Task<C.PreconditionResult> CheckPermissionsAsync(C.ICommandContext context, C.CommandInfo command, IServiceProvider services)
        {
            if (context.Message.Attachments.Count == 1 && context.Message.Attachments.First().Filename.EndsWith(".json"))
            {
                return Task.FromResult(C.PreconditionResult.FromSuccess());
            }
            return Task.FromResult(C.PreconditionResult.FromError("A single `.json` file needs to be attached to the message"));
        }
    }

    internal class RequireMimeType : I.ParameterPreconditionAttribute
    {
        private readonly string[] MimeType;
        public RequireMimeType(params string[] type)
        {
            MimeType = type;
        }
        public override Task<I.PreconditionResult> CheckRequirementsAsync(IInteractionContext context, I.IParameterInfo parameterInfo, object value, IServiceProvider services)
        {
            CultureInfo.CurrentCulture = new(context.Interaction.UserLocale);
            CultureInfo.CurrentUICulture = new(context.Interaction.UserLocale);
            if (parameterInfo.ParameterType != typeof(IAttachment))
            {
                return Task.FromException<I.PreconditionResult>(new ArgumentException("Precondition only applicable to File type Inputs"));
            }
            if (value is not null)
            {
                var file = (IAttachment)value;
                var cType = file.ContentType.Split(';')[0].Trim();
                if (MimeType.Contains(cType))
                {
                    return Task.FromResult(I.PreconditionResult.FromSuccess());
                }
                return Task.FromResult(I.PreconditionResult.FromError(String.Format(Properties.Resources.FileTypeNotAllowed, cType, MimeType.Aggregate(String.Empty, (string agg, string current) => agg == String.Empty ? current : agg + "`, `" + current, (string a) => a))));
            }
            return Task.FromResult(I.PreconditionResult.FromSuccess());
        }
    }

    internal class MaxFileSize : I.ParameterPreconditionAttribute
    {
        private readonly double maxMegabyte;
        public MaxFileSize(double maxMegabyte)
        {
            this.maxMegabyte = maxMegabyte;
        }
        public override Task<I.PreconditionResult> CheckRequirementsAsync(IInteractionContext context, I.IParameterInfo parameterInfo, object value, IServiceProvider services)
        {
            CultureInfo.CurrentCulture = new(context.Interaction.UserLocale);
            CultureInfo.CurrentUICulture = new(context.Interaction.UserLocale);
            if (parameterInfo.ParameterType != typeof(IAttachment))
            {
                return Task.FromException<I.PreconditionResult>(new ArgumentException("Precondition only applicable to File type Inputs"));
            }
            if (value is not null)
            {
                var file = (IAttachment)value;
                if (file.Size < maxMegabyte * 1_000_000)
                {
                    return Task.FromResult(I.PreconditionResult.FromSuccess());
                }
                return Task.FromResult(I.PreconditionResult.FromError(String.Format(Properties.Resources.FileTooLarge, maxMegabyte)));
            }
            return Task.FromResult(I.PreconditionResult.FromSuccess());
        }
    }
}
