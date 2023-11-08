using Golem;
using Golem.IntegrationTests.Tools;
using GolemLib;
using GolemLib.Types;
using Microsoft.Extensions.Logging;

namespace Golem.Tests
{
    public class GolemTests
    {
        string golemPath = "d:\\code\\yagna\\target\\debug";
        ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
               builder.AddSimpleConsole(options => options.SingleLine = true)
            );

        [Fact]
        public async Task StartStop_VerifyStatusAsync()
        {
            Console.WriteLine("Path: " + golemPath);
            IGolem golem = new Golem(golemPath, null, loggerFactory);
            GolemStatus status = GolemStatus.Off;

            Action<GolemStatus> updateStatus = (v) =>
            {
                status = v;
            };

            golem.PropertyChanged += new PropertyChangedHandler<GolemStatus>(nameof(IGolem.Status), updateStatus).Subscribe();

            await golem.Start();

            Assert.Equal(GolemStatus.Ready, status);

            Console.WriteLine("Sleep for a second.");
            Thread.Sleep(1_000);

            await golem.Stop();

            Assert.Equal(GolemStatus.Off, status);
        }

        [Fact]
        public async Task Job_verifyStatusAsync()
        {
            Console.WriteLine("Path: " + golemPath);
            var logger = loggerFactory.CreateLogger(nameof(GolemTests));
            IGolem golem = new Golem(golemPath, null, loggerFactory);

            GolemStatus status = GolemStatus.Off;

            Action<GolemStatus> updateStatus = (v) =>
            {
                status = v;
            };

            golem.PropertyChanged += new PropertyChangedHandler<GolemStatus>(nameof(IGolem.Status), updateStatus).Subscribe();

            await golem.Start();

            Assert.Equal(GolemStatus.Ready, status);
            Assert.Null(golem.CurrentJob);

            Console.WriteLine("Sleep for a second.");
            Thread.Sleep(1_000);

            //TODO: start job

            var current_job = golem.CurrentJob;
            Assert.NotNull(current_job);
            Console.WriteLine("{}", current_job.Status);

            IJob? job = null;
            Action<IJob> update_Job = (v) =>
            {
                job = v;
            };
            golem.PropertyChanged += new PropertyChangedHandler<IJob>(nameof(job), update_Job).Subscribe();

            Assert.NotNull(job);

            //TODO: stop job

            await golem.Stop();

            Assert.Equal(GolemStatus.Off, status);
        }
    }
}
