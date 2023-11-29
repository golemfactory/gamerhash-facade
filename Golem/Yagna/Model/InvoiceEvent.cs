
public enum InvoiceEventType
{
    InvoiceReceivedEvent,
    InvoiceAcceptedEvent,
    InvoiceRejectedEvent,
    InvoiceCancelledEvent,
    InvoiceSettledEvent
}

public class InvoiceEvent
{
    public required string InvoiceId { get; init; }
    public required DateTime EventDate { get; init; }
    public required InvoiceEventType EventType { get; init; }
}
