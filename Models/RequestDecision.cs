public class RequestDecision
{
    public int Id { get; set; }
    public int MediaAccessRequestId { get; set; }
    public string Stage { get; set; } = ""; // Manager|Security|IT
    public string Decision { get; set; } = ""; // Approve|Reject|Complete
    public string? Notes { get; set; }
    public string DecidedBySam { get; set; } = "";
    public DateTime DecidedAt { get; set; } = DateTime.UtcNow;
    public MediaAccessRequest MediaAccessRequest { get; set; } = null!;
}
