using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Timers;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Interactivity;
using DSharpPlus.VoiceNext;
using Newtonsoft.Json;

namespace DiscordBot
{
    public class Bot : IDisposable
    {
        public static Bot Instance;

        public const string ApplicationName = nameof(DiscordBot);
        private const int TimerTimeout = 5 * 60 * 1000;
        private const string DumpDir = nameof(DumpDir);
        private readonly DiscordClient discord;
        private readonly CommandsNextExtension commands;
        private readonly InteractivityExtension interactivity;
        private readonly VoiceNextExtension voice;
        private DiscordUser creator;

        public Bot()
        {
            Instance = this;
            //DownloadAudioClips();

            DiscordConfiguration discordConfiguration = new DiscordConfiguration()
            {
                AutoReconnect = true,
                Token = Config.Instance.Token,
                TokenType = TokenType.Bot,
                UseInternalLogHandler = true,
                LogLevel = LogLevel.Info,
                DateTimeFormat = "MM/dd/yyyy hh:mm:ss tt"
            };

            discord = new DiscordClient(discordConfiguration);
            discord.DebugLogger.LogMessageReceived += LogMessageReceived;

            interactivity = discord.UseInteractivity(new InteractivityConfiguration());
            voice = discord.UseVoiceNext();
            commands = discord.UseCommandsNext(new CommandsNextConfiguration()
            {
                StringPrefixes = new[] { Config.Instance.Prefix },
                CaseSensitive = false
            });

            commands.RegisterCommands<GeneralCommands>();
            commands.RegisterCommands<AudioModule>();
            commands.RegisterCommands<MinecraftCommands>();
            commands.CommandErrored += CommandErrored;
            commands.CommandExecuted += CommandExecuted;
            //discord.MessageCreated += MessageCreated;
            discord.Ready += Ready;
        }

        private void LogMessageReceived(object sender, DebugLogMessageEventArgs e)
        {
            File.AppendAllText(Config.Instance.LogFileLocation, $"{e}{Environment.NewLine}");
        }

        private static void DownloadAudioClips()
        {
            Dictionary<string, string> urls = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText("SLJ.json"));

            if (Directory.Exists(DumpDir))
            {
                Directory.Delete(DumpDir, true);
            }
            Directory.CreateDirectory(DumpDir);

            foreach ((string fileName, string url) in urls)
            {
                WebRequest webRequest = WebRequest.Create(url);

                using WebResponse x = webRequest.GetResponse();
                using Stream y = x.GetResponseStream();
                using (FileStream fileStream = File.Create($"{DumpDir}/{fileName}.mp3"))
                {
                    y.CopyTo(fileStream);
                }
            }
        }

        Timer timer;
        public static string GameName = "Really Cool Games";
        static DiscordActivity getDiscordActivity() => new DiscordActivity(GameName, ActivityType.Playing);
        private Task CommandExecuted(CommandExecutionEventArgs commandExecutionEventArgs)
        {
            return SetTimer();
        }

        public static async Task SetTimer()
        {
            try
            {

                Instance.timer?.Dispose();
                DateTime dateTime = DateTime.Now;
                await Instance.discord.UpdateStatusAsync(getDiscordActivity(), UserStatus.Online);
                Instance.timer = new Timer(TimerTimeout);
                Instance.timer.Elapsed += async (sender, e) =>
                {
                    Instance.timer.Stop();
                    Instance.discord.DebugLogger.LogMessage(LogLevel.Info, ApplicationName, "Bot is Idle", DateTime.Now);
                    await Instance.discord.UpdateStatusAsync(getDiscordActivity(), UserStatus.Idle, DateTimeOffset.Now);
                    Instance.timer?.Dispose();
                };
                Instance.timer.Start();
            }
            catch (Exception e)
            {
                Instance.discord.DebugLogger.LogMessage(LogLevel.Error, ApplicationName, "Failed to set timer", DateTime.Now, e);
            }
        }

        private async Task CommandErrored(CommandErrorEventArgs e)
        {
            if (!(e.Exception is CommandCancelledException))
            {
                if (e.Command == null)
                {
                    await e.Context.RespondAsync($"I couldn't find the command you requested, did you spell it right?");
                    //await MyCommands.Instance.DefaultHelpAsync(e.Context);
                }
                else
                {
                    await e.Context.RespondAsync($"Something happened and I worked as programmed, but I don't think that's really what we wanted here. Did you pass in the wrong arguments? (shame on the creator of this bot if you did {creator.Mention})");
                    if (e.Command.Module.GetInstance(e.Context.Services) is SkiCommandModule skiCommandModule)
                    {
                        await skiCommandModule.DefaultHelpAsync(e.Context, e.Command.Name);
                    }

                    discord.DebugLogger.LogMessage(LogLevel.Error, ApplicationName, e.Command.Name, DateTime.Now, e.Exception);
                }
            }
        }

        private async Task Ready(ReadyEventArgs readyEventArgs)
        {
            await SetTimer();
        }

        public async Task RunAsync()
        {
            discord.DebugLogger.LogMessage(LogLevel.Info, ApplicationName, "Connecting", DateTime.Now);
            await discord.ConnectAsync();
            creator = await discord.GetUserAsync(IDs.Users.Ski);
            discord.DebugLogger.LogMessage(LogLevel.Info, ApplicationName, "Connected", DateTime.Now);
            await WaitForCancellationAsync();
            discord.DebugLogger.LogMessage(LogLevel.Info, ApplicationName, "Disconnecting", DateTime.Now);
            await discord.DisconnectAsync();
            discord.DebugLogger.LogMessage(LogLevel.Info, ApplicationName, $"Disconnected{Environment.NewLine}", DateTime.Now);
        }

        private async Task WaitForCancellationAsync()
        {
            while (!Program.CancellationTokenSource.IsCancellationRequested)
            {
                await Task.Delay(1000);
            }
        }

        private Task MessageCreated(MessageCreateEventArgs messageCreateEventArgs)
        {
            try
            {
                if (messageCreateEventArgs.Author.Id != IDs.Bots.AmaranthBot)
                {
                    discord.DebugLogger.LogMessage(LogLevel.Info, ApplicationName, $"MessageCreated - {messageCreateEventArgs.Author.Username}: {messageCreateEventArgs.Message.Content}", DateTime.Now);
                }
            }
            catch (Exception e)
            {
                discord.DebugLogger.LogMessage(LogLevel.Error, ApplicationName, $"MessageCreated - {messageCreateEventArgs.Author.Username}: {messageCreateEventArgs.Message.Content}", DateTime.Now, e);
            }
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            discord.Dispose();
        }
    }

}
