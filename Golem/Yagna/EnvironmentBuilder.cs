namespace Golem.Yagna
{
    public class EnvironmentBuilder
    {
        public const String DefaultLocalAddress = "127.0.0.1";
        public const int DefaultGsbPort = 12501;
        public const int DefaultApiPort = 12502;

        public const String DefaultBindAddress = "0.0.0.0";
        public const int DefaultBindPort = 12503;

        private static readonly Dictionary<string, string> DefaultEnv = new Dictionary<string, string>()
        {
            { "GSB_URL", $@"tcp://{DefaultLocalAddress}:{DefaultGsbPort}" },
            { "YAGNA_API_URL", $@"http://{DefaultLocalAddress}:{DefaultApiPort}" },
            { "YA_NET_BIND_URL", $@"udp://{DefaultBindAddress}:{DefaultBindPort}" },
            { "YA_NET_BROADCAST_SIZE", "12" },
            { "YA_NET_RELAY_HOST", "yacn2.dev.golem.network:7477" },
            { "YA_NET_TYPE", "hybrid" },
            { "EXE_UNIT_FILE_LOG_LEVEL", "debug,h2=info,hyper=info,hyper_util=info" },
            { "BCAST_NODE_BAN_TIMEOUT", "5s" },
        };

        private readonly Dictionary<string, string> env = new Dictionary<string, string>();

        public EnvironmentBuilder WithGsbUrl(string s)
        {
            env["GSB_URL"] = s;
            return this;
        }

        public EnvironmentBuilder WithMetricsGroup(string s)
        {
            env["YAGNA_METRICS_GROUP"] = s;
            return this;
        }

        public EnvironmentBuilder WithYagnaApiUrl(string s)
        {
            env["YAGNA_API_URL"] = s;
            return this;
        }

        public EnvironmentBuilder WithYagnaAppKey(string s)
        {
            env["YAGNA_APPKEY"] = s;
            return this;
        }

        public EnvironmentBuilder WithYaNetBindUrl(string s)
        {
            env["YA_NET_BIND_URL"] = s;
            return this;
        }

        public EnvironmentBuilder WithExeUnitPath(string s)
        {
            env["EXE_UNIT_PATH"] = s;
            return this;
        }

        public EnvironmentBuilder WithDataDir(string s)
        {
            env["DATA_DIR"] = s;
            return this;
        }

        public EnvironmentBuilder WithYagnaDataDir(string s)
        {
            env["YAGNA_DATADIR"] = s;
            return this;
        }

        public EnvironmentBuilder WithYaNetRelayHost(string s)
        {
            env["YA_NET_RELAY_HOST"] = s;
            return this;
        }

        public EnvironmentBuilder WithPrivateKey(string s)
        {
            env["YAGNA_AUTOCONF_ID_SECRET"] = s;
            return this;
        }

        public EnvironmentBuilder WithAppKey(string s)
        {
            env["YAGNA_AUTOCONF_APPKEY"] = s;
            return this;
        }

        public EnvironmentBuilder WithSslCertFile(string s)
        {
            env["SSL_CERT_FILE"] = s;
            return this;
        }

        public Dictionary<string, string> Build()
        {
            foreach (var kvp in DefaultEnv)
            {
                if (!env.ContainsKey(kvp.Key) && Environment.GetEnvironmentVariable(kvp.Key) == null)
                {
                    env.Add(kvp.Key, kvp.Value);
                }
            }
            return env;
        }
    }
}
