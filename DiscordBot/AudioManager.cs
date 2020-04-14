using DSharpPlus.Entities;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace DiscordBot
{
    public class AudioManager
    {
        public ConcurrentDictionary<ulong, GuildAudioManager> data = new ConcurrentDictionary<ulong, GuildAudioManager>();

        public GuildAudioManager GetOrCreateGuildAudioManager(DiscordGuild discordGuild)
        {
            if (!data.TryGetValue(discordGuild.Id, out GuildAudioManager guildAudioManager))
            {
                guildAudioManager = data.AddOrUpdate(discordGuild.Id, new GuildAudioManager(), (a, b) => b);
            }
            return guildAudioManager;
        }

        public void Reset()
        {
            Dictionary<ulong, GuildAudioManager> temp = new Dictionary<ulong, GuildAudioManager>(data);
            data = new ConcurrentDictionary<ulong, GuildAudioManager>();

            foreach (var x in temp.Values)
            {
                x?.Connection?.Disconnect();
            }
        }
    }
}