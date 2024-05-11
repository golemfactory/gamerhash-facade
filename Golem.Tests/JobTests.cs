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

            var golemStatusChannel = PropertyChangeChannel(golem, nameof(IGolem.Status),
                (GolemStatus v) => _logger.LogInformation($"Golem status update: {v}"));

            var jobStatusChannel = PropertyChangeChannel<IJob, JobStatus>(null, "");
            var jobPaymentStatusChannel = PropertyChangeChannel<IJob, GolemLib.Types.PaymentStatus?>(null, "");
            var jobGolemUsageChannel = PropertyChangeChannel<IJob, GolemUsage>(null, "");
            var jobPaymentConfirmationChannel = PropertyChangeChannel<IJob, List<Payment>>(null, "");

            Channel<Job?> jobChannel = PropertyChangeChannel(golem, nameof(IGolem.CurrentJob), (Job? currentJob) =>
            {
                _logger.LogInformation($"Current Job update: {currentJob}");

                jobStatusChannel = PropertyChangeChannel(currentJob, nameof(currentJob.Status),
                    (JobStatus v) => _logger.LogInformation($"Current job Status update: {v}"));
                jobPaymentStatusChannel = PropertyChangeChannel(currentJob, nameof(currentJob.PaymentStatus),
                    (GolemLib.Types.PaymentStatus? v) => _logger.LogInformation($"Current job Payment Status update: {v}"));
                jobGolemUsageChannel = PropertyChangeChannel(currentJob, nameof(currentJob.CurrentUsage),
                    (GolemUsage? v) => _logger.LogInformation($"Current job Usage update: {v}"));
                jobPaymentConfirmationChannel = PropertyChangeChannel(currentJob, nameof(currentJob.PaymentConfirmation),
                    (List<Payment>? v) => _logger.LogInformation($"Current job Payment Confirmation update: {v}"));

            });


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

            await StartGolem(golem, golemPath, golemStatusChannel);

            // `CurrentJob` after startup, before taking any Job should be null
            Assert.Null(golem.CurrentJob);

            await CheckLogsAfterGolemStart(golem, golemPath);

            // Starting Sample App

            _logger.LogInformation("Starting Sample App");
            var app = _requestor?.CreateSampleApp() ?? throw new Exception("Requestor not started yet");
            Assert.True(app.Start());

            // `CurrentJob` property update notification.
            Job? currentJob = await ReadChannel<Job?>(jobChannel);
            // `CurrentJob` object property and object arriving as a property notification are the same.
            Assert.Same(currentJob, golem.CurrentJob);
            Assert.NotNull(currentJob);

            // Job starts with `Idle` it might switch into `DownloadingModel` state and then transitions to `Computing`
            var currentState = await ReadChannel(jobStatusChannel, (JobStatus s) => s == JobStatus.Idle);
            if (currentState == JobStatus.DownloadingModel)
            {
                Assert.Equal(JobStatus.Computing, await ReadChannel(jobStatusChannel, (JobStatus s) => s == JobStatus.DownloadingModel));
            }
            else
            {
                Assert.Equal(JobStatus.Computing, currentState);
            }

            Assert.Same(currentJob, golem.CurrentJob);
            Assert.NotNull(currentJob);
            Assert.Equal(currentJob.RequestorId, _requestorAppKey?.Id);

            _logger.LogInformation($"Got a job. Status {golem.CurrentJob?.Status}, Id: {golem.CurrentJob?.Id}, RequestorId: {golem.CurrentJob?.RequestorId}");

            // keep references to a finishing job status channels
            var currentJobStatusChannel = jobStatusChannel;
            var currentJobPaymentStatusChannel = jobPaymentStatusChannel;
            var currentJobPaymentConfirmationChannel = jobPaymentConfirmationChannel;

            var jobId = currentJob.Id;
            // Stopping Sample App
            _logger.LogInformation("Stopping App");
            await app.Stop(StopMethod.SigInt);

            Assert.Equal(JobStatus.Finished, await ReadChannel(currentJobStatusChannel, (JobStatus s) => s == JobStatus.Computing));

            var jobs = await golem.ListJobs(DateTime.MinValue);
            var job = jobs.SingleOrDefault(j => j.Id == jobId);
            Assert.Equal(JobStatus.Finished, job?.Status);

            currentJob = await ReadChannel<Job?>(jobChannel);
            Assert.Null(currentJob);
            Assert.Null(golem.CurrentJob);

            // Checking payments

            Assert.Equal(GolemLib.Types.PaymentStatus.Settled, await ReadChannel<GolemLib.Types.PaymentStatus?>(currentJobPaymentStatusChannel, (GolemLib.Types.PaymentStatus? s) => s == GolemLib.Types.PaymentStatus.InvoiceSent));

            //TODO payments is empty
            var payments = await ReadChannel<List<GolemLib.Types.Payment>?>(currentJobPaymentConfirmationChannel);
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
            await StartGolem(golem, golemPath, golemStatusChannel);

            // Restarted Yagna should list job with Finished state
            Assert.Equal(JobStatus.Finished, jobs[0].Status);

            // Stop
            await StopGolem(golem, golemPath, golemStatusChannel);

            // Restart to make Golem to archive old logs
            await StartGolem(golem, golemPath, golemStatusChannel);

            CheckLogGzArchivesAfterRestart(golem);

            // Stop
            await StopGolem(golem, golemPath, golemStatusChannel);
        }

        async Task StartGolem(IGolem golem, String golemPath, ChannelReader<GolemStatus> statusChannel)
        {
            _logger.LogInformation("Starting Golem");
            var startTask = golem.Start();
            Assert.Equal(GolemStatus.Starting, await ReadChannel(statusChannel));
            await startTask;
            Assert.Equal(GolemStatus.Ready, await ReadChannel(statusChannel));
            var providerPidFile = Path.Combine(golemPath, "modules/golem-data/provider/ya-provider.pid");
            await TestUtils.WaitForFile(providerPidFile);
        }

        async Task StopGolem(IGolem golem, String golemPath, ChannelReader<GolemStatus> statusChannel)
        {
            _logger.LogInformation("Stopping Golem");
            var stopTask = golem.Stop();
            Assert.Equal(GolemStatus.Stopping, await ReadChannel(statusChannel));
            await stopTask;
            Assert.Equal(GolemStatus.Off, await ReadChannel(statusChannel));
            var providerPidFile = Path.Combine(golemPath, "modules/golem-data/provider/ya-provider.pid");
            try
            {
                File.Delete(providerPidFile);
            }
            catch { }
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

        // Log files after rerstart of Golem should include log.gz archives.
        void CheckLogGzArchivesAfterRestart(Golem golem)
        {
            var logFiles = golem.LogFiles();
            _logger.LogInformation($"Log files after restart: {String.Join("\n", logFiles)}");
            // After 3rd run there should be runtime logs created by `test`/`offer-template` commands
            var runtimeTestLogFiles = logFiles.FindAll(path => path
                .Contains(Path.Combine("exe-unit", "work", "logs")))
                .FindAll(path => path.EndsWith(".log"));
            Assert.NotEmpty(runtimeTestLogFiles);
            // After 3rd run there should old runtime logs created by previous activity
            var runtimeActivityLogFiles = logFiles.FindAll(path => path
                .Contains(Path.Combine("exe-unit", "work")))
                .FindAll(path => path.EndsWith(".log"))
                .FindAll(path => !runtimeTestLogFiles.Contains(path));
            Assert.Single(runtimeActivityLogFiles);
            var logFileNames = logFiles.Select(file => Path.GetFileName(file)).ToList() ?? new List<string>();
            // After 3rd run there should be current yagna and ya-provider log files
            Assert.Contains("ya-provider_rCURRENT.log", logFileNames);
            Assert.Contains("yagna_rCURRENT.log", logFileNames);
            // After 3rd run there should be previous yagna and ya-provider log files
            Regex providerLogPattern = new Regex(@"^ya-provider_r[0-9]{4}-[0-9]{2}-[0-9]{2}_[0-9]{2}-[0-9]{2}-[0-9]{2}\.log$");
            Assert.Single(logFileNames.FindAll(file => providerLogPattern.IsMatch(file)));
            Regex yagnaLogPattern = new Regex(@"^yagna_r[0-9]{4}-[0-9]{2}-[0-9]{2}_[0-9]{2}-[0-9]{2}-[0-9]{2}\.log$");
            Assert.Single(logFileNames.FindAll(file => yagnaLogPattern.IsMatch(file)));
            // After 3rd run there should be previous previous yagna and ya-provider log gz file archives
            Regex providerLogGzPattern = new Regex(@"^ya-provider_r[0-9]{4}-[0-9]{2}-[0-9]{2}_[0-9]{2}-[0-9]{2}-[0-9]{2}\.log\.gz$");
            Assert.Single(logFileNames.FindAll(file => providerLogGzPattern.IsMatch(file)));
            Regex yagnaLogGzPattern = new Regex(@"^yagna_r[0-9]{4}-[0-9]{2}-[0-9]{2}_[0-9]{2}-[0-9]{2}-[0-9]{2}\.log\.gz$");
            Assert.Single(logFileNames.FindAll(file => yagnaLogGzPattern.IsMatch(file)));
        }

        // Detects provider has already started (when pid file appears)
        async static Task CheckProviderHasStarted(String golemPath)
        {
            var providerPidFile = Path.Combine(golemPath, "modules", "golem-data", "provider", "ya-provider.pid");
            _ = await TestUtils.WaitForFileAndRead(providerPidFile);
        }
    }
}
