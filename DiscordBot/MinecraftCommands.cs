using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using static DiscordBot.Config;
using static DiscordBot.Utilities;

namespace DiscordBot
{
    public class MinecraftCommands : SkiCommandModule
    {
        public override string Module => nameof(MinecraftCommands);

        [Command]
        public async Task MC(CommandContext commandContext,
            [Description("Command (Start|Info)")] string command = DefaultStringValue,
            [RemainingText, Description("Minecraft Version to start")] string version = DefaultStringValue)
        {
            version = HandleDefaultStringValue(version);
            command = HandleDefaultStringValue(command);

            if (command == null)
            {
                await commandContext.RespondAsync("Invalid usage");
                await DefaultHelpAsync(commandContext, nameof(MC));
            }
            else if (command.Equals("Start", StringComparison.OrdinalIgnoreCase))
            {
                await StartAsync(commandContext, version);
            }
            else if (command.Equals("Info", StringComparison.OrdinalIgnoreCase))
            {
                await MCInfoAsync(commandContext);
            }
            else
            {
                await commandContext.RespondAsync("Unknown Command");
                await DefaultHelpAsync(commandContext, nameof(MC));
            }
        }

        private async Task StartAsync(CommandContext commandContext, string version)
        {
            const string serverFolder = @"C:\Users\Nick\Desktop\Minecraft Server";
            string defaultVersion = Instance.MCVersions.Single(a => a.Value.Default).Key;
            if (string.IsNullOrWhiteSpace(version))
            {
                version = defaultVersion;
                await commandContext.RespondAsync($"You did not specify which version, therefore I'm starting {defaultVersion} MC.");
                await MCInfoAsync(commandContext);
            }

            if (Instance.MCVersions.ContainsKey(version))
            {
                MCVersion mcVersion = Instance.MCVersions[version];
                string responseMessage = $"Starting {version} MC Server";
                await commandContext.RespondAsync(responseMessage);
                string directory = Path.Join(serverFolder, mcVersion.FolderName);
                string botStartFile = Path.Join(directory, "botStart.bat");
                ProcessStartInfo processStartInfo = new ProcessStartInfo(botStartFile)
                {
                    WindowStyle = ProcessWindowStyle.Minimized,
                    WorkingDirectory = directory
                };
                Process.Start(processStartInfo);
            }
            else
            {
                await commandContext.RespondAsync($"Unable to find MC Version '{version}'");
                await MCInfoAsync(commandContext);
            }
        }

        private static async Task MCInfoAsync(CommandContext commandContext)
        {
            StringBuilder stringBuilder = new StringBuilder();
            string ipAddress = await GetIPAddressAsync();

            foreach (KeyValuePair<string, MCVersion> nameAndMCVersion in Instance.MCVersions)
            {
                MCVersion mcVersion = nameAndMCVersion.Value;
                stringBuilder.AppendLine($"**[{nameAndMCVersion.Key}]({mcVersion.DownloadURL})** ({ipAddress}{(mcVersion.Port == 25565 ? "" : $":{mcVersion.Port}")})");
                stringBuilder.AppendLine($"*{mcVersion.Description}*");
                stringBuilder.AppendLine();
            }

            DiscordEmbedBuilder discordEmbedBuilder = Instance.GetDiscordEmbedBuilder()
                .WithTitle("Current Minecraft Versions")
                .WithDescription(stringBuilder.ToString().Trim());
            await commandContext.RespondAsync(embed: discordEmbedBuilder.Build());
        }
    }
}
