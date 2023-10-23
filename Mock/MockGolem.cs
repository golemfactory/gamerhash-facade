namespace Mock;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using GolemLib;
using GolemLib.Types;

public class MockGolem : IGolem
{
    public MockGolem()
    {

    }

    public GolemPrice Price { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    public string WalletAddress { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    public uint NetworkSpeed { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public GolemStatus Status => throw new NotImplementedException();

    public string NodeId => throw new NotImplementedException();

    public IJob? CurrentJob => throw new NotImplementedException();

    public event PropertyChangedEventHandler? PropertyChanged
    {
        add { }
        remove { }
    }

    public Task BlacklistNode(string node_id)
    {
        throw new NotImplementedException();
    }

    public Task<GolemUsage> CurrentUsage(string job_id)
    {
        throw new NotImplementedException();
    }

    public Task<List<IJob>> ListJobs(DateTime since)
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
