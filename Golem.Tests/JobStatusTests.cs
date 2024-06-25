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
    /// <summary>
    /// Tests follow scenarios described here: https://github.com/golemfactory/gamerhash-facade/wiki/Task-statuses-%E2%80%90-testing-scenarios
    /// </summary>
    public class JobStatusTests : JobsTestBase
    {
        public JobStatusTests(ITestOutputHelper outputHelper, GolemFixture golemFixture)
            : base(outputHelper, golemFixture, nameof(JobStatusTests))
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
            await using var app = _requestor?.CreateSampleApp() ?? throw new Exception("Requestor not started yet");
            Assert.True(app.Start());

            // Wait for job.
            Job? currentJob = await ReadChannel<Job?>(jobChannel);
            Assert.NotNull(currentJob);

            var jobStatusChannel = JobStatusChannel(currentJob);

            // Wait until ExeUnit will be created.
            Assert.Equal(JobStatus.Computing, await ReadChannel(jobStatusChannel,
                (JobStatus s) => s == JobStatus.DownloadingModel || s == JobStatus.Idle));
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

            _logger.LogInformation("=================== Starting Sample App ===================");
            await using var app = _requestor?.CreateSampleApp() ?? throw new Exception("Requestor not started yet");
            Assert.True(app.Start());

            // Wait for job.
            Job? currentJob = await ReadChannel<Job?>(jobChannel);
            Assert.NotNull(currentJob);

            var jobStatusChannel = JobStatusChannel(currentJob);

            // Wait until ExeUnit will be created.
            Assert.Equal(JobStatus.Computing, await ReadChannel(jobStatusChannel,
                (JobStatus s) => s == JobStatus.DownloadingModel || s == JobStatus.Idle));
            // Let him compute for a while.
            await Task.Delay(2 * 1000);

            _logger.LogInformation("=================== Stopping Provider ===================");
            await StopGolem(golem, golemPath, StatusChannel(golem));

            Assert.Equal(JobStatus.Interrupted, currentJob.Status);
            await AwaitValue<Job?>(jobChannel, null, TimeSpan.FromSeconds(1));
            Assert.Null(golem.CurrentJob);

            _logger.LogInformation("=================== Killing App ===================");
            await app.Stop(StopMethod.SigKill);
        }

        [Fact]
        public async Task RequestorBreaksAgreement_FastTerminatingScript()
        {
            string golemPath = await PackageBuilder.BuildTestDirectory();
            await using var golem = (Golem)await TestUtils.Golem(golemPath, _loggerFactory, null, RelayType.Local);
            _logger.LogInformation($"Path: {golemPath}");

            await StartGolem(golem, StatusChannel(golem));
            var jobChannel = JobChannel(golem);

            _logger.LogInformation("=================== Starting Sample App ===================");
            await using var app = _requestor?.CreateSampleApp() ?? throw new Exception("Requestor not started yet");
            Assert.True(app.Start());

            // Wait for job.
            Job? currentJob = await ReadChannel<Job?>(jobChannel);
            Assert.NotNull(currentJob);

            var jobStatusChannel = JobStatusChannel(currentJob);

            // Wait until ExeUnit will be created.
            Assert.Equal(JobStatus.Computing, await ReadChannel(jobStatusChannel,
                (JobStatus s) => s == JobStatus.DownloadingModel || s == JobStatus.Idle));
            // Let him compute for a while.
            await Task.Delay(2 * 1000);

            _logger.LogInformation("=================== Terminating App ===================");
            var rest = _requestor.Rest != null ? _requestor.Rest : throw new Exception("Rest api on Requestor not initialized.");
            var activities = await rest.GetActivities(currentJob.Id);

            // Kill script so he doesn't have chance to handle tasks termination.
            // Later send `DestroyAcitvity` manually to simulate double ctrl-c in requestor script.
            _ = app.Stop(StopMethod.SigKill);
            await rest.DestroyActivity(activities.First());

            // Acitvity will be destroyed, but Agreement won't. We should be temporary in Idle state.
            await AwaitValue<JobStatus>(jobStatusChannel, JobStatus.Idle, TimeSpan.FromSeconds(10));

            // Timeout is aqual to {no activity timeout} + margin
            await AwaitValue<JobStatus>(jobStatusChannel, JobStatus.Interrupted, TimeSpan.FromMinutes(2));
            await AwaitValue<Job?>(jobChannel, null, TimeSpan.FromSeconds(2));

            Assert.Equal(JobStatus.Interrupted, currentJob.Status);
            Assert.Null(golem.CurrentJob);

            // Stopping Golem
            await StopGolem(golem, golemPath, StatusChannel(golem));
        }

        [Fact]
        public async Task ProviderBreaksAgreement_KillingAgent()
        {
            string golemPath = await PackageBuilder.BuildTestDirectory();
            await using var golem = (Golem)await TestUtils.Golem(golemPath, _loggerFactory, null, RelayType.Local);
            _logger.LogInformation($"Path: {golemPath}");

            await StartGolem(golem, StatusChannel(golem));
            var jobChannel = JobChannel(golem);

            _logger.LogInformation("=================== Starting Sample App ===================");
            await using var app = _requestor?.CreateSampleApp() ?? throw new Exception("Requestor not started yet");
            Assert.True(app.Start());

            // Wait for job.
            Job? currentJob = await ReadChannel<Job?>(jobChannel);
            Assert.NotNull(currentJob);

            var jobStatusChannel = JobStatusChannel(currentJob);

            // Wait until ExeUnit will be created.
            Assert.Equal(JobStatus.Computing, await ReadChannel(jobStatusChannel,
                (JobStatus s) => s == JobStatus.DownloadingModel || s == JobStatus.Idle));
            // Let him compute for a while.
            await Task.Delay(2 * 1000);

            _logger.LogInformation("=================== Killing Provider Agent ===================");
            var pid = golem.GetProviderPid();
            if (pid.HasValue)
                Process.GetProcessById(pid.Value).Kill();

            await AwaitValue<JobStatus>(jobStatusChannel, JobStatus.Interrupted, TimeSpan.FromSeconds(30));
            Assert.Equal(JobStatus.Interrupted, currentJob.Status);
            await AwaitValue<Job?>(jobChannel, null, TimeSpan.FromSeconds(1));
            Assert.Null(golem.CurrentJob);

            _logger.LogInformation("=================== Killing App ===================");
            await app.Stop(StopMethod.SigKill);
        }

        [Fact]
        public async Task ProviderBreaksAgreement_KillingYagna()
        {
            string golemPath = await PackageBuilder.BuildTestDirectory();
            await using var golem = (Golem)await TestUtils.Golem(golemPath, _loggerFactory, null, RelayType.Local);
            _logger.LogInformation($"Path: {golemPath}");

            await StartGolem(golem, StatusChannel(golem));
            var jobChannel = JobChannel(golem);

            _logger.LogInformation("=================== Starting Sample App ===================");
            await using var app = _requestor?.CreateSampleApp() ?? throw new Exception("Requestor not started yet");
            Assert.True(app.Start());

            // Wait for job.
            Job? currentJob = await ReadChannel<Job?>(jobChannel);
            Assert.NotNull(currentJob);

            var jobStatusChannel = JobStatusChannel(currentJob);

            // Wait until ExeUnit will be created.
            Assert.Equal(JobStatus.Computing, await ReadChannel(jobStatusChannel,
                (JobStatus s) => s == JobStatus.DownloadingModel || s == JobStatus.Idle));
            // Let him compute for a while.
            await Task.Delay(2 * 1000);

            _logger.LogInformation("=================== Killing Provider Yagna ===================");
            var pid = golem.GetYagnaPid();
            if (pid.HasValue)
                Process.GetProcessById(pid.Value).Kill();

            await AwaitValue<Job?>(jobChannel, null, TimeSpan.FromSeconds(30));
            Assert.Null(golem.CurrentJob);
            Assert.Equal(JobStatus.Interrupted, currentJob.Status);

            _logger.LogInformation("=================== Killing App ===================");
            await app.Stop(StopMethod.SigKill);
        }

        [Fact]
        public async Task ProviderBreaksAgreement_KillingExeUnit()
        {
            string golemPath = await PackageBuilder.BuildTestDirectory();
            await using var golem = (Golem)await TestUtils.Golem(golemPath, _loggerFactory, null, RelayType.Local);
            _logger.LogInformation($"Path: {golemPath}");

            await StartGolem(golem, StatusChannel(golem));
            var jobChannel = JobChannel(golem);

            _logger.LogInformation("=================== Starting Sample App ===================");
            await using var app = _requestor?.CreateSampleApp() ?? throw new Exception("Requestor not started yet");
            Assert.True(app.Start());

            // Wait for job.
            Job? currentJob = await ReadChannel<Job?>(jobChannel);
            Assert.NotNull(currentJob);

            var jobStatusChannel = JobStatusChannel(currentJob);

            // Wait until ExeUnit will be created.
            Assert.Equal(JobStatus.Computing, await ReadChannel(jobStatusChannel,
                (JobStatus s) => s == JobStatus.DownloadingModel || s == JobStatus.Idle));
            // Let him compute for a while.
            await Task.Delay(2 * 1000);

            _logger.LogInformation("=================== Killing Runtime ===================");
            // TODO: How to avoid killing runtimes managed by non-test runs??
            var runtimes = Process.GetProcessesByName("ya-runtime-ai");
            foreach (var runtime in runtimes)
            {
                runtime.Kill(true);
            }

            await AwaitValue<JobStatus>(jobStatusChannel, JobStatus.Idle, TimeSpan.FromSeconds(5));

            // Task should be Interrupted after Provider won't get new Activity from Requestor.
            await AwaitValue<JobStatus>(jobStatusChannel, JobStatus.Interrupted, TimeSpan.FromSeconds(100));
            Assert.Equal(JobStatus.Interrupted, currentJob.Status);
            await AwaitValue<Job?>(jobChannel, null, TimeSpan.FromSeconds(1));
            Assert.Null(golem.CurrentJob);

            _logger.LogInformation("=================== Killing App ===================");
            await app.Stop(StopMethod.SigKill);
        }

        [Fact]
        public async Task RequestorBreaksAgreement_KillingYagna()
        {
            // Run Requestor yagna before starting Provider to speed up Offers propagation.
            await using var requestor = await GolemRequestor.Build("RequestorBreaksAgreement_KillingYagna", _loggerFactory.CreateLogger("Requestor2"));
            requestor.AutoSetUrls(11000);
            Assert.True(requestor.Start());
            requestor.InitPayment();

            string golemPath = await PackageBuilder.BuildTestDirectory();
            await using var golem = (Golem)await TestUtils.Golem(golemPath, _loggerFactory, null, RelayType.Local);
            _logger.LogInformation($"Path: {golemPath}");

            await StartGolem(golem, StatusChannel(golem));
            var jobChannel = JobChannel(golem);

            _logger.LogInformation("=================== Starting Sample App ===================");
            await using var app = requestor?.CreateSampleApp() ?? throw new Exception("Requestor not started yet");
            Assert.True(app.Start());

            // Wait for job.
            Job? currentJob = await ReadChannel<Job?>(jobChannel, null, TimeSpan.FromSeconds(50));
            Assert.NotNull(currentJob);

            var jobStatusChannel = JobStatusChannel(currentJob);

            // Wait until ExeUnit will be created.
            Assert.Equal(JobStatus.Computing, await ReadChannel(jobStatusChannel,
                (JobStatus s) => s == JobStatus.DownloadingModel || s == JobStatus.Idle));
            // Let him compute for a while.
            await Task.Delay(2 * 1000);

            _logger.LogInformation("=================== Killing Yagna ===================");
            await requestor.Stop(StopMethod.SigKill);

            // Timeout is aqual to {debit notes interval} + {unreachability limit} + {facade idle activity timeout} + margin
            await AwaitValue<JobStatus>(jobStatusChannel, JobStatus.Interrupted, TimeSpan.FromMinutes(6));
            await AwaitValue<Job?>(jobChannel, null, TimeSpan.FromSeconds(2));

            Assert.Equal(JobStatus.Interrupted, currentJob.Status);
            Assert.Null(golem.CurrentJob);

            // Stopping Golem
            await StopGolem(golem, golemPath, StatusChannel(golem));
        }
    }
}
