using App;

using Golem;
using Golem.IntegrationTests.Tools;
using Golem.Yagna.Types;

using GolemLib;
using GolemLib.Types;

using Microsoft.Extensions.Logging;

namespace Golem.Tests
{
    [Collection("Sequential")]
    public class GolemTests : IDisposable
    {
        private readonly ILoggerFactory _loggerFactory;

        public GolemTests(ITestOutputHelper outputHelper)
        {
            XunitContext.Register(outputHelper);
            _loggerFactory = LoggerFactory.Create(builder =>
                builder.AddSimpleConsole(options => options.SingleLine = true)
            );
        }

        [Fact]
        public async Task StartStop_VerifyStatusAsync()
        {
            string golemPath = await PackageBuilder.BuildTestDirectory("StartStop_VerifyStatusAsync");
            Console.WriteLine("Path: " + golemPath);

            var golem = new Golem(PackageBuilder.BinariesDir(golemPath), PackageBuilder.DataDir(golemPath), _loggerFactory);
            GolemStatus status = GolemStatus.Off;

            Action<GolemStatus> updateStatus = (v) =>
            {
                status = v;
            };

            golem.PropertyChanged += new PropertyChangedHandler<GolemStatus>(nameof(IGolem.Status), updateStatus).Subscribe();

            await golem.Start();

            Assert.Equal(GolemStatus.Ready, status);
            await golem.Stop();

            Assert.Equal(GolemStatus.Off, status);
        }

        [Fact]
        public async Task TestDownloadArtifacts()
        {
            var dir = await PackageBuilder.BuildTestDirectory("TestDownloadArtifacts");

            Assert.True(Directory.EnumerateFiles(dir, "modules/golem/yagna*").Any());
            Assert.True(Directory.EnumerateFiles(dir, "modules/golem/ya-provider*").Any());
            Assert.True(Directory.EnumerateFiles(dir, "modules/plugins/ya-runtime-ai*").Any());
            Assert.True(Directory.EnumerateFiles(dir, "modules/plugins/dummy*").Any());
        }

        [Fact]
        public async Task Start_ChangeWallet_VerifyStatusAsync()
        {
            string golemPath = await PackageBuilder.BuildTestDirectory("Start_ChangeWallet_VerifyStatusAsync");
            Console.WriteLine("Path: " + golemPath);

            var golem = new Golem(PackageBuilder.BinariesDir(golemPath), PackageBuilder.DataDir(golemPath), _loggerFactory);
            GolemStatus status = GolemStatus.Off;

            Action<GolemStatus> updateStatus = (v) =>
            {
                status = v;
            };

            golem.PropertyChanged += new PropertyChangedHandler<GolemStatus>(nameof(IGolem.Status), updateStatus).Subscribe();

            await golem.Start();

            golem.WalletAddress = "1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa";

            await golem.Stop();

            Assert.Equal(GolemStatus.Off, status);
        }

        [Fact]
        public async Task StartStop_Job()
        {
            XunitContext.WriteLine("From Test");
            string golemPath = await PackageBuilder.BuildTestDirectory("StartStop_Job");
            Console.WriteLine("Path: " + golemPath);

            var golem = new Golem(PackageBuilder.BinariesDir(golemPath), PackageBuilder.DataDir(golemPath), _loggerFactory);
            GolemStatus status = GolemStatus.Off;

            Action<GolemStatus> golemStatus = (v) =>
            {
                Console.WriteLine("Golem status update. {0}", v);
                status = v;
            };
            golem.PropertyChanged += new PropertyChangedHandler<GolemStatus>(nameof(IGolem.Status), golemStatus).Subscribe();

            IJob? currentJob = null;
            Action<IJob?> currentJobHook = (v) =>
            {
                Console.WriteLine("Current Job update. {0}", v);
                currentJob = v;
            };
            golem.PropertyChanged += new PropertyChangedHandler<IJob?>(nameof(IGolem.CurrentJob), currentJobHook).Subscribe();

            Console.WriteLine("Starting Golem");
            await golem.Start();
            Assert.Equal(GolemStatus.Ready, status);

            // Assert.Null(golem.CurrentJob);

            Console.WriteLine("Starting App");
            var app_process = new SampleApp().CreateProcess();
            app_process.Start();
            Thread.Sleep(3000);

            // Assert.NotNull(golem.CurrentJob);

            Console.WriteLine("Stopping App");
            app_process.Kill();
            Thread.Sleep(3000);

            // Assert.Null(golem.CurrentJob);

            Console.WriteLine("Stopping Golem");
            await golem.Stop();

            Assert.Equal(GolemStatus.Off, status);
        }

        public void Dispose()
        {
            XunitContext.Flush();
        }
    }
}
