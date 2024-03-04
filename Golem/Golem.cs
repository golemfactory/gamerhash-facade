﻿using System.ComponentModel;
using System.Runtime.CompilerServices;

using Golem.GolemUI.Src;
using Golem.Yagna;
using Golem.Yagna.Types;

using GolemLib;
using GolemLib.Types;

using Medallion.Shell;

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

        private readonly ILogger _logger;

        private readonly GolemPrice _golemPrice;

        private readonly Jobs _jobs;

        private readonly Network _network;

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
                    var yagnaOptions = YagnaOptionsFactory.CreateStartupOptions(_network);
                    Yagna.PaymentService.Init(value, yagnaOptions);
                }
                ProviderConfig.UpdateAccount(value, () => OnPropertyChanged(nameof(WalletAddress)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public Task BlacklistNode(string node_id)
        {
            throw new NotImplementedException();
        }

        public Task<List<IJob>> ListJobs(DateTime since)
        {
            return _jobs.List();
        }

        public async Task Resume()
        {
            await Start();
        }

        public async Task Start()
        {
            _logger.LogInformation("Starting Golem");
            if (IsRunning())
                return;

            Status = GolemStatus.Starting;
            await Task.Yield();

            var (yagnaCancellationTokenSource, providerCancellationTokenSource) = resetTokens();

            var yagnaOptions = YagnaOptionsFactory.CreateStartupOptions(_network);

            _logger.LogInformation("Starting Golem's Yagna");
            var success = await StartupYagnaAsync(yagnaOptions, yagnaProcessExitHandler(yagnaCancellationTokenSource, providerCancellationTokenSource), yagnaCancellationTokenSource.Token);

            if (success)
            {
                var defaultKey = Yagna.AppKeyService.Get("default") ?? Yagna.AppKeyService.Get("autoconfigured");
                if (defaultKey is not null)
                {
                    HandleStartupProvider(yagnaOptions, providerProcessExitHandler(yagnaCancellationTokenSource.Token), providerCancellationTokenSource.Token);
                }
            }
            else
            {
                Status = yagnaCancellationTokenSource.Token.IsCancellationRequested ? GolemStatus.Off : GolemStatus.Error;
            }

            OnPropertyChanged(nameof(WalletAddress));
            OnPropertyChanged(nameof(NodeId));
        }

        void HandleStartupProvider(YagnaStartupOptions yagnaOptions, Action<int> exitHandler, CancellationToken providerCancellationToken)
        {
            _logger.LogInformation("Starting Golem's Provider");
            Status = StartupProvider(yagnaOptions, exitHandler, providerCancellationToken)
                ? GolemStatus.Ready
                : providerCancellationToken.IsCancellationRequested
                    ? GolemStatus.Off
                    : GolemStatus.Error;
        }

        Action<int> yagnaProcessExitHandler(CancellationTokenSource yagnaCancellationTokenSource, CancellationTokenSource providerCancellationTokenSource)
        {
            return (int exitCode) =>
            {
                _logger.LogInformation("Handling Yagna process shutdown");
                if (exitCode != 0)
                {
                    Status = GolemStatus.Error;
                    _logger.LogError("Yagna process failed");
                }
                else if (Status != GolemStatus.Error)
                {
                    // `Off` only if status was not already set to `Error`.
                    Status = GolemStatus.Off;
                }
                safeCancel(yagnaCancellationTokenSource);
                safeCancel(providerCancellationTokenSource);
            };
        }

        Action<int> providerProcessExitHandler(CancellationToken providerCancellationToken)
        {
            return (int exitCode) =>
            {
                _logger.LogInformation("Handling Provider process shutdown");
                if (exitCode != 0)
                {
                    Status = GolemStatus.Error;
                    _logger.LogError("Provider process failed");
                }
            };
        }

        void safeCancel(CancellationTokenSource cancellationTokenSource)
        {
            if (!cancellationTokenSource.IsCancellationRequested)
            {
                cancellationTokenSource.Cancel();
            }
            else
            {
                _logger.LogWarning("Cancellation already requested");
            }
        }

        public async Task Stop()
        {
            _logger.LogInformation("Stopping Golem");
            Status = GolemStatus.Stopping;

            try
            {
                if (!_providerCancellationtokenSource.IsCancellationRequested)
                    _providerCancellationtokenSource.Cancel();
                if (!_yagnaCancellationtokenSource.IsCancellationRequested)
                    _yagnaCancellationtokenSource.Cancel();
            }
            catch (Exception err)
            {
                _logger.LogError($"Failed to cancel Golem process. Err {err}");
            }

            await Provider.Stop(5_000);
            await Yagna.Stop(30_000);

            Status = GolemStatus.Off;

            OnPropertyChanged(nameof(WalletAddress));
            OnPropertyChanged(nameof(NodeId));
        }

        public async Task<bool> Suspend()
        {
            await Stop();
            return false;
        }

        public Golem(string golemPath, string? dataDir, ILoggerFactory? loggerFactory, bool mainnet = true)
        {
            var prov_datadir = dataDir != null ? Path.Combine(dataDir, "provider") : "./provider";
            var yagna_datadir = dataDir != null ? Path.Combine(dataDir, "yagna") : "./yagna";

            _network = mainnet ? Network.Mainnet : Network.Goerli;

            loggerFactory ??= NullLoggerFactory.Instance;

            _logger = loggerFactory.CreateLogger<Golem>();
            _yagnaCancellationtokenSource = new CancellationTokenSource();
            _providerCancellationtokenSource = new CancellationTokenSource();

            Yagna = new YagnaService(golemPath, yagna_datadir, loggerFactory);
            Provider = new Provider(golemPath, prov_datadir, loggerFactory);
            ProviderConfig = new ProviderConfigService(Provider, _network, loggerFactory);
            _golemPrice = ProviderConfig.GolemPrice;
            _jobs = new Jobs(SetCurrentJob, loggerFactory);

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

        private async Task<bool> StartupYagnaAsync(YagnaStartupOptions yagnaOptions, Action<int> exitHandler, CancellationToken cancellationToken)
        {
            var success = Yagna.Run(yagnaOptions, exitHandler, cancellationToken);

            if (!success)
                return false;

            var account = await Yagna.WaitForIdentityAsync(cancellationToken);

            _ = Yagna.StartActivityLoop(cancellationToken, SetCurrentJob, _jobs);
            _ = Yagna.StartInvoiceEventsLoop(cancellationToken, _jobs);

            try
            {
                _logger.LogInformation($"Init Payment (node id) {account}");
                Yagna.PaymentService.Init(account ?? "", yagnaOptions);

                var walletAddress = WalletAddress;
                if (walletAddress != account)
                {
                    _logger.LogInformation($"Init Payment (wallet) {walletAddress}");
                    Yagna.PaymentService.Init(walletAddress ?? "", yagnaOptions);
                }
            }
            catch (Exception e)
            {
                _logger.LogError("Payment init failed: {0}", e);
                return false;
            }

            return success;
        }

        public bool StartupProvider(YagnaStartupOptions yagnaOptions, Action<int> exitHandler, CancellationToken cancellationToken)
        {
            try
            {
                Provider.PresetConfig.InitilizeDefaultPresets();

                return Provider.Run(yagnaOptions.AppKey, _network, exitHandler, cancellationToken, true);
            }
            catch (Exception e)
            {
                _logger.LogError("Failed to start provider: {0}", e);
                return false;
            }
        }

        private void SetCurrentJob(Job? job)
        {
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

        private (CancellationTokenSource, CancellationTokenSource) resetTokens()
        {
            if (_yagnaCancellationtokenSource != null && !_yagnaCancellationtokenSource.IsCancellationRequested)
            {
                try
                {
                    _yagnaCancellationtokenSource?.Cancel();
                }
                catch (Exception e)
                {
                    _logger.LogWarning(e, "Failed to cancel token.");
                }
            }
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
