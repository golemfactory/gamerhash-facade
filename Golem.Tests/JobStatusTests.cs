using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Channels;

using Golem.Tools;
using Golem.Yagna.Types;

using GolemLib;
using GolemLib.Types;

using Microsoft.Extensions.Logging;

namespace Golem.Tests
{
    public class JobStatusTests : JobsTestBase
    {
        public JobStatusTests(ITestOutputHelper outputHelper, GolemFixture golemFixture)
            : base(outputHelper, golemFixture, nameof(JobTests))
        { }

        [Fact]
        public async Task RequestorBreaksAgreement_KillingScript()
        {
            string golemPath = await PackageBuilder.BuildTestDirectory();
            await using var golem = (Golem)await TestUtils.Golem(golemPath, _loggerFactory, null, RelayType.Local);
            _logger.LogInformation($"Path: {golemPath}");

            await StartGolem(golem, StatusChannel(golem));
            var jobChannel = JobChannel(golem);

            _logger.LogInformation("=================== Starting Sample App ===================");
            var app = _requestor?.CreateSampleApp() ?? throw new Exception("Requestor not started yet");
            Assert.True(app.Start());

            // Wait for job.
            Job? currentJob = await ReadChannel<Job?>(jobChannel);
            Assert.NotNull(currentJob);

            var jobStatusChannel = JobStatusChannel(currentJob);

            // Wait until ExeUnit will be created.
            await AwaitValue<JobStatus>(jobStatusChannel, JobStatus.Computing);
            // Let him compute for a while.
            await Task.Delay(2 * 1000);

            _logger.LogInformation("=================== Killing App ===================");
            await app.Stop(StopMethod.SigKill);

            // Timeout is aqual to {debit notes interval} + {debit note accept timeout} + margin
            await AwaitValue<JobStatus>(jobStatusChannel, JobStatus.Interrupted, TimeSpan.FromMinutes(3));
            await AwaitValue<Job?>(jobChannel, null, TimeSpan.FromSeconds(2));

            Assert.Equal(JobStatus.Interrupted, currentJob.Status);
            Assert.Null(golem.CurrentJob);

            // Stopping Golem
            await StopGolem(golem, golemPath, StatusChannel(golem));
        }

        [Fact]
        public async Task ProviderBreaksAgreement_Graceful()
        {
            string golemPath = await PackageBuilder.BuildTestDirectory();
            await using var golem = (Golem)await TestUtils.Golem(golemPath, _loggerFactory, null, RelayType.Local);
            _logger.LogInformation($"Path: {golemPath}");

            await StartGolem(golem, StatusChannel(golem));
            var jobChannel = JobChannel(golem);

            _logger.LogInformation("Starting Sample App");
            var app = _requestor?.CreateSampleApp() ?? throw new Exception("Requestor not started yet");
            Assert.True(app.Start());

            // Wait for job.
            Job? currentJob = await ReadChannel<Job?>(jobChannel);
            Assert.NotNull(currentJob);

            var jobStatusChannel = JobStatusChannel(currentJob);

            // Wait until ExeUnit will be created.
            await AwaitValue<JobStatus>(jobStatusChannel, JobStatus.Computing);
            // Let him compute for a while.
            await Task.Delay(2 * 1000);


            _logger.LogInformation("=================== Stopping Provider ===================");
            await StopGolem(golem, golemPath, StatusChannel(golem));

            Assert.Equal(JobStatus.Interrupted, currentJob.Status);

            currentJob = await ReadChannel<Job?>(jobChannel);
            Assert.Null(currentJob);
            Assert.Null(golem.CurrentJob);

            await app.Stop(StopMethod.SigInt);
        }
    }
}