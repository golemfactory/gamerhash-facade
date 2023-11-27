
namespace Golem.IntegrationTests.Tools
{
    public class GolemRelay : GolemRunnable
    {
        const string CURRENT_RELAY_VERSION = "pre-rel-v0.2.3-rc11";

        private GolemRelay(string dir) : base(dir)
        {
        }

        public async static Task<GolemRelay> Build(string test_name)
        {
            var dir = await BuildRelayDir(test_name);
            return new GolemRelay(dir);
        }

        public override bool Start()
        {
            var working_dir = Path.Combine(_dir, "modules", "golem-data", "relay");
            Directory.CreateDirectory(working_dir);
            return StartProcess("ya-relay-server", working_dir, "-a 127.0.0.1:17464", new Dictionary<string, string>());
        }

        protected static async Task<string> BuildRelayDir(string test_name)
        {
            var old_dir = PackageBuilder.TestDir(test_name);

            var dir = PackageBuilder.InitTestDirectory($"{test_name}_relay");

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
    }
}
