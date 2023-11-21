
namespace Golem.IntegrationTests.Tools
{
    public class GolemRelay : GolemRunnable
    {
        const string CURRENT_RELAY_VERSION = "pre-rel-v0.2.3-rc10";

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
            if (Directory.Exists(old_dir))
            {
                Console.WriteLine("Reusing existing relay directory: ", old_dir);
                return old_dir;
            }
            var dir = PackageBuilder.InitTestDirectory(String.Format("{0}_relay", test_name));

            Directory.CreateDirectory(PackageBuilder.BinariesDir(dir));

            var artifact = "ya-relay-server";
            var repo = "pwalski/ya-relay";
            var tag = CURRENT_RELAY_VERSION;

            await DownloadBinaryArtifact(PackageBuilder.BinariesDir(dir), artifact, tag, repo);
            PackageBuilder.SetPermissions(dir);

            return dir;
        }
    }
}
