using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Golem.Yagna
{
    public class YagnaOptionsFactory
    {
        public const string DefaultYagnaApiUrl = "http://127.0.0.1:11502";
        public const string DefaultAppKey = "0x6b0f51cfaae644ee848dfa455dabea5d";
        public static YagnaStartupOptions CreateStartupOptions(bool openConsole)
        {
            var yagnaOptions = new YagnaStartupOptions
            {
                Debug = true,
                OpenConsole = openConsole,
                AppKey = DefaultAppKey,
                YagnaApiUrl = DefaultYagnaApiUrl
            };
            return yagnaOptions;
        }
    }
}
