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

    public class Provider
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
                        builder = builder.WithDataDir(_dataDir);

                    _env = builder.Build();
                }
                return _env;
            }
        }

        private readonly ILogger _logger;


        private static Process? ProviderProcess { get; set; }

        public Provider(string golemPath, string? dataDir, ILoggerFactory? loggerFactory = null)
        {
            loggerFactory = loggerFactory == null ? NullLoggerFactory.Instance : loggerFactory;
            _logger = loggerFactory.CreateLogger<Provider>();
            _yaProviderPath = Path.Combine(golemPath, ProcessFactory.BinName("ya-provider"));
            _pluginsPath = Path.Combine(golemPath, "..", "plugins");
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

        internal T? Exec<T>(string arguments) where T : class
        {
            var text = this.ExecToText(arguments);
            var options = new JsonSerializerOptionsBuilder()
                .WithJsonNamingPolicy(JsonNamingPolicy.CamelCase)
                .Build();
            return JsonSerializer.Deserialize<T>(text, options);
        }

        internal string ExecToText(string arguments)
        {
            _logger?.LogInformation("Executing: provider {0}", arguments);

            var process = ProcessFactory.CreateProcess(_yaProviderPath, arguments, false, Env);
            try
            {
                return ExecToText(process);
            }
            catch (IOException e)
            {
                _logger?.LogError(e, "failed to execute {0}", arguments);
                throw e;
            }
        }
        internal string ExecToText(List<string> arguments)
        {
            _logger?.LogInformation($"Executing: provider {string.Join(", ", arguments)}");
            var process = ProcessFactory.CreateProcess(_yaProviderPath, arguments, false, Env);
            try
            {
                return ExecToText(process);
            }
            catch (IOException e)
            {
                _logger?.LogError(e, "failed to execute {0}", arguments);
                throw e;
            }
        }

        private string ExecToText(Process process)
        {
            process.Start();
            var err = process.StandardError.ReadToEnd();
            var result = process.StandardOutput.ReadToEnd();
            _logger?.LogInformation("Execution result:\nstdout: {0}\nstderr: {1}", result, err);
            return result;
        }

        public List<ExeUnitDesc> ExeUnitList()
        {
            return this.Exec<List<ExeUnitDesc>>("--json exe-unit list") ?? new List<ExeUnitDesc>();
        }

        public Config? Config
        {
            get
            {
                return this.Exec<Config>("config get --json");
            }
            set
            {
                if (value != null)
                {
                    StringBuilder cmd = new StringBuilder("--json config set", 60);
                    if (value.Subnet != null)
                    {
                        cmd.Append(" --subnet \"");
                        cmd.Append(value.Subnet);
                        cmd.Append("\"");
                    }
                    if (value.NodeName != null)
                    {
                        cmd.Append(" --node-name \"");
                        cmd.Append(value.NodeName);
                        cmd.Append("\"");
                    }
                    if (value.Account != null)
                    {
                        cmd.Append(" --account \"").Append(value.Account).Append('"');
                    }
                    var _none = this.ExecToText(cmd.ToString());
                }
            }
        }

        public Profile? DefaultProfile
        {
            get
            {
                var profiles = Exec<Dictionary<string, Profile>>("--json profile list");
                return profiles?["default"];
            }
        }
        public void UpdateDefaultProfile(String param, String value)
        {
            this.ExecToText("profile update " + param + " " + value + " default");
        }

        public bool Run(string appKey, Network network, string? yagnaApiUrl, Action<int> exitHandler, CancellationToken cancellationToken, bool openConsole = false, bool enableDebugLogs = false)
        {
            string debugSwitch = "";
            if (enableDebugLogs)
            {
                debugSwitch = "--debug ";
            }
            var arguments = $"run {debugSwitch}--payment-network {network.Id}";

            var process = ProcessFactory.CreateProcess(_yaProviderPath, arguments, openConsole, Env);

            process.StartInfo.EnvironmentVariables["MIN_AGREEMENT_EXPIRATION"] = "30s";

            process.StartInfo.EnvironmentVariables["YAGNA_APPKEY"] = appKey;

            if (process.Start())
            {
                if (!openConsole)
                {
                    BindOutputEventHandlers(process);
                }

                process
                    .WaitForExitAsync(cancellationToken)
                    .ContinueWith(task =>
                    {
                        if (task.Status == TaskStatus.RanToCompletion && process.HasExited)
                        {
                            var exitCode = process.ExitCode;
                            _logger.LogInformation("Yagna process finished: {0}, exit code {1}", task.Status, exitCode);
                            exitHandler(exitCode);
                        }
                    });

                ChildProcessTracker.AddProcess(process);
                ProviderProcess = process;
                return !ProviderProcess.HasExited;
            }
            ProviderProcess = null;
            return false;
        }

        public async Task Stop()
        {
            if (ProviderProcess == null || ProviderProcess.HasExited)
                return;

            ProviderProcess.Kill(true);
            await ProviderProcess.WaitForExitAsync();
            ProviderProcess = null;
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
