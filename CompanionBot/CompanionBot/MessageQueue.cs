using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Interactivity;
using Microsoft.Extensions.Configuration;

namespace CompanionBot
{
    public class MessageQueue
    {
        private readonly DiscordSocketClient _client;
        private readonly Logger _logger;
        private readonly InteractivityService _inter;
        private readonly HttpClient _webClient;
        private readonly string prefix;
        private readonly Dictionary<ulong, CancellationTokenSource> tokens = new Dictionary<ulong, CancellationTokenSource>();
        private readonly Dictionary<ulong, Queue<string>> createQueues = new Dictionary<ulong, Queue<string>>();
        private readonly Dictionary<ulong, Queue<EditData>> editQueues = new Dictionary<ulong, Queue<EditData>>();
        private readonly Dictionary<ulong, Task> workers = new Dictionary<ulong, Task>();
        private readonly Dictionary<ulong, IUserMessage> progress = new Dictionary<ulong, IUserMessage>();
        private readonly GuildSettings _settings;
        private readonly RequestOptions options;
        private readonly RequestOptions typingOptions;
        private TimeSpan averageCreateTime = TimeSpan.FromSeconds(1);
        private TimeSpan averageEditTime = TimeSpan.FromSeconds(2);
        private DateTime lastNominatimCall = DateTime.UtcNow - TimeSpan.FromMinutes(1);
        private readonly Mutex nominatimLock = new Mutex();

        public MessageQueue(DiscordSocketClient client, InteractivityService interactive, Logger logger, GuildSettings settings, IConfiguration config, HttpClient webClient)
        {
            _client = client;
            _inter = interactive;
            _logger = logger;
            _settings = settings;
            _webClient = webClient;
            prefix = $"<@{config["pokeNavId"]}> ";
            options = RequestOptions.Default;
            options.RetryMode = RetryMode.RetryRatelimit;
            typingOptions = RequestOptions.Default;
            typingOptions.RetryMode = RetryMode.AlwaysFail;
        }

        public Task EnqueueCreate(ICommandContext context, List<string> commands)
        {
            if (createQueues.ContainsKey(context.Guild.Id))
            {
                var queue = createQueues[context.Guild.Id];
                foreach (string command in commands)
                {
                    queue.Enqueue(command);
                }
            }
            else
            {
                createQueues.Add(context.Guild.Id, new Queue<string>(commands));
            }

            UpdateProgress(context.Guild.Id, context.Channel);

            if (!workers.ContainsKey(context.Guild.Id) || (workers[context.Guild.Id].IsCompleted && !workers[context.Guild.Id].IsCompleted))
            {
                CancellationTokenSource source = new CancellationTokenSource();
                tokens[context.Guild.Id] = source;
                workers.Add(context.Guild.Id, Work(context.Guild.Id, context.Channel, source.Token));
            }
            return Task.CompletedTask;
        }

        public Task EnqueueEdit(ICommandContext context, List<EditData> edits)
        {
            if (editQueues.ContainsKey(context.Guild.Id))
            {
                var queue = editQueues[context.Guild.Id];
                foreach (EditData data in edits)
                {
                    queue.Enqueue(data);
                }
            }
            else
            {
                editQueues.Add(context.Guild.Id, new Queue<EditData>(edits));
            }

            UpdateProgress(context.Guild.Id, context.Channel);

            if (!workers.ContainsKey(context.Guild.Id) || (workers[context.Guild.Id].IsCompleted && !workers[context.Guild.Id].IsCanceled))
            {
                CancellationTokenSource source = new CancellationTokenSource();
                tokens[context.Guild.Id] = source;
                workers[context.Guild.Id] = Edit(context.Guild.Id, context.Channel, source.Token);
            }
            return Task.CompletedTask;
        }

        public Task Pause(ICommandContext context)
        {
            if (workers.ContainsKey(context.Guild.Id) && !workers[context.Guild.Id].IsCompleted)
            {
                if (tokens.TryGetValue(context.Guild.Id, out CancellationTokenSource token))
                {
                    token.Cancel(); // Disposing is done in the Task itself when it is canceled or completed.
                    UpdateProgress(context.Guild.Id, context.Channel);
                    context.Channel.SendMessageAsync("Successfully paused!");
                }
                else
                {
                    _logger.Log(new LogMessage(LogSeverity.Error, nameof(Pause), $"Task running for Guild {context.Guild.Id}, but no Cancellation Token was present!"));
                    context.Channel.SendMessageAsync("Error while pausing!");
                }
            }
            else
            {
                _logger.Log(new LogMessage(LogSeverity.Info, nameof(Pause), $"Pause failed in Guild {context.Guild.Id}: No Bulk Export was running."));
                context.Channel.SendMessageAsync("Nothing to Pause here, no Bulk Export running at the moment!");
            }
            return Task.CompletedTask;
        }

        public Task Resume(ICommandContext context)
        {
            if (!workers.ContainsKey(context.Guild.Id) || workers[context.Guild.Id].IsCompleted)
            {
                if (createQueues.ContainsKey(context.Guild.Id) && createQueues[context.Guild.Id].Any())
                {
                    CancellationTokenSource source = new CancellationTokenSource();
                    tokens[context.Guild.Id] = source;
                    workers[context.Guild.Id] = Work(context.Guild.Id, context.Channel, source.Token);
                    UpdateProgress(context.Guild.Id, context.Channel);
                }
                else if (editQueues.ContainsKey(context.Guild.Id) && editQueues[context.Guild.Id].Any())
                {
                    CancellationTokenSource source = new CancellationTokenSource();
                    tokens[context.Guild.Id] = source;
                    workers[context.Guild.Id] = Edit(context.Guild.Id, context.Channel, source.Token);
                    UpdateProgress(context.Guild.Id, context.Channel);
                }
                else
                {
                    context.Channel.SendMessageAsync("No Data to Export present!");
                    _logger.Log(new LogMessage(LogSeverity.Info, nameof(Resume), $"Resume failed in Guild {context.Guild.Id}: No Data was present."));
                }
            }
            else
            {
                context.Channel.SendMessageAsync("Bulk Export is already running, no need to Resume!");
                _logger.Log(new LogMessage(LogSeverity.Info, nameof(Resume), $"Resume failed in Guild {context.Guild.Id}: Bulk Export was already running."));
            }
            return Task.CompletedTask;
        }

        private Task UpdateProgress(ulong guild, IMessageChannel channel, bool actionRequired = false)
        {
            EmbedBuilder embed = new EmbedBuilder()
            {
                Description = "This is still to do:",
                Title = workers.ContainsKey(guild) && !workers[guild].IsCompleted && tokens.ContainsKey(guild) && !tokens[guild].IsCancellationRequested ? "Exporting..." : "Paused",
                Footer = new EmbedFooterBuilder() { Text = "use pause or resume Commands to manage the export!" },
                Fields = new List<EmbedFieldBuilder>
                {
                    new EmbedFieldBuilder()
                    {
                        Name = "Creations",
                        Value = createQueues.ContainsKey(guild) ? createQueues[guild].Count : 0,
                        IsInline = true
                    },
                    new EmbedFieldBuilder()
                    {
                        Name = "Edits",
                        Value = editQueues.ContainsKey(guild) ? editQueues[guild].Count : 0,
                        IsInline = true
                    },
                    new EmbedFieldBuilder()
                    {
                        Name = "Time Remaining",
                        Value = Math.Ceiling((((createQueues.ContainsKey(guild) ? createQueues[guild].Count : 0) * averageCreateTime) + ((editQueues.ContainsKey(guild) ? editQueues[guild].Count : 0) * averageEditTime)).TotalSeconds) + "s"
                    }
                }
            }.WithCurrentTimestamp();

            if (actionRequired)
            {
                embed.Title = "Action Required!";
                embed.Color = Color.DarkRed;
                embed.Description = $"Head to <#{_settings[guild].PNavChannel}> and select the right Location!";
            }

            if (progress.ContainsKey(guild) && (!createQueues.ContainsKey(guild) || createQueues[guild].Count == 0) && (!editQueues.ContainsKey(guild) || editQueues[guild].Count == 0))
            {
                try
                {
                    progress[guild].DeleteAsync(options);
                    progress.Remove(guild);
                }
                catch (Exception e)
                {
                    _logger.Log(new LogMessage(LogSeverity.Error, nameof(UpdateProgress), $"Deleting Progress Message failed in Guild {guild}: {e.Message}", e));
                }
            }
            else if (progress.ContainsKey(guild))
            {
                try
                {
                    progress[guild].ModifyAsync((x) =>
                    {
                        x.Embed = embed.WithTimestamp((DateTimeOffset)progress[guild].Embeds.First().Timestamp).Build();
                    }, options);
                }
                catch (Exception e)
                {
                    _logger.Log(new LogMessage(LogSeverity.Error, nameof(UpdateProgress), $"Editing of Progress Message failed in Guild {guild}: {e.Message}", e));
                }
            }
            else if ((createQueues.ContainsKey(guild) && createQueues[guild].Count > 0) || (editQueues.ContainsKey(guild) && editQueues[guild].Count > 0))
            {
                progress[guild] = channel.SendMessageAsync(embed: embed.Build()).Result;
                progress[guild].PinAsync();
            }
            return Task.CompletedTask;
        }

        private async Task Work(ulong guildId, IMessageChannel invokedChannel, CancellationToken token)
        {

            if (createQueues.TryGetValue(guildId, out Queue<string> queue))
            {

                if (_settings[guildId].PNavChannel != null)
                {
                    IMessageChannel channel = _client.GetChannel((ulong)_settings[guildId].PNavChannel) as IMessageChannel;

                    if (channel == null)
                    {
                        await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Work), $"Mod-Channel was no Message Channel for Guild {guildId}!"));
                        await invokedChannel.SendMessageAsync($"There was a problem with the mod-channel! try to run `{_settings[guildId].Prefix}set mod-channel` and then `{_settings[guildId].Prefix}resume` to try again!");
                        return;
                    }
                    DateTime start;

                    while (queue.Count > 0)
                    {
                        if (token.IsCancellationRequested)
                        {
                            //workers.Remove(guildId);
                            tokens.Remove(guildId, out CancellationTokenSource s);
                            s.Dispose();
                            token.ThrowIfCancellationRequested();
                        }
                        start = DateTime.UtcNow;
                        string current = queue.Dequeue();
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
                        }
                        // wait for PokeNav to respond...
                        var Result = await _inter.NextMessageAsync(x => x.Author.Id == 428187007965986826 && x.Channel.Id == channel.Id && x.Embeds.Count == 1 && (x.Content == "The following poi has been created for use in your community:" || x.Embeds.First().Title == "Error"), null, TimeSpan.FromSeconds(10));
                        await t;
                        if (typing != null)
                            typing.Dispose();
                        if (Result.IsSuccess == false)
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
                            await UpdateProgress(guildId, channel);
                        }
                        averageCreateTime = averageCreateTime * 0.9 + (DateTime.UtcNow - start) * (1 - 0.9); // TODO: does this work like intended?
                    }
                    if (editQueues.ContainsKey(guildId) && editQueues[guildId].Any())
                    {
                        workers[guildId] = Edit(guildId, invokedChannel, token); // proceed with edits after creation is complete if there are any.
                    }
                }
                else
                {
                    try
                    {
                        await invokedChannel.SendMessageAsync($"PokeNav Moderation Channel not set yet! Run `{_settings[guildId].Prefix}set mod-channel` to set it, then run `{_settings[guildId].Prefix}resume` to create the PoI!");
                    }
                    catch (Exception e)
                    {
                        await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Work), $"Error while sending error message in Guild {guildId}: {e.Message}", e));
                    }
                    tokens[guildId]?.Cancel(); // To set IsCancellationRequested to make it paused instead of Exporting
                    await UpdateProgress(guildId, invokedChannel);
                    await _logger.Log(new LogMessage(LogSeverity.Info, nameof(Work), $"Execution failed in Guild {guildId}: Mod-Channel was not set!"));
                }
            }

            workers.Remove(guildId);
            tokens.Remove(guildId, out CancellationTokenSource source);
            source.Dispose();
            await UpdateProgress(guildId, invokedChannel);
        }

        private async Task Edit(ulong guildId, IMessageChannel invokedChannel, CancellationToken token)
        {
            if (editQueues.TryGetValue(guildId, out var queue))
            {
                if (_settings[guildId].PNavChannel != null)
                {
                    IMessageChannel channel = _client.GetChannel((ulong)_settings[guildId].PNavChannel) as IMessageChannel;

                    if (channel == null)
                    {
                        await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Mod-Channel was no Message Channel for Guild {guildId}!"));
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
                    DateTime start;
                    while (queue.Count > 0)
                    {
                        if (token.IsCancellationRequested)
                        {
                            //workers.Remove(guildId);
                            tokens.Remove(guildId, out CancellationTokenSource s);
                            s.Dispose();
                            token.ThrowIfCancellationRequested();
                        }
                        start = DateTime.UtcNow;
                        EditData current = queue.Dequeue();
                        string type = current.oldType == "pokestop" ? "stop" : current.oldType;
                        Task t = Task.CompletedTask;
                        try
                        {
                            t = channel.SendMessageAsync($"{prefix}{type}-info {current.oldName}");
                        }
                        catch (Exception e)
                        {
                            await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Error while sending PoI Info Command in Guild {guildId}: {e.Message}", e));
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
                                    //https://stackoverflow.com/a/12858633 answer by user dtb on StackOverflow
                                    SemaphoreSlim reactSignal = new SemaphoreSlim(0, 1);
                                    Task reactHandler(Cacheable<IUserMessage, ulong> cache, ISocketMessageChannel channl, SocketReaction reaction)
                                    {
                                        bool validEmote = new Regex($"[1-{embed.Fields.Length}]\u20e3").IsMatch(reaction.Emote.Name);
                                        if (reactSignal.CurrentCount == 0 && channl.Id == channel.Id && result.Value.Id == reaction.MessageId && reaction.User.Value.Id != 428187007965986826 && validEmote)
                                        {
                                            reactSignal.Release();
                                            try
                                            {
                                                result.Value.AddReactionAsync(reaction.Emote);
                                            }
                                            catch (Exception e)
                                            {
                                                _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Error reacting to message in Guild {guildId}: {e.Message}", e));
                                            }
                                        }
                                        return Task.CompletedTask;
                                    }
                                    _client.ReactionAdded += reactHandler;

                                    // edit progress message to show that user action is required!
                                    await UpdateProgress(guildId, invokedChannel, true);
                                    string addressJson;
                                    nominatimLock.WaitOne();
                                    TimeSpan timeToWait = DateTime.UtcNow - lastNominatimCall;
                                    if (timeToWait < TimeSpan.FromSeconds(1))
                                    {
                                        await Task.Delay(TimeSpan.FromSeconds(1) - timeToWait); // wait until Nominatim API can be called again
                                    }
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
                                    finally
                                    {
                                        lastNominatimCall = DateTime.UtcNow;
                                        nominatimLock.ReleaseMutex();
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

                                    string prompt = $"@here please select the right location! If you are not sure, let it time out!\nThe following info is available:";
                                    EmbedBuilder infoEmbed = new EmbedBuilder().WithCurrentTimestamp();
                                    infoEmbed.AddField("Name:", current.oldName, true);
                                    infoEmbed.AddField("Type:", current.oldType, true);
                                    infoEmbed.AddField("Address:", $"[{(string.IsNullOrEmpty(response.display_name) ? response.error : response.display_name)}](https://www.google.com/maps/search/?api=1&query={current.lat}%2c%20{current.lng})", true);
                                    string edits = "";
                                    foreach (var pair in current.edits)
                                    {
                                        edits += $"\n{pair.Key} => {pair.Value}";
                                    }
                                    infoEmbed.AddField("Edits:", edits);
                                    try
                                    {
                                        await channel.SendMessageAsync(prompt, embed: infoEmbed.Build(), options: options);
                                    }
                                    catch (Exception e)
                                    {
                                        await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Error sending selection prompt in Guild {guildId}: {e.Message}", e));
                                    }
                                    if (!await reactSignal.WaitAsync(result.Value.Timestamp.AddSeconds(60) - DateTime.Now)) // that should wait a bit shorter than PokeNav is waiting!
                                    {
                                        // No one reacted!
                                        await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Timeout while waiting for User Reaction in Guild {guildId}."));
                                        try
                                        {
                                            await channel.SendMessageAsync("No one reacted! Please try again manually!");
                                        }
                                        catch (Exception e)
                                        {
                                            await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Error sending error message in Guild {guildId}: {e.Message}", e));
                                        }
                                        _client.ReactionAdded -= reactHandler;
                                        reactSignal.Dispose();
                                        continue;
                                    }
                                    _client.ReactionAdded -= reactHandler;
                                    await UpdateProgress(guildId, invokedChannel);
                                }

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
                            await UpdateProgress(guildId, invokedChannel);
                        }
                        averageEditTime = averageEditTime * 0.9 + (DateTime.UtcNow - start) * (1 - 0.9); // TODO: Does this work like intended?
                    }
                    if (createQueues.ContainsKey(guildId) && createQueues[guildId].Any())
                    {
                        workers[guildId] = Work(guildId, invokedChannel, token); // proceed with creation after edits are complete if there is anything to create.
                    }
                }
                else
                {
                    try
                    {
                        await invokedChannel.SendMessageAsync($"PokeNav Moderation Channel not set yet! Run `{_settings[guildId].Prefix}set mod-channel` to set it, then run `{_settings[guildId].Prefix}resume` to create the PoI!");
                    }
                    catch (Exception e)
                    {
                        await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Error while sending error message in Guild {guildId}: {e.Message}", e));
                    }
                    tokens[guildId]?.Cancel(); // Sets IsCancellationRequested to make the Progress Message modify to Paused.
                    await UpdateProgress(guildId, invokedChannel);
                    await _logger.Log(new LogMessage(LogSeverity.Info, nameof(Edit), $"Execution failed in Guild {guildId}: Mod-Channel was not set!"));
                }
            }

            workers.Remove(guildId);
            tokens.Remove(guildId, out CancellationTokenSource source);
            source.Dispose();
            await UpdateProgress(guildId, invokedChannel);
        }
    }
}
