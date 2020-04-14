using DSharpPlus.CommandsNext;
using System.IO;

namespace DiscordBot
{
    public struct AudioInfo
    {
        public AudioInfo(string biteFileMini, CommandContext commandContext)
        {
            BiteFileMini = biteFileMini;
            CommandContext = commandContext;
            FullPath = new FileInfo(biteFileMini).FullName;
        }
        public string FullPath { get; }
        public string BiteFileMini { get; }
        public CommandContext CommandContext { get; }
    }
}