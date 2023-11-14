using Golem;
using Golem.IntegrationTests.Tools;
using GolemLib;
using GolemLib.Types;
using Microsoft.Extensions.Logging;

namespace Golem.Tests
{
    [Collection("Sequential")]
    public class GolemTests
    {
        ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
               builder.AddSimpleConsole(options => options.SingleLine = true)
            );

        [Fact]
        public async Task StartStop_VerifyStatusAsync()
        {
            string golemPath = await PackageBuilder.BuildTestDirectory("StartStop_VerifyStatusAsync");
            Console.WriteLine("Path: " + golemPath);

            var golem = new Golem(PackageBuilder.BinariesDir(golemPath), PackageBuilder.DataDir(golemPath), loggerFactory);
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

            var golem = new Golem(PackageBuilder.BinariesDir(golemPath), PackageBuilder.DataDir(golemPath), loggerFactory);
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
    }
}
