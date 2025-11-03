public class RequestDecision
{
    public int Id { get; set; }

    // FK + Navigation (BOTH are important)
    public int MediaAccessRequestId { get; set; }
    public MediaAccessRequest MediaAccessRequest { get; set; } = null!;

    public string Stage { get; set; } = "";
    public string Decision { get; set; } = "";
    public string? Notes { get; set; }
    public string DecidedBySam { get; set; } = "";
    public DateTime? DecidedAt { get; set; }
}
