using System;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using static DiscordBot.Config;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using DSharpPlus.VoiceNext;

namespace DiscordBot
{
    public class AudioModule : SkiCommandModule
    {
        private AudioManager AudioManager = new AudioManager();

        private readonly HashSet<string> NonVoiceCommands = new[] { nameof(SoundBitesInfo), nameof(ResetAudio) }.Select(a => a.ToLower()).ToHashSet();

        public override string Module => nameof(AudioModule);

        public override async Task BeforeExecutionAsync(CommandContext commandContext)
        {
            if (!NonVoiceCommands.Contains(commandContext.Command.Name.ToLower()))
            {
                await AudioChecksAsync(commandContext);
            }
            await base.BeforeExecutionAsync(commandContext);
        }

        private static async Task AudioChecksAsync(CommandContext commandContext)
        {
            if (commandContext.Guild is DiscordGuild)
            {
                if (commandContext.Member?.VoiceState?.Channel == null)
                {
                    await commandContext.RespondAsync("I cannot speak to you if you aren't in a voice channel!");
                    throw new CommandCancelledException();
                }
            }
            else
            {
                await commandContext.RespondAsync("I cannot speak to you in a DM!");
                throw new CommandCancelledException();
            }
        }

        [Command(nameof(PlayAll)), Hidden]
        public async Task PlayAll(CommandContext commandContext)
        {
            DiscordChannel discordChannel = commandContext.Member.VoiceState.Channel;
            GuildAudioManager guildAudioManager = AudioManager.GetOrCreateGuildAudioManager(commandContext.Guild);
            await guildAudioManager.CreatePlayerAsync(discordChannel);
            foreach (SoundGroup soundGroup in Instance.SoundGroups.Values)
            {
                foreach (string item in soundGroup.SoundBites)
                {
                    guildAudioManager.Enqueue(new AudioInfo($"{soundGroup.FolderName}\\{item}", commandContext));
                }
            }
            await guildAudioManager.PlayAsync();
        }

        [Command(nameof(SoundBitesInfo)), Aliases("SBI", "SBInfo"), Description("Provides info on Sound Groups and Sound Bites")]
        public async Task SoundBitesInfo(CommandContext commandContext)
        {
            StringBuilder stringBuilder = new StringBuilder();
            foreach ((string name, SoundGroup soundGroup) in Instance.SoundGroups)
            {
                stringBuilder.AppendLine($"**{name}{(soundGroup.NSFW ? " (NSFW)" : "")}**");
                stringBuilder.AppendLine(string.Join(",", soundGroup.SoundBites.Select(a => a.Replace($"{soundGroup.FolderName}\\", "").Replace(".mp3", ""))));
            }
            string soundBites = stringBuilder.ToString();
            LogMessage(commandContext, $"SoundBiteInfoLength:{soundBites.Length}");
            DiscordEmbedBuilder discordEmbedBuilder = Instance.GetDiscordEmbedBuilder()
                .WithTitle("Current Sound Groups")
                .WithDescription(soundBites);
            await commandContext.RespondAsync(embed: discordEmbedBuilder.Build());
        }

        [Command(nameof(ResetAudio))]
        public async Task ResetAudio(CommandContext commandContext)
        {
            AudioManager.Reset();
            await commandContext.RespondAsync("Audio Reset");
        }

        [Command(nameof(Play)), Aliases("SB", "SoundBite"), Description("Plays a Sound Bite")]
        public async Task Play(CommandContext commandContext,
            [Description("THe Sound Group to play audio from")] string group = DefaultStringValue,
            [Description("The Sound Bite to play")] string bite = DefaultStringValue)
        {
            DiscordChannel discordChannel = commandContext.Member.VoiceState.Channel;
            bool nsfw = discordChannel.Name.Contains("NSFW", StringComparison.OrdinalIgnoreCase);
            group = HandleDefaultStringValue(group);
            string biteFileMini = null;
            if (group == null)
            {
                biteFileMini = Instance.GetRandomSoundBite(nsfw);
            }
            else if (Instance.GetSoundGroup(group) is SoundGroup soundGroup)
            {
                if (soundGroup.NSFW && !nsfw)
                {
                    await commandContext.RespondAsync("Unable to play from this Sound Group, you must be in a NSFW voice channel");
                }
                else
                {
                    bite = HandleDefaultStringValue(bite);
                    if (bite == null)
                    {
                        biteFileMini = soundGroup.Random();
                    }
                    else
                    {
                        biteFileMini = soundGroup.GetBite(bite);
                        if (biteFileMini == null)
                        {
                            await commandContext.RespondAsync($"Unable to find Sound Bite '{bite}'");
                        }
                    }
                }
            }
            else
            {
                DiscordEmbedBuilder discordEmbedBuilder = Instance.GetDiscordEmbedBuilder()
                    .WithTitle("Current Sound Groups")
                    .WithDescription(string.Join(Environment.NewLine, Instance.SoundGroups.Keys));

                await commandContext.RespondAsync("Unable to find Sound Group", embed: discordEmbedBuilder.Build());
            }

            if (biteFileMini != null)
            {

                GuildAudioManager guildAudioManager = AudioManager.GetOrCreateGuildAudioManager(commandContext.Guild);
                guildAudioManager.Enqueue(new AudioInfo(biteFileMini, commandContext));
                await guildAudioManager.CreatePlayerAsync(discordChannel);
                await guildAudioManager.PlayAsync();
            }
        }
    }
}