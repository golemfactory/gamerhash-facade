using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Golem.Yagna
{
    public class YagnaOptionsFactory
    {
        public static string DefaultYagnaApiUrl => "http://127.0.0.1:11502";
        public static YagnaStartupOptions CreateStartupOptions(bool openConsole, string appKey)
        {
            var yagnaOptions = new YagnaStartupOptions
            {
                Debug = true,
                OpenConsole = openConsole,
                AppKey = appKey,
                YagnaApiUrl = DefaultYagnaApiUrl
            };
            return yagnaOptions;
        }
    }
}
