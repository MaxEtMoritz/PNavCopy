using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using Interactivity;
using Microsoft.Extensions.Configuration;
using ComposableAsync;
using RateLimiter; // TODO: Include License in appropriate location!

namespace CompanionBot
{
    public class MessageQueue
    {
        private readonly DiscordSocketClient _client;
        private readonly Logger _logger;
        private readonly InteractivityService _inter;
        private readonly HttpClient _webClient = new HttpClient(TimeLimiter.GetFromMaxCountByInterval(1, TimeSpan.FromSeconds(1)).AsDelegatingHandler()); // 1 request per second at max!
        private readonly string prefix;
        private readonly ConcurrentDictionary<ulong, CancellationTokenSource> tokens = new ConcurrentDictionary<ulong, CancellationTokenSource>();
        private readonly ConcurrentDictionary<ulong, ConcurrentQueue<string>> createQueues = new ConcurrentDictionary<ulong, ConcurrentQueue<string>>();
        private readonly ConcurrentDictionary<ulong, ConcurrentQueue<EditData>> editQueues = new ConcurrentDictionary<ulong, ConcurrentQueue<EditData>>();
        private readonly ConcurrentDictionary<ulong, Task> workers = new ConcurrentDictionary<ulong, Task>();
        private readonly ConcurrentDictionary<ulong, IUserMessage> progress = new ConcurrentDictionary<ulong, IUserMessage>();
        private readonly GuildSettings _settings;
        private readonly RequestOptions options;
        private readonly RequestOptions typingOptions;
        private TimeSpan averageCreateTime = TimeSpan.FromSeconds(1);
        private TimeSpan averageEditTime = TimeSpan.FromSeconds(2);

        public MessageQueue(DiscordSocketClient client, InteractivityService interactive, Logger logger, GuildSettings settings, IConfiguration config)
        {
            _client = client;
            _inter = interactive;
            _logger = logger;
            _settings = settings;
            prefix = $"<@{config["pokeNavId"]}> ";
            options = RequestOptions.Default;
            options.RetryMode = RetryMode.RetryRatelimit;
            typingOptions = RequestOptions.Default;
            typingOptions.RetryMode = RetryMode.AlwaysFail;
        }

        public Task EnqueueCreate(ICommandContext context, List<string> commands, ChannelPermissions invokingPerms)
        {
            createQueues.AddOrUpdate(context.Guild.Id, new ConcurrentQueue<string>(commands), (ulong id, ConcurrentQueue<string> queue) =>
            {
                foreach (string command in commands)
                {
                    queue.Enqueue(command);
                }
                return queue;
            });

            UpdateProgress(context.Guild.Id, context.Channel, invokingPerms);

            workers.AddOrUpdate(context.Guild.Id, (ulong key) =>
            {
                CancellationTokenSource source = new CancellationTokenSource();
                tokens[key] = source;
                return Work(key, context.Channel, source.Token, invokingPerms);
            }, (ulong key, Task current) =>
            {
                if (current.IsCompleted && !current.IsCanceled)
                {
                    CancellationTokenSource source = new CancellationTokenSource();
                    tokens[key] = source;
                    return Work(key, context.Channel, source.Token, invokingPerms);
                }
                return current;
            });
            return Task.CompletedTask;
        }

        public Task EnqueueEdit(ICommandContext context, List<EditData> edits, ChannelPermissions invokingPerms)
        {
            editQueues.AddOrUpdate(context.Guild.Id, new ConcurrentQueue<EditData>(edits), (ulong key, ConcurrentQueue<EditData> current) =>
              {
                  foreach (EditData data in edits)
                  {
                      current.Enqueue(data);
                  }
                  return current;
              });

            UpdateProgress(context.Guild.Id, context.Channel, invokingPerms);

            workers.AddOrUpdate(context.Guild.Id, (ulong key) =>
            {
                CancellationTokenSource source = new CancellationTokenSource();
                tokens[key] = source;
                return Edit(key, context.Channel, source.Token, invokingPerms);
            }, (ulong key, Task current) =>
            {
                if (current.IsCompleted && !current.IsCanceled)
                {
                    CancellationTokenSource source = new CancellationTokenSource();
                    tokens[key] = source;
                    return Edit(key, context.Channel, source.Token, invokingPerms);
                }
                return current;
            });
            return Task.CompletedTask;
        }

        public Task Pause(SocketCommandContext context)
        {
            ChannelPermissions perms = context.Guild.GetUser(_client.CurrentUser.Id).GetPermissions(context.Channel as IGuildChannel);
            if (workers.TryGetValue(context.Guild.Id, out Task worker) && !worker.IsCompleted)
            {
                if (tokens.TryGetValue(context.Guild.Id, out CancellationTokenSource token))
                { // TODO: Does canceling the same token twice work or does it throw an Exception? can it be in the dictionary, but disposed?
                    token.Cancel(); // Disposing is done in the Task itself when it is canceled or completed.
                    UpdateProgress(context.Guild.Id, context.Channel, perms);
                    if (perms.SendMessages)
                        context.Channel.SendMessageAsync("Successfully paused!");
                }
                else
                {
                    _logger.Log(new LogMessage(LogSeverity.Error, nameof(Pause), $"Task running for Guild {context.Guild.Name} ({context.Guild.Id}), but no Cancellation Token was present!"));
                    if (perms.SendMessages)
                        context.Channel.SendMessageAsync("Error while pausing!");
                }
            }
            else
            {
                _logger.Log(new LogMessage(LogSeverity.Info, nameof(Pause), $"Pause failed in Guild {context.Guild.Name} ({context.Guild.Id}): No Bulk Export was running."));
                if (perms.SendMessages)
                    context.Channel.SendMessageAsync("Nothing to Pause here, no Bulk Export running at the moment!");
            }
            return Task.CompletedTask;
        }

        public Task Resume(SocketCommandContext context)
        {
            ChannelPermissions perms = context.Guild.GetUser(_client.CurrentUser.Id).GetPermissions(context.Channel as IGuildChannel);
            if (!workers.TryGetValue(context.Guild.Id, out Task worker) || worker.IsCompleted)
            {
                if (workers.TryRemove(context.Guild.Id, out Task value))
                    value.Dispose();
                if (createQueues.TryGetValue(context.Guild.Id, out ConcurrentQueue<string> create) && !create.IsEmpty)
                {
                    CancellationTokenSource source = new CancellationTokenSource();
                    tokens[context.Guild.Id] = source;
                    workers[context.Guild.Id] = Work(context.Guild.Id, context.Channel, source.Token, perms);
                    UpdateProgress(context.Guild.Id, context.Channel, perms);
                }
                else if (editQueues.TryGetValue(context.Guild.Id, out ConcurrentQueue<EditData> edit) && !edit.IsEmpty)
                {
                    CancellationTokenSource source = new CancellationTokenSource();
                    tokens[context.Guild.Id] = source;
                    workers[context.Guild.Id] = Edit(context.Guild.Id, context.Channel, source.Token, perms);
                    UpdateProgress(context.Guild.Id, context.Channel, perms);
                }
                else
                {
                    if (perms.SendMessages)
                        context.Channel.SendMessageAsync("No Data to Export present!");
                    _logger.Log(new LogMessage(LogSeverity.Info, nameof(Resume), $"Resume failed in Guild {context.Guild.Name} ({context.Guild.Id}): No Data was present."));
                }
            }
            else
            {
                if (perms.SendMessages)
                    context.Channel.SendMessageAsync("Bulk Export is already running, no need to Resume!");
                _logger.Log(new LogMessage(LogSeverity.Info, nameof(Resume), $"Resume failed in Guild {context.Guild.Name} ({context.Guild.Id}): Bulk Export was already running."));
            }
            return Task.CompletedTask;
        }

        // TODO: When anything fails due to missing permissions but the saved perms said it should be okay, replace perms by calling this helper method!
        private ChannelPermissions RefreshPerms(SocketGuildChannel channel)
        {
            return channel.Guild.GetUser(_client.CurrentUser.Id).GetPermissions(channel);
        }

        private Task UpdateProgress(ulong guild, IMessageChannel channel, ChannelPermissions perms, bool actionRequired = false)
        {
            if (perms.SendMessages && perms.EmbedLinks)
            {
                bool create = createQueues.TryGetValue(guild, out ConcurrentQueue<string> createQueue);
                bool edit = editQueues.TryGetValue(guild, out ConcurrentQueue<EditData> editQueue);
                bool progr = progress.TryGetValue(guild, out IUserMessage message);
                EmbedBuilder embed = new EmbedBuilder()
                {
                    Description = "This is still to do:",
                    Title = workers.TryGetValue(guild, out Task worker) && !worker.IsCompleted && tokens.TryGetValue(guild, out CancellationTokenSource token) && !token.IsCancellationRequested ? "Exporting..." : "Paused",
                    Footer = new EmbedFooterBuilder() { Text = "use pause or resume Commands to manage the export!" },
                    Fields = new List<EmbedFieldBuilder>
                {
                    new EmbedFieldBuilder()
                    {
                        Name = "Creations",
                        Value = create ? createQueue.Count : 0,
                        IsInline = true
                    },
                    new EmbedFieldBuilder()
                    {
                        Name = "Edits",
                        Value = edit ? editQueue.Count : 0,
                        IsInline = true
                    },
                    new EmbedFieldBuilder()
                    {
                        Name = "Time Remaining",
                        Value = Math.Ceiling((((create ? createQueue.Count : 0) * averageCreateTime) + ((edit ? editQueue.Count : 0) * averageEditTime)).TotalSeconds) + "s"
                    }
                }
                }.WithCurrentTimestamp();

                if (actionRequired)
                {
                    embed.Title = "Action Required!";
                    embed.Color = Color.DarkRed;
                    embed.Description = $"Head to <#{_settings[guild].PNavChannel}> and select the right Location!";
                }

                if (progr && (!create || createQueue.IsEmpty) && (!edit || editQueue.IsEmpty))
                {
                    try
                    {
                        message.DeleteAsync(options).Wait();
                        progress.TryRemove(guild, out IUserMessage m); // value is not needed, like the returned bool. if it wasn't in before, that is also good.
                    }
                    catch (Exception e)
                    {
                        _logger.Log(new LogMessage(LogSeverity.Error, nameof(UpdateProgress), $"Deleting Progress Message failed in Guild {guild}: {e.Message}", e));
                    }
                }
                else if (progr)
                {
                    try
                    {
                        message.ModifyAsync((x) =>
                        {
                            x.Embed = embed.WithTimestamp((DateTimeOffset)message.Embeds.First().Timestamp).Build();
                        }, options).Wait();
                    }
                    catch (Exception e)
                    {
                        _logger.Log(new LogMessage(LogSeverity.Error, nameof(UpdateProgress), $"Editing of Progress Message failed in Guild {guild}: {e.Message}", e));
                    }
                }
                else if ((create && !createQueue.IsEmpty) || (edit && !editQueue.IsEmpty))
                {
                    message = channel.SendMessageAsync(embed: embed.Build()).Result;
                    if (perms.ManageMessages)
                        message.PinAsync();
                    progress[guild] = message;
                }
            }
            return Task.CompletedTask;
        }

        private async Task Work(ulong guildId, IMessageChannel invokedChannel, CancellationToken token, ChannelPermissions invokingPerms, ChannelPermissions? modPerms = null)
        {
            if (_settings[guildId].PNavChannel.HasValue)
            {
                IMessageChannel channel = _client.GetChannel(_settings[guildId].PNavChannel.Value) as IMessageChannel;

                if (channel == null)
                {
                    await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Work), $"Mod-Channel was no Message Channel for Guild {guildId}!"));
                    if (invokingPerms.SendMessages)
                        await invokedChannel.SendMessageAsync($"There was a problem with the mod-channel! try to run `{_settings[guildId].Prefix}set mod-channel` and then `{_settings[guildId].Prefix}resume` to try again!");
                    return;
                }

                if (!modPerms.HasValue)
                    modPerms = _client.GetGuild(guildId).CurrentUser.GetPermissions(channel as IGuildChannel);
                DateTime start;
                if (modPerms.Value.SendMessages && createQueues.TryGetValue(guildId, out ConcurrentQueue<string> queue))
                {

                    while (queue.TryDequeue(out string current))
                    {
                        if (token.IsCancellationRequested)
                        {
                            //workers.Remove(guildId);
                            queue.Enqueue(current);
                            if (tokens.TryRemove(guildId, out CancellationTokenSource s))
                                s.Dispose();
                            token.ThrowIfCancellationRequested();
                        }
                        start = DateTime.UtcNow;
                        IDisposable typing = null;
                        try
                        {
                            typing = channel.EnterTypingState(typingOptions);
                        }
                        catch (Exception e)
                        {
                            await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Work), $"Error while entering typing state in Guild {guildId}: {e.Message}", e));
                        }
                        Task t = Task.CompletedTask;
                        try
                        {
                            t = channel.SendMessageAsync($"{prefix}{current}", false, null, options);
                        }
                        catch (Exception e)
                        {
                            await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Work), $"Error while sending Create Message in Guild {guildId}: {e.Message}", e));
                            queue.Enqueue(current); // FIXME: Possibly endless loop if there is a persistent error in sending the message! (can always be aborted with pause command though)
                            continue;
                        }
                        // wait for PokeNav to respond...
                        var Result = await _inter.NextMessageAsync(x => x.Author.Id == 428187007965986826 && x.Channel.Id == channel.Id && x.Embeds.Count == 1 && (x.Content == "The following poi has been created for use in your community:" || x.Embeds.First().Title == "Error"), null, TimeSpan.FromSeconds(10));
                        await t;
                        if (typing != null)
                            typing.Dispose();
                        if (!Result.IsSuccess)
                        {
                            try
                            {
                                await channel.SendMessageAsync($"PokeNav did not respond in time! Please try again by Hand!", false, null, options);
                            }
                            catch (Exception e)
                            {
                                await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Work), $"Error while sending Error Message in Guild {guildId}: {e.Message}", e));
                            }
                            await _logger.Log(new LogMessage(LogSeverity.Info, nameof(Work), $"PokeNav did not respond within 10 seconds in Guild {guildId}."));
                        }
                        if (queue.Count % 5 == 0) // do not edit every time because of rate limits
                        {
                            await UpdateProgress(guildId, channel, modPerms.Value);
                        }
                        averageCreateTime = averageCreateTime * 0.9 + (DateTime.UtcNow - start) * (1 - 0.9); // TODO: does this work like intended?
                    }
                    if (editQueues.TryGetValue(guildId, out ConcurrentQueue<EditData> edit) && !edit.IsEmpty)
                    {
                        workers[guildId] = Edit(guildId, invokedChannel, token, invokingPerms, modPerms); // proceed with edits after creation is complete if there are any.
                    }
                }
            }
            else
            {
                if (invokingPerms.SendMessages)
                    try
                    {
                        await invokedChannel.SendMessageAsync($"PokeNav Moderation Channel not set yet! Run `{_settings[guildId].Prefix}set mod-channel` to set it, then run `{_settings[guildId].Prefix}resume` to create the PoI!");
                    }
                    catch (Exception e)
                    {
                        await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Work), $"Error while sending error message in Guild {guildId}: {e.Message}", e));
                    }
                tokens[guildId]?.Cancel(); // To set IsCancellationRequested to make it paused instead of Exporting
                await UpdateProgress(guildId, invokedChannel, invokingPerms);
                await _logger.Log(new LogMessage(LogSeverity.Info, nameof(Work), $"Execution failed in Guild {guildId}: Mod-Channel was not set!"));
            }


            workers.TryRemove(guildId, out Task value); // value is not needed, bool return also not.
            if (tokens.TryRemove(guildId, out CancellationTokenSource source))
                source.Dispose();
            await UpdateProgress(guildId, invokedChannel, invokingPerms);
        }

        private async Task Edit(ulong guildId, IMessageChannel invokedChannel, CancellationToken token, ChannelPermissions invokingPerms, ChannelPermissions? modPerms = null)
        {

            if (_settings[guildId].PNavChannel != null)
            {
                IMessageChannel channel = _client.GetChannel((ulong)_settings[guildId].PNavChannel) as IMessageChannel;

                if (channel == null)
                {
                    await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Mod-Channel was no Message Channel for Guild {guildId}!"));
                    if (invokingPerms.SendMessages)
                        try
                        {
                            await invokedChannel.SendMessageAsync($"There was a problem with the mod-channel! try to run `{_settings[guildId].Prefix}set mod-channel` and then `{_settings[guildId].Prefix}resume` to try again!");
                        }
                        catch (Exception e)
                        {
                            await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Error while sending error message in Guild {guildId}: {e.Message}", e));
                        }
                    return;
                }

                if (!modPerms.HasValue)
                    modPerms = _client.GetGuild(guildId).CurrentUser.GetPermissions(channel as IGuildChannel);
                if (modPerms.Value.SendMessages && editQueues.TryGetValue(guildId, out var queue))
                {
                    DateTime start;
                    while (queue.TryDequeue(out EditData current))
                    {
                        if (token.IsCancellationRequested)
                        {
                            //workers.Remove(guildId);
                            queue.Enqueue(current); // revert dequeuing
                            if (tokens.TryRemove(guildId, out CancellationTokenSource s))
                                s.Dispose();
                            token.ThrowIfCancellationRequested();
                        }
                        start = DateTime.UtcNow;
                        string type = current.oldType == "pokestop" ? "stop" : current.oldType;
                        Task t = Task.CompletedTask;
                        try
                        {
                            t = channel.SendMessageAsync($"{prefix}{type}-info {current.oldName}");
                        }
                        catch (Exception e)
                        {
                            queue.Enqueue(current);
                            await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Error while sending PoI Info Command in Guild {guildId}: {e.Message}", e));
                            continue;
                        }
                        var result = await _inter.NextMessageAsync((msg) => msg.Author.Id == 428187007965986826 && msg.Channel.Id == channel.Id && msg.Embeds.Count == 1 && (msg.Embeds.First().Title.Equals(current.oldName, StringComparison.OrdinalIgnoreCase) || msg.Embeds.First().Title == "Error" || msg.Embeds.First().Title == "Select Location"), timeout: TimeSpan.FromSeconds(10));
                        await t;
                        if (result.IsSuccess)
                        {
                            Embed embed = result.Value.Embeds.First();
                            if (embed.Title == "Error")
                            {
                                try
                                {
                                    await channel.SendMessageAsync("Edit Failed! PoI not found!");
                                }
                                catch (Exception e)
                                {
                                    await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Error while sending error message in Guild {guildId}: {e.Message}", e));
                                }
                                continue;
                            }
                            else if (embed.Title == "Select Location")
                            {
                                if (modPerms.Value.ReadMessageHistory)
                                {
                                    // handle the Location Select Dialog or skip this edit!
                                    int i;

                                    for (i = 0; i < embed.Fields.Length; i++)
                                    {
                                        string test = embed.Fields[i].Value;
                                        test = test.Substring(test.LastIndexOf("destination=") + 12, test.Length - (test.LastIndexOf("destination=") + 13)); // filter out Coordinates for Google Maps Link
                                        string[] coords = test.Split("%2C+"); // split into north and east Coordinate
                                        if (current.lat == coords[0] && current.lng == coords[1])
                                        {
                                            // found the right one! i will be used to select it!
                                            break;
                                        }
                                    }

                                    //https://stackoverflow.com/a/12858633 answer by user dtb on StackOverflow
                                    SemaphoreSlim signal = new SemaphoreSlim(0, 1);
                                    Task handler(SocketMessage msg)
                                    {
                                        if (signal.CurrentCount == 0 && msg.Channel == channel && msg.Author.Id == 428187007965986826 && msg.Embeds.Count == 1 && string.IsNullOrEmpty(msg.Content)
                                            && msg.Embeds.First().Fields.Any((field) => { return field.Name == "coordinates"; })
                                            && msg.Embeds.First().Fields.Any((field) => { return field.Name == "near"; }))
                                        {
                                            embed = msg.Embeds.First();
                                            signal.Release();
                                        }
                                        return Task.CompletedTask;
                                    }
                                    _client.MessageReceived += handler;

                                    if (i < embed.Fields.Length)
                                    {
                                        // something was found obviously!
                                        Emoji reaction = new Emoji(embed.Fields[i].Name.Substring(0, 2));
                                        if (!modPerms.Value.AddReactions)
                                            await _inter.NextReactionAsync((SocketReaction r) =>
                                            {
                                                return r.MessageId == result.Value.Id && r.Emote.Name == reaction.Name;
                                            }, (SocketReaction r, bool filteredOut) =>
                                            {
                                                if (!filteredOut)
                                                    try
                                                    {
                                                        result.Value.AddReactionAsync(r.Emote, options).Wait();
                                                    }
                                                    catch (Exception e)
                                                    {
                                                        _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Error adding Reaction in Guild {guildId}: {e.Message}", e));
                                                    }
                                                return Task.CompletedTask;
                                            });
                                        else // we have the permission to add reactions, so add it immediately.
                                            try
                                            {
                                                await result.Value.AddReactionAsync(reaction, options);
                                            }
                                            catch (Exception e)
                                            {
                                                await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Error adding Reaction in Guild {guildId}: {e.Message}", e));
                                            }
                                    }
                                    else
                                    {
                                        // edit progress message to show that user action is required!
                                        await UpdateProgress(guildId, invokedChannel, invokingPerms, true);
                                        Task infoEmbed = Task.Run(async () => //separate task to hopefully not block the reaction detection when waiting for Nominatim
                                        {
                                            string addressJson;
                                            try
                                            {
                                                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"https://nominatim.openstreetmap.org/reverse?lat={current.lat}&lon={current.lng}&format=json&addressdetails=0&accept-language={_client.GetGuild(guildId)?.PreferredLocale}");
                                                request.Headers.UserAgent.Clear();
                                                request.Headers.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("IITCPokenavCompanion", null));
                                                var temp = await _webClient.SendAsync(request);
                                                addressJson = await temp.Content.ReadAsStringAsync();
                                            }
                                            catch (Exception e)
                                            {
                                                addressJson = "{\"error\":\"Error getting Address!\"}";
                                                await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Address Request failed in Guild {guildId}: {e.Message}", e));
                                            }
                                            AddressResponse response;
                                            try
                                            {
                                                response = Newtonsoft.Json.JsonConvert.DeserializeObject<AddressResponse>(addressJson);
                                            }
                                            catch (Exception e)
                                            {
                                                await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Error Converting Address Response JSON in Guild {guildId}: {e.Message}", e));
                                                response = new AddressResponse() { error = "Error getting Address!" };
                                            }
                                            string prompt = "Please select the right location! If you are not sure, let it time out!\nThe following info is available:";
                                            if (modPerms.Value.MentionEveryone)
                                                prompt = "@here " + prompt;
                                            EmbedBuilder infoEmbed = null;
                                            if (modPerms.Value.EmbedLinks)
                                            {
                                                infoEmbed = new EmbedBuilder().WithCurrentTimestamp();
                                                infoEmbed.AddField("Name:", current.oldName, true);
                                                infoEmbed.AddField("Type:", current.oldType, true);
                                                infoEmbed.AddField("Address:", $"[{(string.IsNullOrEmpty(response.display_name) ? response.error : response.display_name)}](https://www.google.com/maps/search/?api=1&query={current.lat}%2c%20{current.lng})", true);
                                                string edits = "";
                                                foreach (var pair in current.edits)
                                                {
                                                    edits += $"\n{pair.Key} => {pair.Value}";
                                                }
                                                infoEmbed.AddField("Edits:", edits);
                                            }
                                            else
                                            {
                                                prompt += $"\n\tName: {current.oldName}" +
                                                $"\n\tType: {current.oldType}" +
                                                $"\n\tAddress: {(String.IsNullOrEmpty(response.display_name) ? response.error : response.display_name)} (https://www.google.com/maps/search/?api=1&query={current.lat}%2c%20{current.lng})" +
                                                "\n\tEdits:";
                                                foreach (var pair in current.edits)
                                                {
                                                    prompt += $"\n\t\t{pair.Key} => {pair.Value}";
                                                }
                                            }
                                            try
                                            {
                                                await channel.SendMessageAsync(prompt, embed: infoEmbed?.Build(), options: options);
                                            }
                                            catch (Exception e)
                                            {
                                                await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Error sending selection prompt in Guild {guildId}: {e.Message}", e));
                                            }
                                            return Task.CompletedTask;
                                        });
                                        Regex validEmote = new Regex($"[1-{embed.Fields.Length}]\u20e3");
                                        var reactResult = await _inter.NextReactionAsync((SocketReaction r) =>
                                        r.MessageId == result.Value.Id && r.User.Value.Id != 428187007965986826 && validEmote.IsMatch(r.Emote.Name),
                                        runOnGateway: false,
                                        timeout: TimeSpan.FromMinutes(1));
                                        Console.WriteLine("Success!");
                                        if (!reactResult.IsSuccess)
                                        {
                                            // No one reacted!
                                            await _logger.Log(new LogMessage(LogSeverity.Info, nameof(Edit), $"Timeout while waiting for User Reaction in Guild {guildId}."));
                                            try
                                            {
                                                await channel.SendMessageAsync("No one reacted! Please try again manually!");
                                            }
                                            catch (Exception e)
                                            {
                                                await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Error sending error message in Guild {guildId}: {e.Message}", e));
                                            }
                                            continue;
                                        }
                                        await infoEmbed;
                                        try
                                        {
                                            await result.Value.AddReactionAsync(reactResult.Value.Emote); // FIXME: http 403 forbidden from Discord (if Message History permission lacks)
                                        }
                                        catch (Exception e)
                                        {
                                            await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Error reacting to message in Guild {guildId}: {e.Message}", e));
                                        }
                                    }
                                    await UpdateProgress(guildId, invokedChannel, invokingPerms);

                                    // wait for 10 seconds for the message to get sent
                                    if (!await signal.WaitAsync(10000))
                                    {
                                        // Timeout!
                                        await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Timeout while waiting for PokeNav to send the new Message in Guild {guildId}."));
                                        try
                                        {
                                            await channel.SendMessageAsync("PokeNav did not update the select message within 10 seconds, please try again manually!");
                                        }
                                        catch (Exception e)
                                        {
                                            await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Error sending error message in Guild {guildId}: {e.Message}", e));
                                        }
                                        _client.MessageReceived -= handler;
                                        signal.Dispose();
                                        continue;
                                    }
                                    _client.MessageReceived -= handler;
                                }
                                else
                                {
                                    await channel.SendMessageAsync("Message History permission missing! Unable to handle location select dialog, edit skipped.");
                                    continue;
                                }
                            }
                            string text = embed.Footer.Value.Text.Split('\u25AB')[2];
                            if (!uint.TryParse(text[2..], out uint id))
                            {
                                try
                                {
                                    await channel.SendMessageAsync("Error: Parsing of Location ID failed!");
                                }
                                catch (Exception e)
                                {
                                    await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Error while sending error message in Guild {guildId}: {e.Message}", e));
                                }
                                await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Parsing of location Id failed in Guild {guildId}! Embed had the Footer {text}!"));
                                continue;
                            }
                            string editString;
                            if (current.edits.ContainsKey("type") && current.edits["type"] == "none")
                                editString = $"{prefix}delete poi {id}";
                            else
                            {
                                editString = $"{prefix}update poi {id}";
                                foreach (var pair in current.edits)
                                {
                                    editString += $" \"{pair.Key}: {pair.Value}\"";
                                }
                            }
                            try
                            {
                                t = channel.SendMessageAsync(editString, options: options);
                            }
                            catch (Exception e)
                            {
                                await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Error while sending edit message in Guild {guildId}: {e.Message}", e));
                            }
                            result = await _inter.NextMessageAsync((msg) => msg.Author.Id == 428187007965986826 && msg.Channel.Id == channel.Id && msg.Embeds.Count == 1, timeout: TimeSpan.FromSeconds(10));
                            await t;
                            if (!result.IsSuccess)
                            {
                                //no response in timeout!
                                try
                                {
                                    await channel.SendMessageAsync($"PokeNav did not respond in time! Please try again manually!");
                                }
                                catch (Exception e)
                                {
                                    await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Error while sending error message in Guild {guildId}: {e.Message}", e));
                                }
                                await _logger.Log(new LogMessage(LogSeverity.Info, nameof(Edit), $"PokeNav did not respond in guild {guildId}!"));
                            }
                        }
                        else
                        {
                            // no response in timeout!
                            try
                            {
                                await channel.SendMessageAsync($"PokeNav did not respond in time! Please try again manually!");
                            }
                            catch (Exception e)
                            {
                                await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Error while sending error message in Guild {guildId}: {e.Message}", e));
                            }
                            await _logger.Log(new LogMessage(LogSeverity.Info, nameof(Edit), $"PokeNav did not respond in guild {guildId}!"));
                        }
                        if (queue.Count % 5 == 0) // do not edit every time
                        {
                            await UpdateProgress(guildId, invokedChannel, invokingPerms);
                        }
                        averageEditTime = averageEditTime * 0.9 + (DateTime.UtcNow - start) * (1 - 0.9); // TODO: Does this work like intended?
                    }
                    if (createQueues.TryGetValue(guildId, out ConcurrentQueue<string> create) && !create.IsEmpty)
                    {
                        workers[guildId] = Work(guildId, invokedChannel, token, invokingPerms, modPerms); // proceed with creation after edits are complete if there is anything to create.
                    }
                }
            }
            else
            {
                if (invokingPerms.SendMessages)
                    try
                    {
                        await invokedChannel.SendMessageAsync($"PokeNav Moderation Channel not set yet! Run `{_settings[guildId].Prefix}set mod-channel` to set it, then run `{_settings[guildId].Prefix}resume` to create the PoI!");
                    }
                    catch (Exception e)
                    {
                        await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Error while sending error message in Guild {guildId}: {e.Message}", e));
                    }
                tokens[guildId]?.Cancel(); // Sets IsCancellationRequested to make the Progress Message modify to Paused.
                await UpdateProgress(guildId, invokedChannel, invokingPerms);
                await _logger.Log(new LogMessage(LogSeverity.Info, nameof(Edit), $"Execution failed in Guild {guildId}: Mod-Channel was not set!"));
            }
            workers.TryRemove(guildId, out Task value); // value is not needed, as well as success/failure information.
            if (tokens.TryRemove(guildId, out CancellationTokenSource source))
                source.Dispose();
            await UpdateProgress(guildId, invokedChannel, invokingPerms);
        }
    }
}
