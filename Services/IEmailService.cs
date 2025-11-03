namespace RMPortal.Services;

public sealed record EmailAttachment(string FileName, byte[] Content, string ContentType);
public sealed record EmailResult(bool Succeeded, string? Error = null);

public interface IEmailService
{
    Task<EmailResult> SendAsync(
        string to,
        string subject,
        string htmlBody,
        IEnumerable<string>? cc = null,
        IEnumerable<string>? bcc = null,
        IEnumerable<EmailAttachment>? attachments = null,
        CancellationToken ct = default);
}
