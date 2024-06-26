using System.Diagnostics;
using Golem.Yagna.Types;
using System.Text.Json;
using Golem.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using GolemLib.Types;

namespace Golem.Yagna
{
    public class YagnaStartupOptions
    {
        public string AppKey { get; set; } = "";

        public string? PrivateKey { get; set; }

        public bool Debug { get; set; }

        public string YagnaApiUrl { get; set; } = "";
        public Network Network { get; set; } = Factory.Network(true);
        public PaymentDriver PaymentDriver { get; set; } = PaymentDriver.ERC20;
    }

    public class YagnaService
    {
        public readonly YagnaStartupOptions Options;
        private readonly string _yaExePath;
        private readonly string? _dataDir;
        public Process? YagnaProcess { get; private set; }
        private SemaphoreSlim ProcLock { get; } = new SemaphoreSlim(1, 1);

        public readonly YagnaApi Api;
        private readonly ILogger _logger;
        private readonly EventsPublisher _events;

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

        public YagnaService(string golemPath, string dataDir, YagnaStartupOptions startupOptions, EventsPublisher events, ILoggerFactory loggerFactory)
        {
            Options = startupOptions;
            Api = new YagnaApi(loggerFactory, events);

            _logger = loggerFactory.CreateLogger<YagnaService>();
            _events = events;
            _yaExePath = Path.GetFullPath(Path.Combine(golemPath, ProcessFactory.BinName("yagna")));
            _dataDir = Path.GetFullPath(dataDir);
            if (!File.Exists(_yaExePath))
            {
                throw new Exception($"File not found: {_yaExePath}");
            }
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

        public async Task Run(Func<int, string, Task> exitHandler, CancellationToken cancellationToken)
        {
            Api.Authorize(Options.AppKey);
            cancellationToken.Register(Api.CancelPendingRequests);

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

                Api.Authorize(Options.AppKey);

                string debugFlag = "";
                if (Options.Debug)
                {
                    debugFlag = "--debug";
                }

                EnvironmentBuilder environment = Env;
                environment = Options.YagnaApiUrl != null ? environment.WithYagnaApiUrl(Options.YagnaApiUrl) : environment;
                environment = Options.PrivateKey != null ? environment.WithPrivateKey(Options.PrivateKey) : environment;
                environment = Options.AppKey != null ? environment.WithAppKey(Options.AppKey) : environment;
                environment = environment.WithMetricsGroup("GamerHash");

                var args = $"service run {debugFlag}".Split();

                YagnaProcess = await Task.Run(() => ProcessFactory.StartProcess(_yaExePath, args, environment.Build()));
                ChildProcessTracker.AddProcess(YagnaProcess);

                _ = YagnaProcess.WaitForExitAsync()
                    .ContinueWith(async result =>
                    {
                        _events.Raise(new ApplicationEventArgs("Yagna", $"Process exited: {YagnaProcess?.HasExited ?? true}, handle is {(YagnaProcess == null ? "" : "not ")}null", ApplicationEventArgs.SeverityLevel.Error, null));
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

        public async Task<string?> WaitForIdentityAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Waiting for yagna to start... Checking /me endpoint.");

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
                    MeInfo meInfo = await Api.Me(cancellationToken);

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

        /// TODO: Reconsider API of this function.
        public Task StartActivityLoop(CancellationToken token, IJobs jobs, EventsPublisher events)
        {
            return new ActivityLoop(Api, jobs, token, events, _logger).Start();
        }

        /// TODO: Reconsider API of this function.
        public Task StartInvoiceEventsLoop(CancellationToken token, IJobs jobs, EventsPublisher events)
        {
            return Task.Run(async () => await new InvoiceEventsLoop(Api, jobs, token, events, _logger).Start());
        }

        internal IEnumerable<string> LogFiles()
        {
            if (!Directory.Exists(_dataDir))
            {
                return new List<string>();
            }
            var logFiles = Directory.GetFiles(_dataDir, "yagna_*.log");
            var logGzFiles = Directory.GetFiles(_dataDir, "yagna_*.log.gz");
            return logFiles.Concat(logGzFiles);
        }
    }
}
