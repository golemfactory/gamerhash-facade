namespace GolemLib.Events;

using GolemLib.Types;

public interface IGolemEvent
{

}

public class JobEvent : EventArgs
{
    string Id { get; }
}

public class JobStarted : JobEvent { }

public class Computing : JobEvent { }

public class JobFinished : JobEvent
{
    GolemUsage Usage { get; }
    decimal Amount { get; }
}

public class PaymentConfirmed : JobEvent
{
    Payment payment;
}
