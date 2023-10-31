using Golem;
using Golem.IntegrationTests.Tools;
using GolemLib;
using GolemLib.Types;

namespace Golem.Tests
{
    public class GolemTests
    {
        string golemPath = "d:\\code\\yagna\\target\\debug";

        [Fact]
        public async Task StartStop_VerifyStatusAsync()
        {
            Console.WriteLine("Path: " + golemPath);

            var golem = new Golem(golemPath);
            GolemStatus status = GolemStatus.Off;

            Action<GolemStatus> updateStatus = (v) => { 
                status = v;
            };

            golem.PropertyChanged += new PropertyChangedHandler<GolemStatus>(nameof(IGolem.Status), updateStatus).Subscribe();

            await golem.Start();

            Assert.Equal(GolemStatus.Ready, status);

            Console.WriteLine("Sleep for 1000 seconds.");
            Thread.Sleep(1_000_000);

            await golem.Stop();

            Assert.Equal(GolemStatus.Off, status);
        }
    }
}
