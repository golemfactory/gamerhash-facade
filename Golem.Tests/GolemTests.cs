using Golem;
using Golem.IntegrationTests.Tools;
using GolemLib;
using GolemLib.Types;

namespace Golem.Tests
{
    public class GolemTests
    {
        [Fact]
        public async Task StartStop_VerifyStatusAsync()
        {
            string golemPath = await PackageBuilder.BuildTestDirectory("StartStop_VerifyStatusAsync");
            Console.WriteLine("Path: " + golemPath);

            var golem = new Golem(PackageBuilder.BinariesDir(golemPath), PackageBuilder.DataDir(golemPath));
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
    }
}
