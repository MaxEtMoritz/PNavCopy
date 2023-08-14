using System;
using System.Threading.Tasks;
using Discord.Interactions;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Fergun.Interactive;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using System.Text;
using System.Net.Http;
using Microsoft.VisualBasic.FileIO;
using System.IO;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace CompanionBot
{
    public class SlashModules : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly Logger _logger;
        private readonly DiscordSocketClient _client;
        private readonly IConfiguration _config;
        private readonly InteractiveService _interactive;
        private readonly GuildSettings _settings;
        private readonly MessageQueue _queue;
        private readonly HttpClient _webClient;
        public SlashModules(Logger logger, DiscordSocketClient client, IConfiguration config, InteractiveService interactive, GuildSettings settings, MessageQueue queue, HttpClient webClient)
        {
            _logger = logger;
            _client = client;
            _config = config;
            _interactive = interactive;
            _settings = settings;
            _queue = queue;
            _webClient = webClient;
        }

        public override void BeforeExecute(ICommandInfo cmd)
        {
            CultureInfo.CurrentCulture = new(Context.Interaction.UserLocale);
            CultureInfo.CurrentUICulture = new(Context.Interaction.UserLocale);
        }

        [SlashCommand("mod-channel", "Requests PokeNav's mod channel and saves it."), RequireBotPermission(ChannelPermission.ViewChannel), EnabledInDm(false)]
        public async Task SetModChannelAsync()
        {
            Task T = RespondAsync($"<@{_config["pokeNavId"]}> show mod-channel");
            var result = await _interactive.NextMessageAsync((message) => message.Author.Id == ulong.Parse(_config["pokeNavId"]) && message.Channel.Id == Context.Channel.Id && message.MentionedChannels.Count == 1, null, TimeSpan.FromSeconds(10));
            await T;
            if (result.IsSuccess)
            {
                var channel = result.Value.MentionedChannels.First();
                Settings currentSettings = _settings[Context.Guild.Id];
                currentSettings.PNavChannel = channel.Id;
                _settings[Context.Guild.Id] = currentSettings;
                await FollowupAsync(String.Format(Properties.Resources.modChannelSuccess, channel.Id));
                await _logger.Log(new LogMessage(LogSeverity.Info, nameof(SetModChannelAsync), $"PokeNav Mod Channel set to #{channel.Name} ({channel.Id}) for Guild {Context.Guild.Name} ({Context.Guild.Id})."));
                ChannelPermissions modPerms = Context.Guild.GetUser(_client.CurrentUser.Id).GetPermissions(channel);
                if (!modPerms.SendMessages || !modPerms.ViewChannel || !modPerms.AddReactions || !modPerms.ReadMessageHistory)
                    await FollowupAsync(String.Format(
                        Properties.Resources.promptMissingPerms,
                        modPerms.ViewChannel ? "✅" : "❌",
                        modPerms.SendMessages ? "✅" : "❌",
                        modPerms.AddReactions ? "✅" : "❌",
                        modPerms.ReadMessageHistory ? "✅" : "❌",
                        channel.Id
                    ));
            }
            else
                await FollowupAsync(Properties.Resources.modChannelFail);
        }

        [SlashCommand("pause", "Pauses the currently running PokeNav POI import."), EnabledInDm(false)]
        public Task Pause()
        {
            Console.WriteLine(CultureInfo.CurrentCulture.Name);
            return _queue.Pause(Context);
        }

        [SlashCommand("resume", "Restarts the previously paused PokeNav POI import."), EnabledInDm(false)]
        public Task Resume()
        {
            return _queue.Resume(Context);
        }

        public enum Choices
        {
            [ChoiceDisplay("Only Pokéstops")]
            pokestop,
            [ChoiceDisplay("Only Gyms")]
            gym,
            [ChoiceDisplay("No manual type override")]
            none
        }

        [SlashCommand("import", "imports POI data from an arbitrary CSV file."), EnabledInDm(false)]
        public async Task Import(
            [Summary(description:"The CSV file containing the data"), RequireMimeType("text/csv", "text/tab-separated-values"), MaxFileSize(300)]
        IAttachment file,
            [Summary(description:"Column name or zero-based index of the POI names in the file."), MinLength(1)]
        string name,
            [Summary(description:"Column name or zero-based index of lat. If lat and lon is combined, use params latLon and latLonSep.")]
        string latitude = null,
            [Summary(description:"Column name or zero-based index of lon. If lat and lon is combined, use params latLon and latLonSep.")]
        string longitude = null,
            [Summary(description:"Column name or index of lat/lon. If lat and lon are separated, use params latitude and longitude.")]
        string latLon = null,
            [Summary(description:"Separator of combined lat/lon. If lat and lon are separated, use parameters latitude and longitude.")]
        string latLonSep = null,
            [Summary(description:"Column name or zero-based index of POI type. If all POI are of the same type, use param manualType.")]
        string type = null,
            [Summary(description:"Set the POI type for every POI of this file. If the POI type is inside the file, use parameter type."), Choice("Stop", ((int)Choices.pokestop)),Choice("Gym", ((int)Choices.gym)),Choice("None", ((int)Choices.none))]
        int manualType = (int)Choices.none,
            [Summary(description:"Column name or zero-based index of the Ex gym status in the file.")]
        string ex = null,
            [Summary(description:"Is the first row a Header row (does it contain the column names)? Default: true")]
        bool header = true,
            [Summary(description:"How the columns are separated from each other. Enter the delimiter or \"TAB\" (for Tab). Default: ,")]
        string delimiter=",",
            [Summary(description:"Are the values enclosed with double quotes? Default: false")]
        bool quoted = false)
        {
            var start = DateTime.Now;
            // validate if necessary params are set
            if ((latitude == null || longitude == null) && (latLon == null || latLonSep == null))
            {
                await RespondAsync("You have to either specify the parameters `latitude` and `longitude` or the parameters `latLon` and `latLonSep`.");
                return;
            }
            if (type == null && manualType == (int)Choices.none)
            {
                await RespondAsync("You have to either specify the parameter `type` or `manualType`.");
                return;
            }

            // download file
            Stream dataString;
            try
            {
                dataString = await _webClient.GetStreamAsync(file.Url);
            }
            catch (HttpRequestException e)
            {
                await _logger.Log(new LogMessage(LogSeverity.Error, nameof(Import), $"Download of attached File failed: {e.Message}", e));
                await RespondAsync(String.Format(Properties.Resources.downloadFailed, e.Message));
                return;
            }
            var csvReader = new TextFieldParser(dataString);
            csvReader.SetDelimiters(delimiter.ToUpperInvariant() == "TAB" ? "\t" : delimiter);
            csvReader.HasFieldsEnclosedInQuotes = quoted;
            csvReader.TrimWhiteSpace = true;

            // check if all indices or names are valid
            string[] line;
            try
            {
                line = csvReader.ReadFields();
                if (line != null)
                {
                    foreach (var colId in new[] { name, latitude, longitude, latLon, type, ex })
                    {
                        if (colId is not null)
                        {
                            if (colId.All(Char.IsAsciiDigit))
                            {
                                var index = Int32.Parse(colId);
                                if (index > line.Length)
                                {
                                    await RespondAsync($"Index {index} is too big. File only has {line.Length} columns.\nRemember that the first column has index 0, not 1.");
                                    return;
                                }
                            }
                            else if (!header || !line.Contains(colId))
                            {
                                await RespondAsync($"The file does not contain a column named {colId}.");
                                return;
                            }
                        }
                    }
                }
            }
            catch (MalformedLineException e)
            {
                await RespondAsync($"""
                Malformed CSV in line {csvReader.ErrorLineNumber}:
                ```csv
                {(csvReader.ErrorLine.Length <= 150 ? csvReader.ErrorLine : csvReader.ErrorLine[..150] + '…')}
                ```
                """);
                return;
            }

            // map names to indices
            Dictionary<string?, int> mapping = new();

            for (int i = 0; i < line.Length; i++)
            {
                mapping[i.ToString()] = i;
                if (header)
                    mapping[line[i]] = i;
            }

            List<PortalData> data = new();

            /**
             * assemble data
             */
            PortalData asseble(string[] line)
            {
                PortalData portalData = new();

                portalData.name = line[mapping[name]];
                if (type is not null)
                {
                    portalData.type = line[mapping[type]].ToLowerInvariant() switch
                    {
                        var s when s.Contains("stop") => PoiType.pokestop,
                        _ => PoiType.gym,
                    };
                }
                else
                {
                    portalData.type = (PoiType)manualType;
                }
                if (latitude is not null && longitude is not null)
                {
                    if (!Double.TryParse(line[mapping[latitude]], CultureInfo.InvariantCulture.NumberFormat, out var lat))
                    {
                        throw new InvalidDataException(csvReader.LineNumber - 1, nameof(latitude), line[mapping[latitude]]);
                    }
                    if (!Double.TryParse(line[mapping[longitude]], CultureInfo.InvariantCulture.NumberFormat, out var lon))
                    {
                        throw new InvalidDataException(csvReader.LineNumber - 1, nameof(longitude), line[mapping[longitude]]);
                    }
                    portalData.lat = lat.ToString(CultureInfo.InvariantCulture.NumberFormat);
                    portalData.lng = lon.ToString(CultureInfo.InvariantCulture.NumberFormat);
                }
                else
                {
                    var lalo = line[mapping[latLon]].Split(latLonSep, 2, StringSplitOptions.RemoveEmptyEntries & StringSplitOptions.TrimEntries);
                    if (lalo.Length != 2)
                    {
                        throw new InvalidDataException(csvReader.LineNumber - 1, nameof(latLon), line[mapping[latLon]]);
                    }
                    portalData.lat = lalo[0];
                    portalData.lng = lalo[1];
                }

                if (ex is not null)
                {
                    var str = line[mapping[ex]].ToLowerInvariant();
                    portalData.isEx = new HashSet<string>() { "true", "yes", "on", "ex", "x", "1" }.Contains(str);
                }

                return portalData;
            }

            if (!header)
                // first data row was consumed by index check, assemble data afterwards
                try
                {
                    data.Add(asseble(line));
                }
                catch (MalformedLineException e)
                {
                    await _logger.Log(new(LogSeverity.Info, nameof(Import), $"Malformed CSV uploaded (line {csvReader.ErrorLineNumber}).", e));
                    await RespondAsync($"""
                    Malformed CSV in line {csvReader.ErrorLineNumber}:
                    ```csv
                    {(csvReader.ErrorLine.Length <= 150 ? csvReader.ErrorLine : csvReader.ErrorLine[..150] + '…')}
                    ```
                    """);
                    return;
                }
                catch (InvalidDataException e)
                {
                    await _logger.Log(new(LogSeverity.Info, nameof(Import), e.Message, e));
                    await RespondAsync(e.Message);
                    return;
                }

            while (!csvReader.EndOfData)
            {
                try
                {
                    line = csvReader.ReadFields();
                    data.Add(asseble(line));
                }
                catch (MalformedLineException e)
                {
                    await _logger.Log(new(LogSeverity.Info, nameof(Import), $"Malformed CSV uploaded (line {csvReader.ErrorLineNumber}).", e));
                    await RespondAsync($"""
                    Malformed CSV in line {csvReader.ErrorLineNumber}:
                    ```csv
                    {(csvReader.ErrorLine.Length <= 150 ? csvReader.ErrorLine : csvReader.ErrorLine[..150] + '…')}
                    ```
                    """);
                    return;
                }
                catch (InvalidDataException e)
                {
                    await _logger.Log(new(LogSeverity.Info, nameof(Import), e.Message, e));
                    await RespondAsync(e.Message);
                    return;
                }
            }
            await _logger.Log(new(LogSeverity.Debug, nameof(Import), $"took {DateTime.Now - start}"));

            await RespondAsync($"Successfully read {data.Count} POI from the file: {data.Count(d=>d.type == PoiType.gym)} Gyms and {data.Count(d => d.type == PoiType.pokestop)} PokéStops.");

            csvReader.Close();

            await _queue.EnqueueCreate(Context.Guild.Id, Context.Channel, data, Context.Guild.GetUser(_client.CurrentUser.Id).GetPermissions(Context.Channel as IGuildChannel));
        }

#if DEBUG 
        [ComponentInteraction("devdisconnect"), RequireOwner]
#else
        [ComponentInteraction("disconnect"), RequireOwner]
#endif
        public async Task ConfirmDisconnectAsync()
        {
            //await RespondAsync("Saving state...", ephemeral: true);
            //await _queue.SaveState();
            await RespondAsync(Properties.Resources.goodbye, ephemeral: true);
            await _client.LogoutAsync();
            await _client.StopAsync();
            //Environment.Exit(0);
        }
    }

    [DontAutoRegister]
    public class ManualSlashModules : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly Logger _logger;
        private readonly MessageQueue _queue;
        private readonly DiscordSocketClient _client;
        public ManualSlashModules(Logger logger, MessageQueue queue, DiscordSocketClient client)
        {
            _logger = logger;
            _queue = queue;
            _client = client;
        }

        public override void BeforeExecute(ICommandInfo cmd)
        {
            CultureInfo.CurrentCulture = new(Context.Interaction.UserLocale);
            CultureInfo.CurrentUICulture = new(Context.Interaction.UserLocale);
        }

        [SlashCommand("disconnect", "Disconnects the Bot from the gateway."), RequireOwner]
        public async Task DisconnectAsync()
        {
            string prompt = Properties.Resources.sure;
#if DEBUG
            prompt += Properties.Resources.testInstance;
#endif
            await _logger.Log(new LogMessage(LogSeverity.Warning, nameof(DisconnectAsync), "Disconnect requested by Interaction, waiting for confirmation..."));
            await RespondAsync(prompt, ephemeral: true, components: new ComponentBuilder()
                .WithButton(Properties.Resources.yesIAm,
#if DEBUG
                "devdisconnect",
#else
                "disconnect",
#endif
                ButtonStyle.Danger, new Emoji("⚠"))
                .Build());
        }

        [SlashCommand("status", "shows the current bot status"), RequireOwner]
        public async Task BotStatus()
        {
            var status = _queue.GetState();
            // embed fields have max length of 1024 chars (https://discord.com/developers/docs/resources/channel#embed-object-embed-limits)
            StringBuilder servers = new(100, 1024);
            StringBuilder creations = new(100, 1024);
            StringBuilder edits = new(100, 1024);
            foreach (var state in status)
            {
                try
                {
                    servers.AppendLine(state.server.ToString());
                    creations.AppendLine(state.creations.ToString());
                    edits.AppendLine(state.edits.ToString());
                }
                catch (ArgumentOutOfRangeException)
                {
                    break;
                }
            }
            List<EmbedFieldBuilder> fields = new()
            {
                new() { IsInline = true, Name = Properties.Resources.guildId, Value = String.IsNullOrEmpty(servers.ToString()) ? "---" : servers.ToString() },
                new() { IsInline = true, Name = Properties.Resources.numCreations, Value = String.IsNullOrEmpty(creations.ToString()) ? "---" : creations.ToString() },
                new() { IsInline = true, Name = Properties.Resources.numEdits, Value = String.IsNullOrEmpty(edits.ToString()) ? "---" : edits.ToString() }
            };
            EmbedBuilder embed = new EmbedBuilder()
                .WithTitle(Properties.Resources.currentBotState)
                .WithDescription(String.Format(Properties.Resources.currentlyXServers, _client.Guilds.Count))
                .WithCurrentTimestamp()
                .WithFooter(Properties.Resources.dataByBot)
                .WithAuthor(_client.CurrentUser)
                .WithFields(fields);
            await RespondAsync(embed: embed.Build(), ephemeral: true);
        }
    }

    class InvalidDataException : Exception
    {
        internal InvalidDataException(long line, string type, string value, string detail = null)
        {
            Line = line;
            Type = type;
            Value = value;
            Detail = detail;
        }

        public long Line { get; }
        public string Type { get; }
        public string Value { get; }
        public string Detail { get; }
        public override string Message
        {
            get
            {
                return $"Invalid {Type} in line {Line}: `{Value}`{(!String.IsNullOrWhiteSpace(Detail) ? $"\n{Detail}" : String.Empty)}";
            }
        }

    }
}
