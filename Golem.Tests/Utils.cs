using System.ComponentModel;
using System.Reflection;
using System.Threading.Channels;
using System.Diagnostics;
using System.Runtime.CompilerServices;

using Golem.Tools;
using GolemLib;
using GolemLib.Types;

using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;


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
            var factory = obj as IFactory ?? throw new Exception("Cast to IFactory failed.");
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
        public async static Task WaitForFile(String path, int timeoutSec = 15)
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
        public async static Task<T> ReadChannel<T>(ChannelReader<T> channel, Func<T, bool>? matcher = null, double timeoutMs = 10_000, ILogger? logger = null)
        {
            var cancelTokenSource = new CancellationTokenSource();
            cancelTokenSource.CancelAfter(TimeSpan.FromMilliseconds(timeoutMs));
            static bool FalseMatcher(T x) => false;
            matcher ??= FalseMatcher;
            while (await channel.WaitToReadAsync(cancelTokenSource.Token))
            {
                if (channel.TryRead(out var value) && value is T tValue && !matcher.Invoke(tValue))
                {
                    return tValue;
                }
                else
                {
                    logger?.LogInformation($"Skipping element: {value}");
                }
            }

            throw new Exception($"Failed to find matching {nameof(T)} within {timeoutMs} ms.");
        }
    }

    public class JobsTestBase : IDisposable, IAsyncLifetime, IClassFixture<GolemFixture>
    {
        protected readonly ILoggerFactory _loggerFactory;
        protected readonly ILogger _logger;
        protected GolemRelay? _relay;
        protected GolemRequestor? _requestor;
        protected AppKey? _requestorAppKey;
        protected String _testClassName;


        public JobsTestBase(ITestOutputHelper outputHelper, GolemFixture golemFixture, string testClassName)
        {
            XunitContext.Register(outputHelper);
            _testClassName = testClassName;
            // Log file directly in `tests` directory (like `tests/Jobtests-20231231.log )
            var logfile = Path.Combine(PackageBuilder.TestDir(""), testClassName + "-{Date}.log");
            var loggerProvider = new TestLoggerProvider(golemFixture.Sink);
            _loggerFactory = LoggerFactory.Create(builder => builder
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

        /// <summary>
        /// Reads from `channel` and returns first `T` for which `matcher` returns `false`
        /// </summary>
        /// <exception cref="Exception">Thrown when reading channel exceeds in total `timeoutMs`</exception>
        public async Task<T> ReadChannel<T>(ChannelReader<T> channel, Func<T, bool>? matcher = null, double timeoutMs = 10_000)
        {
            return await TestUtils.ReadChannel(channel, matcher, timeoutMs, _logger);
        }

        public Channel<T?> PropertyChangeChannel<OBJ, T>(OBJ? obj, string propName, Action<T?>? extraHandler = null) where OBJ : INotifyPropertyChanged
        {
            return TestUtils.PropertyChangeChannel(obj, propName, _loggerFactory, extraHandler);
        }

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
