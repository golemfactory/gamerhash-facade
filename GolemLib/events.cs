namespace GolemLib.Events;

using GolemLib.Types;


public class JobEvent : EventArgs
{
    public string Id { get; }
}

public class JobStarted : JobEvent { }

public class JobStatusChanged : JobEvent
{
    public JobStatus Status { get; }
}

public class JobFinished : JobEvent
{
    public GolemUsage Usage { get; }
    public decimal Amount { get; }
}

public class PaymentConfirmed : JobEvent
{
    public Payment Payment { get; }
}
