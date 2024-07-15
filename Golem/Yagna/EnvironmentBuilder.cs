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

        private readonly Dictionary<string, string> _env = new Dictionary<string, string>();


        public string this[string key]
        {
            get => _env[key];
            set => _env[key] = value;
        }

        public EnvironmentBuilder WithGsbUrl(string s)
        {
            _env["GSB_URL"] = s;
            return this;
        }

        public EnvironmentBuilder WithMetricsGroup(string s)
        {
            _env["YAGNA_METRICS_GROUP"] = s;
            return this;
        }

        public EnvironmentBuilder WithYagnaApiUrl(string s)
        {
            _env["YAGNA_API_URL"] = s;
            return this;
        }

        public EnvironmentBuilder WithYagnaAppKey(string s)
        {
            _env["YAGNA_APPKEY"] = s;
            return this;
        }

        public EnvironmentBuilder WithYaNetBindUrl(string s)
        {
            _env["YA_NET_BIND_URL"] = s;
            return this;
        }

        public EnvironmentBuilder WithExeUnitPath(string s)
        {
            _env["EXE_UNIT_PATH"] = s;
            return this;
        }

        public EnvironmentBuilder WithDataDir(string s)
        {
            _env["DATA_DIR"] = s;
            return this;
        }

        public EnvironmentBuilder WithYagnaDataDir(string s)
        {
            _env["YAGNA_DATADIR"] = s;
            return this;
        }

        public EnvironmentBuilder WithYaNetRelayHost(string s)
        {
            _env["YA_NET_RELAY_HOST"] = s;
            return this;
        }

        public EnvironmentBuilder WithPrivateKey(string s)
        {
            _env["YAGNA_AUTOCONF_ID_SECRET"] = s;
            return this;
        }

        public EnvironmentBuilder WithAppKey(string s)
        {
            _env["YAGNA_AUTOCONF_APPKEY"] = s;
            return this;
        }

        public EnvironmentBuilder WithSslCertFile(string s)
        {
            _env["SSL_CERT_FILE"] = s;
            return this;
        }

        public Dictionary<string, string> Build()
        {
            foreach (var kvp in DefaultEnv)
            {
                if (!_env.ContainsKey(kvp.Key) && Environment.GetEnvironmentVariable(kvp.Key) == null)
                {
                    _env.Add(kvp.Key, kvp.Value);
                }
            }
            return _env;
        }
    }
}
