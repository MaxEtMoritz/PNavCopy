﻿using Bot;
using Discord.WebSocket;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CompanionBot.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProgressController : ControllerBase
    {
        private readonly MessageQueue _queue;
        private readonly DiscordSocketClient _client;
        private readonly GuildSettings _settings;
        public ProgressController(MessageQueue queue, DiscordSocketClient client, GuildSettings settings)
        {
            _queue = queue;
            _client = client;
            _settings = settings;
        }

        // GET api/<ProgressController>
        [HttpGet("{guildId}")]
        public async Task<ActionResult> Post(ulong guildId, [FromQuery] uint pwd, [FromBody] EditData[] data)
        {
            if (_client.Guilds.Any((x) => x.Id == guildId))
            {
                if (pwd == _settings[guildId].Pwd)
                    return Ok(_queue.GetProgress(guildId));
                else
                    return Unauthorized("Wrong Password!");
            }
            else
            {
                return NotFound("Guild not found!");
            }
        }
    }
}
