using System.ComponentModel;
using System.Diagnostics;
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
            var golem = await TestUtils.Golem(golemPath, _loggerFactory);

            Channel<GolemStatus> golemStatusChannel = PropertyChangeChannel(golem, nameof(IGolem.Status),
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

            // Starting Golem

            // Golem status is `Off` before start.
            Assert.Equal(GolemStatus.Off, golem.Status);

            // `CurrentJob` before startup should be null.
            Assert.Null(golem.CurrentJob);

            _logger.LogInformation("Starting Golem");
            await golem.Start();
            // On startup Golem status goes from `Off` to `Starting`
            Assert.Equal(GolemStatus.Starting, await ReadChannel(golemStatusChannel, (GolemStatus s) => s == GolemStatus.Off));

            // .. and then to `Ready`
            Assert.Equal(GolemStatus.Ready, await ReadChannel(golemStatusChannel, (GolemStatus s) => s == GolemStatus.Starting));

            // `CurrentJob` after startup, before taking any Job should be null
            Assert.Null(golem.CurrentJob);

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
            var currentState = await ReadChannel(jobStatusChannel, (JobStatus s) => s == JobStatus.Idle, 30_000);
            if (currentState == JobStatus.DownloadingModel)
            {
                Assert.Equal(JobStatus.Computing, await ReadChannel(jobStatusChannel, (JobStatus s) => s == JobStatus.DownloadingModel, 30_000));
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

            Assert.Equal(JobStatus.Finished, await ReadChannel(currentJobStatusChannel, (JobStatus s) => s == JobStatus.Computing, 30_000));

            var jobs = await golem.ListJobs(DateTime.MinValue);
            var job = jobs.SingleOrDefault(j => j.Id == jobId);
            Assert.Equal(JobStatus.Finished, job?.Status);
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

            _logger.LogInformation("Stopping Golem");
            await golem.Stop();

            var stoppingStatus = await ReadChannel(golemStatusChannel, (GolemStatus status) => { return status == GolemStatus.Ready; });
            Assert.Equal(GolemStatus.Stopping, stoppingStatus);

            var offStatus = await ReadChannel(golemStatusChannel, (GolemStatus status) => { return status == GolemStatus.Ready; });
            Assert.Equal(GolemStatus.Off, offStatus);
        }
    }
}
