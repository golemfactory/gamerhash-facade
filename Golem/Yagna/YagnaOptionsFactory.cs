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
        public static Network DefaultNetwork = Network.Goerli;
        public static string DefaultAppKey { get => _generatedAppKey.Value; }
        public static YagnaStartupOptions CreateStartupOptions(bool openConsole)
        {
            var yagnaOptions = new YagnaStartupOptions
            {
                Debug = false,
                OpenConsole = openConsole,
                AppKey = DefaultAppKey,
                YagnaApiUrl = DefaultYagnaApiUrl,
                Network = DefaultNetwork
            };
            return yagnaOptions;
        }
    }
}
