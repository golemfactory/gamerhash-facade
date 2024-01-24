using System.ComponentModel;
using System.Threading.Channels;

using Golem.Tools;
using Golem.Yagna.Types;

using GolemLib;
using GolemLib.Types;

using Microsoft.Extensions.Logging;

namespace Golem.Tests
{
    [Collection(nameof(SerialTestCollection))]
    public class JobTests : IDisposable, IAsyncLifetime, IClassFixture<GolemFixture>
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;
        private GolemRelay? _relay;
        private GolemRequestor? _requestor;
        private AppKey? _requestorAppKey;

        public JobTests(ITestOutputHelper outputHelper, GolemFixture golemFixture)
        {
            XunitContext.Register(outputHelper);
            // Log file directly in `tests` directory (like `tests/Jobtests-20231231.log )
            var logfile = Path.Combine(PackageBuilder.TestDir(""), nameof(JobTests) + "-{Date}.log");
            var loggerProvider = new TestLoggerProvider(golemFixture.Sink);
            _loggerFactory = LoggerFactory.Create(builder => builder
                //// Console logger makes `dotnet test` hang on Windows
                // .AddSimpleConsole(options => options.SingleLine = true)
                .AddFile(logfile)
                .AddProvider(loggerProvider)
            );
            _logger = _loggerFactory.CreateLogger(nameof(JobTests));
        }

        public async Task InitializeAsync()
        {
            var testDir = PackageBuilder.TestDir($"{nameof(JobTests)}_relay");
            _relay = await GolemRelay.Build(testDir, _loggerFactory.CreateLogger("Relay"));
            Assert.True(_relay.Start());
            System.Environment.SetEnvironmentVariable("YA_NET_RELAY_HOST", "127.0.0.1:16464");
            System.Environment.SetEnvironmentVariable("RUST_LOG", "debug");

            _requestor = await GolemRequestor.Build(nameof(JobTests), _loggerFactory.CreateLogger("Requestor"));
            Assert.True(_requestor.Start());
            _requestor.InitPayment();
            _requestorAppKey = _requestor.getTestAppKey();
        }

        [Fact]
        public async Task CompleteScenario()
        {
            // Having

            string golemPath = await PackageBuilder.BuildTestDirectory(nameof(JobTests));
            _logger.LogInformation($"Path: {golemPath}");
            var golem = new Golem(PackageBuilder.BinariesDir(golemPath), PackageBuilder.DataDir(golemPath), _loggerFactory);

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
            Assert.Equal(GolemStatus.Starting, await SkipMatching(golemStatusChannel, (GolemStatus s) => s == GolemStatus.Off));

            // .. and then to `Ready`
            Assert.Equal(GolemStatus.Ready, await SkipMatching(golemStatusChannel, (GolemStatus s) => s == GolemStatus.Starting));

            // `CurrentJob` after startup, before taking any Job should be null
            Assert.Null(golem.CurrentJob);

            // Starting Sample App

            _logger.LogInformation("Starting Sample App");
            var app = _requestor?.CreateSampleApp() ?? throw new Exception("Requestor not started yet");
            Assert.True(app.Start());

            // `CurrentJob` property update notification.
            Job? currentJob = await SkipMatching<Job?>(jobChannel);
            // `CurrentJob` object property and object arriving as a property notification are the same.
            Assert.Same(currentJob, golem.CurrentJob);
            Assert.NotNull(currentJob);

            // Job starts with `Idle` it might switch into `DownloadingModel` state and then transitions to `Computing`
            var currentState = await SkipMatching(jobStatusChannel, (JobStatus s) => s == JobStatus.Idle, 30_000);
            if(currentState == JobStatus.DownloadingModel)
            {
                Assert.Equal(JobStatus.Computing, await SkipMatching(jobStatusChannel, (JobStatus s) => s == JobStatus.DownloadingModel, 30_000));
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
            var currentJobPaymentStatusChannel = jobPaymentStatusChannel;
            var currentJobPaymentConfirmationChannel = jobPaymentConfirmationChannel;

            var jobId = currentJob.Id;
            // Stopping Sample App
            _logger.LogInformation("Stopping App");
            await app.Stop(StopMethod.SigInt);

            var jobs = await golem.ListJobs(DateTime.MinValue);
            var job = jobs.SingleOrDefault(j => j.Id == jobId);
            Assert.True(job != null && job.Status == JobStatus.Finished);
            // Job? finishedCurrentJob = await SkipMatching(jobChannel, (Job? j) => { return j?.Status == JobStatus.Finished; });
            // _logger.LogInformation("No more jobs");
            Assert.Null(golem.CurrentJob);

            // Checking payments

            // TODO where is InvoiceSent?
            // Assert.Equal(GolemLib.Types.PaymentStatus.InvoiceSent , await SkipMatching<GolemLib.Types.PaymentStatus?>(currentJobPaymentStatusChannel));
            Assert.Equal(GolemLib.Types.PaymentStatus.Settled, await SkipMatching<GolemLib.Types.PaymentStatus?>(currentJobPaymentStatusChannel, (GolemLib.Types.PaymentStatus? s) => s == GolemLib.Types.PaymentStatus.InvoiceSent));
            // TODO when these will arrive?
            // Assert.Equal(GolemLib.Types.PaymentStatus.Accepted , await SkipMatching(currentJobPaymentStatusChannel, (GolemLib.Types.PaymentStatus? s) => s == GolemLib.Types.PaymentStatus.Settled));
            // Assert.Equal(GolemLib.Types.PaymentStatus.InvoiceSent , await SkipMatching<GolemLib.Types.PaymentStatus?>(currentJobPaymentStatusChannel));

            //TODO payments is empty
            var payments = await SkipMatching<List<GolemLib.Types.Payment>?>(currentJobPaymentConfirmationChannel);
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

            var offStatus = await SkipMatching(golemStatusChannel, (GolemStatus status) => { return status == GolemStatus.Ready; });
            Assert.Equal(GolemStatus.Off, offStatus);
        }

        /// <summary>
        /// Reads from `channel` and returns first `T` for which `matcher` returns `false`
        /// </summary>
        /// <exception cref="Exception">Thrown when reading channel exceeds in total `timeoutMs`</exception>
        public async Task<T> SkipMatching<T>(ChannelReader<T> channel, Func<T, bool>? matcher = null, double timeoutMs = 10_000)
        {
            var cancelTokenSource = new CancellationTokenSource();
            cancelTokenSource.CancelAfter(TimeSpan.FromMilliseconds(timeoutMs));
            static bool FalseMatcher(T x) => false;
            matcher ??= FalseMatcher;
            while (await channel.WaitToReadAsync(cancelTokenSource.Token))
            {
                if (channel.TryRead(out var value) && value is T tValue && !matcher.Invoke(tValue))
                {
                    return tValue;
                }
                else
                {
                    _logger.LogInformation($"Skipping element: {value}");
                }
            }

            throw new Exception($"Failed to find matching {nameof(T)} within {timeoutMs} ms.");
        }

        /// <summary>
        /// Creates channel of updated properties.
        /// `extraHandler` is invoked each time property arrives.
        /// </summary>
        public Channel<T?> PropertyChangeChannel<OBJ, T>(OBJ? obj, string propName, Action<T?>? extraHandler = null) where OBJ : INotifyPropertyChanged
        {
            var eventChannel = Channel.CreateUnbounded<T?>();
            Action<T?> emitEvent = async (v) =>
            {
                extraHandler?.Invoke(v);
                await eventChannel.Writer.WriteAsync(v);
            };
            if (obj != null)
            {
                obj.PropertyChanged += new PropertyChangedHandler<OBJ, T>(propName, emitEvent, _loggerFactory).Subscribe();
            }
            else
            {
                _logger.LogInformation($"Property {typeof(OBJ)} is null. Event channel will be empty.");
            }
            return eventChannel;
        }

        public async Task DisposeAsync()
        {
            if (_requestor != null)
            {
                await _requestor.Stop(StopMethod.SigInt);
            }
            if (_relay != null)
            {
                await _relay.Stop(StopMethod.SigInt);
            }
        }

        public void Dispose()
        {
            XunitContext.Flush();
        }
    }
}
