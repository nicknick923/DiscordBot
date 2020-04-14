using System.Threading.Tasks;
using DSharpPlus.Entities;
using System.Collections.Concurrent;
using DSharpPlus.VoiceNext;
using System.IO;
using System.Diagnostics;

namespace DiscordBot
{
    public class GuildAudioManager
    {
        public VoiceNextConnection Connection;
        public ConcurrentQueue<AudioInfo> AudioInfos = new ConcurrentQueue<AudioInfo>();
        public void Enqueue(AudioInfo audioInfo)
        {
            AudioInfos.Enqueue(audioInfo);
        }

        public async Task CreatePlayerAsync(DiscordChannel discordChannel)
        {
            if (Connection != null)
            {
                return;
            }

            Connection = await discordChannel.ConnectAsync();
        }

        public async Task PlayAsync()
        {
            if (Connection == null)
            {
                return;
            }

            if (AudioInfos.TryDequeue(out AudioInfo audioInfo))
            {
                await PlayAsync(Connection, audioInfo);

                if (AudioInfos.Count == 0 && Connection != null)
                {
                    Connection.Disconnect();
                    Connection = null;
                }
                else
                {
                    await PlayAsync();
                }
            }
        }

        private async Task PlayAsync(VoiceNextConnection connection, AudioInfo audioInfo)
        {
            if (audioInfo.CommandContext.Command.Module.GetInstance(audioInfo.CommandContext.Services) is SkiCommandModule skiCommandModule)
            {
                skiCommandModule.LogMessage(audioInfo.CommandContext, $"Playing: {audioInfo.FullPath}");
                try
                {
                    ProcessStartInfo processStartInfo = new ProcessStartInfo("ffmpeg", $@"-loglevel panic -i ""{audioInfo.FullPath}"" -ac 2 -f s16le -ar 48000 pipe:1")
                    {
                        RedirectStandardOutput = true,
                        UseShellExecute = false
                    };
                    Process ffmpeg = Process.Start(processStartInfo);
                    Stream audioStream = ffmpeg.StandardOutput.BaseStream;
                    VoiceTransmitStream transmitStream = connection.GetTransmitStream();
                    await audioStream.CopyToAsync(transmitStream, Program.CancellationTokenSource.Token);
                    await transmitStream.FlushAsync(Program.CancellationTokenSource.Token);
                    await connection.WaitForPlaybackFinishAsync();
                }

                catch (System.Exception e)
                {
                    skiCommandModule.LogMessage(audioInfo.CommandContext, "Failed to play", e);
                }
            }
        }
    }
}