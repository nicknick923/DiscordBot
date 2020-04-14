using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace DiscordBot
{
    public class Utilities
    {
        public static async Task<string> GetIPAddressAsync()
        {
            HttpWebRequest request = WebRequest.CreateHttp("http://ipv4bot.whatismyipaddress.com");
            WebResponse response = await request.GetResponseAsync();
            Stream dataStream = response.GetResponseStream();
            StreamReader reader = new StreamReader(dataStream);
            return await reader.ReadToEndAsync();
        }
    }
}
