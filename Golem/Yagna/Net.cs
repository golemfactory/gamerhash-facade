using GolemLib;

namespace Golem
{
    public class NetConfig
    {
        public static void SetEnv(RelayType relay)
        {
            Environment.SetEnvironmentVariable("YA_NET_TYPE", "hybrid");

            if (relay == RelayType.Devnet)
                Environment.SetEnvironmentVariable("YA_NET_RELAY_HOST", "yacn2a.dev.golem.network:7477");
            else if (relay == RelayType.Public)
                Environment.SetEnvironmentVariable("YA_NET_RELAY_HOST", "yacn2.dev.golem.network:7477");
            else if (relay == RelayType.Local)
            {
                Environment.SetEnvironmentVariable("YA_NET_RELAY_HOST", "127.0.0.1:16464");
                Environment.SetEnvironmentVariable("MEAN_CYCLIC_BCAST_INTERVAL", "3s");
            }
            else if (relay == RelayType.Central)
            {
                Environment.SetEnvironmentVariable("YA_NET_TYPE", "central");
                // Will use default yagna central net configuration: resolving `_net._tcp.dev.golem.network` record.
            }
        }
    }
}