using System.Reflection;

using Golem.Tools;

using GolemLib;
using GolemLib.Types;

using Microsoft.Extensions.Logging;

namespace Golem.Tests
{
    [Collection(nameof(SerialTestCollection))]
    public class StartStopProcessTests : IDisposable, IClassFixture<GolemFixture>
    {
        private readonly TestLoggerProvider _loggerProvider;
        private readonly string _golemLib;
        private readonly ITestOutputHelper output;


        public StartStopProcessTests(ITestOutputHelper outputHelper, GolemFixture golemFixture)
        {
            var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;

            _golemLib = Path.Combine(dir, "Golem.dll");

            XunitContext.Register(outputHelper);
            output = outputHelper;

            _loggerProvider = new TestLoggerProvider(golemFixture.Sink);
        }

        ILoggerFactory CreateLoggerFactory(string testName)
        {
            var logfile = Path.Combine(PackageBuilder.TestDir(testName), testName + "-{Date}.log");
            return LoggerFactory.Create(builder => builder
                            .AddSimpleConsole(options => options.SingleLine = true)
                            .AddFile(logfile)
                            .AddProvider(_loggerProvider));
        }
        public void Dispose()
        {
            XunitContext.Flush();
        }

        [Fact]
        public async Task StartStop_VerifyStatusAsync()
        {
            var testName = nameof(StartStop_VerifyStatusAsync);
            var loggerFactory = CreateLoggerFactory(testName);

            string golemPath = await PackageBuilder.BuildTestDirectory(testName);
            output.WriteLine("Path: " + golemPath);

            var golem = new Golem(PackageBuilder.BinariesDir(golemPath), PackageBuilder.DataDir(golemPath), loggerFactory);
            GolemStatus status = GolemStatus.Off;

            Action<GolemStatus> updateStatus = (v) =>
            {
                status = v;
            };

            golem.PropertyChanged += new PropertyChangedHandler<Golem, GolemStatus>(nameof(IGolem.Status), updateStatus, loggerFactory).Subscribe();

            var startTask = golem.Start();
            Assert.Equal(GolemStatus.Starting, status);
            await startTask;
            Assert.Equal(GolemStatus.Ready, status);

            var stopTask = golem.Stop();
            Assert.Equal(GolemStatus.Stopping, status);
            await stopTask;

            Assert.Equal(GolemStatus.Off, status);
        }

        [Fact]
        public async Task LoadBinaryStartAndStop_VerifyStatusAsync()
        {
            var testName = nameof(LoadBinaryStartAndStop_VerifyStatusAsync);
            var loggerFactory = CreateLoggerFactory(testName);

            string golemPath = await PackageBuilder.BuildTestDirectory(testName);
            Console.WriteLine("Path: " + golemPath);

            var golem = await TestUtils.LoadBinaryLib(_golemLib, PackageBuilder.ModulesDir(golemPath), loggerFactory);
            GolemStatus status = GolemStatus.Off;

            Action<GolemStatus> updateStatus = (v) =>
            {
                status = v;
            };

            golem.PropertyChanged += new PropertyChangedHandler<Golem, GolemStatus>(nameof(IGolem.Status), updateStatus, loggerFactory).Subscribe();

            var startTask = golem.Start();
            Assert.Equal(GolemStatus.Starting, status);
            await startTask;

            Assert.Equal(GolemStatus.Ready, status);
            var stopTask = golem.Stop();

            Assert.Equal(GolemStatus.Stopping, status);
            await stopTask;

            Assert.Equal(GolemStatus.Off, status);
        }

        [Fact]
        public async Task StartAndStopWithoutWaiting_VerifyStatusAsync()
        {
            var testName = nameof(StartAndStopWithoutWaiting_VerifyStatusAsync);
            var loggerFactory = CreateLoggerFactory(testName);

            string golemPath = await PackageBuilder.BuildTestDirectory(testName);
            output.WriteLine("Path: " + golemPath);

            var golem = new Golem(PackageBuilder.BinariesDir(golemPath), PackageBuilder.ModulesDir(golemPath), loggerFactory);
            GolemStatus status = GolemStatus.Off;

            Action<GolemStatus> updateStatus = (v) =>
            {
                status = v;
            };

            golem.PropertyChanged += new PropertyChangedHandler<Golem, GolemStatus>(nameof(IGolem.Status), updateStatus, loggerFactory).Subscribe();

            var startTask = golem.Start();
            await golem.Stop();

            Assert.Equal(GolemStatus.Off, status);
        }
    }
}
