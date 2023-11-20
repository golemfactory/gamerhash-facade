
using System.Diagnostics;
using System.Runtime.Serialization.Json;
using System.Text;

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
                { "YA_NET_RELAY_HOST", "127.0.0.1:17464" },
                { "YAGNA_DATADIR", Path.Combine(dir, "modules", "golem-data", "yagna") }
            };
        }

        public async static Task<GolemRequestor> Build(string test_name)
        {
            var dir = await PackageBuilder.BuildRequestorDirectory(test_name);
            return new GolemRequestor(dir);
        }

        public override bool Start()
        {
            return StartProcess("yagna", "service run", _env);
        }

        public void InitAccount()
        {
            var app_key_process = CreateProcess("yagna", String.Format("app-key create {0}", AppKeyName), _env);
            app_key_process.Start();
            app_key_process.WaitForExit();

            var payment_fund_process = CreateProcess("yagna", "payment fund", _env);
            try
            {
                payment_fund_process.Start();
                payment_fund_process.WaitForExit();
            }
            catch (Exception e)
            {
                Console.WriteLine("Payment fund process error: {}", e);
            }

            var env = _env.ToDictionary(entry => entry.Key, entry => entry.Value);
            env.Add("RUST_LOG", "none");
            var app_key_list_process = CreateProcess("yagna", "app-key list --json", env);
            var app_key_list_output = new StringBuilder();
            app_key_list_process.OutputDataReceived += new DataReceivedEventHandler(
                delegate (object sender, DataReceivedEventArgs e)
                {
                    app_key_list_output.Append(e.Data);
                }
            );
            app_key_list_process.Start();
            app_key_process.WaitForExit();
            var app_key_list_output_json = app_key_list_output.ToString();
            var objects = JArray.Parse(app_key_list_output_json);
            foreach (JObject root in objects)
            {
                if (root.ContainsKey(AppKeyName))
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
        string key;

        string id;

        public GolemAppKey(string key, string id)
        {
            this.key = key;
            this.id = id;
        }
    }


}
