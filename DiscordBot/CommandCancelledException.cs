using System;

namespace DiscordBot
{
    public class CommandCancelledException : Exception
    {
        public CommandCancelledException() : base("Command execution was cancelled due to unmet criteria.")
        {
        }
    }
}