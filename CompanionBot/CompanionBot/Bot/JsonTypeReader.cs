using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Bot
{
    class JsonTypeReader<T> : TypeReader
    {
        public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services)
        {
            T result;
            try
            {
                result = JsonConvert.DeserializeObject<T>(input);
            }
            catch (Exception e)
            {
                return Task.FromResult(TypeReaderResult.FromError(e));
            }
            return Task.FromResult(TypeReaderResult.FromSuccess(result));
        }
    }
}
