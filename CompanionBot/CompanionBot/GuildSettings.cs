using Discord;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;

namespace CompanionBot
{
    public class GuildSettings
    {
        private readonly Logger logger;
        private const string path = @"guildSettings.json";
        private ConcurrentDictionary<ulong, Settings> settings;

        public GuildSettings(Logger log)
        {
            logger = log;
        }

        internal Settings this[ulong server]
        {
            get
            {
                if (settings == null)
                {
                    LoadSettings();
                }

                if (settings.TryGetValue(server, out Settings prefs))
                {
                    return prefs;
                }
                else
                {
                    return new();
                }
            }
            set
            {
                settings[server] = value;
                SaveSettings();
            }
        }

        private void LoadSettings()
        {
            if (File.Exists(path))
            {
                string data;
                try
                {
                    data = File.ReadAllText(path);
                }
                catch (Exception e)
                {
                    logger.Log(new LogMessage(LogSeverity.Warning, this.GetType().Name, $"Unable to read Settings File: {e.GetType().Name} - {e.Message}.\n" +
                        "If there are present settings, they will be overridden!", e));
                    data = "{}";
                }
                try
                {
                    settings = JsonConvert.DeserializeObject<ConcurrentDictionary<ulong, Settings>>(data);
                }
                catch (Exception e)
                {
                    logger.Log(new LogMessage(LogSeverity.Error, this.GetType().Name, $"Invalid Settings File (JSON parsing failed)! Exception: {e.GetType().Name} - {e.Message}.\n" +
                        "If there are present settings, they will be overridden!", e));
                    settings = new ConcurrentDictionary<ulong, Settings>();
                }
            }
            else
            {
                settings = new ConcurrentDictionary<ulong, Settings>();
            }
        }

        private void SaveSettings()
        {
            string data = JsonConvert.SerializeObject(settings, Formatting.Indented);
            try
            {
                File.WriteAllText(path, data);
            }
            catch (Exception e)
            {
                logger.Log(new LogMessage(LogSeverity.Error, this.GetType().Name, $"Unable to write Settings file, the Settings will get lost when the Bot is stopped: {e.GetType().Name} - {e.Message}", e));
            }
        }

        internal void DeleteSettings(ulong guildId)
        {
            if (settings == null)
            {
                LoadSettings();
            }

            if (settings.TryRemove(guildId, out Settings prefs))
            {
                logger.Log(new LogMessage(LogSeverity.Info, nameof(DeleteSettings), $"Settings for Guild {guildId} removed."));
                SaveSettings();
            }
            else
            {
                logger.Log(new LogMessage(LogSeverity.Warning, nameof(DeleteSettings), $"There were no settings stored for Guild {guildId}!"));
            }
        }
    }

    internal struct Settings
    {
        //public char Prefix { get; set; }
        public ulong? PNavChannel { get; set; }

        //public static readonly Settings Default = new Settings('!');

        public Settings(ulong? pokeNavChannel = null)
        {
            //Prefix = prefix;
            PNavChannel = pokeNavChannel;
        }
    }
}
