using System.ComponentModel;
using GolemLib;
using GolemLib.Types;

namespace Mock;

public class MockJob : IJob
{
    public string Id { get => throw new NotImplementedException(); init => throw new NotImplementedException(); }
    public GolemPrice Price { get => throw new NotImplementedException(); init => throw new NotImplementedException(); }

    public JobStatus Status => throw new NotImplementedException();

    public PaymentStatus? PaymentStatus => throw new NotImplementedException();

    public string RequestorId => throw new NotImplementedException();

    public event PropertyChangedEventHandler? PropertyChanged
    {
        add { throw new NotImplementedException(); }
        remove { throw new NotImplementedException(); }
    }

    public Task<GolemUsage> CurrentUsage()
    {
        throw new NotImplementedException();
    }

    public Task<Payment> PaymentConfirmation()
    {
        throw new NotImplementedException();
    }
}

