public class Invoice
{
    public required string InvoiceId { get; set; }
    public required string IssuerId { get; set; }
    public required string RecipientId { get; set; }
    public required string PayeeAddr { get; set; }
    public required string PayerAddr { get; set; }
    public required string PaymentPlatform { get; set; }
    public DateTime Timestamp { get; set; }
    public required string AgreementId { get; set; }
    public required List<string> ActivityIds { get; set; }
    public required string Amount { get; set; }
    public DateTime PaymentDueDate { get; set; }
    public InvoiceStatus Status { get; set; }
}

public enum InvoiceStatus
{
    ISSUED,
    RECEIVED,
    ACCEPTED,
    REJECTED,
    FAILED,
    SETTLED,
    CANCELLED
}