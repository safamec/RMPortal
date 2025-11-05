using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace RMPortal.Services
{
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
                    From = new MailAddress(_opt.FromAddress, _opt.FromName),
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
                        var att = new Attachment(stream, a.ContentType) { Name = a.FileName };
                        message.Attachments.Add(att);
                    }
                }

                using var client = CreateClient();
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
                Directory.CreateDirectory(_opt.PickupFolder);
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

            // لاحظ: UserName (مو User)
            if (!string.IsNullOrWhiteSpace(_opt.UserName))
            {
                client.Credentials = new NetworkCredential(_opt.UserName, _opt.Password);
            }

            return client;
        }
    }
}
