using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using System.IO; // <--- add this


namespace RMPortal.Services;

public sealed class SmtpEmailService : IEmailService
{
    private readonly EmailOptions _opt;

    public SmtpEmailService(IOptions<EmailOptions> opt)
        => _opt = opt.Value;

    public async Task<EmailResult> SendAsync(
        string to,
        string subject,
        string htmlBody,
        IEnumerable<string>? cc = null,
        IEnumerable<string>? bcc = null,
        IEnumerable<EmailAttachment>? attachments = null,
        CancellationToken ct = default)
    {
        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(_opt.From),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(to);

            if (cc != null)
                foreach (var c in cc.Where(s => !string.IsNullOrWhiteSpace(s)))
                    message.CC.Add(c);

            if (bcc != null)
                foreach (var b in bcc.Where(s => !string.IsNullOrWhiteSpace(s)))
                    message.Bcc.Add(b);

            if (attachments != null)
            {
                foreach (var a in attachments)
                {
                    var stream = new MemoryStream(a.Content);
                    // MailMessage disposes attachments, which disposes the stream
                    var att = new Attachment(stream, a.ContentType) { Name = a.FileName };
                    message.Attachments.Add(att);
                }
            }

            using var client = CreateClient();
            // Honor cancellation manually because SmtpClient doesn't support ct
            ct.ThrowIfCancellationRequested();
            await client.SendMailAsync(message);

            return new EmailResult(true);
        }
        catch (Exception ex)
        {
            return new EmailResult(false, ex.Message);
        }
    }

    private SmtpClient CreateClient()
    {
        if (_opt.UsePickupFolder)
        {
            System.IO.Directory.CreateDirectory(_opt.PickupFolder);

            return new SmtpClient
            {
                DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory,
                PickupDirectoryLocation = _opt.PickupFolder
            };
        }

        var client = new SmtpClient(_opt.Host, _opt.Port)
        {
            EnableSsl = _opt.EnableSsl
        };

        if (!string.IsNullOrWhiteSpace(_opt.User))
        {
            client.Credentials = new NetworkCredential(_opt.User, _opt.Password);
        }
        return client;
    }
}
