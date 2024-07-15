
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

    public class GolemRequestor : GolemRunnable, IAsyncLifetime
    {
        private readonly EnvironmentBuilder _env;

        public string AppKey;
        public string ApiUrl { get; set; }
        public string GsbUrl { get; set; }
        public string NetBindUrl { get; set; }
        private readonly string _dataDir;

        public YagnaApi? Rest { get; internal set; }

        private readonly bool _mainnet;

        private GolemRequestor(string dir, bool mainnet, ILogger logger) : base(dir, logger)
        {
            AppKey = "";    // Will be set in `SetAppKey` function. This line supresses warning.

            ApiUrl = "http://127.0.0.1:7465";
            GsbUrl = "tcp://127.0.0.1:7464";
            NetBindUrl = "udp://0.0.0.0:11500";

            _mainnet = mainnet;
            _dataDir = Path.GetFullPath(Path.Combine(dir, "modules", "golem-data", "yagna"));

            var envBuilder = new EnvironmentBuilder();
            envBuilder.WithYagnaDataDir(_dataDir);
            envBuilder.WithYagnaApiUrl(ApiUrl);
            envBuilder.WithGsbUrl(GsbUrl);
            envBuilder.WithYaNetBindUrl(NetBindUrl);
            envBuilder.WithMetricsGroup("Example-GamerHash");
            _env = envBuilder;

            SetSecret(_mainnet ? "main_key.plain" : "test_key.plain");
            SetAppKey(GenerateRandomAppkey());
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

            var env = _env.Build();
            var result = StartProcess("yagna", working_dir, "service run", env, false);

            Rest = CreateRestAPI(AppKey);
            return result;
        }

        public void AutoSetUrls(UInt16 portBase)
        {
            var apiPort = portBase;
            var gsbPort = portBase + 1;
            var bindPort = portBase + 2;

            ApiUrl = $"http://127.0.0.1:{apiPort}";
            GsbUrl = $"tcp://127.0.0.1:{gsbPort}";
            NetBindUrl = $"udp://0.0.0.0:{bindPort}";

            _env.WithYagnaApiUrl(ApiUrl);
            _env.WithGsbUrl(GsbUrl);
            _env.WithYaNetBindUrl(NetBindUrl);
        }

        public void SetSecret(string resourceFile)
        {
            _env.WithPrivateKey(LoadSecret(resourceFile));
        }

        private static string LoadSecret(string resourceFile)
        {
            var keyReader = PackageBuilder.ReadResource(resourceFile);
            return keyReader.ReadLine() ?? throw new Exception($"Failed to read key from file {resourceFile}");
        }

        private static string GenerateRandomAppkey()
        {
            string? appKey = null;
            if ((appKey = (string?)Environment.GetEnvironmentVariable("REQUESTOR_AUTOCONF_APPKEY")) != null)
            {
                return appKey;
            }
            else
            {
                byte[] data = RandomNumberGenerator.GetBytes(20);
                return Convert.ToBase64String(data);
            }

        }

        private void SetAppKey(string appKey)
        {
            AppKey = appKey;
            _env.WithAppKey(appKey);
            _env.WithYagnaAppKey(appKey);
        }

        public SampleApp CreateSampleApp(string? extraArgs = null)
        {
            var env = _env.Build();

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

        public async Task InitPayment(double minFundThreshold = 100.0)
        {
            await WaitForIdentityAsync();

            var env = _env.Build();
            env["RUST_LOG"] = "none";

            var network = Factory.Network(_mainnet);
            var payment_status_process = WaitAndPrintOnError(RunCommand("yagna", WorkingDir(), $"payment status --json --network {network.Id}", env));
            var payment_status_output_json = String.Join("\n", payment_status_process.GetOutputAndErrorLines());
            var payment_status_output_obj = JObject.Parse(payment_status_output_json);
            var totalGlm = payment_status_output_obj.Value<float>("amount");
            var reserved = payment_status_output_obj.Value<float>("reserved");

            if (reserved > 0.0)
            {
                WaitAndPrintOnError(RunCommand("yagna", WorkingDir(), "payment release-allocations", env));
            }

            if (totalGlm < minFundThreshold && !_mainnet)
            {
                WaitAndPrintOnError(RunCommand("yagna", WorkingDir(), $"payment fund --network {network.Id}", env));
            }
            return;
        }

        public async Task<string?> WaitForIdentityAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Waiting for yagna to start... Checking /me endpoint.");

            //yagna is starting and /me won't work until all services are running
            for (int tries = 0; tries < 200; ++tries)
            {
                Thread.Sleep(300);
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var api = Rest ?? throw new Exception("REST API not initialized");
                    MeInfo meInfo = await Rest.Me(cancellationToken);

                    _logger.LogDebug("Yagna started; REST API is available.");
                    return meInfo.Identity;
                }
                catch (Exception)
                {
                    // consciously swallow the exception... presumably REST call error...
                }
            }
            return null;
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

        public AppKey GetTestAppKey()
        {
            var dataDir = _env["YAGNA_DATADIR"];
            if (!Path.Exists(dataDir) || Directory.EnumerateFiles(dataDir).Count() == 0)
            {
                throw new Exception("No data dir");
            }

            var env = _env.Build();
            env["RUST_LOG"] = "none";

            var app_key_list_process = RunCommand("yagna", WorkingDir(), "app-key list --json", env);
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

        private YagnaApi? CreateRestAPI(string appKey)
        {
            var rest = new YagnaApi(ApiUrl, _logger, new EventsPublisher());
            rest.Authorize(appKey);
            return rest;
        }

        private string WorkingDir()
        {
            var working_dir = Path.Combine(_dir, "modules", "golem-data", "yagna");
            Directory.CreateDirectory(working_dir);
            return working_dir;
        }

        public async Task InitializeAsync()
        {
            await Task.CompletedTask;
        }

        public async Task DisposeAsync()
        {
            await Stop(StopMethod.SigInt);
            Rest = null;
        }
    }
}
