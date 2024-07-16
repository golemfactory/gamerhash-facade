using GolemLib;

namespace Golem
{
    public enum RelayType
    {
        /// <summary>
        /// Main public relay at: yacn2.dev.golem.network:7477
        /// </summary>
        Public,
        /// <summary>
        /// Devnet relay at: yacn2a.dev.golem.network:7477
        /// </summary>
        Devnet,
        /// <summary>
        /// Local relay at: 127.0.0.1:16464
        /// </summary>
        Local,
        /// <summary>
        /// Use older centralized solution.
        /// </summary>
        Central,
        /// <summary>
        /// Use central net local setup.
        /// </summary>
        LocalCentral,
        /// <summary>
        /// Facade won't set any value. It must be set from outside using env variables.
        /// </summary>
        None,
    }

    public class NetConfig
    {
        public static void SetEnv(RelayType relay)
        {
            if (relay == RelayType.None)
                return;

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
            else if (relay == RelayType.LocalCentral)
            {
                Environment.SetEnvironmentVariable("YA_NET_TYPE", "central");
                Environment.SetEnvironmentVariable("CENTRAL_NET_HOST", "127.0.0.1:6464");
            }
        }
    }
}
