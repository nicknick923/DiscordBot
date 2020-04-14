using System;
using System.Collections.Generic;
using System.Linq;

namespace DiscordBot
{
    public static class Extensions
    {
        public static T Random<T>(this ICollection<T> source)
        {
            return source.ToList()[Program.Random.Next(source.Count())];
        }

        public static TValue Random<TKey, TValue>(this IDictionary<TKey, TValue> source)
        {
            return source[source.Keys.ToArray()[Program.Random.Next(source.Keys.Count)]];
        }

        public static IEnumerable<string> SplitByLength(this string str, int maxLength)
        {
            for (int index = 0; index < str.Length; index += maxLength)
            {
                yield return str.Substring(index, Math.Min(maxLength, str.Length - index));
            }
        }
    }
}
