
namespace Golem.IntegrationTests.Tools
{
    public class GolemRequestor : GolemRunnable
    {

        private GolemRequestor(string dir) : base(dir)
        {
        }

        public async static Task<GolemRequestor> Build(string test_name)
        {
            var dir = await PackageBuilder.BuildRequestorDirectory(test_name);
            return new GolemRequestor(dir);
        }

        public override bool Start()
        {
            var env = new Dictionary<string, string>
            {
                { "YA_NET_RELAY_HOST", "127.0.0.1:17464" },
                { "YAGNA_DATADIR", Path.Combine(_dir, "modules", "golem-data", "yagna") }
            };
            return StartProcess("yagna", "service run", env);
        }

        // public string InitAccount()
        // {

        // }
    }
}
