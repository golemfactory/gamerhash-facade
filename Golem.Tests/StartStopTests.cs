using System.Reflection;
using System.Runtime.CompilerServices;

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

        ILoggerFactory CreateLoggerFactory([CallerMemberName] string testName = "test")
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
            var loggerFactory = CreateLoggerFactory();
            string golemPath = await PackageBuilder.BuildTestDirectory();
            output.WriteLine("Path: " + golemPath);

            var golem =  await TestUtils.Golem(golemPath, loggerFactory);

            var status = new PropertyChangedHandler<Golem, GolemStatus>(nameof(IGolem.Status), loggerFactory).Observe(golem);

            var startTask = golem.Start();
            Assert.Equal(GolemStatus.Starting, status.Value);
            await startTask;
            Assert.Equal(GolemStatus.Ready, status.Value);

            var stopTask = golem.Stop();
            Assert.Equal(GolemStatus.Stopping, status.Value);
            await stopTask;

            Assert.Equal(GolemStatus.Off, status.Value);
        }

        [Fact]
        public async Task LoadBinaryStartAndStop_VerifyStatusAsync()
        {
            var loggerFactory = CreateLoggerFactory();
            string golemPath = await PackageBuilder.BuildTestDirectory();
            Console.WriteLine("Path: " + golemPath);

            var golem = await TestUtils.LoadBinaryLib(_golemLib, PackageBuilder.ModulesDir(golemPath), loggerFactory);

            var status = new PropertyChangedHandler<Golem, GolemStatus>(nameof(IGolem.Status), loggerFactory).Observe(golem);

            var startTask = golem.Start();
            Assert.Equal(GolemStatus.Starting, status.Value);
            await startTask;

            Assert.Equal(GolemStatus.Ready, status.Value);
            var stopTask = golem.Stop();

            Assert.Equal(GolemStatus.Stopping, status.Value);
            await stopTask;

            Assert.Equal(GolemStatus.Off, status.Value);
        }

        [Fact]
        public async Task StartAndStopWithoutWaiting_VerifyStatusAsync()
        {
            var loggerFactory = CreateLoggerFactory();
            string golemPath = await PackageBuilder.BuildTestDirectory();
            output.WriteLine("Path: " + golemPath);

            var golem = await TestUtils.Golem(golemPath, loggerFactory);

            var status = new PropertyChangedHandler<Golem, GolemStatus>(nameof(IGolem.Status), loggerFactory).Observe(golem);

            var startTask = golem.Start();
            await golem.Stop();

            Assert.Equal(GolemStatus.Off, status.Value);
        }

        [Fact]
        public async Task StartStopLoop_SingleStop()
        {
            var loggerFactory = CreateLoggerFactory();
            string golemPath = await PackageBuilder.BuildTestDirectory();
            output.WriteLine("Path: " + golemPath);

            var golem = await TestUtils.Golem(golemPath, loggerFactory);

            var status = new PropertyChangedHandler<Golem, GolemStatus>(nameof(IGolem.Status), loggerFactory).Observe(golem);

            for (int i = 0; i < 5; i++)
            {
                var startTask = golem.Start();
                Assert.Equal(GolemStatus.Starting, status.Value);
                await startTask;
                Assert.Equal(GolemStatus.Ready, status.Value);

                await Task.Delay(TimeSpan.FromMilliseconds(10 + 300 * i));

                var stopTask = golem.Stop();
                Assert.Equal(GolemStatus.Stopping, status.Value);
                await stopTask;

                Assert.Equal(GolemStatus.Off, status.Value);
            }
        }

        [Fact]
        public async Task StartStopLoop_BreakStartup()
        {
            var loggerFactory = CreateLoggerFactory();
            string golemPath = await PackageBuilder.BuildTestDirectory();
            output.WriteLine("Path: " + golemPath);

            var golem = await TestUtils.Golem(golemPath, loggerFactory);

            var status = new PropertyChangedHandler<Golem, GolemStatus>(nameof(IGolem.Status), loggerFactory).Observe(golem);

            for (int i = 0; i < 20; i++)
            {
                var startTask = golem.Start();

                await Task.Delay(TimeSpan.FromMilliseconds(10 + 100 * i));

                var stopTask1 = golem.Stop();
                var stopTask2 = golem.Stop();

                await stopTask1;
                await stopTask2;

                Assert.Equal(GolemStatus.Off, status.Value);
            }
        }

        [Fact]
        public async Task StartStopLoop_StartDuringStopping()
        {
            var loggerFactory = CreateLoggerFactory();
            string golemPath = await PackageBuilder.BuildTestDirectory();
            output.WriteLine("Path: " + golemPath);

            var golem = await TestUtils.Golem(golemPath, loggerFactory);

            var status = new PropertyChangedHandler<Golem, GolemStatus>(nameof(IGolem.Status), loggerFactory).Observe(golem);

            for (int i = 0; i < 3; i++)
            {
                await golem.Start();
                Assert.Equal(GolemStatus.Ready, status.Value);

                var stopTask = golem.Stop();
                Assert.Equal(GolemStatus.Stopping, status.Value);

                await golem.Start();
                await stopTask;

                Assert.Equal(GolemStatus.Off, status.Value);
            }
        }
    }
}
