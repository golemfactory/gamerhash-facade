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
    public class JobTests : JobsTestBase
    {

        public JobTests(ITestOutputHelper outputHelper, GolemFixture golemFixture)
            : base(outputHelper, golemFixture, nameof(JobTests))
        { }

        [Fact]
        public async Task CompleteScenario()
        {
            // Having

            string golemPath = await PackageBuilder.BuildTestDirectory();
            _logger.LogInformation($"Path: {golemPath}");
            await using var golem = (Golem)await TestUtils.Golem(golemPath, _loggerFactory, null, RelayType.Local);

            var golemStatusChannel = StatusChannel(golem);

            var jobChannel = JobChannel(golem);

            // Then

            // Golem status is `Off` before start.
            Assert.Equal(GolemStatus.Off, golem.Status);

            // `CurrentJob` before startup should be null.
            Assert.Null(golem.CurrentJob);

            // Before any run there should be no log files.
            var logFiles = golem.LogFiles();
            _logger.LogInformation($"Log files after 2nd run: {String.Join("\n", logFiles)}");
            Assert.Empty(logFiles);

            // Starting Golem

            await StartGolem(golem, golemStatusChannel);

            // `CurrentJob` after startup, before taking any Job should be null
            Assert.Null(golem.CurrentJob);

            await CheckLogsAfterGolemStart(golem, golemPath);

            // Starting Sample App

            _logger.LogInformation("=================== Starting Sample App ===================");
            var app = _requestor?.CreateSampleApp() ?? throw new Exception("Requestor not started yet");
            Assert.True(app.Start());

            // `CurrentJob` property update notification.
            Job? currentJob = await ReadChannel<Job?>(jobChannel);
            // `CurrentJob` object property and object arriving as a property notification are the same.
            Assert.Same(currentJob, golem.CurrentJob);
            Assert.NotNull(currentJob);

            var jobStatusChannel = JobStatusChannel(currentJob);
            var jobPaymentStatusChannel = JobPaymentStatusChannel(currentJob);
            var jobPaymentConfirmationChannel = JobPaymentConfirmationChannel(currentJob);

            // Job starts with `Idle` it might switch into `DownloadingModel` state and then transitions to `Computing`
            Assert.Equal(JobStatus.Computing, await ReadChannel(jobStatusChannel,
                (JobStatus s) => s == JobStatus.DownloadingModel || s == JobStatus.Idle));

            Assert.Same(currentJob, golem.CurrentJob);
            Assert.NotNull(currentJob);
            Assert.Equal(currentJob.RequestorId, _requestorAppKey?.Id);

            _logger.LogInformation($"Got a job. Status {golem.CurrentJob?.Status}, Id: {golem.CurrentJob?.Id}, RequestorId: {golem.CurrentJob?.RequestorId}");

            var jobId = currentJob.Id;

            _logger.LogInformation("=================== Stopping App ===================");
            await app.Stop(StopMethod.SigInt);

            await AwaitValue<JobStatus>(jobStatusChannel, JobStatus.Finished, TimeSpan.FromSeconds(30));

            Assert.Null(await AwaitValue<Job?>(jobChannel, null));
            Assert.Null(golem.CurrentJob);

            var jobs = await golem.ListJobs(DateTime.MinValue);
            var job = jobs.SingleOrDefault(j => j.Id == jobId);
            Assert.Equal(JobStatus.Finished, job?.Status);

            // Checking payments

            Assert.Equal(GolemLib.Types.PaymentStatus.Settled, await ReadChannel<GolemLib.Types.PaymentStatus?>(jobPaymentStatusChannel, (GolemLib.Types.PaymentStatus? s) => s == GolemLib.Types.PaymentStatus.InvoiceSent));

            //TODO payments is empty
            var payments = await ReadChannel<List<GolemLib.Types.Payment>?>(jobPaymentConfirmationChannel);
            // Assert.Single(payments);
            // Assert.Equal(_requestorAppKey.Id, payments[0].PayerId);
            // _logger.LogInformation($"Invoice amount {payments[0].Amount}");
            // Assert.True(Convert.ToDouble(payments[0].Amount) > 0.0);

            foreach (Payment payment in payments ?? new List<Payment>())
            {
                _logger.LogInformation($"Got payment confirmation {payment.PaymentId}, payee {payment.PayeeId}, payee adr {payment.PayeeAddr}, amount {payment.Amount}, details {payment.Details}");
            }

            // Stopping Golem
            await StopGolem(golem, golemPath, golemStatusChannel);

            CheckRuntimeLogsAfterAppRun(golem);

            // Restarting to have Golem again in a Ready state
            await StartGolem(golem, golemStatusChannel);

            // Restarted Yagna should list job with Finished state
            Assert.Equal(JobStatus.Finished, jobs[0].Status);

            // Stop
            await StopGolem(golem, golemPath, golemStatusChannel);
        }

        // After startup yagna and provider logs should be available
        async Task CheckLogsAfterGolemStart(Golem golem, String golemPath)
        {
            await CheckProviderHasStarted(golemPath);
            var logFiles = golem.LogFiles();
            _logger.LogInformation($"Log files after start: {String.Join("\n", logFiles)}");
            var logFileNames = logFiles.Select(file => Path.GetFileName(file)).ToList() ?? new List<string>();
            // After 1st run there should be yagna and ya-provider log files
            Assert.Contains("ya-provider_rCURRENT.log", logFileNames);
            Assert.Contains("yagna_rCURRENT.log", logFileNames);
        }

        void CheckRuntimeLogsAfterAppRun(Golem golem)
        {
            var logFiles = golem.LogFiles();
            _logger.LogInformation($"Log files after app run: {String.Join("\n", logFiles)}");
            // After app run there should be runtime logs created by `test`/`offer-template` commands
            var runtimeTestLogFiles = logFiles.FindAll(path => path
                .Contains(Path.Combine("exe-unit", "work", "logs")))
                .FindAll(path => path.EndsWith(".log"));
            Assert.NotEmpty(runtimeTestLogFiles);
            // There should be new runtime logs created by running the app
            var runtimeActivityLogFiles = logFiles.FindAll(path => path
                .Contains(Path.Combine("exe-unit", "work")))
                .FindAll(path => path.EndsWith(".log"))
                .FindAll(path => !runtimeTestLogFiles.Contains(path));
            Assert.Single(runtimeActivityLogFiles);
            var logFileNames = logFiles.Select(file => Path.GetFileName(file)).ToList() ?? new List<string>();
            // There should be current yagna and ya-provider log files
            Assert.Contains("ya-provider_rCURRENT.log", logFileNames);
            Assert.Contains("yagna_rCURRENT.log", logFileNames);

        }

        // Detects provider has already started (when pid file appears)
        async static Task CheckProviderHasStarted(String golemPath)
        {
            var providerPidFile = Path.Combine(golemPath, "modules", "golem-data", "provider", "ya-provider.pid");
            _ = await TestUtils.WaitForFileAndRead(providerPidFile);
        }
    }
}
