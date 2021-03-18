using Discord;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CompanionBot.Bot
{
    public class GuildSettings
    {
        private readonly Logger logger;
        private static readonly string path = @"guildSettings.json";
        private Dictionary<ulong, Settings> settings;

        public GuildSettings(Logger log)
        {
            logger = log;
        }

        public Settings this[ulong server]
        {
            get
            {
                if (settings == null)
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
                            logger.Log(new LogMessage(LogSeverity.Warning, this.GetType().Name, $"Unable to read Settings File: {e.GetType().Name} - {e.Message}", e));
                            return Settings.Default();
                        }

                        try
                        {
                            settings = JsonConvert.DeserializeObject<Dictionary<ulong, Settings>>(data);
                        }
                        catch (Exception e)
                        {
                            logger.Log(new LogMessage(LogSeverity.Error, this.GetType().Name, $"Invalid Settings File (JSON parsing failed)! Exception: {e.GetType().Name} - {e.Message}", e));
                            settings = new Dictionary<ulong, Settings>();
                            return Settings.Default();
                        }

                        if (settings.ContainsKey(server))
                        {
                            return settings[server];
                        }
                        else
                        {
                            return Settings.Default();
                        }
                    }
                    else
                    {
                        settings = new Dictionary<ulong, Settings>();
                        return Settings.Default();
                    }
                }
                else
                {
                    if (settings.ContainsKey(server))
                    {
                        return settings[server];
                    }
                    else
                    {
                        return Settings.Default();
                    }
                }
            }
            set
            {
                settings[server] = value;

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
        }
    }

    public struct Settings
    {
        public char Prefix { get; set; }
        public ulong? PNavChannel { get; set; }
        public uint Pwd { get; set; }
        private static readonly Random rand = new Random();

        public Settings(char prefix = '!', ulong? pokeNavChannel = null, uint? Password = null)
        {
            Prefix = prefix;
            PNavChannel = pokeNavChannel;
            if(Password == null)
            {
                Pwd = (uint)rand.Next(int.MaxValue) + (uint)rand.Next(int.MaxValue);
            }
            else
            {
                Pwd = (uint)Password;
            }
        }

        public static Settings Default()
        {
            // switched away from property because every new settings would get the same password then!
            return new Settings('!');
        }
    }
}
