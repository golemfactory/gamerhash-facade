using System.Text;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Golem.Yagna.Types;
using System.Text.Json.Serialization;
using Golem.Tools;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using GolemLib.Types;

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
        T? Exec<T>(IEnumerable<object> args) where T : class;
        string ExecToText(IEnumerable<object> args);
    }

    public class Provider : IProvider
    {
        public PresetConfigService PresetConfig { get; set; }
        public Rule Blacklist { get; set; }
        public Rule AllowList { get; set; }

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
        private readonly EventsPublisher _events;


        public Process? ProviderProcess { get; private set; }
        private SemaphoreSlim ProcLock { get; } = new SemaphoreSlim(1, 1);

        public Provider(string golemPath, string? dataDir, EventsPublisher events, ILoggerFactory loggerFactory)
        {
            golemPath = Path.GetFullPath(golemPath);

            _logger = loggerFactory.CreateLogger<Provider>();
            _events = events;
            _yaProviderPath = Path.Combine(golemPath, ProcessFactory.BinName("ya-provider"));
            _pluginsPath = Path.Combine(golemPath, "..", "plugins");
            _pluginsPath = Path.GetFullPath(_pluginsPath);
            _exeUnitsPath = Path.Combine(_pluginsPath, @"ya-*.json");
            _dataDir = dataDir;
            _env = new Dictionary<string, string>();

            PresetConfig = new PresetConfigService(this);
            Blacklist = new Rule(this, RuleCategory.Blacklist);
            AllowList = new Rule(this, RuleCategory.AllowList);

            if (!File.Exists(_yaProviderPath))
            {
                throw new Exception($"File not found: {_yaProviderPath}");
            }
            if (!Directory.Exists(_pluginsPath))
            {
                throw new Exception($"Plugins directory not found: {_pluginsPath}");
            }

        }

        public T? Exec<T>(IEnumerable<object> args) where T : class
        {
            return new ProcessFactory(_yaProviderPath, _logger).WithEnv(Env).Exec<T>(args);
        }

        public string ExecToText(IEnumerable<object> args)
        {
            return new ProcessFactory(_yaProviderPath, _logger).WithEnv(Env).ExecToText(args);
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

        public bool HasExited => ProviderProcess?.HasExited ?? true;

        public void UpdateDefaultProfile(String param, String value)
        {
            ExecToText($"profile update {param} {value} default".Split());
        }

        public async Task Run(string appKey, Network network, Func<int, string, Task> exitHandler, CancellationToken cancellationToken, bool enableDebugLogs = false)
        {
            await ProcLock.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (ProviderProcess != null)
                {
                    throw new GolemException("Provider process is already running");
                }

                string debugSwitch = "";
                if (enableDebugLogs)
                {
                    debugSwitch = "--debug";
                }
                var arguments = $"run {debugSwitch} --payment-network {network.Id}".Split();

                var env = new Dictionary<string, string>(Env);
                env["MIN_AGREEMENT_EXPIRATION"] = "30s";
                env["YAGNA_APPKEY"] = appKey;
                env["RUST_LOG"] = "debug,ya_client=info";

                ProviderProcess = await Task.Run(() => ProcessFactory.StartProcess(_yaProviderPath, arguments, env));
                ChildProcessTracker.AddProcess(ProviderProcess);

                _ = ProviderProcess.WaitForExitAsync()
                    .ContinueWith(async result =>
                {
                    _events.Raise(new ApplicationEventArgs("Provider", $"Process exited: {ProviderProcess.HasExited}, handle is {(ProviderProcess == null ? "" : "not ")}null", ApplicationEventArgs.SeverityLevel.Error, null));
                    if (ProviderProcess != null && ProviderProcess.HasExited)
                    {
                        var exitCode = ProviderProcess?.ExitCode ?? 1;
                        await exitHandler(exitCode, "Provider");
                    }
                    ProviderProcess = null;
                });
            }
            finally
            {
                ProcLock.Release();
            }
        }

        public async Task Stop(int stopTimeoutMs = 30_000)
        {
            Process proc;
            await ProcLock.WaitAsync();
            try
            {
                if (ProviderProcess == null)
                    return;
                proc = ProviderProcess;
            }
            finally
            {
                ProcLock.Release();
            }

            _logger.LogInformation("Stopping Provider process");
            await ProcessFactory.StopProcess(proc, stopTimeoutMs, _logger);
            ProviderProcess = null;
        }

        internal IEnumerable<string> LogFiles()
        {
            if (!Directory.Exists(_dataDir))
            {
                return new List<string>();
            }
            var logFiles = Directory.GetFiles(_dataDir, "ya-provider_*.log");
            var logGzFiles = Directory.GetFiles(_dataDir, "ya-provider_*.log.gz");
            var workDir = Path.Combine(_dataDir, "exe-unit", "work");
            if (!Path.Exists(workDir))
            {
                return logFiles.Concat(logGzFiles);
            }
            var runtimeLogFiles = Directory.GetFiles(workDir, "*.log", SearchOption.AllDirectories);
            var runtimeLogGzFiles = Directory.GetFiles(workDir, "*.log.gz", SearchOption.AllDirectories);
            return logFiles.Concat(logGzFiles).Concat(runtimeLogFiles).Concat(runtimeLogGzFiles);
        }
    }
}
