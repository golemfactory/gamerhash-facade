using System.Diagnostics;
using System.Globalization;
using Golem.Yagna.Types;
using System.Text.Json;
using Golem.Tools;
using System.Text.Json.Serialization;

namespace Golem.Yagna
{
    public class YagnaStartupOptions
    {
        public string? ForceAppKey { get; set; }

        public string? PrivateKey { get; set; }

        public bool Debug { get; set; }

        public bool OpenConsole { get; set; }
    }



    //[JsonObject(MemberSerialization.OptIn)]
    public class IdInfo
    {
        [JsonPropertyName("alias")]
        public string? Alias { get; set; }

        [JsonPropertyName("default")]
        public bool IsDefault { get; set; }

        [JsonPropertyName("locked")]
        public bool IsLocked { get; set; }

        [JsonPropertyName("nodeId")]
        public string Address { get; set; }

        public IdInfo(bool _isDefault, bool _isLocked, string? _alias, string _address)
        {
            IsDefault = _isDefault;
            IsLocked = _isLocked;
            Alias = _alias;
            Address = _address;
        }
    }

    public class YagnaService
    {
        private string _yaExePath;
        private static Process? YagnaProcess { get; set; }

        public YagnaService(string golemPath)
        {
            _yaExePath = Path.Combine(golemPath, "yagna.exe");
            if (!File.Exists(_yaExePath))
            {
                throw new Exception($"File not found: {_yaExePath}");
            }
        }

        private string EscapeArgument(string argument)
        {
            if (argument.Contains(" ") || argument.StartsWith("\""))
            {
                return $"\"{argument.Replace("\"", "\\\"")}\"";
            }
            return argument;
        }
        private Process CreateProcess(params string[] arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = this._yaExePath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = false,
                Arguments = String.Join(" ", (from arg in arguments where arg != null select EscapeArgument(arg)))
            };

            startInfo.EnvironmentVariables.Add("GSB_URL", "tcp://127.0.0.1:11501");
            startInfo.EnvironmentVariables.Add("YAGNA_API_URL", "http://127.0.0.1:11502");
            startInfo.EnvironmentVariables.Add("SUBNET", "testnet");
            startInfo.EnvironmentVariables.Add("YA_PAYMENT_NETWORK_GROUP", "testnet");
            startInfo.EnvironmentVariables.Add("YA_NET_BIND_URL", "udp://0.0.0.0:12503");

            var p = new Process
            {
                StartInfo = startInfo
            };
            p.Start();
            return p;
        }

        internal string ExecToText(params string[] arguments)
        {
            var process = CreateProcess(arguments);
            string output = process.StandardOutput.ReadToEnd();
            if (process.ExitCode != 0)
            {
                var error = process.StandardError.ReadToEnd();
                throw new Exception("Yagna call failed");
            }
            return output;
        }

        internal async Task<string> ExecToTextAsync(params string[] arguments)
        {
            var process = CreateProcess(arguments);
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

        public IdInfo? Id => Exec<Result<IdInfo>>("--json", "id", "show")?.Ok;

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

        public bool Run(YagnaStartupOptions options)
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

            var startInfo = new ProcessStartInfo
            {
                FileName = this._yaExePath,
                Arguments = $"service run {debugFlag}",
            };

            startInfo.EnvironmentVariables.Add("GSB_URL", "tcp://127.0.0.1:11501");
            startInfo.EnvironmentVariables.Add("YAGNA_API_URL", "http://127.0.0.1:11502");
            startInfo.EnvironmentVariables.Add("SUBNET", "testnet");
            startInfo.EnvironmentVariables.Add("YA_PAYMENT_NETWORK_GROUP", "testnet");
            startInfo.EnvironmentVariables.Add("YA_NET_BIND_URL", "udp://0.0.0.0:12503");

            if (options.PrivateKey != null)
            {
                startInfo.EnvironmentVariables.Add("YAGNA_AUTOCONF_ID_SECRET", options.PrivateKey);
            }

            if (options.ForceAppKey != null)
            {
                startInfo.EnvironmentVariables.Add("YAGNA_AUTOCONF_APPKEY", options.ForceAppKey);
            }

            var certs = Path.Combine(Path.GetDirectoryName(_yaExePath) ?? "", "cacert.pem");
            if (File.Exists(certs))
            {
                startInfo.EnvironmentVariables.Add("SSL_CERT_FILE", certs);
            }

            if (options.OpenConsole)
            {
                startInfo.RedirectStandardOutput = false;
                startInfo.RedirectStandardError = false;
                startInfo.UseShellExecute = false;
            }
            else
            {
                startInfo.RedirectStandardOutput = true;
                startInfo.RedirectStandardError = true;
                startInfo.CreateNoWindow = true;
                startInfo.UseShellExecute = false;
            }


            var process = new Process
            {
                StartInfo = startInfo
            };

            if(process.Start())
            {
                YagnaProcess = process;
                return !YagnaProcess.HasExited;
            }
            YagnaProcess = null;
            return false;
        }

        public async Task Stop()
        {
            if (YagnaProcess == null)
                return;

            YagnaProcess.Kill(true);
            await YagnaProcess.WaitForExitAsync();
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

    public class AppKeyService
    {
        private YagnaService _yagnaService;
        private string? _id;

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

        public List<KeyInfo> List()
        {
            var output = new List<KeyInfo>();
            int tries = 0;
            while (output.Count == 0)
            {
                try
                {
                    var o = Exec<List<KeyInfo>>("list");
                    if(o != null)
                        output.AddRange(o);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e.ToString());
                    //do nothing
                }
                Thread.Sleep(1000);
                tries++;
                if (tries == 10)
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
        YagnaService _yagna;

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
        YagnaService _yagna;

        internal PaymentService(YagnaService yagna)
        {
            _yagna = yagna;
        }

        public void Init(Network network, string driver, string account)
        {
            _yagna.Exec<PaymentStatus>("payment", "init", "--receiver", "--network", network.Id, "--driver", driver, "--account", account);
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
