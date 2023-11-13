using Golem;
using Golem.IntegrationTests.Tools;
using GolemLib;
using GolemLib.Types;

namespace Golem.Tests
{
    public class GolemTests
    {
        string golemPath = "c:\\git\\yagna\\target\\debug";

        [Fact]
        public async Task StartStop_VerifyStatusAsync()
        {
            Console.WriteLine("Path: " + golemPath);

            var golem = new Golem(golemPath, null);
            GolemStatus status = GolemStatus.Off;

            Action<GolemStatus> updateStatus = (v) => { 
                status = v;
            };

            golem.PropertyChanged += new PropertyChangedHandler<GolemStatus>(nameof(IGolem.Status), updateStatus).Subscribe();

            await golem.Start();

            Assert.Equal(GolemStatus.Ready, status);

            await golem.Stop();

            Assert.Equal(GolemStatus.Off, status);
        }

        [Fact]
        public async Task Start_ChangeWallet_VerifyStatusAsync()
        {
            Console.WriteLine("Path: " + golemPath);

            var golem = new Golem(golemPath, null);
            GolemStatus status = GolemStatus.Off;

            Action<GolemStatus> updateStatus = (v) => {
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
