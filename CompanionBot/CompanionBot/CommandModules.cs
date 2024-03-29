﻿using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace CompanionBot
{
    [Name("PoI Management")]
    public class PoIManagement : ModuleBase<SocketCommandContext>
    {
        private readonly MessageQueue _queue;
        private readonly Logger _logger;
        private readonly HttpClient _webClient;
        private readonly DiscordSocketClient _client;
        public PoIManagement(MessageQueue queue, Logger logger, HttpClient webClient, DiscordSocketClient socketClient)
        {
            _queue = queue;
            _logger = logger;
            _webClient = webClient;
            _client = socketClient;
        }

        protected override void BeforeExecute(CommandInfo command)
        {
            CultureInfo.CurrentCulture = new(Context.Guild.PreferredLocale);
            CultureInfo.CurrentUICulture = new(Context.Guild.PreferredLocale);
        }

        [Command("createmultiple", RunMode = RunMode.Async), Alias("cm"), Summary("Receives data for multiple PoI from the IITC plugin and sends the data one by one for the PokeNav Bot."), RequireWebhook(Group = "Perm"), RequireOwner(Group = "Perm"), RequireAttachedJson]
        public async Task CreatePoIAsync() // Async because download could take time
        {
            ChannelPermissions perms = Context.Guild.GetUser(_client.CurrentUser.Id).GetPermissions(Context.Channel as IGuildChannel);
            string dataString = "";
            try
            {
                dataString = await _webClient.GetStringAsync(Context.Message.Attachments.First().Url);
            }
            catch (HttpRequestException e)
            {
                dataString = "[]";
                await _logger.Log(new LogMessage(LogSeverity.Error, nameof(CreatePoIAsync), $"Download of attached File failed: {e.Message}", e));
                if (perms.SendMessages)
                    await Context.Message.ReplyAsync(String.Format(Properties.Resources.downloadFailed, e.Message));
            }
            List<PortalData> data;
            try
            {
                data = JsonConvert.DeserializeObject<List<PortalData>>(dataString, new JsonSerializerSettings() { MissingMemberHandling = MissingMemberHandling.Error });
            }
            catch (Exception e)
            {
                await Context.Message.ReplyAsync(String.Format(Properties.Resources.jsonParsingFailed, e.Message));
                await _logger.Log(new LogMessage(LogSeverity.Error, "createmultiple", $"JSON Parsing failed in guild {Context.Guild.Name} ({Context.Guild.Id}): {e.Message}", e));
                return;
            }

            await _queue.EnqueueCreate(Context.Guild.Id, Context.Channel, data, perms);
        }

        [Command("edit", RunMode = RunMode.Async), Alias("e"), Summary("Receives a list of Edits to make from the IITC Plugin, sends the PoI Info Command to obtain the PokeNav id and makes the Edit afterwards."), RequireWebhook(Group = "g"), RequireOwner(Group = "g"), RequireAttachedJson]
        public async Task Edit() // Async because Download can take time...
        {
            ChannelPermissions perms = Context.Guild.GetUser(_client.CurrentUser.Id).GetPermissions(Context.Channel as IGuildChannel);
            string dataString = "";
            try
            {
                dataString = await _webClient.GetStringAsync(Context.Message.Attachments.First().Url);
            }
            catch (HttpRequestException e)
            {
                dataString = "[]";
                await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Download of attached File failed: {e.Message}", e));
                if (perms.SendMessages)
                    await Context.Message.ReplyAsync(String.Format(Properties.Resources.downloadFailed, e.Message));
            }

            List<EditData> data;
            try
            {
                data = JsonConvert.DeserializeObject<List<EditData>>(dataString, new JsonSerializerSettings() { MissingMemberHandling = MissingMemberHandling.Error });
            }
            catch (Exception e)
            {
                await Context.Message.ReplyAsync(String.Format(Properties.Resources.jsonParsingFailed, e.Message));
                await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"JSON Parsing failed in guild {Context.Guild.Name} ({Context.Guild.Id}): {e.Message}", e));
                return;
            }
            await _queue.EnqueueEdit(Context, data, perms);
        }
    }
}
