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

        [Command]
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

        [Command, Description("What do you expect to happen?")]
        public async Task Ping(CommandContext commandContext)
        {
            await commandContext.RespondAsync("pong");
        }

        [Command, Aliases("Hello", "Sup", "Hey"), Description("Just a generic hi")]
        public async Task Hi(CommandContext commandContext)
        {
            await commandContext.RespondAsync(Instance.GetGreetingMessage(commandContext.User.Mention));
        }

        [Command, Description("This will stop the bot")]
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

        [Command, Description("Reloads string data from file")]
        public async Task Reload(CommandContext commandContext)
        {
            LoadConfig();
            await commandContext.RespondAsync("Content reloaded");
        }

        [Command]
        public async Task IPAddress(CommandContext commandContext)
        {
            await commandContext.Message.CreateReactionAsync(DiscordEmoji.FromName(commandContext.Client, ":eyes:"));
            string ipAddress = await GetIPAddressAsync();
            await commandContext.RespondAsync(ipAddress);
        }

        [Command, Hidden]
        public async Task ThrowException(CommandContext commandContext)
        {
            await commandContext.RespondAsync("doing stuff");
            throw new Exception("Hey, I borked");
        }

        [Command, Hidden]
        public async Task UpdateGame(CommandContext commandContext, [RemainingText] string name)
        {
            Bot.GameName = name;
            await Bot.SetTimer();
            await commandContext.RespondAsync($"Game set to {name}");
        }

        private enum GradeTypes
        {
            CSharp,
            CPlusPlus
        }

        [Command, Hidden]
        public async Task Grade(CommandContext commandContext, string className = "CS1430", bool final = false)
        {
            DiscordAttachment attachment = commandContext.Message.Attachments.FirstOrDefault();

            GradeTypes gradeType;
            switch (className.ToLower())
            {
                case "cs1430":
                case "cs143":
                case "1430":
                case "143":
                    if (attachment == null || !(attachment.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || attachment.FileName.EndsWith(".cpp", StringComparison.OrdinalIgnoreCase)))
                    {
                        await commandContext.RespondAsync(attachment == null ? "You forgot attach the program to grade" : "I can only grade zips or a single cpp");
                        await DefaultHelpAsync(commandContext, nameof(Grade));
                        return;
                    }
                    gradeType = GradeTypes.CPlusPlus;
                    break;
                case "cs2430":
                case "cs243":
                case "2430":
                case "243":
                    if (attachment == null || !(attachment.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || attachment.FileName.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
                    {
                        await commandContext.RespondAsync(attachment == null ? "You forgot attach the program to grade" : "I can only grade zips or a single cs");
                        await DefaultHelpAsync(commandContext, nameof(Grade));
                        return;
                    }
                    gradeType = GradeTypes.CSharp;
                    break;
                default:
                    await commandContext.RespondAsync("I need to know what class to grade for! (Valid options: CS1430, CS143, 1430, 143, CS2430, CS243, 2430, 243)");
                    await DefaultHelpAsync(commandContext, nameof(Grade));
                    return;
            }

            string currentDirectory = Directory.GetCurrentDirectory();
            await commandContext.RespondAsync("Getting files and building");

            string programToGrade = attachment.FileName.Split('.')[0];
            LogMessage(commandContext, $"Grading {gradeType} {programToGrade} for {commandContext.User.Username}({commandContext.User.Id})");

            switch (gradeType)
            {
                case GradeTypes.CSharp: await Grader.GradeCSharp(commandContext, attachment, currentDirectory, programToGrade, final); break;
                case GradeTypes.CPlusPlus: await Grader.GradeCPP(commandContext, attachment, currentDirectory, programToGrade, final); break;
            }

        }
    }
}