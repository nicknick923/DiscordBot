using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSharpPlus.Entities;
using Newtonsoft.Json;

namespace DiscordBot
{
    public class Config
    {
        public static Config Instance;
        public string Token = "The token";
        public string Prefix = "The prefix";
        public string LogFileLocation = "LogFilePath";
        public string GraderDump = "GraderDump";
        public string Color = "#E52B52";
        public DiscordColor DiscordColor => new DiscordColor(Color);
        public List<string> Greetings;
        public List<string> CannotStopInsults;
        public Dictionary<string, MCVersion> MCVersions = new Dictionary<string, MCVersion>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, SoundGroup> SoundGroups = new Dictionary<string, SoundGroup>(StringComparer.OrdinalIgnoreCase);

        public DiscordEmbedBuilder GetDiscordEmbedBuilder() => new DiscordEmbedBuilder().WithColor(DiscordColor);

        public static void LoadConfig()
        {
            if (!File.Exists("config.json"))
            {
                throw new FileNotFoundException("Couldn't find config.json");
            }
            Instance = LoadFromFile("config.json");
            //process and add commands
        }

        private static Config LoadFromFile(string path)
        {
            using StreamReader sr = new StreamReader(path);
            Config config = JsonConvert.DeserializeObject<Config>(sr.ReadToEnd());
            config.PopulateSoundBites();
            return config;
        }

        private void PopulateSoundBites()
        {
            foreach (var item in SoundGroups.Values)
            {
                item.LoadSoundBites();
            }
        }

        private void SaveToFile(string path)
        {
            File.WriteAllText(path, JsonConvert.SerializeObject(this));
        }

        public string GetGreetingMessage(string mentionString)
        {
            return $"{Greetings.Random()} {mentionString}";
        }

        public SoundGroup GetSoundGroup(string group)
        {
            return SoundGroups.ContainsKey(group) ? SoundGroups[group] : null;
        }

        public string GetRandomSoundBite(bool allowNSFW = false)
        {
            return SoundGroups.Where(a => allowNSFW || a.Value.NSFW == false).SelectMany(soundGroup => soundGroup.Value.SoundBites.Select(fileName => $"{soundGroup.Value.FolderName}\\{fileName}")).ToList().Random();
        }

        public class MCVersion
        {
            public string Description;
            public int Port;
            public Uri DownloadURL;
            public string FolderName;
            public bool Default = false;
        }

        public class SoundGroup
        {
            [NonSerialized]
            public HashSet<string> SoundBites;
            public string FolderName;
            public bool NSFW = true;

            public void LoadSoundBites()
            {
                SoundBites = Directory.EnumerateFiles(FolderName).Select(a => a.Replace($"{FolderName}\\", "")).ToHashSet(StringComparer.OrdinalIgnoreCase);
            }

            public string Random()
            {
                return $"{FolderName}\\{SoundBites.Random()}";
            }
            public string GetBite(string name)
            {
                if (SoundBites.Contains(name))
                {
                    return $"{FolderName}\\{name}";
                }
                else if (SoundBites.Contains($"{name}.mp3"))
                {
                    return $"{FolderName}\\{name}.mp3";
                }
                else
                {
                    return null;
                }
            }
        }

        public class BiteInfo
        {
            public SoundGroup SoundGroup;
            public string FilePath;
        }
    }
}
