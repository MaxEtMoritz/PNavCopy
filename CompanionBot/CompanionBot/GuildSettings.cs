using Discord;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CompanionBot
{
    public class GuildSettings
    {
        private static readonly string path = @"guildSettings.json";
        private Dictionary<ulong, Settings> settings;
        public Settings this[IGuild server]
        {
            get
            {
                if(settings == null)
                {
                    if (File.Exists(path))
                    {
                        string data = "";
                        try
                        {
                            data = File.ReadAllText(path);
                        }
                        catch (Exception e)
                        {
                            Console.Error.WriteLine($"Unable to read Settings File: {e.GetType().Name} - {e.Message}");
                            return Settings.Default;
                        }

                        try
                        {
                            settings = JsonConvert.DeserializeObject<Dictionary<ulong, Settings>>(data);
                        }
                        catch (Exception e)
                        {
                            Console.Error.WriteLine($"Invalid Settings File (JSON parsing failed)! Exception: {e.GetType().Name} - {e.Message}");
                            settings = new Dictionary<ulong, Settings>();
                            return Settings.Default;
                        }

                        if (settings.ContainsKey(server.Id))
                        {
                            return settings[server.Id];
                        }
                        else
                        {
                            return Settings.Default;
                        }
                    }
                    else
                    {
                        settings = new Dictionary<ulong, Settings>();
                        return Settings.Default;
                    }
                }
                else
                {
                    if (settings.ContainsKey(server.Id))
                    {
                        return settings[server.Id];
                    }
                    else
                    {
                        return Settings.Default;
                    }
                }
            }
            set
            {
                settings[server.Id] = value;

                string data = JsonConvert.SerializeObject(settings, Formatting.Indented);
                try
                {
                    File.WriteAllText(path, data);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine($"Unable to write Settings file, the Settings will get lost when the Bot is stopped: {e.GetType().Name} - {e.Message}");
                }
            }
        }
    }

    public struct Settings
    {
        public char Prefix { get; set; }
        public char PNavPrefix { get; set; }
        public ulong? PNavChannel { get; set; }

        public static readonly Settings Default = new Settings('!');

        public Settings(char prefix = '!', ulong? pokeNavChannel = null, char pokeNavPrefix = '$')
        {
            Prefix = prefix;
            PNavChannel = pokeNavChannel;
            PNavPrefix = pokeNavPrefix;
        }
    }
}
