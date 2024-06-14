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

            _logger.LogInformation("Starting Sample App");
            var app = _requestor?.CreateSampleApp() ?? throw new Exception("Requestor not started yet");
            Assert.True(app.Start());

            // Wait for job.
            Job? currentJob = await ReadChannel<Job?>(jobChannel);
            Assert.NotNull(currentJob);

            var jobStatusChannel = JobStatusChannel(currentJob);

            // Wait until ExeUnit will be created.
            await Task.Delay(5 * 1000);

            // Stopping Sample App
            _logger.LogInformation("Stopping App");
            await app.Stop(StopMethod.SigKill);

            Assert.Equal(JobStatus.Interrupted, await ReadChannel<JobStatus>(jobStatusChannel));

            currentJob = await ReadChannel<Job?>(jobChannel);
            Assert.Null(currentJob);
            Assert.Null(golem.CurrentJob);


            // Stopping Golem
            await StopGolem(golem, golemPath, StatusChannel(golem));


            // // Restarting to have Golem again in a Ready state
            // await StartGolem(golem, StatusChannel(golem));

            // // Restarted Yagna should list job with Finished state
            // Assert.Equal(JobStatus.Finished, jobs[0].Status);

            // // Stop
            // await StopGolem(golem, golemPath, StatusChannel(golem));
        }
    }
}