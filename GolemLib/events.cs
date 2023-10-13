namespace GolemLib.Events;

using GolemLib.Types;


public class JobEvent : EventArgs
{
    public Job Job { get; init; }

    public JobEvent(Job job)
    {
        Job = job;
    }
}

public class JobStarted : JobEvent
{
    public JobStarted(Job job) : base(job) { }
}

public class JobFinished : JobEvent
{
    public GolemUsage Usage { get; init; }
    public decimal Amount { get; init; }

    public JobFinished(Job job, GolemUsage usage, decimal amount) : base(job)
    {
        this.Usage = usage;
        this.Amount = amount;
    }
}

public class PaymentConfirmed : JobEvent
{
    public Payment Payment { get; init; }

    public PaymentConfirmed(Job job, Payment payment) : base(job)
    {
        this.Payment = payment;
    }
}
