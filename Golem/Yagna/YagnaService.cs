﻿using System.Diagnostics;
using Golem.Yagna.Types;
using System.Text.Json;
using Golem.Tools;
using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;

namespace Golem.Yagna
{
    public class YagnaStartupOptions
    {
        public string AppKey { get; set; } = "";

        public string? PrivateKey { get; set; }

        public bool Debug { get; set; }

        public bool OpenConsole { get; set; }
        public string YagnaApiUrl { get; set; } = "";
        public Network Network { get; set; } = Network.Goerli;
    }



    //[JsonObject(MemberSerialization.OptIn)]
    public class IdInfo
    {
        // [JsonPropertyName("alias")]
        public string? Alias { get; set; }

        // [JsonPropertyName("default")]
        public bool IsDefault { get; set; }

        // [JsonPropertyName("locked")]
        public bool IsLocked { get; set; }

        // [JsonPropertyName("nodeId")]
        public string NodeId { get; set; }

        public IdInfo(bool _isDefault, bool _isLocked, string? _alias, string _nodeId)
        {
            IsDefault = _isDefault;
            IsLocked = _isLocked;
            Alias = _alias;
            NodeId = _nodeId;
        }

        [JsonConstructor]
        public IdInfo()
        {
            NodeId = "";
        }
    }

    public class YagnaService
    {
        private readonly string _yaExePath;
        private readonly string? _dataDir;
        private static Process? YagnaProcess { get; set; }
        private readonly ILogger _logger;

        private EnvironmentBuilder Env
        {
            get
            {
                var env = new EnvironmentBuilder();
                return env;
            }
        }

        public YagnaService(string golemPath, string? dataDir, ILoggerFactory? loggerFactory)
        {
            loggerFactory = loggerFactory == null ? NullLoggerFactory.Instance : loggerFactory;
            _logger = loggerFactory.CreateLogger<YagnaService>();
            _yaExePath = Path.Combine(golemPath, ProcessFactory.BinName("yagna"));
            _dataDir = dataDir;
            if (!File.Exists(_yaExePath))
            {
                throw new Exception($"File not found: {_yaExePath}");
            }
        }

        private Process CreateCommandProcessAndStart(params string[] arguments)
        {
            var process = ProcessFactory.CreateProcess(_yaExePath, arguments.ToList(), false, Env.Build());

            process.Start();
            return process;
        }

        internal string ExecToText(params string[] arguments)
        {
            var process = CreateCommandProcessAndStart(arguments);
            process.WaitForExit();
            string output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            if (process.ExitCode != 0)
            {
                throw new Exception("Yagna call failed. E: " + error);
            }
            return output;
        }

        internal async Task<string> ExecToTextAsync(params string[] arguments)
        {
            var process = CreateCommandProcessAndStart(arguments);
            return await process.StandardOutput.ReadToEndAsync();
        }

        internal T? Exec<T>(params string[] arguments) where T : class
        {
            var text = ExecToText(arguments);
            var options = new JsonSerializerOptionsBuilder()
                .WithJsonNamingPolicy(JsonNamingPolicy.CamelCase)
                .Build();
            return JsonSerializer.Deserialize<T>(text, options);
        }

        internal async Task<T?> ExecAsync<T>(params string[] arguments) where T : class
        {
            var text = await ExecToTextAsync(arguments);
            var options = new JsonSerializerOptionsBuilder()
                .WithJsonNamingPolicy(JsonNamingPolicy.CamelCase)
                .Build();
            return JsonSerializer.Deserialize<T>(text, options);
        }


        public IdService Ids
        {
            get
            {
                return new IdService(this);
            }
        }

        // public IdInfo? Id => Exec<Result<IdInfo>>("--json", "id", "show")?.Ok;

        public IdInfo? Id
        {
            get
            {
                try
                {
                    return Exec<Result<IdInfo>>("--json", "id", "show")?.Ok;
                }
                catch (System.Exception)
                {
                    return null;
                }

            }
        }

        public PaymentService PaymentService
        {
            get
            {
                return new PaymentService(this);
            }
        }

        public AppKeyService AppKeyService
        {
            get
            {
                return new AppKeyService(this, null);
            }
        }

        public bool HasExited => YagnaProcess?.HasExited ?? true;

        public bool Run(YagnaStartupOptions options, Action<int> exitHandler, CancellationToken cancellationToken)
        {
            if (YagnaProcess != null)
            {
                return false;
            }

            string debugFlag = "";
            if (options.Debug)
            {
                debugFlag = "--debug";
            }

            var certs = Path.Combine(Path.GetDirectoryName(_yaExePath) ?? "", "cacert.pem");

            EnvironmentBuilder environment = Env;
            environment = options.YagnaApiUrl != null ? environment.WithYagnaApiUrl(options.YagnaApiUrl) : environment;
            environment = options.PrivateKey != null ? environment.WithPrivateKey(options.PrivateKey) : environment;
            environment = options.AppKey != null ? environment.WithAppKey(options.AppKey) : environment;
            environment = File.Exists(certs) ? environment.WithSslCertFile(certs) : environment;
            environment = _dataDir != null ? environment.WithYagnaDataDir(_dataDir) : environment;


            var process = ProcessFactory.CreateProcess(_yaExePath, $"service run {debugFlag}", options.OpenConsole, environment.Build());

            if (process.Start())
            {
                if (!options.OpenConsole)
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
                            _logger.LogDebug("Yagna process finished: {0}, exit code {1}", task.Status, exitCode);
                            exitHandler(exitCode);
                        }
                    });
                YagnaProcess = process;

                ChildProcessTracker.AddProcess(process);
                return !YagnaProcess.HasExited;
            }
            YagnaProcess = null;
            return false;
        }

        public async Task Stop()
        {
            if (YagnaProcess == null || YagnaProcess.HasExited)
                return;

            YagnaProcess.Kill(true);
            await YagnaProcess.WaitForExitAsync();
            YagnaProcess = null;
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

    public class KeyInfo
    {
        public string Name { get; set; }

        public string? Key { get; set; }

        public string Id { get; set; }

        public string? Role { get; set; }

        public DateTime? Created { get; set; }

        [JsonConstructor]
        public KeyInfo()
        {
            Name = "";
            Id = "";
        }
    }

    public class MeInfo
    {
        public string? Name { get; set; }
        public string? Identity { get; set; }

        public string? Role { get; set; }

        [JsonConstructor]
        public MeInfo()
        {
        }
    }

    public class AppKeyService
    {
        private readonly YagnaService _yagnaService;
        private readonly string? _id;

        internal AppKeyService(YagnaService yagnaService, string? id)
        {
            this._yagnaService = yagnaService;
            this._id = id;
        }

        private string[] prepareArgs(params string[] arguments)
        {
            var execArgs = new List<string>(3 + arguments.Length);
            execArgs.Add("--json");
            if (_id != null)
            {
                execArgs.Add("--id");
                execArgs.Add(_id);
            }
            execArgs.Add("app-key");
            execArgs.AddRange(arguments);

            return execArgs.ToArray();
        }

        private T? Exec<T>(params string[] arguments) where T : class
        {
            return _yagnaService.Exec<T>(prepareArgs(arguments));
        }

        private async Task<T?> ExecAsync<T>(params string[] arguments) where T : class
        {
            return await _yagnaService.ExecAsync<T>(prepareArgs(arguments));
        }

        public string? Create(string name)
        {
            return Exec<string>("create", name);
        }
        public async Task<string?> CreateAsync(string name)
        {
            return await ExecAsync<string>("create", name);
        }

        public void Drop(string name)
        {
            var opt = Exec<string>("drop", name);
        }

        public KeyInfo? Get(string name)
        {
            var keys = List();
            if (keys is not null && keys.Count > 0)
            {
                if (keys.Count > 1)
                {
                    return keys.Where(x => x.Name.Equals(name)).FirstOrDefault();
                }
                else if (keys.Count == 1)
                {
                    return keys.First();
                }
            }
            return null;
        }

        public List<KeyInfo> List()
        {
            var output = new List<KeyInfo>();
            int tries = 0;
            while (output.Count == 0)
            {
                List<KeyInfo>? o;
                try
                {
                    o = Exec<List<KeyInfo>>("list");
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e.ToString());
                    o = null;
                }
                if (o != null)
                    output.AddRange(o);
                else
                    Thread.Sleep(1000);
                if (++tries == 10)
                {
                    throw new Exception("Failed to obtain key list from yagna service");
                }
            }
            return output;
        }

        public async Task<List<KeyInfo>> ListAsync()
        {
            var output = await ExecAsync<List<KeyInfo>>("list");
            return output ?? new List<KeyInfo>();
        }


    }

    public class IdService
    {
        readonly YagnaService _yagna;

        internal IdService(YagnaService yagna)
        {
            _yagna = yagna;
        }

        public List<IdInfo> List()
        {
            var table = _yagna.Exec<List<IdInfo>>("--json", "id", "list");
            return table ?? new List<IdInfo>();
        }
    }


    public class PaymentService
    {
        readonly YagnaService _yagna;

        internal PaymentService(YagnaService yagna)
        {
            _yagna = yagna;
        }

        public void Init(Network network, string driver, string account)
        {
            _yagna.ExecToText("payment", "init", "--receiver", "--network", network.Id, "--driver", driver, "--account", account);
        }

        public void Init(Network network, string account)
        {
            _yagna.ExecToText("payment", "init", "--receiver", "--network", network.Id, "--account", account);
        }

        public async Task<PaymentStatus?> Status(Network network, string driver, string account)
        {
            return await _yagna.ExecAsync<PaymentStatus>("--json", "payment", "status", "--network", network.Id, "--driver", driver, "--account", account);
        }

        public async Task<string?> ExitTo(Network network, string driver, string account, string? destination)
        {
            if (destination == null)
            {
                return null;
            }
            return await _yagna.ExecToTextAsync("--json", "payment", "exit", "--network", network.Id, "--driver", driver, "--account", account, "--to-address", destination);
        }

        public async Task<ActivityStatus?> ActivityStatus()
        {
            return await _yagna.ExecAsync<ActivityStatus>("--json", "activity", "status");
        }

    }
}
