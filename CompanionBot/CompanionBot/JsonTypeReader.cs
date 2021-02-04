using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace CompanionBot
{
    class JsonTypeReader : TypeReader
    {
        public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services)
        {
            object result;
            try
            {
                result = JsonConvert.DeserializeObject(input);
            }
            catch (Exception e)
            {
                return Task.FromResult(TypeReaderResult.FromError(CommandError.ParseFailed, "Input was not a valid Json string."));
            }
            return Task.FromResult(TypeReaderResult.FromSuccess(result));
        }
    }
}
