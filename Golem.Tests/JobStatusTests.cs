using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Channels;

using Golem.Model;
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
        {
            Environment.SetEnvironmentVariable("DEBIT_NOTE_ACCEPTANCE_DEADLINE", "30s");
        }

        [Fact]
        public async Task RequestorBreaksAgreement_KillingScript()
        {
            string golemPath = await PackageBuilder.BuildTestDirectory();
            await using var golem = (Golem)await TestUtils.Golem(golemPath, _loggerFactory, null, RelayType.LocalCentral);
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
            // Workaround for situations, when status update was so fast, that we were not able to create
            // channel yet, so waiting for update would be pointless.
            if (currentJob.Status != JobStatus.Computing)
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
            await using var golem = (Golem)await TestUtils.Golem(golemPath, _loggerFactory, null, RelayType.LocalCentral);
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
            // Workaround for situations, when status update was so fast, that we were not able to create
            // channel yet, so waiting for update would be pointless.
            if (currentJob.Status != JobStatus.Computing)
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
            await using var golem = (Golem)await TestUtils.Golem(golemPath, _loggerFactory, null, RelayType.LocalCentral);
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
            // Workaround for situations, when status update was so fast, that we were not able to create
            // channel yet, so waiting for update would be pointless.
            if (currentJob.Status != JobStatus.Computing)
                Assert.Equal(JobStatus.Computing, await ReadChannel(jobStatusChannel,
                    (JobStatus s) => s == JobStatus.DownloadingModel || s == JobStatus.Idle));
            // Let him compute for a while.
            await Task.Delay(2 * 1000);

            _logger.LogInformation("=================== Terminating App ===================");
            var rest = _requestor.Rest ?? throw new Exception("Rest api on Requestor not initialized.");
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
            await using var golem = (Golem)await TestUtils.Golem(golemPath, _loggerFactory, null, RelayType.LocalCentral);
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
            // Workaround for situations, when status update was so fast, that we were not able to create
            // channel yet, so waiting for update would be pointless.
            if (currentJob.Status != JobStatus.Computing)
                Assert.Equal(JobStatus.Computing, await ReadChannel(jobStatusChannel,
                    (JobStatus s) => s == JobStatus.DownloadingModel || s == JobStatus.Idle));
            // Let him compute for a while.
            await Task.Delay(2 * 1000);

            _logger.LogInformation("=================== Killing Provider Agent ===================");
            var pid = golem.GetProviderPid();
            if (pid.HasValue)
                Process.GetProcessById(pid.Value).Kill(true);

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
            await using var golem = (Golem)await TestUtils.Golem(golemPath, _loggerFactory, null, RelayType.LocalCentral);
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
            // Workaround for situations, when status update was so fast, that we were not able to create
            // channel yet, so waiting for update would be pointless.
            if (currentJob.Status != JobStatus.Computing)
                Assert.Equal(JobStatus.Computing, await ReadChannel(jobStatusChannel,
                    (JobStatus s) => s == JobStatus.DownloadingModel || s == JobStatus.Idle));
            // Let him compute for a while.
            await Task.Delay(2 * 1000);

            _logger.LogInformation("=================== Killing Provider Yagna ===================");
            var pid = golem.GetYagnaPid();
            if (pid.HasValue)
                Process.GetProcessById(pid.Value).Kill(true);

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
            await using var golem = (Golem)await TestUtils.Golem(golemPath, _loggerFactory, null, RelayType.LocalCentral);
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
            // Workaround for situations, when status update was so fast, that we were not able to create
            // channel yet, so waiting for update would be pointless.
            if (currentJob.Status != JobStatus.Computing)
                Assert.Equal(JobStatus.Computing, await ReadChannel(jobStatusChannel,
                    (JobStatus s) => s == JobStatus.DownloadingModel || s == JobStatus.Idle));
            // Let him compute for a while.
            await Task.Delay(2 * 1000);

            _logger.LogInformation("=================== Killing App ===================");
            // Killing app, so it won't terminate Agreement.
            await app.Stop(StopMethod.SigKill);

            _logger.LogInformation("=================== Killing Runtime ===================");
            // TODO: How to avoid killing runtimes managed by non-test runs??
            var runtimes = Process.GetProcessesByName("ya-runtime-ai").ToList();
            // Last runtime should be the best quess, since we spawned it a while ago.
            runtimes.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));
            runtimes.Last().Kill(true);

            await AwaitValue<JobStatus>(jobStatusChannel, JobStatus.Idle, TimeSpan.FromSeconds(15));

            // Task should be Interrupted after Provider won't get new Activity from Requestor.
            await AwaitValue<JobStatus>(jobStatusChannel, JobStatus.Interrupted, TimeSpan.FromSeconds(100));
            Assert.Equal(JobStatus.Interrupted, currentJob.Status);
            await AwaitValue<Job?>(jobChannel, null, TimeSpan.FromSeconds(1));
            Assert.Null(golem.CurrentJob);
        }

        [Fact]
        public async Task RequestorBreaksAgreement_KillingYagna()
        {
            // Run Requestor yagna before starting Provider to speed up Offers propagation.
            await using var requestor = await GolemRequestor.Build("RequestorBreaksAgreement_KillingYagna", _loggerFactory.CreateLogger("Requestor2"));
            requestor.AutoSetUrls(11000);
            requestor.SetSecret("test_key_2.plain");
            Assert.True(requestor.Start());
            await requestor.InitPayment();

            string golemPath = await PackageBuilder.BuildTestDirectory();
            await using var golem = (Golem)await TestUtils.Golem(golemPath, _loggerFactory, null, RelayType.LocalCentral);
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
            // Workaround for situations, when status update was so fast, that we were not able to create
            // channel yet, so waiting for update would be pointless.
            if (currentJob.Status != JobStatus.Computing)
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

        [Fact]
        public async Task ProviderRestart_RequestorBreaksAgreement()
        {
            string golemPath = await PackageBuilder.BuildTestDirectory();
            await using var golem = (Golem)await TestUtils.Golem(golemPath, _loggerFactory, null, RelayType.LocalCentral);
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
            var agreementId = currentJob.Id;

            // Wait until ExeUnit will be created.
            // Workaround for situations, when status update was so fast, that we were not able to create
            // channel yet, so waiting for update would be pointless.
            if (currentJob.Status != JobStatus.Computing)
                Assert.Equal(JobStatus.Computing, await ReadChannel(jobStatusChannel,
                    (JobStatus s) => s == JobStatus.DownloadingModel || s == JobStatus.Idle));
            // Let him compute for a while.
            await Task.Delay(TimeSpan.FromSeconds(2));

            _logger.LogInformation("=================== Killing App ===================");
            // Note: We need to manually send termination from Requestor with correct Reason.
            // It's better to avoid yapapi finding out that Provider stopped working, because it
            // could take action otherwise.
            await app.Stop(StopMethod.SigKill);

            _logger.LogInformation("=================== Killing Provider Agent ===================");
            // Provider is killed so he is not able to terminate Agreement.
            // Yagna has chance to be closed gracefully and close net connection with Requestor.
            // If we would double Stop here, re-establishing connection would last longer.
            var golemStatusChannel = StatusChannel(golem);
            var pid = golem.GetProviderPid();
            if (pid.HasValue)
                Process.GetProcessById(pid.Value).Kill(true);

            await AwaitValue<JobStatus>(jobStatusChannel, JobStatus.Interrupted, TimeSpan.FromSeconds(30));
            Assert.Equal(JobStatus.Interrupted, currentJob.Status);
            await AwaitValue<Job?>(jobChannel, null, TimeSpan.FromSeconds(1));
            Assert.Null(golem.CurrentJob);

            await AwaitValue(golemStatusChannel, GolemStatus.Error, TimeSpan.FromSeconds(3));

            _logger.LogInformation("=================== Restart Provider Agent ===================");
            await golem.Start();

            await Task.Delay(TimeSpan.FromSeconds(3));
            Assert.Equal(JobStatus.Interrupted, currentJob.Status);

            _logger.LogInformation("=================== Requestor terminates Agreement ===================");
            var rest = _requestor.Rest ?? throw new Exception("Rest api on Requestor not initialized.");
            var reason = new Reason(null!, "Healthcheck failed");
            reason.RequestorCode = "HealthCheckFailed";
            await rest.TerminateAgreement(agreementId, reason);

            // Wait for propagation of termination event
            await Task.Delay(TimeSpan.FromSeconds(2));

            Assert.Equal(JobStatus.Interrupted, currentJob.Status);
        }

        [Fact]
        public async Task ProviderRestart_ProviderBreaksAgreement()
        {
            string golemPath = await PackageBuilder.BuildTestDirectory();
            await using var golem = (Golem)await TestUtils.Golem(golemPath, _loggerFactory, null, RelayType.LocalCentral);
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
            var agreementId = currentJob.Id;

            // Wait until ExeUnit will be created.
            // Workaround for situations, when status update was so fast, that we were not able to create
            // channel yet, so waiting for update would be pointless.
            if (currentJob.Status != JobStatus.Computing)
                Assert.Equal(JobStatus.Computing, await ReadChannel(jobStatusChannel,
                    (JobStatus s) => s == JobStatus.DownloadingModel || s == JobStatus.Idle));
            // Let him compute for a while.
            await Task.Delay(2 * 1000);

            _logger.LogInformation("=================== Killing Provider Agent ===================");
            // Provider is killed so he is not able to terminate Agreement.
            // Yagna has chance to be closed gracefully and close net connection with Requestor.
            // If we would double Stop here, re-establishing connection would last longer.
            var golemStatusChannel = StatusChannel(golem);
            var pid = golem.GetProviderPid();
            if (pid.HasValue)
                Process.GetProcessById(pid.Value).Kill(true);

            _logger.LogInformation("=================== Killing App ===================");
            // Note: Provider needs to send termination with correct Reason.
            // It's better to avoid yapapi finding out that Provider stopped working, because it
            // could take action otherwise.
            await app.Stop(StopMethod.SigKill);

            await AwaitValue<JobStatus>(jobStatusChannel, JobStatus.Interrupted, TimeSpan.FromSeconds(30));
            Assert.Equal(JobStatus.Interrupted, currentJob.Status);
            await AwaitValue<Job?>(jobChannel, null, TimeSpan.FromSeconds(1));
            Assert.Null(golem.CurrentJob);

            await AwaitValue(golemStatusChannel, GolemStatus.Error, TimeSpan.FromSeconds(3));

            _logger.LogInformation("=================== Restart Provider Agent ===================");
            await golem.Start();

            Assert.Null(golem.CurrentJob);

            // ListJobs is one of things that can trigger termination of interrupted job.
            // This will happen as well, when new event from yagna API will be processed.
            var jobs = await golem.ListJobs(DateTime.Now - TimeSpan.FromMinutes(1));
            var prevJob = jobs.Find(job => job.Id == agreementId) as Job;

            Assert.NotNull(prevJob);
            Assert.Equal(JobStatus.Interrupted, prevJob.Status);

            _logger.LogInformation("=================== Requestor terminates Agreement ===================");
            // Try to terminate Agreement with Reason which in normal situation would be treated as correct termination
            // resulting in Finished status. If Provider didn't terminate Agreement with Reason leading to Interrupted
            // state, then JobStatus would be restored to Finished and it would reveal error.
            var rest = _requestor.Rest ?? throw new Exception("Rest api on Requestor not initialized.");
            var reason = new Reason(null!, "Agreement is no longer needed");
            reason.RequestorCode = "NoLongerNeeded";
            try
            {
                // Termination should fail, because Provider already terminated Agreement.
                await rest.TerminateAgreement(agreementId, reason);
            }
            catch (Exception e)
            {
                _logger.LogInformation(e, "Failed to terminate agreement");
            }

            // Wait for propagation of termination event. Status should be still Interrupted.
            await Task.Delay(TimeSpan.FromSeconds(2));
            Assert.Equal(JobStatus.Interrupted, prevJob.Status);
        }
    }
}
