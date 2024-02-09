using System.Text;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Golem.Yagna.Types;
using System.Text.Json.Serialization;
using Golem.Tools;
using System.Text.Json;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Medallion.Shell;

namespace Golem.Yagna
{
    public class ExeUnitDesc
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("version")]
        public string? Version { get; set; }

        [JsonPropertyName("supervisor-path")]
        public string? SupervisiorPath { get; set; }

        [JsonPropertyName("runtime-path")]
        public string? RuntimePath { get; set; }

        [JsonPropertyName("extra-args")]
        public List<string>? ExtraArgs { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("properties")]
        public object? Properties { get; set; }
    }

    public class Config
    {
        [JsonPropertyName("node_name")]
        public string? NodeName { get; set; }

        [JsonPropertyName("subnet")]
        public string? Subnet { get; set; }

        [JsonPropertyName("account")]
        public string? Account { get; set; }
    }


    public class Profile
    {
        [JsonConstructor]
        public Profile(int cpuThreads, double memGib, double storageGib)
        {

            CpuThreads = cpuThreads;
            MemGib = memGib;
            StorageGib = storageGib;
        }

        [JsonPropertyName("cpu_threads")]
        public int CpuThreads { get; set; }

        [JsonPropertyName("mem_gib")]
        public double MemGib { get; set; }

        [JsonPropertyName("storage_gib")]
        public double StorageGib { get; set; }
    }

    // public class TW

    public interface IProvider
    {
        T? Exec<T>(IEnumerable<object>? args) where T : class;
        string ExecToText(IEnumerable<object>? args);
    }

    public class Provider : IProvider
    {
        public PresetConfigService PresetConfig { get; set; }

        private readonly string _yaProviderPath;
        private readonly string _pluginsPath;
        private readonly string _exeUnitsPath;
        private readonly string? _dataDir;

        private Dictionary<string, string> _env;
        private Dictionary<string, string> Env
        {
            get
            {
                if (_env.Count == 0)
                {
                    var builder = new EnvironmentBuilder()
                                        .WithExeUnitPath(_exeUnitsPath);

                    if (_dataDir != null)
                        builder = builder.WithDataDir(Path.GetFullPath(_dataDir));

                    _env = builder.Build();
                }
                return _env;
            }
        }

        private readonly ILogger _logger;


        private static Command? ProviderProcess { get; set; }

        public Provider(string golemPath, string? dataDir, ILoggerFactory? loggerFactory = null)
        {
            golemPath = Path.GetFullPath(golemPath);
            loggerFactory = loggerFactory == null ? NullLoggerFactory.Instance : loggerFactory;
            _logger = loggerFactory.CreateLogger<Provider>();
            _yaProviderPath = Path.Combine(golemPath, ProcessFactory.BinName("ya-provider"));
            _pluginsPath = Path.Combine(golemPath, "..", "plugins");
            _pluginsPath = Path.GetFullPath(_pluginsPath);
            _exeUnitsPath = Path.Combine(_pluginsPath, @"ya-*.json");
            _dataDir = dataDir;
            _env = new Dictionary<string, string>();

            PresetConfig = new PresetConfigService(this);

            if (!File.Exists(_yaProviderPath))
            {
                throw new Exception($"File not found: {_yaProviderPath}");
            }
            if (!Directory.Exists(_pluginsPath))
            {
                throw new Exception($"Plugins directory not found: {_pluginsPath}");
            }

        }

        public T? Exec<T>(IEnumerable<object>? args) where T : class
        {
            var text = ExecToText(args);
            var options = new JsonSerializerOptionsBuilder()
                .WithJsonNamingPolicy(JsonNamingPolicy.CamelCase)
                .Build();
            return JsonSerializer.Deserialize<T>(text, options);
        }

        public string ExecToText(IEnumerable<object>? args)
        {
            var strWriter = new StringWriter();
            var outLogger = new OutputLogger(_logger, "Provider");
            var cmd = ProcessFactory.CreateProcess(_yaProviderPath, args, Env, strWriter, outLogger);
            try
            {
                cmd.Wait();
                return strWriter.ToString();
            }
            catch (Exception e)
            {
                _logger?.LogError(e, "failed to execute {0}", args);
                throw new GolemProcessException(string.Format("Failed to execute Provider command: {0}", e.Message));
            }
        }

        public List<ExeUnitDesc> ExeUnitList()
        {
            return Exec<List<ExeUnitDesc>>("--json exe-unit list".Split()) ?? new List<ExeUnitDesc>();
        }

        public Config? Config
        {
            get
            {
                return Exec<Config>("config get --json".Split());
            }
            set
            {
                if (value != null)
                {
                    List<string> cmd = "--json config set".Split().ToList();
                    if (value.Subnet != null)
                    {
                        cmd.Add("--subnet");
                        cmd.Add(value.Subnet);
                    }
                    if (value.NodeName != null)
                    {
                        cmd.Add("--node-name");
                        cmd.Add(value.NodeName);
                    }
                    if (value.Account != null)
                    {
                        cmd.Add("--account");
                        cmd.Add(value.Account);
                    }
                    ExecToText(cmd);
                }
            }
        }

        public Profile? DefaultProfile
        {
            get
            {
                var profiles = Exec<Dictionary<string, Profile>>($"--json profile list".Split());
                return profiles?["default"];
            }
        }
        public void UpdateDefaultProfile(String param, String value)
        {
            ExecToText($"profile update {param} {value} default".Split());
        }

        public bool Run(string appKey, Network network, Action<int> exitHandler, CancellationToken cancellationToken, bool enableDebugLogs = false)
        {
            if (cancellationToken.IsCancellationRequested)
                return false;

            string debugSwitch = "";
            if (enableDebugLogs)
            {
                debugSwitch = "--debug";
            }
            var arguments = $"run {debugSwitch} --payment-network {network.Id}".Split();

            var env = new Dictionary<string, string>(Env);
            env["MIN_AGREEMENT_EXPIRATION"] = "30s";
            env["YAGNA_APPKEY"] = appKey;

            var outLogger = new OutputLogger(_logger, "Provider");

            var cmd = ProcessFactory.CreateProcess(_yaProviderPath, arguments, env, outLogger, outLogger);

            cmd.Task.ContinueWith(result =>
            {
                UpdateStatus();
                if (result.IsFaulted)
                {
                    var res = result.Result;
                    _logger.LogInformation("Provider process cmd has failed.");
                    exitHandler(1);
                    return;
                }
                _logger.LogInformation("Provider process finished: exit code {1}", result.Result.ExitCode);
                exitHandler(result.Result.ExitCode);
            });

            ChildProcessTracker.AddProcess(cmd);

            ProviderProcess = cmd;

            cancellationToken.Register(async () =>
            {
                _logger.LogInformation("Canceling Provider process");
                await Stop();
            });

            return UpdateStatus();
        }

        public async Task Stop()
        {
            if (!UpdateStatus())
            {
                return;
            }
            _logger.LogInformation("Stopping Provider process");
            var cmd = ProviderProcess;
            await ProcessFactory.StopCmd(cmd, logger: _logger);
            UpdateStatus();
        }

        /// <summary>
        /// Check and update Provider process status.
        /// </summary>
        /// <returns>`True` if Proider is alive. `False` if it is not.</returns>
        public bool UpdateStatus()
        {
            if (ProviderProcess == null)
                return false;
            if (ProviderProcess.Process.HasExited)
            {
                ProviderProcess = null;
                return false;
            }
            return true;
        }

        private void BindOutputEventHandlers(Process proc)
        {
            proc.OutputDataReceived += OnOutputDataRecv;
            proc.ErrorDataReceived += OnErrorDataRecv;
            proc.BeginErrorReadLine();
            proc.BeginOutputReadLine();
        }

        private void OnOutputDataRecv(object sender, DataReceivedEventArgs e)
        {
            _logger.LogInformation($"{e.Data}");
        }
        private void OnErrorDataRecv(object sender, DataReceivedEventArgs e)
        {
            _logger.LogInformation($"{e.Data}");
        }
    }
}
