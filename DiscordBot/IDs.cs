using System.Collections.Generic;

namespace DiscordBot
{
    public static class IDs
    {
        public static class Bots
        {
            public const ulong AmaranthBot = 693527693156810784;
        }

        public static class Users
        {
            public const ulong Ski = 142396629230682112;
        }

        public static class UserGroups
        {
            public static HashSet<ulong> Admins = new HashSet<ulong>() { Users.Ski };
        }

        public static class Roles
        {
            public const ulong Admins = 693558683812233216;
        }
    }

}
