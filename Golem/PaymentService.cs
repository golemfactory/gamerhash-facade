using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Golem.Yagna.Types;
using Golem.Yagna;
using Microsoft.Extensions.Logging;
using GolemLib.Types;
using Golem.GolemUI.Src;

namespace Golem
{
    public class PaymentService //: Interfaces.IPaymentService
    {
        private Network _network;
        private string? _walletAddress;
        private string? _buildInAdress;
        private YagnaService _yagna;
        private readonly ProviderConfigService _providerConfig;
        private Golem _golem;
        private ILogger<PaymentService> _logger;

        public DateTime? LastSuccessfullRefresh { get; private set; } = null;

        public event PropertyChangedEventHandler? PropertyChanged;
        private bool _shouldCheckForInternalWallet = true;

        public PaymentService(Network network, YagnaService yagna, Golem golem, ProviderConfigService providerConfig, ILogger<PaymentService> logger)
        {
            _logger = logger;
            _network = network;
            _yagna = yagna;
            _golem = golem;
            _providerConfig = providerConfig;

            _walletAddress = _providerConfig.WalletAddress;

            //_timer = new DispatcherTimer();
            //_timer.Interval = TimeSpan.FromSeconds(20);
            //_timer.Tick += (object? s, EventArgs a) => this.UpdateState();
            //_timer.Start();
            //if (processController.IsServerRunning)
            //{
            //    UpdateState();
            //}
            //else
            //{
            //    _processController.PropertyChanged += this.OnProcessControllerStateChange;
            //}
        }

        
        //public string? LastError { get; private set; }

        //public string? Address => _walletAddress ?? _buildInAdress;

        //public string InternalAddress => _buildInAdress ?? "";

        //private void OnProcessControllerStateChange(object? sender, PropertyChangedEventArgs ev)
        //{
        //    if (ev.PropertyName == "IsServerRunning" && this._golem.IsServerRunning)
        //    {
        //        UpdateState();
        //    }
        //}

        //private void OnProviderConfigChange(object? sender, PropertyChangedEventArgs ev)
        //{
        //    _walletAddress = _providerConfig.Config?.Account ?? _buildInAdress;
        //    UpdateState();
        //    OnPropertyChanged("Address");
        //}

       

        //public async Task Refresh()
        //{
        //    try
        //    {
        //        if (!_golem.IsServerRunning)
        //        {
        //            return;
        //        }
        //        if (_buildInAdress == null)
        //        {
        //            _buildInAdress = _yagna.Id?.Address;
        //            if (_walletAddress == null)
        //            {
        //                OnPropertyChanged("Address");
        //            }
        //            OnPropertyChanged("InternalAddress");
        //        }
        //        var walletAddress = _walletAddress ?? _buildInAdress;

        //        if (walletAddress == null)
        //        {
        //            throw new Exception("Wallet address is null");
        //        }

        //        var state = await GetWalletState(walletAddress);


        //        if (walletAddress != _buildInAdress)
        //        {
        //            if (_shouldCheckForInternalWallet && _buildInAdress != null)
        //            {
        //                var internalWalletstate = await GetWalletState(_buildInAdress);
        //                if (internalWalletstate == null || internalWalletstate?.Balance == 0) _shouldCheckForInternalWallet = false;

        //                if (internalWalletstate != InternalWalletState)
        //                {
        //                    InternalWalletState = internalWalletstate;
        //                    OnPropertyChanged("InternalWalletState");
        //                }
        //            }
        //        }
        //        var oldState = State;
        //        LastError = null;
        //        if (state != oldState)
        //        {
        //            State = state;
        //            OnPropertyChanged("State");
        //        }
        //        LastSuccessfullRefresh = DateTime.Now;
        //    }
        //    catch (HttpRequestException ex)
        //    {
        //        string errorMsg = $"HttpRequestException when updating payment status: {ex.Message}";
        //        _logger.LogError(errorMsg);
        //        LastError = "No connection to payment service";
        //        State = null;
        //        OnPropertyChanged("State");
        //    }
        //    catch (Exception ex)
        //    {
        //        string errorMsg = $"Exception when updating payment status: {ex.Message}";
        //        _logger.LogError(errorMsg);
        //        LastError = "Unknown problem with payment service";
        //        State = null;
        //        OnPropertyChanged("State");
        //    }
        //}

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        //public async Task<bool> TransferOutTo(string address)
        //{
        //    if (_buildInAdress == null)
        //    {
        //        return false;
        //    }
        //    var balance = await _gsbPayment.GetStatus(_buildInAdress, PaymentDriver.ERC20.Id, _network.Id);
        //    var result = await _gsbPayment.TransferTo(PaymentDriver.ERC20.Id, _buildInAdress, _network.Id, address, amount: balance.Amount);
        //    return true;
        //}

        //private async void UpdateState()
        //{
        //    await Refresh();
        //}
    }
}
