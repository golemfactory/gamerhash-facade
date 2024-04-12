public class YagnaAgreementInfo
{
    public string AgreementID 
    {
        get => Id;
        set => Id = value;
    }
    public required string Id { get; set; }
    public DateTime ValidTo { get; set; }
    public DateTime Timestamp { get; set; }
}