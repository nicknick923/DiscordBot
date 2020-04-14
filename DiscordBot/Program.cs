using System;
using System.Threading;

namespace DiscordBot
{
    public class Program
    {
        public static Random Random = new Random();
        public static CancellationTokenSource CancellationTokenSource = new CancellationTokenSource();

        static void Main()
        {
            Config.LoadConfig();
            using (Bot b = new Bot())
            {
                b.RunAsync().Wait();
            }
        }
    }
}
