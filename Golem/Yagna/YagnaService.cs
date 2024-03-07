using System.Diagnostics;
using Golem.Yagna.Types;
using System.Text.Json;
using Golem.Tools;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Net.Http.Headers;
using Golem.Model;
using System.Text;
using System.Runtime.CompilerServices;

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
        private Process? YagnaProcess { get; set; }
        private SemaphoreSlim ProcLock { get; } = new SemaphoreSlim(1, 1);

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

        public async Task Run(YagnaStartupOptions options, Func<int, string, Task> exitHandler, CancellationToken cancellationToken)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.AppKey);
            cancellationToken.Register(_httpClient.CancelPendingRequests);

            // Synchronizing of process creation is necessary to avoid scenario, when `YagnaSerice::Stop` is called
            // during `YagnaSerice::Run` execution. There is race condition possible, when `YagnaProcess`
            // is still null, but will be created in a moment and at the same time `YagnaSerice::Stop` will
            // check that `YagnaProcess` is null and will not stop the process.
            await ProcLock.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (YagnaProcess != null)
                {
                    throw new GolemException("Yagna process is already running");
                }

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.AppKey);

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

                YagnaProcess = await Task.Run(() => ProcessFactory.StartProcess(_yaExePath, args, environment.Build()));
                ChildProcessTracker.AddProcess(YagnaProcess);

                _ = YagnaProcess.WaitForExitAsync()
                    .ContinueWith(async result =>
                    {
                        // This code is not synchronized, to avoid deadlocks in case exitHandler will call any
                        // functions on YagnaService.
                        if (YagnaProcess != null && YagnaProcess.HasExited)
                        {
                            // If we can't acquire exitCode it is better to send any code to exiHandler,
                            // than to throw an exception here. Otherwise exitHandler won't be called.
                            var exitCode = YagnaProcess?.ExitCode ?? 1;
                            await exitHandler(exitCode, "Yagna");
                        }
                        YagnaProcess = null;
                    });
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                ProcLock.Release();
            }
        }

        public async Task Stop(int stopTimeoutMs = 30_000)
        {
            // There is no symmetry in synchronization of `Run` and `Stop` methods. It is possible to call
            // `YagnaService::Stop` multiple times, but `YagnaService::Run` can be called only once.
            // We can't lock here for entire duration of `StopProcess`, because we would block another `Stop`.
            //
            // Spawning process and setting YagnaProcess should be atomic operation, but stopping process
            // doesn't need to happen under the lock.
            Process proc;
            await ProcLock.WaitAsync();
            try
            {
                // Save reference to YagnaProcess under lock, because it can change before we will reach StopProcess.
                if (YagnaProcess == null)
                    return;
                proc = YagnaProcess;
            }
            finally
            {
                ProcLock.Release();
            }

            _logger.LogInformation("Stopping Yagna process");

            await ProcessFactory.StopProcess(proc, stopTimeoutMs, _logger);
            YagnaProcess = null;
        }

        public async Task<T> RestCall<T>(string path, CancellationToken token = default) where T : class
        {
            var response = _httpClient.GetAsync(path, token).Result;
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                throw new Exception("Unauthorized call to yagna daemon - is another instance of yagna running?");
            }
            var txt = await response.Content.ReadAsStringAsync(token);
            return Deserialize<T>(txt);
        }

        public async IAsyncEnumerable<T> RestStream<T>(string path, [EnumeratorCancellation] CancellationToken token = default) where T : class
        {
            var stream = await _httpClient.GetStreamAsync(path, token);
            using StreamReader reader = new StreamReader(stream);

            while (true)
            {
                T result;
                try
                {
                    result = await Next<T>(reader, "data:", token);
                }
                catch (OperationCanceledException e)
                {
                    throw e;
                }
                catch (Exception error)
                {
                    _logger.LogError("Failed to get next stream event: {0}", error);
                    break;
                }
                yield return result;
            }
            yield break;
        }

        private async Task<T> Next<T>(StreamReader reader, string dataPrefix = "data:", CancellationToken token = default) where T : class
        {
            StringBuilder messageBuilder = new StringBuilder();

            string? line;
            while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync(token)))
            {
                if (line.StartsWith(dataPrefix))
                {
                    messageBuilder.Append(line.Substring(dataPrefix.Length).TrimStart());
                    _logger.LogDebug("got line {0}", line);
                }
                else
                {
                    _logger.LogError("Unable to deserialize message: {0}", line);
                }
            }

            return Deserialize<T>(messageBuilder.ToString());
        }

        internal static T Deserialize<T>(string text) where T : class
        {
            var options = new JsonSerializerOptionsBuilder()
                            .WithJsonNamingPolicy(JsonNamingPolicy.CamelCase)
                            .Build();

            return JsonSerializer.Deserialize<T>(text, options)
                ?? throw new Exception($"Failed to deserialize REST call reponse to type: {typeof(T).Name}");
        }

        public async Task<MeInfo> Me(CancellationToken token)
        {
            return await RestCall<MeInfo>("/me", token);
        }

        public async Task<YagnaAgreement> GetAgreement(string agreementId, CancellationToken token = default)
        {
            return await RestCall<YagnaAgreement>($"/market-api/v1/agreements/{agreementId}", token);
        }

        public async Task<ActivityStatePair> GetState(string activityId, CancellationToken token = default)
        {
            return await RestCall<ActivityStatePair>($"/activity-api/v1/activity/{activityId}/state", token);
        }

        public async IAsyncEnumerable<TrackingEvent> ActivityMonitorStream([EnumeratorCancellation] CancellationToken token = default)
        {
            await foreach (var item in RestStream<TrackingEvent>($"/activity-api/v1/_monitor", token))
            {
                yield return item;
            }
        }

        public async Task<string?> WaitForIdentityAsync(CancellationToken cancellationToken = default)
        {
            //yagna is starting and /me won't work until all services are running
            for (int tries = 0; tries < 200; ++tries)
            {
                Thread.Sleep(300);
                cancellationToken.ThrowIfCancellationRequested();

                if (HasExited) // yagna has stopped
                {
                    // In case there was race condition between HasExited and cancellation.
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new Exception("Yagna failed to start when waiting for REST endpoints...");
                }

                try
                {
                    MeInfo meInfo = await Me(cancellationToken);
                    return meInfo.Identity;
                }
                catch (Exception)
                {
                    // consciously swallow the exception... presumably REST call error...
                }
            }
            return null;
        }

        /// TODO: Reconsider API of this function.
        public Task StartActivityLoop(CancellationToken token, Action<Job?> setCurrentJob, IJobsUpdater jobs)
        {
            return new ActivityLoop(this, token, _logger).Start(
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
    }
}
