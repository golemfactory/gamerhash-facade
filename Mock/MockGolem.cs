namespace Mock;

using System;
using System.ComponentModel;
using System.Threading.Tasks;
using GolemLib;
using GolemLib.Events;
using GolemLib.Types;

public class MockGolem : IGolem
{
    public MockGolem()
    {

    }

    public GolemPrice Price { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    public string WalletAddress { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    public uint SetNetworkSpeed { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public GolemStatus Status => throw new NotImplementedException();

    public string NodeId => throw new NotImplementedException();

    public Job? CurrentJob => throw new NotImplementedException();

    public event EventHandler<JobStarted> OnJobStarted;
    public event EventHandler<JobFinished> OnJobFinished;
    public event EventHandler<PaymentConfirmed> OnPaymentConfirmed;
    public event PropertyChangedEventHandler? PropertyChanged;

    public Task BlacklistNode(string node_id)
    {
        throw new NotImplementedException();
    }

    public Task<GolemUsage> CurrentUsage(string job_id)
    {
        throw new NotImplementedException();
    }

    public Task Resume()
    {
        throw new NotImplementedException();
    }

    public Task StartYagna()
    {
        throw new NotImplementedException();
    }

    public Task StopYagna()
    {
        throw new NotImplementedException();
    }

    public Task<bool> Suspend()
    {
        throw new NotImplementedException();
    }
}
