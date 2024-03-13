
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;

using App;

using Golem.Yagna;

using Medallion.Shell;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json.Linq;

namespace Golem.Tools
{

    public class AppKey
    {
        public string? Key;
        public string? Id;
    };

    public class GolemRequestor : GolemRunnable
    {
        private readonly Dictionary<string, string> _env;

        public string? AppKey;

        private readonly bool _mainnet;

        private GolemRequestor(string dir, bool mainnet, ILogger logger) : base(dir, logger)
        {
            var envBuilder = new EnvironmentBuilder();
            envBuilder.WithYagnaDataDir(Path.GetFullPath(Path.Combine(dir, "modules", "golem-data", "yagna")));
            envBuilder.WithYagnaApiUrl("http://127.0.0.1:7465");
            envBuilder.WithGsbUrl("tcp://127.0.0.1:7464");
            envBuilder.WithYaNetBindUrl("udp://0.0.0.0:11500");
            _env = envBuilder.Build();
            _mainnet = mainnet;
        }

        public async static Task<GolemRequestor> Build(string test_name, ILogger logger, bool cleanupData = true, bool mainnet = false)
        {
            var dir = await PackageBuilder.BuildRequestorDirectory(test_name, cleanupData);
            return new GolemRequestor(dir, mainnet, logger);
        }

        public async static Task<GolemRequestor> BuildRelative(string datadir, ILogger logger, bool cleanupData = true, bool mainnet = false)
        {
            var dir = await PackageBuilder.BuildRequestorDirectoryRelative(datadir, cleanupData);
            return new GolemRequestor(dir, mainnet, logger);
        }

        public override bool Start()
        {
            var working_dir = Path.Combine(_dir, "modules", "golem-data", "yagna");
            Directory.CreateDirectory(working_dir);
            AppKey = generateRandomAppkey();
            var env = _env.ToDictionary(entry => entry.Key, entry => entry.Value);
            env["YAGNA_AUTOCONF_ID_SECRET"] = getRequestorAutoconfIdSecret();
            env["YAGNA_AUTOCONF_APPKEY"] = AppKey;
            return StartProcess("yagna", working_dir, "service run", env, false);
        }

        private string getRequestorAutoconfIdSecret()
        {
            string? key = null;
            if ((key = (string?)Environment.GetEnvironmentVariable("REQUESTOR_AUTOCONF_ID_SECRET")) != null)
            {
                return key;
            }
            var keyFilename = _mainnet ? "main_key.plain" : "test_key.plain";
            var keyReader = PackageBuilder.ReadResource(keyFilename);
            return keyReader.ReadLine() ?? throw new Exception($"Failed to read key from file {keyFilename}");
        }

        private string generateRandomAppkey()
        {
            string? appKey = null;
            if ((appKey = (string?)Environment.GetEnvironmentVariable("REQUESTOR_AUTOCONF_APPKEY")) != null)
            {
                return appKey;
            }
            byte[] data = RandomNumberGenerator.GetBytes(20);
            return Convert.ToBase64String(data);
        }

        public SampleApp CreateSampleApp(string? extraArgs = null)
        {
            var env = _env.ToDictionary(entry => entry.Key, entry => entry.Value);
            env["YAGNA_APPKEY"] = AppKey ?? throw new Exception("Unable to create app process. No YAGNA_APPKEY.");
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
            var network = Factory.Network(_mainnet);
            return new SampleApp(_dir, env, network, _logger, extraArgs);
        }

        public void InitPayment(double minFundThreshold = 100.0)
        {
            Thread.Sleep(6000);
            var env = _env.ToDictionary(entry => entry.Key, entry => entry.Value);
            env.Add("RUST_LOG", "none");

            var network = Factory.Network(_mainnet);
            var payment_status_process = WaitAndPrintOnError(RunCommand("yagna", workingDir(), $"payment status --json --network {network.Id}", env));
            var payment_status_output_json = String.Join("\n", payment_status_process.GetOutputAndErrorLines());
            var payment_status_output_obj = JObject.Parse(payment_status_output_json);
            var totalGlm = payment_status_output_obj.Value<float>("amount");
            var reserved = payment_status_output_obj.Value<float>("reserved");

            if (reserved > 0.0)
            {
                WaitAndPrintOnError(RunCommand("yagna", workingDir(), "payment release-allocations", _env));
            }

            if (totalGlm < minFundThreshold && !_mainnet)
            {
                WaitAndPrintOnError(RunCommand("yagna", workingDir(), $"payment fund --network {network.Id}", _env));
            }
            return;
        }

        private Command WaitAndPrintOnError(Command cmd)
        {
            try
            {
                cmd.Wait();
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Failed to run cmd: {cmd}");
                _logger.LogError(String.Join("\n", cmd.GetOutputAndErrorLines()));
                throw;
            }
            return cmd;
        }

        public AppKey getTestAppKey()
        {
            var dataDir = _env["YAGNA_DATADIR"];
            if (!Path.Exists(dataDir) || Directory.EnumerateFiles(dataDir).Count() == 0)
            {
                throw new Exception("No data dir");
            }

            var env = _env.ToDictionary(entry => entry.Key, entry => entry.Value);
            env.Add("RUST_LOG", "none");

            var app_key_list_process = RunCommand("yagna", workingDir(), "app-key list --json", env);
            app_key_list_process.Wait();
            var app_key_list_output_json = string.Join("\n", app_key_list_process.GetOutputAndErrorLines());

            var objects = JArray.Parse(app_key_list_output_json);
            foreach (JObject root in objects)
            {
                var name = root.Value<string>("name");
                if ("autoconfigured".Equals(name))
                {
                    var key = root.Value<string>("key") ?? throw new Exception("Failed to get app key");
                    var id = root.Value<string>("id") ?? throw new Exception("Failed to get app id");
                    return new AppKey() { Id = id, Key = key };
                }
            }
            return new AppKey();
        }

        private string workingDir()
        {
            var working_dir = Path.Combine(_dir, "modules", "golem-data", "yagna");
            Directory.CreateDirectory(working_dir);
            return working_dir;
        }
    }
}
