
using System.Diagnostics;
using System.Runtime.Serialization.Json;
using System.Text;

using App;

using Newtonsoft.Json.Linq;

namespace Golem.IntegrationTests.Tools
{
    public class GolemRequestor : GolemRunnable
    {
        private const string AppKeyName = "tests";
        private readonly Dictionary<string, string> _env;

        public GolemAppKey? AppKey;

        private GolemRequestor(string dir) : base(dir)
        {
            _env = new Dictionary<string, string>
            {
                { "YAGNA_DATADIR", Path.GetFullPath(Path.Combine(dir, "modules", "golem-data", "yagna")) },
                { "YAGNA_API_URL", "http://127.0.0.1:7465" },
                { "GSB_URL", "tcp://127.0.0.1:7464" },
            };
        }

        public async static Task<GolemRequestor> Build(string test_name)
        {
            var dir = await PackageBuilder.BuildRequestorDirectory(test_name);
            return new GolemRequestor(dir);
        }

        public override bool Start()
        {
            return StartProcess("yagna", Path.Combine(_dir, "modules", "golem-data", "yagna"), "service run", _env);
        }

        public Process CreateAppProcess()
        {
            var env = _env.ToDictionary(entry => entry.Key, entry => entry.Value);
            if (env.ContainsKey("YAGNA_APPKEY")) {
                env.Remove("YAGNA_APPKEY");
            }
            env.Add("YAGNA_APPKEY", AppKey?.Key ?? throw new Exception("Unable to create app process. No YAGNA_APPKEY."));
            if (env.ContainsKey("YAGNA_API_URL")) {
                env.Remove("YAGNA_API_URL");
            }
            env.Add("YAGNA_API_URL", "http://127.0.0.1:7465");
            var process = SampleApp.CreateProcess(env);
            process.StartInfo.WorkingDirectory = Path.Combine(_dir, "modules", "golem-data", "yagna");
            return process;
        }

        public void InitAccount()
        {
            Thread.Sleep(5000);

            var app_key_process = CreateProcess("yagna", String.Format("app-key create {0}", AppKeyName), _env);
            app_key_process.Start();
            app_key_process.WaitForExit();

            // var payment_fund_process = CreateProcess("yagna", "payment fund", _env);
            // try
            // {
            //     payment_fund_process.Start();
            //     payment_fund_process.WaitForExit();
            // }
            // catch (Exception e)
            // {
            //     Console.WriteLine("Payment fund process error: {}", e);
            // }

            var env = _env.ToDictionary(entry => entry.Key, entry => entry.Value);

            env.Add("RUST_LOG", "none");
            var app_key_list_process = CreateProcess("yagna", "app-key list --json", env, false);

            app_key_list_process.StartInfo.CreateNoWindow = true;
            app_key_list_process.StartInfo.RedirectStandardOutput = true;
            app_key_list_process.StartInfo.UseShellExecute = false;
            app_key_list_process.Start();
            var app_key_list_output_json = app_key_list_process.StandardOutput.ReadToEnd();
            app_key_list_process.WaitForExit();
            var objects = JArray.Parse(app_key_list_output_json);
            foreach (JObject root in objects)
            {
                var name = (string)root.GetValue("name");
                if (AppKeyName.Equals(name))
                {
                    var key = (string)root.GetValue("key") ?? throw new Exception("Failed to get app key");
                    var id = (string)root.GetValue("id") ?? throw new Exception("Failed to get app id");
                    AppKey = new GolemAppKey(key, id);
                    return;
                }
            }
            throw new Exception(String.Format("Failed to get {0} app key", AppKeyName));
        }
    }

    public class GolemAppKey
    {
        public readonly string Key;

        public readonly string Id;

        public GolemAppKey(string key, string id)
        {
            this.Key = key;
            this.Id = id;
        }
    }


}
