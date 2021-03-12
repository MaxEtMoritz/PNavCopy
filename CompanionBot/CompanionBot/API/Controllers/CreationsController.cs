using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bot;
using Discord.WebSocket;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace TestApi.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CreationsController : ControllerBase
    {
        private readonly MessageQueue _queue;
        private readonly DiscordSocketClient _client;
        private readonly GuildSettings _settings;
        public CreationsController(MessageQueue queue, DiscordSocketClient client, GuildSettings settings)
        {
            _queue = queue;
            _client = client;
            _settings = settings;
        }

        // POST api/<CreationsController>
        [HttpPost("{guildId}")]
        public async Task<ActionResult> Post(ulong guildId,[FromQuery] uint pwd, [FromBody] PortalData[] data)
        {
            if (_client.Guilds.Any((x) => x.Id == guildId))
            {
                if (pwd == _settings[guildId].Pwd)
                    await _queue.EnqueueCreate(guildId, data);
                else
                    return Unauthorized("Wrong Password!");
            }
            else
            {
                return NotFound("Guild not found!");
            }
            return Ok();
        }
    }
}
