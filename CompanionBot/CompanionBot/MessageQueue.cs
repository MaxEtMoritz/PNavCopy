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
using Fergun.Interactive;
using Microsoft.Extensions.Configuration;
using ComposableAsync;
using RateLimiter;
using Discord.Interactions;
using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.IO;

namespace CompanionBot
{
    public class MessageQueue
    {
        private readonly DiscordSocketClient _client;
        private readonly Logger _logger;
        private readonly InteractiveService _inter;
        private readonly HttpClient _webClient = new(TimeLimiter.GetFromMaxCountByInterval(1, TimeSpan.FromSeconds(1)).AsDelegatingHandler()); // 1 request per second at max!
        private readonly string prefix;
        private readonly ConcurrentDictionary<ulong, CancellationTokenSource> tokens = new();
        private readonly ConcurrentDictionary<ulong, ConcurrentQueue<string>> createQueues;
        private readonly ConcurrentDictionary<ulong, ConcurrentQueue<EditData>> editQueues;
        private readonly ConcurrentDictionary<ulong, Task> workers = new();
        private readonly ConcurrentDictionary<ulong, IUserMessage> progress = new();
        private readonly GuildSettings _settings;
        private readonly RequestOptions options;
        private readonly RequestOptions typingOptions;
        private readonly IConfiguration _config;
        private TimeSpan averageCreateTime = TimeSpan.FromSeconds(1);
        private TimeSpan averageEditTime = TimeSpan.FromSeconds(2);

        public MessageQueue(DiscordSocketClient client, InteractiveService interactive, Logger logger, GuildSettings settings, IConfiguration config)
        {
            _client = client;
            _inter = interactive;
            _logger = logger;
            _settings = settings;
            _config = config;
            prefix = $"<@{config["pokeNavId"]}> ";
            options = RequestOptions.Default;
            options.RetryMode = RetryMode.RetryRatelimit;
            typingOptions = RequestOptions.Default;
            typingOptions.RetryMode = RetryMode.AlwaysFail;

            //deserialize state
            if (File.Exists("pendingCreations.json"))
            {
                var ms = File.OpenRead("pendingCreations.json");
                createQueues = JsonSerializer.Deserialize<ConcurrentDictionary<ulong, ConcurrentQueue<string>>>(ms);
                _logger.Log(new(LogSeverity.Info, nameof(MessageQueue), "deserialized createQueues."));
                ms.Close();
                File.Delete("pendingCreations.json");
            }
            createQueues ??= new();
            if (File.Exists("pendingEdits.json"))
            {
                var ms = File.OpenRead("pendingEdits.json");
                editQueues = JsonSerializer.Deserialize<ConcurrentDictionary<ulong, ConcurrentQueue<EditData>>>(ms);
                _logger.Log(new(LogSeverity.Info, nameof(MessageQueue), "deserialized editQueues."));
                ms.Close();
                File.Delete("pendingEdits.json");
            }
            editQueues ??= new();
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
                CancellationTokenSource source = new();
                tokens[key] = source;
                return Work(key, context.Channel, invokingPerms, source.Token);
            }, (ulong key, Task current) =>
            {
                if (current.IsCompleted && !current.IsCanceled)
                {
                    CancellationTokenSource source = new();
                    tokens[key] = source;
                    return Work(key, context.Channel, invokingPerms, source.Token);
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
                CancellationTokenSource source = new();
                tokens[key] = source;
                return Edit(key, context.Channel, invokingPerms, source.Token);
            }, (ulong key, Task current) =>
            {
                if (current.IsCompleted && !current.IsCanceled)
                {
                    CancellationTokenSource source = new();
                    tokens[key] = source;
                    return Edit(key, context.Channel, invokingPerms, source.Token);
                }
                return current;
            });
            return Task.CompletedTask;
        }

        public async Task Pause(SocketInteractionContext context)
        {
            CultureInfo.CurrentCulture = new(context.Interaction.UserLocale);
            CultureInfo.CurrentUICulture = new(context.Interaction.UserLocale);
            ChannelPermissions perms = context.Guild.GetUser(_client.CurrentUser.Id).GetPermissions(context.Channel as IGuildChannel);
            if (workers.TryGetValue(context.Guild.Id, out Task worker) && !worker.IsCompleted)
            {
                if (tokens.TryGetValue(context.Guild.Id, out CancellationTokenSource token))
                { // TODO: Does canceling the same token twice work or does it throw an Exception? can it be in the dictionary, but disposed?
                    token.Cancel(); // Disposing is done in the Task itself when it is canceled or completed.
                    await context.Interaction.RespondAsync(Properties.Resources.pauseInProgress);
                    await worker.ContinueWith((state) =>
                    {
                        context.Interaction.FollowupAsync(Properties.Resources.pauseSuccessful);
                        UpdateProgress(context.Guild.Id, context.Channel, perms);
                    });
                }
                else
                {
                    await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Pause), $"Task running for Guild {context.Guild.Name} ({context.Guild.Id}), but no Cancellation Token was present!"));
                    await context.Interaction.RespondAsync(Properties.Resources.pauseError);
                }
            }
            else
            {
                await _logger.Log(new LogMessage(LogSeverity.Info, nameof(Pause), $"Pause failed in Guild {context.Guild.Name} ({context.Guild.Id}): No Bulk Import was running."));
                await context.Interaction.RespondAsync(Properties.Resources.pauseNoOp);
            }
        }

        public async Task Resume(SocketInteractionContext context)
        {
            CultureInfo.CurrentCulture = new(context.Interaction.UserLocale);
            CultureInfo.CurrentUICulture = new(context.Interaction.UserLocale);
            ChannelPermissions perms = context.Guild.GetUser(_client.CurrentUser.Id).GetPermissions(context.Channel as IGuildChannel);
            if (!workers.TryGetValue(context.Guild.Id, out Task worker) || worker.IsCompleted)
            {
                if (workers.TryRemove(context.Guild.Id, out Task value))
                    value.Dispose();
                if (createQueues.TryGetValue(context.Guild.Id, out ConcurrentQueue<string> create) && !create.IsEmpty)
                {
                    CancellationTokenSource source = new();
                    tokens[context.Guild.Id] = source;
                    workers[context.Guild.Id] = Work(context.Guild.Id, context.Channel, perms, source.Token);
                    await context.Interaction.RespondAsync(Properties.Resources.resume);
                    await UpdateProgress(context.Guild.Id, context.Channel, perms);
                }
                else if (editQueues.TryGetValue(context.Guild.Id, out ConcurrentQueue<EditData> edit) && !edit.IsEmpty)
                {
                    CancellationTokenSource source = new();
                    tokens[context.Guild.Id] = source;
                    workers[context.Guild.Id] = Edit(context.Guild.Id, context.Channel, perms, source.Token);
                    await context.Interaction.RespondAsync(Properties.Resources.resume);
                    await UpdateProgress(context.Guild.Id, context.Channel, perms);
                }
                else
                {
                    await context.Interaction.RespondAsync(Properties.Resources.resumeNoOp);
                    await _logger.Log(new LogMessage(LogSeverity.Info, nameof(Resume), $"Resume failed in Guild {context.Guild.Name} ({context.Guild.Id}): No Data was present."));
                }
            }
            else
            {
                await context.Interaction.RespondAsync(Properties.Resources.resumeAlreadyRunning);
                await _logger.Log(new LogMessage(LogSeverity.Info, nameof(Resume), $"Resume failed in Guild {context.Guild.Name} ({context.Guild.Id}): Bulk Import was already running."));
            }
        }

        internal List<(ulong server, int creations, int edits)> GetState()
        {
            List<(ulong, int, int)> result = new();
            // get keys that are in both dicts
            foreach (ulong key in createQueues.Keys.Intersect(editQueues.Keys))
            {
                int c = 0;
                if (createQueues.TryGetValue(key, out var v))
                {
                    c = v.Count;
                }
                int e = 0;
                if (editQueues.TryGetValue(key, out var v2))
                {
                    e = v2.Count;
                }
                if (c > 0 || e > 0)
                    result.Add((key, c, e));
            }
            // get keys only in createQueues
            foreach (ulong key in createQueues.Keys.Except(editQueues.Keys))
            {
                int c = 0;
                if (createQueues.TryGetValue(key, out var v))
                {
                    c = v.Count;
                }
                if (c > 0)
                    result.Add((key, c, 0));
            }
            // get keys only in editQueues
            foreach (ulong key in editQueues.Keys.Except(createQueues.Keys))
            {
                int e = 0;
                if (editQueues.TryGetValue(key, out var v2))
                {
                    e = v2.Count;
                }
                if (e > 0)
                    result.Add((key, 0, e));
            }
            return result;
        }

        internal async Task SaveState()
        {
            // cancel all workers and wait for them
            foreach (CancellationTokenSource token in tokens.Values)
            {
                if (!token.IsCancellationRequested)
                {
                    await _logger.Log(new(LogSeverity.Debug, nameof(SaveState), "canceling token..."));
                    token.Cancel();
                }
            }
            var waiter = Task.WhenAll(workers.Values);
            try
            {
                waiter.Wait(100000);
            }
            catch (AggregateException agg)
            {
                if (agg.InnerExceptions.Any(x => !(x is TaskCanceledException)))
                {
                    throw;
                }
                else
                {
                    await _logger.Log(new LogMessage(LogSeverity.Info, nameof(SaveState), $"{agg.InnerExceptions.Count} TaskCanceledExceptions"));
                }
            }
            if (!waiter.IsCompleted)
            {
                await _logger.Log(new(LogSeverity.Warning, nameof(SaveState), "Not all workers completed after 100 seconds, maybe inconsistencies..."));
            }

            // serialize creations and edits to json
            var ms = File.Create("pendingCreations.json");
            await JsonSerializer.SerializeAsync(ms, createQueues);
            await ms.FlushAsync();
            ms.Close();

            ms = File.Create("pendingEdits.json");
            await JsonSerializer.SerializeAsync(ms, editQueues);
            await ms.FlushAsync();
            ms.Close();
        }

        private Task<bool> TryToSendMessage(IMessageChannel channel, ref ChannelPermissions perms, string text = null, Embed embed = null, RequestOptions options = null, ushort numTry = 0)
        {
            if (perms.SendMessages && (embed != null && perms.EmbedLinks || embed == null) && numTry < 2)
            {
                try
                {
                    channel.SendMessageAsync(text, embed: embed, options: options).Wait();
                }
                catch (HttpException e)
                {
                    if (e.HttpCode == System.Net.HttpStatusCode.Forbidden || e.DiscordCode != null && e.DiscordCode == DiscordErrorCode.MissingPermissions) // Permissions Missing
                    {
                        _logger.Log(new LogMessage(LogSeverity.Info, nameof(TryToSendMessage), $"SendMessage Permission lost in channel #{channel.Name} (Guild {(channel as IGuildChannel)?.GuildId}), refreshing permissions...", e));
                        perms = (channel as SocketGuildChannel).Guild.GetUser(_client.CurrentUser.Id).GetPermissions(channel as IGuildChannel);
                        return TryToSendMessage(channel, ref perms, text, embed, options, ++numTry);
                    }
                    else
                    {
                        _logger.Log(new LogMessage(LogSeverity.Error, this.GetType().Name, $"Unexpected Exception while trying to send Message to Channel {channel.Id}.", e));
                        return Task.FromResult(false);
                    }
                }
                return Task.FromResult(true);
            }
            else
            {
                return Task.FromResult(false);
            }
        }

        private Task<bool> TryToReact(SocketMessage message, Emoji reaction, ref ChannelPermissions perms, RequestOptions options = null, ushort numTry = 0)
        {
            if (numTry < 2 && perms.ReadMessageHistory && (perms.AddReactions || message.Reactions.ContainsKey(reaction)))
            {
                try
                {
                    message.AddReactionAsync(reaction, options).Wait();
                }
                catch (HttpException e)
                {
                    if (e.HttpCode == System.Net.HttpStatusCode.Forbidden || e.DiscordCode != null && e.DiscordCode == DiscordErrorCode.MissingPermissions)
                    {
                        _logger.Log(new LogMessage(LogSeverity.Info, nameof(TryToReact), $"Reaction related permission(s) lost in channel {message.Channel.Name}, refreshing perms...", e));
                        perms = (message.Channel as SocketGuildChannel).Guild.GetUser(_client.CurrentUser.Id).GetPermissions(message.Channel as IGuildChannel);
                        return TryToReact(message, reaction, ref perms, options, ++numTry);
                    }
                    else
                    {
                        _logger.Log(new LogMessage(LogSeverity.Error, nameof(TryToReact), $"Unexpected exception while adding reaction to message in channel {message.Channel.Name} ({message.Channel.Id}): {e.Message}", e));
                        return Task.FromResult(false);
                    }
                }
                return Task.FromResult(true);
            }
            else
                return Task.FromResult(false);
        }

        private Task UpdateProgress(ulong guild, IMessageChannel channel, ChannelPermissions perms, bool actionRequired = false)
        {
            if (perms.SendMessages && perms.EmbedLinks)
            {
                CultureInfo.CurrentCulture = new((channel as IGuildChannel).Guild.PreferredLocale);
                CultureInfo.CurrentUICulture = new((channel as IGuildChannel).Guild.PreferredLocale);
                bool create = createQueues.TryGetValue(guild, out ConcurrentQueue<string> createQueue);
                bool edit = editQueues.TryGetValue(guild, out ConcurrentQueue<EditData> editQueue);
                bool progr = progress.TryGetValue(guild, out IUserMessage message);
                EmbedBuilder embed = new EmbedBuilder()
                {
                    Description = Properties.Resources.stillToDo,
                    Title = workers.TryGetValue(guild, out Task worker) && !worker.IsCompleted && tokens.TryGetValue(guild, out CancellationTokenSource token) && !token.IsCancellationRequested ? Properties.Resources.importing : Properties.Resources.paused,
                    Footer = new EmbedFooterBuilder() { Text = Properties.Resources.embedFooter },
                    Fields = new List<EmbedFieldBuilder>
                {
                    new EmbedFieldBuilder()
                    {
                        Name = Properties.Resources.creations,
                        Value = create ? createQueue.Count : 0,
                        IsInline = true
                    },
                    new EmbedFieldBuilder()
                    {
                        Name = Properties.Resources.edits,
                        Value = edit ? editQueue.Count : 0,
                        IsInline = true
                    },
                    new EmbedFieldBuilder()
                    {
                        Name = Properties.Resources.timeRemaining,
                        Value = Math.Ceiling((((create ? createQueue.Count : 0) * averageCreateTime) + ((edit ? editQueue.Count : 0) * averageEditTime)).TotalSeconds) + "s"
                    }
                }
                }.WithCurrentTimestamp();

                if (actionRequired)
                {
                    embed.Title = Properties.Resources.actionRequired;
                    embed.Color = Color.DarkRed;
                    embed.Description = String.Format(Properties.Resources.selectRightLocation, _settings[guild].PNavChannel.Value);
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
                    try
                    {
                        message = channel.SendMessageAsync(embed: embed.Build()).Result;
                    }
                    catch (HttpException e)
                    {
                        _logger.Log(new LogMessage(LogSeverity.Error, nameof(UpdateProgress), $"Error while sending Progress embed in guild {guild}: {e.Message}", e));
                        message = null;
                    }
                    if (perms.ManageMessages)
                        try
                        {
                            message?.PinAsync().Wait();
                        }
                        catch (HttpException e)
                        {
                            _logger.Log(new LogMessage(LogSeverity.Error, nameof(UpdateProgress), $"Error pinning progress message in guild {guild}: {e.Message}", e));
                        }
                    progress[guild] = message;
                }
            }
            return Task.CompletedTask;
        }

#pragma warning disable CA2016 // Parameter "CancellationToken" an Methoden weiterleiten, die diesen Parameter akzeptieren
        private async Task Work(ulong guildId, IMessageChannel invokedChannel, ChannelPermissions invokingPerms, CancellationToken token)
        {
            CultureInfo.CurrentCulture = new((invokedChannel as IGuildChannel).Guild.PreferredLocale);
            CultureInfo.CurrentUICulture = new((invokedChannel as IGuildChannel).Guild.PreferredLocale);
            if (_settings[guildId].PNavChannel.HasValue)
            {
                if (_client.GetChannel(_settings[guildId].PNavChannel.Value) is not IMessageChannel channel)
                {
                    await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Work), $"Mod-Channel was no Message Channel for Guild {guildId}!"));
                    if (invokingPerms.SendMessages)
                        await TryToSendMessage(invokedChannel, ref invokingPerms, Properties.Resources.modChannelProblem);
                    return;
                }

                ChannelPermissions modPerms = _client.GetGuild(guildId).CurrentUser.GetPermissions(channel as IGuildChannel);
                DateTime start;
                if (modPerms.SendMessages && createQueues.TryGetValue(guildId, out ConcurrentQueue<string> queue))
                {

                    while (modPerms.SendMessages && queue.TryDequeue(out string current))
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
                        CancellationTokenSource ct = new();
                        // wait for PokeNav to respond...
                        var nma = _inter.NextMessageAsync(x => x.Author.Id == ulong.Parse(_config["pokeNavId"]) && x.Channel.Id == channel.Id && x.Embeds.Count == 1 && (x.Content == "The following poi has been created for use in your community:" || x.Embeds.First().Title == "Error" || x.Content.StartsWith("Error:")), null, TimeSpan.FromSeconds(10), cancellationToken: ct.Token);
                        Task<bool> t = TryToSendMessage(channel, ref modPerms, $"{prefix}{current}", options: options);
                        if (!await t)
                        {
                            ct.Cancel();
                            await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Work), $"Error while sending Create Message in Guild {guildId}."));
                            queue.Enqueue(current); // FIXME: Possibly endless loop if there is a persistent error in sending the message! (can always be aborted with pause command though)
                            continue;
                        }
                        var Result = await nma;
                        ct.Dispose();
                        if (typing != null)
                            typing.Dispose();
                        if (!Result.IsSuccess)
                        {
                            if (!await TryToSendMessage(channel, ref modPerms, Properties.Resources.pokeNavTimeout, options: options))
                            {
                                await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Work), $"Error while sending Error Message in Guild {guildId}."));
                            }
                            await _logger.Log(new LogMessage(LogSeverity.Info, nameof(Work), $"PokeNav did not respond within 10 seconds in Guild {guildId}."));
                        }
                        if (queue.Count % 5 == 0) // do not edit every time because of rate limits
                        {
                            await UpdateProgress(guildId, channel, modPerms);
                        }
                        averageCreateTime = averageCreateTime * 0.9 + (DateTime.UtcNow - start) * (1 - 0.9); // TODO: does this work like intended?
                    }
                    if (editQueues.TryGetValue(guildId, out ConcurrentQueue<EditData> edit) && !edit.IsEmpty)
                    {
                        workers[guildId] = Edit(guildId, invokedChannel, invokingPerms, token); // proceed with edits after creation is complete if there are any.
                    }
                }
            }
            else
            {
                if (invokingPerms.SendMessages)
                    if (!await TryToSendMessage(invokedChannel, ref invokingPerms, Properties.Resources.modChannelUnset))
                    {
                        await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Work), $"Error while sending error message in Guild {guildId}."));
                    }
                tokens[guildId]?.Cancel(); // To set IsCancellationRequested to make it paused instead of Exporting
                await UpdateProgress(guildId, invokedChannel, invokingPerms);
                await _logger.Log(new LogMessage(LogSeverity.Info, nameof(Work), $"Execution failed in Guild {guildId}: Mod-Channel was not set!"));
            }


            workers.TryRemove(guildId, out _); // value is not needed, bool return also not.
            if (tokens.TryRemove(guildId, out CancellationTokenSource source))
                source.Dispose();
            await UpdateProgress(guildId, invokedChannel, invokingPerms);
        }

        private async Task Edit(ulong guildId, IMessageChannel invokedChannel, ChannelPermissions invokingPerms, CancellationToken token)
        {
            CultureInfo.CurrentCulture = new((invokedChannel as IGuildChannel).Guild.PreferredLocale);
            CultureInfo.CurrentUICulture = new((invokedChannel as IGuildChannel).Guild.PreferredLocale);
            if (_settings[guildId].PNavChannel != null)
            {
                if (_client.GetChannel((ulong)_settings[guildId].PNavChannel) is not IMessageChannel channel)
                {
                    await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Mod-Channel was no Message Channel for Guild {guildId}!"));
                    if (invokingPerms.SendMessages)
                        if (!await TryToSendMessage(invokedChannel, ref invokingPerms, Properties.Resources.modChannelProblem))
                        {
                            await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Error while sending error message in Guild {guildId}."));
                        }
                    return;
                }

                ChannelPermissions modPerms = _client.GetGuild(guildId).CurrentUser.GetPermissions(channel as IGuildChannel);
                if (modPerms.SendMessages && editQueues.TryGetValue(guildId, out var queue))
                {
                    DateTime start;
                    while (modPerms.SendMessages && queue.TryDequeue(out EditData current))
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
                        string type = current.oldType == PoiType.pokestop ? "stop" : "gym";
                        CancellationTokenSource ct = new();
                        var nma = _inter.NextMessageAsync((msg) =>
                            msg.Author.Id == ulong.Parse(_config["pokeNavId"])
                            && msg.Channel.Id == channel.Id
                            && msg.Embeds.Count == 1
                            && (msg.Embeds.First().Title.Equals(current.oldName, StringComparison.OrdinalIgnoreCase)
                            || msg.Embeds.First().Title == "Error"
                            || msg.Embeds.First().Title == "Select Location"),
                        timeout: TimeSpan.FromSeconds(10),
                        cancellationToken: ct.Token);
                        Task<bool> t = TryToSendMessage(channel, ref modPerms, $"{prefix}{type}-info {current.oldName}");
                        if (!await t)
                        {
                            ct.Cancel();
                            queue.Enqueue(current);
                            await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Error while sending PoI Info Command in Guild {guildId}."));
                            continue;
                        }
                        var result = await nma;
                        if (result.IsSuccess)
                        {
                            Embed embed = result.Value.Embeds.First();
                            if (embed.Title == "Error")
                            {
                                if (!await TryToSendMessage(channel, ref modPerms, Properties.Resources.poiNotFound))
                                {
                                    await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Error while sending error message in Guild {guildId}."));
                                }
                                continue;
                            }
                            else if (embed.Title == "Select Location")
                            {
                                if (modPerms.ReadMessageHistory)
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
                                            // found the right one! Variable "i" will be used to select it!
                                            break;
                                        }
                                    }

                                    //https://stackoverflow.com/a/12858633 answer by user dtb on StackOverflow
                                    SemaphoreSlim signal = new(0, 1);
                                    Task handler(SocketMessage msg)
                                    {
                                        if (signal.CurrentCount == 0 && msg.Channel == channel && msg.Author.Id == ulong.Parse(_config["pokeNavId"]) && msg.Embeds.Count == 1 && string.IsNullOrEmpty(msg.Content)
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
                                        Emoji reaction = new(embed.Fields[i].Name.Substring(0, 2));
                                        if (!modPerms.AddReactions)
                                        {

                                            await _inter.NextReactionAsync((SocketReaction r) =>
                                            {
                                                return r.MessageId == result.Value.Id && r.Emote.Name == reaction.Name && r.UserId != _client.CurrentUser.Id;
                                            }, async (SocketReaction r, bool passedFilter) =>
                                            {
                                                Console.WriteLine(passedFilter);
                                                if (passedFilter)
                                                {
                                                    try
                                                    {
                                                        await result.Value.AddReactionAsync(r.Emote, options);
                                                    }
                                                    catch (Exception e)
                                                    {
                                                        await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Error adding Reaction in Guild {guildId}: {e.Message}", e));
                                                    }
                                                }
                                            });
                                        }
                                        else // we have the permission to add reactions, so add it immediately.
                                            if (!await TryToReact(result.Value, reaction, ref modPerms, options))
                                        {

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
                                                HttpRequestMessage request = new(HttpMethod.Get, $"https://nominatim.openstreetmap.org/reverse?lat={current.lat}&lon={current.lng}&format=json&addressdetails=0&accept-language={_client.GetGuild(guildId)?.PreferredLocale}");
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
                                            string prompt = Properties.Resources.editMultiMatches;
                                            if (modPerms.MentionEveryone)
                                                prompt = "@here " + prompt;
                                            EmbedBuilder infoEmbed = null;
                                            if (modPerms.EmbedLinks)
                                            {
                                                infoEmbed = new EmbedBuilder().WithCurrentTimestamp();
                                                infoEmbed.AddField(Properties.Resources.name, current.oldName, true);
                                                infoEmbed.AddField(Properties.Resources.type, current.oldType, true);
                                                infoEmbed.AddField(Properties.Resources.address, $"[{(string.IsNullOrEmpty(response.display_name) ? response.error : response.display_name)}](https://www.google.com/maps/search/?api=1&query={current.lat}%2c%20{current.lng})", true);
                                                string edits = "";
                                                foreach (var pair in current.edits)
                                                {
                                                    edits += $"\n{pair.Key} => {pair.Value}";
                                                }
                                                infoEmbed.AddField(Properties.Resources.edits_, edits);
                                            }
                                            else
                                            {
                                                prompt += $"\n\t{Properties.Resources.name} {current.oldName}" +
                                                $"\n\t{Properties.Resources.type} {current.oldType}" +
                                                $"\n\t{Properties.Resources.address} {(String.IsNullOrEmpty(response.display_name) ? response.error : response.display_name)} (https://www.google.com/maps/search/?api=1&query={current.lat}%2c%20{current.lng})" +
                                                $"\n\t{Properties.Resources.edits_}";
                                                foreach (var pair in current.edits)
                                                {
                                                    prompt += $"\n\t\t{pair.Key} => {pair.Value}";
                                                }
                                            }
                                            if (!await TryToSendMessage(channel, ref modPerms, prompt, embed: infoEmbed?.Build(), options: options))
                                            {
                                                await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Error sending selection prompt in Guild {guildId}."));
                                            }
                                            return Task.CompletedTask;
                                        });
                                        Regex validEmote = new($"[1-{embed.Fields.Length}]\u20e3");
                                        var reactResult = await _inter.NextReactionAsync((SocketReaction r) =>
                                        r.MessageId == result.Value.Id && r.User.Value.Id != ulong.Parse(_config["pokeNavId"]) && validEmote.IsMatch(r.Emote.Name),
                                        timeout: TimeSpan.FromMinutes(1));
                                        if (!reactResult.IsSuccess)
                                        {
                                            // No one reacted!
                                            await _logger.Log(new LogMessage(LogSeverity.Info, nameof(Edit), $"Timeout while waiting for User Reaction in Guild {guildId}."));
                                            if (!await TryToSendMessage(channel, ref modPerms, Properties.Resources.reactTimeout))
                                            {
                                                await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Error sending error message in Guild {guildId}."));
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
                                        await _logger.Log(new LogMessage(LogSeverity.Info, nameof(Edit), $"Timeout while waiting for PokeNav to send the new Message in Guild {guildId}."));
                                        if (!await TryToSendMessage(channel, ref modPerms, Properties.Resources.pokeNavUpdateTimeout))
                                        {
                                            await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Error sending error message in Guild {guildId}."));
                                        }
                                        _client.MessageReceived -= handler;
                                        signal.Dispose();
                                        continue;
                                    }
                                    _client.MessageReceived -= handler;
                                }
                                else
                                {
                                    await TryToSendMessage(channel, ref modPerms, Properties.Resources.noMsgHistoryPerm);
                                    continue;
                                }
                            }
                            string text = embed.Footer.Value.Text.Split('\u25AB')[2];
                            if (!uint.TryParse(text[2..], out uint id))
                            {
                                if (!await TryToSendMessage(channel, ref modPerms, Properties.Resources.locationIdParseFailed))
                                {
                                    await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Error while sending error message in Guild {guildId}."));
                                }
                                await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Parsing of location Id failed in Guild {guildId}! Embed had the Footer {text}!"));
                                continue;
                            }
                            string editString;
                            if (current.edits.ContainsKey(EditType.type) && current.edits[EditType.type] == "none")
                                editString = $"{prefix}delete poi {id}";
                            else
                            {
                                editString = $"{prefix}update poi {id}";
                                foreach (var pair in current.edits)
                                {
                                    editString += $" «{pair.Key}: {pair.Value}»";
                                }
                            }
                            nma = _inter.NextMessageAsync((msg) => msg.Author.Id == ulong.Parse(_config["pokeNavId"]) && msg.Channel.Id == channel.Id && msg.Embeds.Count == 1, timeout: TimeSpan.FromSeconds(10), cancellationToken: ct.Token);
                            ct = new CancellationTokenSource();
                            t = TryToSendMessage(channel, ref modPerms, editString, options: options);
                            if (!await t)
                            {
                                ct.Cancel();
                                queue.Enqueue(current);
                                await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Error while sending edit message in Guild {guildId}."));
                                continue;
                            }
                            try
                            {
                                result = await nma;
                            }
                            catch (TaskCanceledException) { } // nothing to do, already handled.
                            if (!result.IsSuccess)
                            {
                                //no response in timeout!
                                if (!await TryToSendMessage(channel, ref modPerms, Properties.Resources.pokeNavTimeout))
                                {
                                    await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Error while sending error message in Guild {guildId}."));
                                }
                                await _logger.Log(new LogMessage(LogSeverity.Info, nameof(Edit), $"PokeNav did not respond in guild {guildId}!"));
                            }
                        }
                        else
                        {
                            //no response in timeout!
                            if (!await TryToSendMessage(channel, ref modPerms, Properties.Resources.pokeNavTimeout))
                            {
                                await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Error while sending error message in Guild {guildId}."));
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
                        workers[guildId] = Work(guildId, invokedChannel, invokingPerms, token); // proceed with creation after edits are complete if there is anything to create.
                    }
                }
            }
            else
            {
                if (invokingPerms.SendMessages)
                    if (!await TryToSendMessage(invokedChannel, ref invokingPerms, Properties.Resources.modChannelUnset))
                    {
                        await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Error while sending error message in Guild {guildId}."));
                    }
                tokens[guildId]?.Cancel(); // Sets IsCancellationRequested to make the Progress Message modify to Paused.
                await UpdateProgress(guildId, invokedChannel, invokingPerms);
                await _logger.Log(new LogMessage(LogSeverity.Info, nameof(Edit), $"Execution failed in Guild {guildId}: Mod-Channel was not set!"));
            }
            workers.TryRemove(guildId, out _); // value is not needed, as well as success/failure information.
            if (tokens.TryRemove(guildId, out CancellationTokenSource source))
                source.Dispose();
            await UpdateProgress(guildId, invokedChannel, invokingPerms);
        }
#pragma warning restore CA2016 // Parameter "CancellationToken" an Methoden weiterleiten, die diesen Parameter akzeptieren
    }
}
