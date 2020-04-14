using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using static DiscordBot.Utilities;
using static DiscordBot.Config;

namespace DiscordBot
{
    public class GeneralCommands : SkiCommandModule
    {
        public override string Module => nameof(GeneralCommands);

        [Command(nameof(Info)), Description("Another way to access help")]
        public async Task Info(CommandContext commandContext, string command = DefaultStringValue)
        {
            command = HandleDefaultStringValue(command);

            if (command == null)
            {
                await DefaultHelpAsync(commandContext);
            }
            else
            {
                await DefaultHelpAsync(commandContext, command);
            }
        }

        [Command(nameof(Ping)), Description("What do you expect to happen?")]
        public async Task Ping(CommandContext commandContext)
        {
            await commandContext.RespondAsync("pong");
        }

        [Command(nameof(Hi)), Aliases("Hello", "Sup", "Hey"), Description("Just a generic hi")]
        public async Task Hi(CommandContext commandContext)
        {
            await commandContext.RespondAsync(Instance.GetGreetingMessage(commandContext.User.Mention));
        }

        [Command(nameof(Stop)), Description("This will stop the bot")]
        public async Task Stop(CommandContext commandContext)
        {
            ulong authorID = commandContext.User.Id;
            DiscordGuild guild = commandContext.Guild;
            async Task<bool> isInRoleAsync(ulong roleID) => (await guild.GetMemberAsync(authorID)).Roles.Any(a => a.Id == roleID);
            bool hasPermission = guild == null
                ? IDs.UserGroups.Admins.Contains(authorID)
                : await isInRoleAsync(IDs.Roles.Admins);
            if (hasPermission)
            {
                await commandContext.RespondAsync("Stopping");
                Program.CancellationTokenSource.Cancel();
            }
            else
            {
                DiscordRole role = guild.GetRole(IDs.Roles.Admins);
                await commandContext.RespondAsync($"{Instance.CannotStopInsults.Random()} You must be a member of {role.Name}!");
            }
        }

        [Command(nameof(Reload)), Description("Reloads string data from file")]
        public async Task Reload(CommandContext commandContext)
        {
            LoadConfig();
            await commandContext.RespondAsync("Content reloaded");
        }

        [Command(nameof(IPAddress)), Aliases("IP"), Description("Gets the Minecraft Server's IP Address")]
        public async Task IPAddress(CommandContext commandContext)
        {
            await commandContext.Message.CreateReactionAsync(DiscordEmoji.FromName(commandContext.Client, ":eyes:"));
            string ipAddress = await GetIPAddressAsync();
            await commandContext.RespondAsync(ipAddress);
        }

        [Command(nameof(ThrowException)), Description("Dis will trow an exception"), Aliases("throw"), Hidden]
        public async Task ThrowException(CommandContext commandContext)
        {
            await commandContext.RespondAsync("doing stuff");
            throw new Exception("Hey, I borked");
        }

        [Command(nameof(Grade)), Description("Grades a program that is zipped up and attached"), Hidden]
        public async Task Grade(CommandContext commandContext)
        {
            DiscordAttachment attachment = commandContext.Message.Attachments.FirstOrDefault();
            if (attachment == null || !attachment.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                await commandContext.RespondAsync(attachment == null ? "You forgot attach the program to grade" : "I can only grade zips");
                await DefaultHelpAsync(commandContext, nameof(Grade));
                return;
            }
            string currentDirectory = Directory.GetCurrentDirectory();
            await commandContext.RespondAsync("Getting files and building");

            string programToGrade = attachment.FileName.Replace(".zip", "", StringComparison.OrdinalIgnoreCase);
            LogMessage(commandContext, $"Grading {programToGrade} for {commandContext.User.Username}({commandContext.User.Id})");

            await Grader.Grade(commandContext, attachment, currentDirectory);
        }
    }
}