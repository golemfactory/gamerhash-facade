using Microsoft.Extensions.Logging;

namespace Golem.Tools
{
    public class GolemCentralNet : GolemRunnable
    {
        const string CURRENT_ROUTER_VERSION = "v0.7.2";

        private GolemCentralNet(string dir, ILogger logger) : base(dir, logger)
        {
        }

        public async static Task<GolemCentralNet> Build(string testDir, ILogger logger)
        {
            var dir = await BuildCentralNetDir(testDir);
            return new GolemCentralNet(dir, logger);
        }

        public override bool Start()
        {
            var working_dir = Path.Combine(_dir, "modules", "golem-data", "central-net");
            Directory.CreateDirectory(working_dir);
            return StartProcess("ya-sb-router", working_dir, "-l tcp://127.0.0.1:6464", new Dictionary<string, string>());
        }

        protected static async Task<string> BuildCentralNetDir(string test_dir)
        {
            var dir = PackageBuilder.PrepareTestDirectory(test_dir, true);
            var binaries_dir = PackageBuilder.BinariesDir(dir);

            Directory.CreateDirectory(binaries_dir);

            var artifact = "ya-sb-router";
            var repo = "golemfactory/ya-service-bus";
            var tag = CURRENT_ROUTER_VERSION;

            await PackageBuilder.DownloadExtractPackage(binaries_dir, artifact, repo, tag);
            return dir;
        }
    }
}
