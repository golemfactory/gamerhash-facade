using System.Text;
using Golem.Yagna.Types;
using System.Security.Cryptography;

namespace Golem.Yagna
{
    public class YagnaOptionsFactory
    {
        private static readonly Lazy<string> _generatedAppKey = new Lazy<string>(() =>
        {
            byte[] data = RandomNumberGenerator.GetBytes(20);
            var str = Convert.ToBase64String(data);
            return str;
        });

        public const string DefaultYagnaApiUrl = "http://127.0.0.1:12502";
        public static string DefaultAppKey { get => _generatedAppKey.Value; }
        public static YagnaStartupOptions CreateStartupOptions(Network network)
        {
            var yagnaOptions = new YagnaStartupOptions
            {
                Debug = false,
                AppKey = DefaultAppKey,
                YagnaApiUrl = DefaultYagnaApiUrl,
                Network = network
            };
            return yagnaOptions;
        }
    }
}
