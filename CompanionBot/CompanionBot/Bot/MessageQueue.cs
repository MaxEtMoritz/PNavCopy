using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Interactivity;
using Microsoft.Extensions.Configuration;

namespace Bot
{
    public class MessageQueue
    {
        private readonly DiscordSocketClient _client;
        private readonly Logger _logger;
        private static InteractivityService _inter;
        private readonly string prefix;
        private readonly Dictionary<ulong, CancellationTokenSource> tokens = new Dictionary<ulong, CancellationTokenSource>();
        private readonly Dictionary<ulong, Queue<string>> createQueues = new Dictionary<ulong, Queue<string>>();
        private readonly Dictionary<ulong, Queue<EditData>> editQueues = new Dictionary<ulong, Queue<EditData>>();
        private readonly Dictionary<ulong, Task> workers = new Dictionary<ulong, Task>();
        private readonly Dictionary<ulong, IUserMessage> progress = new Dictionary<ulong, IUserMessage>();

        internal Task EnqueueEdit(ulong guildId, global::EditData[] data)
        {
            // TODO: implement it
            throw new NotImplementedException();
        }

        internal Progress GetProgress(ulong guildId)
        {
            return new Progress()
            {
                Creations = createQueues.ContainsKey(guildId) ? createQueues[guildId].Count : 0,
                Edits = editQueues.ContainsKey(guildId) ? editQueues[guildId].Count : 0,
                EstimatedTimeRemaining = TimeSpan.FromSeconds(0), // TODO: implement time guessing!
                AttentionNeeded = false // TODO: track whether user attention is needed!
            };
        }

        private readonly GuildSettings _settings;
        RequestOptions options;
        RequestOptions typingOptions;
        EmbedBuilder defaultEmbed;
        // FIXME: Make a helper method that updates the Progress Message instead of duplicating the code always!
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
            defaultEmbed = new EmbedBuilder();
            defaultEmbed.Title = "Exporting...";
            defaultEmbed.Description = "This is still to do:";
            defaultEmbed.Fields.Add(new EmbedFieldBuilder() { Name = "Creations", Value = 0, IsInline = true });
            defaultEmbed.Fields.Add(new EmbedFieldBuilder() { Name = "Edits", Value = 0, IsInline = true });
            defaultEmbed.Footer = new EmbedFooterBuilder() { Text = "use pause or resume Commands to manage the export!" };
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

            if (progress.ContainsKey(context.Guild.Id))
            {
                try
                {
                    progress[context.Guild.Id].ModifyAsync((x) =>
                    {
                        EmbedBuilder embed = progress[context.Guild.Id].Embeds.First().ToEmbedBuilder();
                        embed.Fields.Find((x) => x.Name == "Creations").Value = createQueues[context.Guild.Id].Count;
                        x.Embed = embed.Build();
                    }, options);
                }
                catch (Exception e)
                {
                    _logger.Log(new LogMessage(LogSeverity.Error, nameof(EnqueueCreate), $"Error while editing progress message in Guild {context.Guild.Id}: {e.Message}", e));
                }
            }
            else
            {
                var embed = defaultEmbed.WithCurrentTimestamp();
                embed.Fields[0].Value = createQueues[context.Guild.Id].Count;
                embed.Fields[1].Value = editQueues.ContainsKey(context.Guild.Id) ? editQueues[context.Guild.Id].Count : 0;
                embed.Footer.Text = $"use {_settings[context.Guild.Id].Prefix}pause or {_settings[context.Guild.Id].Prefix}resume to manage the export!";
                IUserMessage msg = context.Channel.SendMessageAsync(embed: embed.Build()).Result;
                try
                {
                    msg.PinAsync(options);
                }
                catch (Exception e)
                {
                    _logger.Log(new LogMessage(LogSeverity.Error, nameof(EnqueueCreate), $"Error pinning progress message in Guild {context.Guild.Id}: {e.Message}", e));
                }
                progress[context.Guild.Id] = msg;
            }

            if (!workers.ContainsKey(context.Guild.Id) || (workers[context.Guild.Id].IsCompleted && !workers[context.Guild.Id].IsCompleted))
            {
                CancellationTokenSource source = new CancellationTokenSource();
                tokens[context.Guild.Id] = source;
                workers.Add(context.Guild.Id, Work(context.Guild.Id, context.Channel, source.Token));
            }
            return Task.CompletedTask;
        }

        public Task EnqueueCreate(ulong guildId, PortalData[] data)
        {
            Queue<string> queue;
            if (createQueues.ContainsKey(guildId))
            {
                queue = createQueues[guildId];
            }
            else
            {
                queue = new Queue<string>();
                createQueues[guildId] = queue;
            }

            foreach (PortalData item in data)
            {
                string command = $"<@428187007965986826> create poi {item.Type} «{item.Name}» {item.Lat} {item.Lng}";
                if (item.IsEx != null)
                    command += $" \"ex_eligible: {Convert.ToInt16(item.IsEx)}\"";
                queue.Enqueue(command);
            }

            if (progress.ContainsKey(guildId))
            {
                try
                {
                    progress[guildId].ModifyAsync((x) =>
                    {
                        EmbedBuilder embed = progress[guildId].Embeds.First().ToEmbedBuilder();
                        embed.Fields.Find((x) => x.Name == "Creations").Value = createQueues[guildId].Count;
                        x.Embed = embed.Build();
                    }, options);
                }
                catch (Exception e)
                {
                    _logger.Log(new LogMessage(LogSeverity.Error, nameof(EnqueueCreate), $"Error while editing progress message in Guild {guildId}: {e.Message}", e));
                }
            }
            else
            {
                var embed = defaultEmbed.WithCurrentTimestamp();
                embed.Fields[0].Value = createQueues[guildId].Count;
                embed.Fields[1].Value = editQueues.ContainsKey(guildId) ? editQueues[guildId].Count : 0;
                embed.Footer.Text = $"use {_settings[guildId].Prefix}pause or {_settings[guildId].Prefix}resume to manage the export!";
                IUserMessage msg = context.Channel.SendMessageAsync(embed: embed.Build()).Result; // FIXME
                try
                {
                    msg.PinAsync(options);
                }
                catch (Exception e)
                {
                    _logger.Log(new LogMessage(LogSeverity.Error, nameof(EnqueueCreate), $"Error pinning progress message in Guild {guildId}: {e.Message}", e));
                }
                progress[guildId] = msg;
            }

            if (!workers.ContainsKey(guildId) || (workers[guildId].IsCompleted && !workers[guildId].IsCompleted))
            {
                CancellationTokenSource source = new CancellationTokenSource();
                tokens[guildId] = source;
                workers.Add(guildId, Work(guildId, context.Channel, source.Token)); // FIXME
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

            if (progress.ContainsKey(context.Guild.Id))
            {
                try
                {
                    progress[context.Guild.Id].ModifyAsync((x) =>
                    {
                        EmbedBuilder embed = progress[context.Guild.Id].Embeds.First().ToEmbedBuilder();
                        embed.Fields.Find((x) => x.Name == "Edits").Value = editQueues[context.Guild.Id].Count;
                        x.Embed = embed.Build();
                    }, options);
                }
                catch (Exception e)
                {
                    _logger.Log(new LogMessage(LogSeverity.Error, nameof(EnqueueEdit), $"Error while editing progress message in Guild {context.Guild.Id}: {e.Message}", e));
                }
            }
            else
            {
                var embed = defaultEmbed.WithCurrentTimestamp();
                embed.Fields[0].Value = createQueues.ContainsKey(context.Guild.Id) ? createQueues[context.Guild.Id].Count : 0;
                embed.Fields[1].Value = editQueues[context.Guild.Id].Count;
                embed.Footer.Text = $"use {_settings[context.Guild.Id].Prefix}pause or {_settings[context.Guild.Id].Prefix}resume to manage the export!";
                IUserMessage msg = context.Channel.SendMessageAsync(embed: embed.Build()).Result;
                try
                {
                    msg.PinAsync(options);
                }
                catch (Exception e)
                {
                    _logger.Log(new LogMessage(LogSeverity.Error, nameof(EnqueueEdit), $"Error pinning progress message in Guild {context.Guild.Id}: {e.Message}", e));
                }
                progress[context.Guild.Id] = msg;
            }

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
                    if (progress.ContainsKey(context.Guild.Id))
                    {
                        IUserMessage message = progress[context.Guild.Id];
                        try
                        {
                            message.ModifyAsync((x) =>
                            {
                                EmbedBuilder embed = progress[context.Guild.Id].Embeds.First().ToEmbedBuilder();
                                embed.Title = "Paused";
                                x.Embed = embed.Build();
                            }, options);
                        }
                        catch (Exception e)
                        {
                            _logger.Log(new LogMessage(LogSeverity.Error, nameof(Pause), $"Error modifying progress message in Guild {context.Guild.Id}: {e.Message}", e));
                        }
                    }
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
                if (progress.ContainsKey(context.Guild.Id))
                {
                    IUserMessage message = progress[context.Guild.Id];
                    message.ModifyAsync((x) =>
                    {
                        EmbedBuilder embed = progress[context.Guild.Id].Embeds.First().ToEmbedBuilder();
                        embed.Title = "Exporting...";
                        x.Embed = embed.Build();
                    }, options);
                }
                if (createQueues.ContainsKey(context.Guild.Id) && createQueues[context.Guild.Id].Any())
                {
                    CancellationTokenSource source = new CancellationTokenSource();
                    tokens[context.Guild.Id] = source;
                    workers[context.Guild.Id] = Work(context.Guild.Id, context.Channel, source.Token);
                }
                else if (editQueues.ContainsKey(context.Guild.Id) && editQueues[context.Guild.Id].Any())
                {
                    CancellationTokenSource source = new CancellationTokenSource();
                    tokens[context.Guild.Id] = source;
                    workers[context.Guild.Id] = Edit(context.Guild.Id, context.Channel, source.Token);
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

        private async Task Work(ulong guildId, IMessageChannel invokedChannel, CancellationToken token)
        {
            IUserMessage message = null;
            if (progress.ContainsKey(guildId))
            {
                message = progress[guildId];
            }

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

                    while (queue.Count > 0)
                    {
                        if (token.IsCancellationRequested)
                        {
                            //workers.Remove(guildId);
                            tokens.Remove(guildId, out CancellationTokenSource s);
                            s.Dispose();
                            token.ThrowIfCancellationRequested();
                        }
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
                        if (message != null && queue.Count % 5 == 0) // do not edit every time because of rate limits
                        {
                            try
                            {
                                await message.ModifyAsync((x) =>
                                {
                                    EmbedBuilder embed = message.Embeds.First().ToEmbedBuilder();
                                    embed.Fields.Find((x) => x.Name == "Creations").Value = queue.Count;
                                    x.Embed = embed.Build();
                                }, options);
                            }
                            catch (Exception e)
                            {
                                await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Work), $"Error while editing progress message in Guild {guildId}: {e.Message}", e));
                            }
                        }
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
                    if (message != null)
                    {
                        try
                        {
                            await message.ModifyAsync((x) =>
                            {
                                EmbedBuilder embed = message.Embeds.First().ToEmbedBuilder();
                                embed.Title = "Paused";
                                x.Embed = embed.Build();
                            }, options);
                        }
                        catch (Exception e)
                        {
                            await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Work), $"Error while editing progress message in Guild {guildId}: {e.Message}", e));
                        }
                    }
                    await _logger.Log(new LogMessage(LogSeverity.Info, nameof(Work), $"Execution failed in Guild {guildId}: Mod-Channel was not set!"));
                }
            }

            if (editQueues.ContainsKey(guildId) && editQueues[guildId].Any())
            {
                if (message != null)
                {
                    try
                    {
                        await message.ModifyAsync((x) =>
                        {
                            EmbedBuilder embed = message.Embeds.First().ToEmbedBuilder();
                            embed.Fields.Find((x) => x.Name == "Creations").Value = queue.Count;
                            x.Embed = embed.Build();
                        }, options);
                    }
                    catch (Exception e)
                    {
                        await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Work), $"Error while editing progress message in Guild {guildId}: {e.Message}", e));
                    }
                }
                workers[guildId] = Edit(guildId, invokedChannel, token); // proceed with edits after creation is complete if there are any.
            }
            else
            {
                workers.Remove(guildId);
                tokens.Remove(guildId, out CancellationTokenSource source);
                if (message != null)
                {
                    try
                    {
                        await message.DeleteAsync(options);
                    }
                    catch (Exception e)
                    {
                        await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Work), $"Error while deleting progress message in Guild {guildId}: {e.Message}", e));
                    }
                    progress.Remove(guildId);
                }
                source.Dispose();
            }
        }

        private async Task Edit(ulong guildId, IMessageChannel invokedChannel, CancellationToken token)
        {
            IUserMessage message = null;
            if (progress.ContainsKey(guildId))
            {
                message = progress[guildId];
            }

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
                    while (queue.Count > 0)
                    {
                        if (token.IsCancellationRequested)
                        {
                            //workers.Remove(guildId);
                            tokens.Remove(guildId, out CancellationTokenSource s);
                            s.Dispose();
                            token.ThrowIfCancellationRequested();
                        }
                        EditData current = queue.Dequeue();
                        string type = current.t == 0 ? "stop" : current.t.ToString();
                        Task t = Task.CompletedTask;
                        try
                        {
                            t = channel.SendMessageAsync($"{prefix}{type}-info {current.n}");
                        }
                        catch (Exception e)
                        {
                            await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Error while sending PoI Info Command in Guild {guildId}: {e.Message}", e));
                        }
                        var result = await _inter.NextMessageAsync((msg) => msg.Author.Id == 428187007965986826 && msg.Channel.Id == channel.Id && msg.Embeds.Count == 1 && (msg.Embeds.First().Title.Equals(current.n, StringComparison.OrdinalIgnoreCase) || msg.Embeds.First().Title == "Error" || msg.Embeds.First().Title == "Select Location"), timeout: TimeSpan.FromSeconds(10));
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

                                int? onlyOneExactMatch = null;
                                for (int i = 0; i < embed.Fields.Length; i++)
                                {
                                    var field = embed.Fields[i];
                                    string name = field.Name.Substring(3);
                                    if (current.n == name && onlyOneExactMatch == null)
                                        onlyOneExactMatch = i;
                                    else if (current.n == name && onlyOneExactMatch != null)
                                    {
                                        onlyOneExactMatch = null;
                                        break;
                                    }
                                }

                                if (onlyOneExactMatch != null)
                                {
                                    // assume this is the right one!
                                    var field = embed.Fields[(int)onlyOneExactMatch];
                                    Emoji reaction = new Emoji(field.Name.Substring(0, 2));

                                    try
                                    {
                                        await result.Value.AddReactionAsync(reaction, options);
                                    }
                                    catch (Exception e)
                                    {
                                        await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Error while selecting right location in Guild {guildId}: {e.Message}", e));
                                    }
                                }
                                else
                                {
                                    // edit progress message to show that user action is required!
                                    if (message != null)
                                    {
                                        try
                                        {
                                            await message.ModifyAsync((x) =>
                                            {
                                                EmbedBuilder embed = message.Embeds.First().ToEmbedBuilder();
                                                embed.Title = "Action Required!";
                                                embed.Color = Color.DarkRed;
                                                embed.Description = $"head to <#{channel.Id}> and select the right PoI to edit!";
                                                embed.Fields.Find((x) => x.Name == "Edits").Value = queue.Count;
                                                x.Embed = embed.Build();
                                            }, options);
                                        }
                                        catch (Exception e)
                                        {
                                            await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Work), $"Error while editing progress message in Guild {guildId}: {e.Message}", e));
                                        }
                                    }

                                    string prompt = $"@here please select the right location! If you are not sure, let it time out!\nThe following info is available:";
                                    prompt += $"\n\tName: {current.n}";
                                    prompt += $"\n\tType: {current.t}";
                                    prompt += "\n\tEdits:";
                                    foreach (var item in current.e)
                                    {
                                        switch (item.Key)
                                        {
                                            case 'n':
                                                prompt += $"\n\t\tName => {item.Value}";
                                                break;
                                            case 't':
                                                prompt += "\n\t\tType => ";
                                                if (Enum.TryParse(item.Value, out LocationType locationType))
                                                {
                                                    prompt += locationType;
                                                }
                                                else
                                                {
                                                    prompt += "unknown";
                                                }
                                                break;
                                            case 'a':
                                                prompt += $"\n\t\tLatitude => {item.Value}";
                                                break;
                                            case 'o':
                                                prompt += $"\n\t\tLongitude => {item.Value}";
                                                break;
                                            case 'e':
                                                prompt += $"\n\t\tEx-eligibility => {(item.Value == "0" ? bool.FalseString : bool.TrueString)}";
                                                break;
                                            default:
                                                break;
                                        }
                                    }
                                    try
                                    {
                                        await channel.SendMessageAsync(prompt, options: options);
                                    }
                                    catch (Exception e)
                                    {
                                        await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Error sending selection prompt in Guild {guildId}: {e.Message}", e));
                                    }
                                    //https://stackoverflow.com/a/12858633 answer by user dtb on StackOverflow
                                    SemaphoreSlim reactSignal = new SemaphoreSlim(0, 1);
                                    Func<Cacheable<IUserMessage, ulong>, ISocketMessageChannel, SocketReaction, Task> reactHandler = delegate (Cacheable<IUserMessage, ulong> cache, ISocketMessageChannel channl, SocketReaction reaction)
                                    {
                                        bool validEmote = new Regex($"[1-{embed.Fields.Length}]\u20e3").IsMatch(reaction.Emote.Name);
                                        if (channl.Id == channel.Id && result.Value.Id == reaction.MessageId && reaction.User.Value.Id != 428187007965986826 && validEmote)
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
                                    };
                                    _client.ReactionAdded += reactHandler;
                                    if (!await reactSignal.WaitAsync(result.Value.Timestamp.AddSeconds(58) - DateTime.Now)) // that should wait a bit shorter than PokeNav is waiting!
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
                                        if (message != null)
                                        {
                                            try
                                            {
                                                await message.ModifyAsync((x) =>
                                                {
                                                    EmbedBuilder embed = message.Embeds.First().ToEmbedBuilder();
                                                    embed.Title = "Exporting...";
                                                    embed.Color = null;
                                                    embed.Description = $"This is still to do:";
                                                    embed.Fields.Find((x) => x.Name == "Edits").Value = queue.Count;
                                                    x.Embed = embed.Build();
                                                }, options);
                                            }
                                            catch (Exception e)
                                            {
                                                await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Work), $"Error while editing progress message in Guild {guildId}: {e.Message}", e));
                                            }
                                        }
                                        continue;
                                    }
                                    _client.ReactionAdded -= reactHandler;
                                    if (message != null)
                                    {
                                        try
                                        {
                                            await message.ModifyAsync((x) =>
                                            {
                                                EmbedBuilder embed = message.Embeds.First().ToEmbedBuilder();
                                                embed.Title = "Exporting...";
                                                embed.Color = null;
                                                embed.Description = $"This is still to do:";
                                                embed.Fields.Find((x) => x.Name == "Edits").Value = queue.Count;
                                                x.Embed = embed.Build();
                                            }, options);
                                        }
                                        catch (Exception e)
                                        {
                                            await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Work), $"Error while editing progress message in Guild {guildId}: {e.Message}", e));
                                        }
                                    }
                                }

                                //https://stackoverflow.com/a/12858633 answer by user dtb on StackOverflow
                                SemaphoreSlim signal = new SemaphoreSlim(0, 1);
                                Func<SocketMessage, Task> handler = delegate (SocketMessage msg)
                                {
                                    if (msg.Channel == channel && msg.Author.Id == 428187007965986826 && msg.Embeds.Count == 1 && string.IsNullOrEmpty(msg.Content))
                                    {
                                        embed = msg.Embeds.First();
                                        signal.Release();
                                    }
                                    return Task.CompletedTask;
                                };
                                _client.MessageReceived += handler;
                                //_client.MessageReceived += delegate (SocketMessage msg) { Console.WriteLine("Sie haben Post! um " + DateTime.Now); return Task.CompletedTask; };
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
                            if (!uint.TryParse(text.Substring(2), out uint id))
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
                            if (current.e.ContainsKey('t') && current.e['t'] == ((int)LocationType.none).ToString())
                                editString = $"{prefix}delete poi {id}";
                            else
                            {
                                editString = $"{prefix}update poi {id}";
                                foreach (var item in current.e)
                                {
                                    string newEdit = " \"";
                                    switch (item.Key)
                                    {
                                        case 'n':
                                            newEdit += $"name: {item.Value}\"";
                                            break;
                                        case 't':
                                            newEdit += "type: ";
                                            if (!Enum.TryParse(item.Value, out LocationType locationType))
                                            {
                                                try
                                                {
                                                    await channel.SendMessageAsync($"Error: Unknown Location Type number {item.Value}");
                                                }
                                                catch (Exception e)
                                                {
                                                    await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Error while sending error message in Guild {guildId}: {e.Message}", e));
                                                }
                                                await _logger.Log(new LogMessage(LogSeverity.Info, nameof(Edit), $"unknown Location Type Number in Guild {guildId}: {item.Value}"));
                                            }
                                            newEdit += locationType + "\"";
                                            break;
                                        case 'a':
                                            newEdit += $"latitude: {item.Value}\"";
                                            break;
                                        case 'o':
                                            newEdit += $"longitude: {item.Value}\"";
                                            break;
                                        case 'e':
                                            newEdit += $"ex_eligible: {item.Value}\"";
                                            break;
                                        default:
                                            await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Unknown Edits Key in Guild {guildId}: '{item.Key}' (Value {item.Value})!"));
                                            try
                                            {
                                                await channel.SendMessageAsync($"Error: Unknown Edit Key '{item.Key}'!");
                                            }
                                            catch (Exception e)
                                            {
                                                await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Error while sending error message in Guild {guildId}: {e.Message}", e));
                                            }
                                            continue;
                                    }
                                    editString += newEdit;
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
                        if (message != null && queue.Count % 5 == 0) // do not edit every time
                        {
                            try
                            {
                                await message.ModifyAsync((x) =>
                                {
                                    EmbedBuilder embed = message.Embeds.First().ToEmbedBuilder();
                                    embed.Fields.Find((x) => x.Name == "Edits").Value = queue.Count;
                                    x.Embed = embed.Build();
                                }, options);
                            }
                            catch (Exception e)
                            {
                                await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Error while editing progress message in Guild {guildId}: {e.Message}", e));
                            }
                        }
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
                    if (message != null)
                    {
                        try
                        {
                            await message.ModifyAsync((x) =>
                            {
                                EmbedBuilder embed = message.Embeds.First().ToEmbedBuilder();
                                embed.Title = "Paused";
                                x.Embed = embed.Build();
                            }, options);
                        }
                        catch (Exception e)
                        {
                            await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Error while editing progress message in Guild {guildId}: {e.Message}", e));
                        }
                    }
                    await _logger.Log(new LogMessage(LogSeverity.Info, nameof(Edit), $"Execution failed in Guild {guildId}: Mod-Channel was not set!"));
                }
            }

            if (createQueues.ContainsKey(guildId) && createQueues[guildId].Any())
            {
                if (message != null)
                {
                    try
                    {
                        await message.ModifyAsync((x) =>
                        {
                            EmbedBuilder embed = message.Embeds.First().ToEmbedBuilder();
                            embed.Fields.Find((x) => x.Name == "Edits").Value = queue.Count;
                            x.Embed = embed.Build();
                        }, options);
                    }
                    catch (Exception e)
                    {
                        await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Error while editing progress message in Guild {guildId}: {e.Message}", e));
                    }
                }
                workers[guildId] = Work(guildId, invokedChannel, token); // proceed with creation after edits are complete if there is anything to create.
            }
            else
            {
                workers.Remove(guildId);
                if (message != null)
                {
                    try
                    {
                        await message.DeleteAsync(options);
                    }
                    catch (Exception e)
                    {
                        await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Edit), $"Error while deleting progress message in Guild {guildId}: {e.Message}", e));
                    }
                }
                progress.Remove(guildId);
                tokens.Remove(guildId, out CancellationTokenSource source);
                source.Dispose();
            }
        }
    }
}
