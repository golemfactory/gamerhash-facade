using Microsoft.Extensions.Logging;

namespace Golem.Tools
{
    public enum RelayType
    {
        // yacn2.dev.golem.network:7477
        Public,
        // yacn2a.dev.golem.network:7477
        Devnet,
        // 127.0.0.1:16464
        Local,
    }

    public class GolemRelay : GolemRunnable
    {
        const string CURRENT_RELAY_VERSION = "pre-rel-v0.3.1-windows_rc1";

        private GolemRelay(string dir, ILogger logger) : base(dir, logger)
        {
        }

        public async static Task<GolemRelay> Build(string testDir, ILogger logger)
        {
            var dir = await BuildRelayDir(testDir);
            return new GolemRelay(dir, logger);
        }

        public override bool Start()
        {
            var working_dir = Path.Combine(_dir, "modules", "golem-data", "relay");
            Directory.CreateDirectory(working_dir);
            return StartProcess("ya-relay-server", working_dir, "-a 127.0.0.1:16464", new Dictionary<string, string>());
        }

        protected static async Task<string> BuildRelayDir(string test_dir)
        {
            var dir = PackageBuilder.PrepareTestDirectory(test_dir, true);

            Directory.CreateDirectory(PackageBuilder.BinariesDir(dir));

            var artifact = "ya-relay-server";
            var repo = "pwalski/ya-relay";
            var tag = CURRENT_RELAY_VERSION;

            var file = await DownloadBinaryArtifact(artifact, tag, repo);
            var binaries_dir = PackageBuilder.BinariesDir(dir);
            var relay_server_bin = Path.Combine(binaries_dir, Path.GetFileName(file));
            File.Copy(file, relay_server_bin);
            PackageBuilder.SetFilePermissions(relay_server_bin);

            return dir;
        }

        public static void SetEnv(RelayType relay)
        {
            if (relay == RelayType.Devnet)
                Environment.SetEnvironmentVariable("YA_NET_RELAY_HOST", "yacn2a.dev.golem.network:7477");
            else if (relay == RelayType.Public)
                // Environment.SetEnvironmentVariable("YA_NET_RELAY_HOST", "yacn2a.dev.golem.network:7477");
                Environment.SetEnvironmentVariable("YA_NET_RELAY_HOST", "18.184.73.24:7464");
            else if (relay == RelayType.Local)
            {
                Environment.SetEnvironmentVariable("YA_NET_RELAY_HOST", "127.0.0.1:16464");
                Environment.SetEnvironmentVariable("MEAN_CYCLIC_BCAST_INTERVAL", "3s");
            }
        }
    }
}
