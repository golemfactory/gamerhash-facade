using System.Diagnostics;
using Golem.Yagna.Types;
using System.Text.Json;
using Golem.Tools;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Net.Http.Headers;

namespace Golem.Yagna
{
    public class YagnaStartupOptions
    {
        public string AppKey { get; set; } = "";

        public string? PrivateKey { get; set; }

        public bool Debug { get; set; }

        public string YagnaApiUrl { get; set; } = "";
        public Network Network { get; set; } = Network.Holesky;
        public PaymentDriver PaymentDriver { get; set; } = PaymentDriver.ERC20;
    }

    public class YagnaService
    {
        private readonly string _yaExePath;
        private readonly string? _dataDir;
        private static Process? YagnaProcess { get; set; }
        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;

        private EnvironmentBuilder Env
        {
            get
            {
                var certs = Path.Combine(Path.GetDirectoryName(_yaExePath) ?? "", "cacert.pem");
                var env = new EnvironmentBuilder();
                env = File.Exists(certs) ? env.WithSslCertFile(certs) : env;
                env = _dataDir != null ? env.WithYagnaDataDir(_dataDir) : env;
                return env;
            }
        }

        public YagnaService(string golemPath, string dataDir, ILoggerFactory? loggerFactory)
        {
            loggerFactory = loggerFactory == null ? NullLoggerFactory.Instance : loggerFactory;
            _logger = loggerFactory.CreateLogger<YagnaService>();
            _yaExePath = Path.GetFullPath(Path.Combine(golemPath, ProcessFactory.BinName("yagna")));
            _dataDir = Path.GetFullPath(dataDir);
            if (!File.Exists(_yaExePath))
            {
                throw new Exception($"File not found: {_yaExePath}");
            }
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(YagnaOptionsFactory.DefaultYagnaApiUrl)
            };
        }

        internal string ExecToText(params string[] arguments)
        {
            return new ProcessFactory(_yaExePath, _logger).WithEnv(Env.Build()).ExecToText(arguments);
        }

        internal async Task<string> ExecToTextAsync(params string[] arguments)
        {
            return await Task.Run(() => Task.FromResult(ExecToText(arguments)));
        }

        internal T? Exec<T>(params string[] arguments) where T : class
        {
            return new ProcessFactory(_yaExePath, _logger).WithEnv(Env.Build()).Exec<T>(arguments);
        }

        internal async Task<T?> ExecAsync<T>(params string[] arguments) where T : class
        {
            // TODO: This should be funciton in ProcessFactory.
            var text = await ExecToTextAsync(arguments);
            var options = new JsonSerializerOptionsBuilder()
                .WithJsonNamingPolicy(JsonNamingPolicy.CamelCase)
                .Build();
            return JsonSerializer.Deserialize<T>(text, options);
        }

        internal YagnaStartupOptions StartupOptions()
        {
            return YagnaOptionsFactory.CreateStartupOptions();
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
                if (YagnaProcess == null)
                {
                    return null;
                }
                try
                {
                    return Exec<Result<IdInfo>>("--json", "id", "show")?.Ok;
                }
                catch (Exception e)
                {
                    _logger.LogError($"Failed to get Node Id. Err {e}");
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
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.AppKey);

            if (YagnaProcess != null || cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            string debugFlag = "";
            if (options.Debug)
            {
                debugFlag = "--debug";
            }

            EnvironmentBuilder environment = Env;
            environment = options.YagnaApiUrl != null ? environment.WithYagnaApiUrl(options.YagnaApiUrl) : environment;
            environment = options.PrivateKey != null ? environment.WithPrivateKey(options.PrivateKey) : environment;
            environment = options.AppKey != null ? environment.WithAppKey(options.AppKey) : environment;

            var args = $"service run {debugFlag}".Split();
            var process = ProcessFactory.StartProcess(_yaExePath, args, environment.Build());

            YagnaProcess = process;

            process.WaitForExitAsync(cancellationToken)
                .ContinueWith(result =>
                {
                    if(YagnaProcess != null)
                    {
                        var exitCode = YagnaProcess?.ExitCode ?? throw new GolemException("Unable to get Yagna process exit code");
                        YagnaProcess = null;
                        exitHandler(exitCode);
                    }
                });

            ChildProcessTracker.AddProcess(process);
           

            cancellationToken.Register(async () =>
            {
                _logger.LogInformation("Canceling Yagna process");
                await Stop();
            });

            return !YagnaProcess.HasExited;
        }

        public async Task Stop(int stopTimeoutMs = 30_000)
        {
            if (YagnaProcess == null)
                return;
            if (YagnaProcess.HasExited)
            {
                YagnaProcess = null;
                return;
            }
            _logger.LogInformation("Stopping Yagna process");
            var cmd = YagnaProcess;
            await ProcessFactory.StopProcess(cmd, stopTimeoutMs, _logger);
            YagnaProcess = null;
        }

        public async Task<MeInfo?> Me(CancellationToken cancellationToken)
        {
            var response = _httpClient.GetAsync($"/me", cancellationToken).Result;
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                throw new Exception("Unauthorized call to yagna daemon - is another instance of yagna running?");
            }
            var txt = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptionsBuilder()
                            .WithJsonNamingPolicy(JsonNamingPolicy.CamelCase)
                            .Build();

            return JsonSerializer.Deserialize<MeInfo>(txt, options) ?? null;
        }

        public async Task<string?> WaitForIdentityAsync(CancellationToken cancellationToken)
        {
            string? identity = null;

            //yagna is starting and /me won't work until all services are running
            for (int tries = 0; tries < 300; ++tries)
            {
                if (cancellationToken.IsCancellationRequested)
                    return null;

                Thread.Sleep(300);

                if (HasExited) // yagna has stopped
                {
                    throw new Exception("Failed to start yagna ...");
                }

                try
                {
                    MeInfo? meInfo = await Me(cancellationToken);
                    //sanity check
                    if (meInfo != null)
                    {
                        if (String.IsNullOrEmpty(identity))
                            identity = meInfo.Identity;
                        break;
                    }
                    throw new Exception("Failed to get key");

                }
                catch (Exception)
                {
                    // consciously swallow the exception... presumably REST call error...
                }
            }
            return identity;
        }

        /// TODO: Reconsider API of this function.
        public Task StartActivityLoop(CancellationToken token, Action<Job?> setCurrentJob, IJobsUpdater jobs)
        {
            token.Register(_httpClient.CancelPendingRequests);
            return new ActivityLoop(_httpClient, token, _logger).Start(
                setCurrentJob,
                jobs,
                token
            );
        }

        /// TODO: Reconsider API of this function.
        public Task StartInvoiceEventsLoop(CancellationToken token, IJobsUpdater jobs)
        {
            token.Register(_httpClient.CancelPendingRequests);
            return new InvoiceEventsLoop(_httpClient, token, _logger).Start(jobs.UpdatePaymentStatus, jobs.UpdatePaymentConfirmation);
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
