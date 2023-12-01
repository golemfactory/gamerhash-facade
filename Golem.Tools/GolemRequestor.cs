
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Json;
using System.Text;

using App;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json.Linq;

namespace Golem.Tools
{
    public class GolemRequestor : GolemRunnable
    {
        private const string AppKeyName = "tests";
        private readonly Dictionary<string, string> _env;

        public GolemAppKey? AppKey;

        private GolemRequestor(string dir, ILogger logger) : base(dir, logger)
        {
            _env = new Dictionary<string, string>
            {
                { "YAGNA_DATADIR", Path.GetFullPath(Path.Combine(dir, "modules", "golem-data", "yagna")) },
                { "YAGNA_API_URL", "http://127.0.0.1:7465" },
                { "GSB_URL", "tcp://127.0.0.1:7464" },
            };
        }

        public async static Task<GolemRequestor> Build(string test_name, ILogger logger, bool cleanupData = true)
        {
            var dir = await PackageBuilder.BuildRequestorDirectory(test_name, cleanupData);
            return new GolemRequestor(dir, logger);
        }

        public async static Task<GolemRequestor> BuildRelative(string datadir, ILogger logger, bool cleanupData = true)
        {
            var dir = await PackageBuilder.BuildRequestorDirectoryRelative(datadir, cleanupData);
            return new GolemRequestor(dir, logger);
        }

        public override bool Start()
        {
            var working_dir = Path.Combine(_dir, "modules", "golem-data", "yagna");
            Directory.CreateDirectory(working_dir);
            return StartProcess("yagna", working_dir, "service run", _env);
        }

        public SampleApp CreateSampleApp()
        {
            var env = _env.ToDictionary(entry => entry.Key, entry => entry.Value);
            env["YAGNA_APPKEY"] = AppKey?.Key ?? throw new Exception("Unable to create app process. No YAGNA_APPKEY.");
            env["YAGNA_API_URL"] = "http://127.0.0.1:7465";
            var pathEnvVar = Environment.GetEnvironmentVariable("PATH") ?? "";
            var binariesDir = Path.GetFullPath(PackageBuilder.BinariesDir(_dir));
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                pathEnvVar = $"{pathEnvVar};{binariesDir}";
            }
            else
            {
                pathEnvVar = $"{pathEnvVar}:{binariesDir}";
            }
            env["PATH"] = pathEnvVar;
            return new SampleApp(_dir, env, _logger);
        }

        public void InitAccount()
        {
            Thread.Sleep(3000);

            var totalGlm = 0.0;
            AppKey = getTestAppKey();
            if (AppKey == null)
            {
                var app_key_process = RunCommand("yagna", workingDir(), $"app-key create {AppKeyName}", _env);
                app_key_process.Wait();
                AppKey = getTestAppKey();
            }
            else
            {
                var env = _env.ToDictionary(entry => entry.Key, entry => entry.Value);
                env.Add("RUST_LOG", "none");

                var payment_status_process = RunCommand("yagna", workingDir(), "payment status --json", env);
                payment_status_process.Wait();
                var payment_status_output_json = string.Join("\n", payment_status_process.GetOutputAndErrorLines());
                var payment_status_output_obj = JObject.Parse(payment_status_output_json);
                totalGlm = (float)payment_status_output_obj["amount"];
                var reserved = (float)payment_status_output_obj["reserved"];

                if (reserved > 0.0)
                {
                    var release_allocations_process = RunCommand("yagna", workingDir(), "payment release-allocations", _env);
                    release_allocations_process.Wait();
                }
            }

            if (totalGlm < 100.0)
            {
                try
                {
                    var payment_fund_process = RunCommand("yagna", workingDir(), "payment fund", _env);
                    payment_fund_process.Wait();
                }
                catch (Exception e)
                {
                    _logger.LogInformation($"Payment fund process error: {e}");
                }
            }
        }

        private GolemAppKey? getTestAppKey()
        {
            var dataDir = _env["YAGNA_DATADIR"];
            if (!Path.Exists(dataDir) || Directory.EnumerateFiles(dataDir).Count() == 0)
            {
                return null;
            }

            var env = _env.ToDictionary(entry => entry.Key, entry => entry.Value);
            env.Add("RUST_LOG", "none");

            var app_key_list_process = RunCommand("yagna", workingDir(), "app-key list --json", env);
            app_key_list_process.Wait();
            var app_key_list_output_json = string.Join("\n", app_key_list_process.GetOutputAndErrorLines());

            var objects = JArray.Parse(app_key_list_output_json);
            foreach (JObject root in objects)
            {
                var name = (string)root.GetValue("name");
                if (AppKeyName.Equals(name))
                {
                    var key = (string)root.GetValue("key") ?? throw new Exception("Failed to get app key");
                    var id = (string)root.GetValue("id") ?? throw new Exception("Failed to get app id");
                    return new GolemAppKey(key, id);
                }
            }
            return null;
        }

        private string workingDir()
        {
            var working_dir = Path.Combine(_dir, "modules", "golem-data", "yagna");
            Directory.CreateDirectory(working_dir);
            return working_dir;
        }
    }

    public class GolemAppKey
    {
        public readonly string Key;

        public readonly string Id;

        public GolemAppKey(string key, string id)
        {
            Key = key;
            Id = id;
        }
    }
}
