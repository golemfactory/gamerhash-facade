using System.ComponentModel;
using System.Reflection;
using System.Threading.Channels;

using Golem.Tools;
using GolemLib;
using GolemLib.Types;

using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Net.Sockets;
using Golem.Yagna;
using Golem.Yagna.Types;


namespace Golem.Tests
{
    public class TestUtils
    {
        public async static Task<IGolem> LoadBinaryLib(string dllPath, string modulesDir, ILoggerFactory loggerFactory, string? dataDir = null)
        {
            DisableMetricsReporting();

            const string factoryType = "Golem.Factory";

            Assembly ass = Assembly.LoadFrom(dllPath);
            Type? t = ass.GetType(factoryType) ?? throw new Exception("Factory Type not found. Lib not loaded: " + dllPath);
            var obj = Activator.CreateInstance(t) ?? throw new Exception("Creating Factory instance failed. Lib not loaded: " + dllPath);
            var factory = obj as Factory ?? throw new Exception("Cast to IFactory failed.");
            return await factory.Create(modulesDir, loggerFactory, false, dataDir, RelayType.Devnet);
        }

        public async static Task<IGolem> Golem(string golemPath, ILoggerFactory loggerFactory, string? dataDir = null, RelayType relay = RelayType.Devnet)
        {
            DisableMetricsReporting();

            var modulesDir = PackageBuilder.ModulesDir(golemPath);
            return await new Factory().Create(modulesDir, loggerFactory, false, dataDir, relay);
        }

        public static void DisableMetricsReporting()
        {
            // We don't want to report CI Providers to Grafana under the same label as production apps.
            Environment.SetEnvironmentVariable("YAGNA_METRICS_GROUP", "CI-GamerHash");
        }

        /// <summary>
        /// Waits for file up to `timeoutSec`. Throws Exception on timeout.
        /// </summary>
        public async static Task WaitForFile(string path, int timeoutSec = 15)
        {
            int i = 0;
            while (!File.Exists(path) && i < timeoutSec)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                i++;
            }
            if (i == timeoutSec)
                throw new Exception($"File {path} was not created");
        }

        /// <summary>
        /// Waits for file up to `timeoutSec` and reads it as a text file. Throws Exception on timeout.
        /// </summary>
        public async static Task<String> WaitForFileAndRead(String path, int timeoutSec = 15)
        {
            await WaitForFile(path, timeoutSec);
            return File.ReadAllText(path);
        }

        /// <summary>
        /// Waits for creation of Provider pid file
        /// </summary>
        public static async Task WaitForProviderPidFile(string golemPath, int timeoutSec = 15)
        {
            var providerPidFile = Path.Combine(golemPath, "modules", "golem-data", "provider", "ya-provider.pid");
            await WaitForFile(providerPidFile, timeoutSec);
        }

        /// <summary>
        /// Creates channel of updated properties.
        /// `extraHandler` is invoked each time property arrives.
        /// </summary>
        public static Channel<T?> PropertyChangeChannel<OBJ, T>(OBJ? obj, string propName, ILoggerFactory loggerFactory, Action<T?>? extraHandler = null) where OBJ : INotifyPropertyChanged
        {
            var eventChannel = Channel.CreateUnbounded<T?>();
            Action<T?> emitEvent = async (v) =>
            {
                extraHandler?.Invoke(v);
                await eventChannel.Writer.WriteAsync(v);
            };

            if (obj != null)
                obj.PropertyChanged += new PropertyChangedHandler<OBJ, T>(propName, emitEvent, loggerFactory).Subscribe();

            return eventChannel;
        }

        /// <summary>
        /// Reads from `channel` and returns first `T` for which `matcher` returns `false`
        /// </summary>
        /// <exception cref="Exception">Thrown when reading channel exceeds in total `timeoutMs`</exception>
        public async static Task<T> ReadChannel<T>(ChannelReader<T> channel, Func<T, bool>? filter = null, TimeSpan? timeout = null, ILogger? logger = null)
        {
            var timeout_ = timeout ?? TimeSpan.FromSeconds(10);

            var cancelTokenSource = new CancellationTokenSource();
            cancelTokenSource.CancelAfter(timeout_);

            static bool FalseMatcher(T x) => false;
            filter ??= FalseMatcher;

            try
            {
                while (await channel.WaitToReadAsync(cancelTokenSource.Token))
                {
                    if (channel.TryRead(out var value))
                    {
                        if (!filter.Invoke(value))
                            return value;
                        logger?.LogInformation($"Skipping element: {value}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw new Exception($"Failed to find expected value of type {nameof(T)} within {timeout_} ms.");
            }
            throw new Exception($"`AwaitValue` for {nameof(T)} returned unexpectedly.");
        }

        /// <summary>
        /// Reads channel until it will find `expected` value or throws exception after timeout.
        /// </summary>
        /// <exception cref="Exception"></exception>
        public async static Task<T> AwaitValue<T>(ChannelReader<T> channel, T expected, TimeSpan? timeout = null, ILogger? logger = null)
        {
            bool MatchesExpected(T x)
            {
                // If `expected` is null, than Equals won't work correctly, so we need to
                // check it separately.
                return expected == null ? x == null : x != null && x.Equals(expected);
            }

            return await ReadChannel<T>(channel, (T x) => !MatchesExpected(x), timeout, logger);
        }

        public static Channel<GolemStatus> StatusChannel(Golem golem, ILoggerFactory loggerFactory) =>
            PropertyChangeChannel<Golem, GolemStatus>(golem, nameof(IGolem.Status), loggerFactory);

        public static Channel<Job?> JobChannel(Golem golem, ILoggerFactory loggerFactory) =>
            PropertyChangeChannel<Golem, Job?>(golem, nameof(IGolem.CurrentJob), loggerFactory);

        public static Channel<JobStatus> JobStatusChannel(Job job, ILoggerFactory loggerFactory) =>
            PropertyChangeChannel<Job, JobStatus>(job, nameof(job.Status), loggerFactory);

        public static Channel<GolemLib.Types.PaymentStatus?> JobPaymentStatusChannel(Job job, ILoggerFactory loggerFactory) =>
            PropertyChangeChannel<Job, GolemLib.Types.PaymentStatus?>(job, nameof(job.PaymentStatus), loggerFactory);

        public static Channel<GolemUsage?> JobUsageChannel(Job job, ILoggerFactory loggerFactory) =>
            PropertyChangeChannel<Job, GolemUsage?>(job, nameof(job.CurrentUsage), loggerFactory);

        public static Channel<List<Payment>?> JobPaymentConfirmationChannel(Job job, ILoggerFactory loggerFactory) =>
            PropertyChangeChannel<Job, List<Payment>?>(job, nameof(job.PaymentConfirmation), loggerFactory);

        public static void CheckPortIsAvailable()
        {
            if (!IsPortAvailable(EnvironmentBuilder.DefaultLocalAddress, EnvironmentBuilder.DefaultApiPort))
            {
                throw new Exception("API port is open");
            }
        }

        public static bool IsPortAvailable(
            string host,
            int port,
            int retries = 30,
            int retry_delay = 1000,
            int connection_timeout = 250
        )
        {
            for (int retries_count = 0; retries_count < retries; retries_count++)
            {
                try
                {
                    using var client = new TcpClient();
                    IAsyncResult connection = client.BeginConnect(host, port, null, null);
                    if (!connection.AsyncWaitHandle.WaitOne(connection_timeout))
                        return true;
                    client.EndConnect(connection);
                }
                catch (Exception)
                {
                    return true;
                }
                Thread.Sleep(retry_delay);
            }
            return false;
        }
    }

    public class WithAvailablePort
    {
        public WithAvailablePort(ITestOutputHelper outputHelper)
        {
            outputHelper.WriteLine("Checking if port is available");
            TestUtils.CheckPortIsAvailable();
        }
    }

    public class JobsTestBase : WithAvailablePort, IDisposable, IAsyncLifetime, IClassFixture<GolemFixture>
    {
        protected readonly ILoggerFactory _loggerFactory;
        protected readonly ILogger _logger;
        protected GolemRelay? _relay;
        protected GolemRequestor? _requestor;
        protected AppKey? _requestorAppKey;
        protected String _testClassName;


        public JobsTestBase(ITestOutputHelper outputHelper, GolemFixture golemFixture, string testClassName) : base(outputHelper)
        {
            TestUtils.CheckPortIsAvailable();
            XunitContext.Register(outputHelper);
            _testClassName = testClassName;
            // Log file directly in `tests` directory (like `tests/Jobtests-20231231.log )
            var logfile = Path.Combine(PackageBuilder.TestDir(""), testClassName + "-{Date}.log");
            var loggerProvider = new TestLoggerProvider(golemFixture.Sink);
            _loggerFactory = LoggerFactory.Create(builder => builder
                .AddFilter("Golem", LogLevel.Debug)
                //// Console logger makes `dotnet test` hang on Windows
                // .AddSimpleConsole(options => options.SingleLine = true)
                .AddFile(logfile)
                .AddProvider(loggerProvider)
            );
            _logger = _loggerFactory.CreateLogger(testClassName);
        }

        public async Task InitializeAsync()
        {
            var testDir = PackageBuilder.TestDir($"{_testClassName}_relay");
            _relay = await GolemRelay.Build(testDir, _loggerFactory.CreateLogger("Relay"));
            Assert.True(_relay.Start());
            NetConfig.SetEnv(RelayType.Local);
            System.Environment.SetEnvironmentVariable("RUST_LOG", "debug");

            _requestor = await GolemRequestor.Build(_testClassName, _loggerFactory.CreateLogger("Requestor"));
            Assert.True(_requestor.Start());
            _requestor.InitPayment();
            _requestorAppKey = _requestor.getTestAppKey();
        }

        public async Task StartGolem(IGolem golem, ChannelReader<GolemStatus> statusChannel)
        {
            _logger.LogInformation("Starting Golem");
            var startTask = golem.Start();
            Assert.Equal(GolemStatus.Starting, await ReadChannel(statusChannel));
            await startTask;
            Assert.Equal(GolemStatus.Ready, await ReadChannel(statusChannel));
        }

        public async Task StopGolem(IGolem golem, String golemPath, ChannelReader<GolemStatus> statusChannel)
        {
            _logger.LogInformation("Stopping Golem");
            var stopTask = golem.Stop();
            Assert.Equal(GolemStatus.Stopping, await ReadChannel(statusChannel));
            await stopTask;
            Assert.Equal(GolemStatus.Off, await ReadChannel(statusChannel));
            var providerPidFile = Path.Combine(golemPath, "modules/golem-data/provider/ya-provider.pid");
            try
            {
                File.Delete(providerPidFile);
            }
            catch { }
        }

        /// <summary>
        /// Reads from `channel` and returns first `T` for which `filter` returns `false`
        /// </summary>
        /// <exception cref="Exception">Thrown when reading channel exceeds in total `timeoutMs`</exception>
        public async Task<T> ReadChannel<T>(ChannelReader<T> channel, Func<T, bool>? filter = null, TimeSpan? timeout = null)
        {
            var timeout_ = timeout ?? TimeSpan.FromSeconds(30);
            return await TestUtils.ReadChannel(channel, filter, timeout_, _logger);
        }

        public async Task<T> AwaitValue<T>(ChannelReader<T> channel, T expected, TimeSpan? timeout = null)
        {
            return await TestUtils.AwaitValue(channel, expected, timeout, _logger);
        }

        public Channel<T?> PropertyChangeChannel<OBJ, T>(OBJ? obj, string propName, Action<T?>? extraHandler = null) where OBJ : INotifyPropertyChanged
        {
            return TestUtils.PropertyChangeChannel(obj, propName, _loggerFactory, extraHandler);
        }

        public Channel<GolemStatus> StatusChannel(Golem golem) =>
            TestUtils.StatusChannel(golem, _loggerFactory);

        public Channel<Job?> JobChannel(Golem golem) =>
            TestUtils.JobChannel(golem, _loggerFactory);

        public Channel<JobStatus> JobStatusChannel(Job job) =>
            TestUtils.JobStatusChannel(job, _loggerFactory);

        public Channel<GolemLib.Types.PaymentStatus?> JobPaymentStatusChannel(Job job) =>
            TestUtils.JobPaymentStatusChannel(job, _loggerFactory);

        public Channel<GolemUsage?> JobUsageChannel(Job job) =>
            TestUtils.JobUsageChannel(job, _loggerFactory);

        public Channel<List<Payment>?> JobPaymentConfirmationChannel(Job job) =>
            TestUtils.JobPaymentConfirmationChannel(job, _loggerFactory);

        public async Task DisposeAsync()
        {
            if (_requestor != null)
                await _requestor.Stop(StopMethod.SigInt);

            if (_relay != null)
                await _relay.Stop(StopMethod.SigInt);
        }

        public void Dispose()
        {
            XunitContext.Flush();
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class OsFactAttribute : FactAttribute
    {
        public OsFactAttribute(params string[] platforms)
        {
            try
            {
                if (!Array.Exists(platforms, platform => RuntimeInformation.IsOSPlatform(OSPlatform.Create(platform))))
                    Skip = $"Unsupported OS. Supported platforms: '{platforms}'";
            }
            catch { }
        }
    }


}
