using GolemLib;
using GolemLib.Types;

namespace Mock;

public class MockJob : Job
{
    public MockJob(string id, GolemPrice price) : base(id, price) { }

    public override JobStatus Status => throw new NotImplementedException();

    public override PaymentStatus? PaymentStatus => throw new NotImplementedException();

    public override Task<GolemUsage> CurrentUsage()
    {
        throw new NotImplementedException();
    }

    public override Task<Payment> PaymentConfirmation()
    {
        throw new NotImplementedException();
    }
}

