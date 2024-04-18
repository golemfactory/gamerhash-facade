using System.ComponentModel;
using System.Runtime.CompilerServices;

using Golem.GolemUI.Src;
using Golem.Yagna;
using Golem.Yagna.Types;

using GolemLib;
using GolemLib.Types;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Golem
{
    public class Golem : IGolem, IAsyncDisposable
    {
        private YagnaService Yagna { get; set; }
        private Provider Provider { get; set; }
        private ProviderConfigService ProviderConfig { get; set; }
        private CancellationTokenSource _yagnaCancellationtokenSource;
        private CancellationTokenSource _providerCancellationtokenSource;
        private EventsPublisher _events { get; set;}

        private readonly ILogger _logger;

        private readonly GolemPrice _golemPrice;

        private readonly Jobs _jobs;

        public GolemPrice Price
        {
            get
            {
                return _golemPrice;
            }
            set
            {
                if (!value.Equals(_golemPrice))
                {
                    // Set individual values, because we don't want to drop GolemPrice object here.
                    _golemPrice.StartPrice = value.StartPrice;
                    _golemPrice.GpuPerSec = value.GpuPerSec;
                    _golemPrice.EnvPerSec = value.EnvPerSec;
                    _golemPrice.NumRequests = value.NumRequests;

                    OnPropertyChanged();
                }
            }
        }

        private uint _networkSpeed;

        public uint NetworkSpeed
        {
            get => _networkSpeed;
            set
            {
                if (_networkSpeed != value)
                {
                    _networkSpeed = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Network
        {
            get => Yagna.Options.Network.Id;
        }

        public bool Mainnet
        {
            get => Yagna.Options.Network == Factory.Network(true);
        }

        public bool BlacklistEnabled
        {
            get => Provider.Blacklist.Enabled;
            set => Provider.Blacklist.Enabled = value;
        }

        public bool FilterRequestors
        {
            get => Provider.AllowList.Enabled;
            set => Provider.AllowList.Enabled = value;
        }

        private GolemStatus status;
        public GolemStatus Status
        {
            get { return status; }
            set
            {
                if (status != value)
                {
                    _logger.LogInformation($"Status change from {status} into {value}");
                    status = value;
                    OnPropertyChanged();
                }
            }
        }

        public IJob? CurrentJob { get; private set; }

        public string NodeId
        {
            get { return Yagna.Id?.NodeId ?? ""; }
        }

        public string WalletAddress
        {
            get
            {
                var walletAddress = ProviderConfig.WalletAddress;
                if (String.IsNullOrEmpty(walletAddress))
                {
                    _logger.LogInformation("No WalletAddress set. Using NodeId as a wallet address.");
                    walletAddress = Yagna.Id?.NodeId;
                }
                return walletAddress ?? "";
            }

            set
            {
                _logger.LogInformation($"Set WalletAddress '{value}'");
                if (Status == GolemStatus.Ready)
                {
                    _logger.LogInformation($"Init Payment (wallet changed) {value}");
                    Yagna.PaymentService.Init(value);
                }
                ProviderConfig.UpdateAccount(value, () => OnPropertyChanged(nameof(WalletAddress)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public Task BlacklistNode(string nodeId)
        {
            return Task.FromResult(Provider.Blacklist.AddIdentity(nodeId));
        }

        public Task AllowCertified(string cert)
        {
            return Task.FromResult(Provider.AllowList.AddCertificate(cert));
        }

        public Rule Blacklist
        {
            get => Provider.Blacklist;
        }

        public Rule AllowList
        {
            get => Provider.AllowList;
        }

        public EventHandler<ApplicationEventArgs> ApplicationEvents { get => _events.ApplicationEvent; set => _events.ApplicationEvent = value; }

        public Task<List<IJob>> ListJobs(DateTime since)
        {
            return _jobs.List(since);
        }

        public async Task Resume()
        {
            await Start();
        }

        public async Task<bool> Suspend()
        {
            await Stop();
            return false;
        }

        public async Task Start()
        {
            if (IsRunning())
                return;

            _logger.LogInformation("Starting Golem");

            Status = GolemStatus.Starting;

            var (yagnaCancellationTokenSource, providerCancellationTokenSource) = ResetTokens();
            var exitHandler = ExitCleanupHandler(yagnaCancellationTokenSource, providerCancellationTokenSource);

            try
            {
                await Task.Yield();

                await StartupYagna(exitHandler, yagnaCancellationTokenSource.Token);
                var defaultKey = (Yagna.AppKeyService.Get("default") ?? Yagna.AppKeyService.Get("autoconfigured"))
                    ?? throw new Exception("Can't get app-key, neither 'default' nor 'autoconfigured'");

                await StartupProvider(exitHandler, providerCancellationTokenSource.Token);
                Status = GolemStatus.Ready;
            }
            catch (OperationCanceledException e)
            {
                _events.Raise(new ApplicationEventArgs("Golem", $"Start cancelled: {e.Message}", ApplicationEventArgs.SeverityLevel.Error, e));
                _logger.LogInformation("Golem startup canceled");
                // Stopping function is responsible for setting the status.
            }
            catch (Exception e)
            {
                _events.Raise(new ApplicationEventArgs("Golem", $"Failed to start: {e.Message}", ApplicationEventArgs.SeverityLevel.Error, e));
                _logger.LogError("Failed to start Golem: {0}", e);

                // Cleanup to avoid leaving processes running.
                await exitHandler(1, "Golem");
                Status = GolemStatus.Error;
            }
        }

        /// <summary>
        /// Stops Golem. This function can be called multiple times. Second call will try to kill
        /// the processes faster.
        /// </summary>
        public async Task Stop()
        {
            if (!IsRunning() && Status != GolemStatus.Error)
                return;

            _logger.LogInformation("Stopping Golem");

            // Timeout is shorter when Stop was already called earlier.
            int yagnaTimeout = Status == GolemStatus.Stopping ? 200 : 30_000;
            int providerTimeout = Status == GolemStatus.Stopping ? 200 : 5_000;

            Status = GolemStatus.Stopping;

            SafeCancel(_providerCancellationtokenSource);
            SafeCancel(_yagnaCancellationtokenSource);

            await Provider.Stop(providerTimeout);
            await Yagna.Stop(yagnaTimeout);

            Status = GolemStatus.Off;

            OnPropertyChanged(nameof(WalletAddress));
            OnPropertyChanged(nameof(NodeId));
        }

        private async Task StartupYagna(Func<int, string, Task> exitHandler, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting Golem's Yagna");

            await Yagna.Run(exitHandler, cancellationToken);

            var account = await Yagna.WaitForIdentityAsync(cancellationToken);

            _ = Yagna.StartActivityLoop(cancellationToken, SetCurrentJob, _jobs, _events);
            _ = Yagna.StartInvoiceEventsLoop(cancellationToken, _jobs, _events);

            try
            {
                _logger.LogInformation($"Init Payment (node id) {account}");
                Yagna.PaymentService.Init(account ?? "");

                var walletAddress = WalletAddress;
                if (walletAddress != account)
                {
                    _logger.LogInformation($"Init Payment (wallet) {walletAddress}");
                    Yagna.PaymentService.Init(walletAddress ?? "");
                }

                OnPropertyChanged(nameof(WalletAddress));
                OnPropertyChanged(nameof(NodeId));
            }
            catch (Exception e)
            {
                _events.Raise(new ApplicationEventArgs("Golem", $"Payment init failed: {e.Message}", ApplicationEventArgs.SeverityLevel.Error, e));
                _logger.LogError("Payment init failed: {0}", e);
                throw new Exception("Payment init failed {0}", e);
            }
        }

        private async Task StartupProvider(Func<int, string, Task> exitHandler, CancellationToken cancellationToken)
        {
            try
            {
                Provider.PresetConfig.InitilizeDefaultPresets();
                await Provider.Run(Yagna.Options.AppKey, Yagna.Options.Network, exitHandler, cancellationToken, true);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                _events.Raise(new ApplicationEventArgs("Golem", $"Payment init failed: {e.Message}", ApplicationEventArgs.SeverityLevel.Error, e));
                throw new Exception($"Failed to start provider: {e}");
            }
        }

        /// <summary>
        /// Function restores the state of the Golem after cancellation or process exit.
        /// It can happen either during startup or in case of unexpected shutdown of one of the processes.
        /// </summary>
        Func<int, string, Task> ExitCleanupHandler(CancellationTokenSource yagnaCancellationTokenSource, CancellationTokenSource providerCancellationTokenSource)
        {
            return async (int exitCode, string which) =>
            {
                _events.Raise(new ApplicationEventArgs("Golem", $"ExitCleanupHandler: {which} exited with code {exitCode}", ApplicationEventArgs.SeverityLevel.Error, null));

                if (Status != GolemStatus.Stopping && Status != GolemStatus.Off)
                {
                    _logger.LogError("Unexpected {which} shutdown. Exit code: {exitCode}", which, exitCode);

                    if (!providerCancellationTokenSource.IsCancellationRequested || !Provider.HasExited)
                    {
                        SafeCancel(providerCancellationTokenSource);
                        await Provider.Stop();
                    }

                    if (!yagnaCancellationTokenSource.IsCancellationRequested || !Yagna.HasExited)
                    {
                        SafeCancel(yagnaCancellationTokenSource);
                        await Yagna.Stop();
                    }

                    Status = GolemStatus.Error;
                }
            };
        }

        void SafeCancel(CancellationTokenSource cancellationTokenSource)
        {
            if (!cancellationTokenSource.IsCancellationRequested)
            {
                _logger.LogDebug("Requesting cancellation");
                cancellationTokenSource.Cancel();
            }
            else
            {
                _logger.LogDebug("Cancellation already requested");
            }
        }

        public Golem(string golemPath, string? dataDir, ILoggerFactory loggerFactory, Network network)
        {
            var prov_datadir = dataDir != null ? Path.Combine(dataDir, "provider") : "./provider";
            var yagna_datadir = dataDir != null ? Path.Combine(dataDir, "yagna") : "./yagna";

            _logger = loggerFactory.CreateLogger<Golem>();
            _events = new EventsPublisher();
            _yagnaCancellationtokenSource = new CancellationTokenSource();
            _providerCancellationtokenSource = new CancellationTokenSource();
            var options = YagnaOptionsFactory.CreateStartupOptions(network);
            Yagna = new YagnaService(golemPath, yagna_datadir, options, _events, loggerFactory);
            Provider = new Provider(golemPath, prov_datadir, _events, loggerFactory);
            ProviderConfig = new ProviderConfigService(Provider, options.Network, loggerFactory);
            _golemPrice = ProviderConfig.GolemPrice;
            _jobs = new Jobs(Yagna, SetCurrentJob, loggerFactory);

            // Listen to property changed event on nested properties to update Provider presets.
            Price.PropertyChanged += OnGolemPriceChanged;
        }

        private void OnGolemPriceChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is GolemPrice price)
            {
                ProviderConfig.GolemPrice = price;
            }
        }

        private void SetCurrentJob(Job? job)
        {
            _logger.LogDebug($"Setting current job to {job?.Id}, status {job?.Status}");

            if (CurrentJob != job && (CurrentJob == null || !CurrentJob.Equals(job)))
            {
                CurrentJob = job;
                _logger.LogInformation("New job. Id: {0}, Requestor id: {1}, Status: {2}", job?.Id, job?.RequestorId, job?.Status);
                OnPropertyChanged(nameof(CurrentJob));
            }
            else
            {
                _logger.LogInformation("Job has not changed.");
            }
        }

        public async ValueTask DisposeAsync()
        {
            await (this as IGolem).Stop();
        }

        private (CancellationTokenSource, CancellationTokenSource) ResetTokens()
        {
            SafeCancel(_yagnaCancellationtokenSource);
            SafeCancel(_providerCancellationtokenSource);
            _yagnaCancellationtokenSource = new CancellationTokenSource();
            _providerCancellationtokenSource = new CancellationTokenSource();
            return (_yagnaCancellationtokenSource, _providerCancellationtokenSource);
        }

        private bool IsRunning()
        {
            return Status == GolemStatus.Starting ||
                Status == GolemStatus.Ready ||
                Status == GolemStatus.Stopping;
        }
    }
}
