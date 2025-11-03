namespace RMPortal.Services;

public sealed class EmailOptions
{
    // Common
    public string From { get; set; } = "no-reply@local.test";

    // Mode switch
    public bool UsePickupFolder { get; set; } = true;

    // Pickup (dev)
    public string PickupFolder { get; set; } = @"C:\temp\maildrop";

    // SMTP (prod)
    public string Host { get; set; } = "smtp.example.com";
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string? User { get; set; }
    public string? Password { get; set; }
}
