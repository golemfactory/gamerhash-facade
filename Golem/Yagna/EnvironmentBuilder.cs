namespace Golem.Yagna
{
    public class EnvironmentBuilder
    {
        private Dictionary<string, string> env = new Dictionary<string, string>
        {
            { "GSB_URL", "tcp://127.0.0.1:11501" },
            { "YAGNA_API_URL", "http://127.0.0.1:11502" },
            { "YA_PAYMENT_NETWORK_GROUP", "testnet" },
            { "YA_NET_BIND_URL", "udp://0.0.0.0:12503" },
            // { "EXE_UNIT_PATH", exeUnitPath },
            // { "DATA_DIR", dataDir },
            // { "YA_NET_RELAY_HOST", "10.0.2.2:7464" },
        };

        public EnvironmentBuilder WithGsbUrl(string s)
        {
            env["GSB_URL"] = s;
            return this;
        }

        public EnvironmentBuilder WithYagnaApiUrl(string s)
        {
            env["YAGNA_API_URL"] = s;
            return this;
        }

        public EnvironmentBuilder WithYaPaymentNetworkGroup(string s)
        {
            env["YA_PAYMENT_NETWORK_GROUP"] = s;
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
            return env;
        }
    }
}