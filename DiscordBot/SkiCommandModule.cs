using DSharpPlus;
using DSharpPlus.CommandsNext;
using System;
using static DSharpPlus.CommandsNext.CommandsNextExtension;

namespace DiscordBot
{
    public abstract class SkiCommandModule : DefaultHelpModule
    {
        public const string DefaultStringValue = "(Blank)";

        public static string HandleDefaultStringValue(string s)
        {
            if (s != null && s.Equals(DefaultStringValue, StringComparison.OrdinalIgnoreCase))
            {
                s = null;
            }

            return s;
        }

        public abstract string Module { get; }

        //delegate void LogMessageAction(CommandContext commandContext, string message, Exception exception = null);//    Action<CommandContext, string, Exception> LogMessageAction;

        public void LogMessage(CommandContext commandContext, string message, Exception exception = null)
        {
            commandContext.Client.DebugLogger.LogMessage(exception == null ? LogLevel.Info : LogLevel.Error, Bot.ApplicationName, $"{Module}: {message}", DateTime.Now, exception);
        }
    }
}