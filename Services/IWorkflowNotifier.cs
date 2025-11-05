using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc; // for IUrlHelper
using RMPortal.Models;

namespace RMPortal.Services
{
    public interface IWorkflowNotifier
    {
        Task RequestSubmittedAsync(MediaAccessRequest req, IUrlHelper url);
        Task ManagerApprovedAsync(MediaAccessRequest req, IUrlHelper url);
        Task RejectedAsync(MediaAccessRequest req, string rejectedBy, string? notes, IUrlHelper url);
    }

    public sealed class WorkflowNotifier : IWorkflowNotifier
    {
        private readonly IFakeAdService _ad;
        private readonly IEmailService _email;

        public WorkflowNotifier(IFakeAdService ad, IEmailService email)
        {
            _ad = ad; _email = email;
        }

        public async Task RequestSubmittedAsync(MediaAccessRequest req, IUrlHelper url)
        {
            var requester = _ad.GetUser(req.CreatedBySam);
            if (requester == null || string.IsNullOrWhiteSpace(requester.Email)) return;

            var detailsUrl = url.Action("Details", "Requests",
                new { id = req.Id }, protocol: "http") ?? "";

            await _email.SendAsync(
                to: requester.Email,
                subject: $"Request {req.RequestNumber} submitted successfully",
                htmlBody: $@"
<p>Dear {requester.DisplayName},</p>
<p>Your Removable Media Access request <b>{req.RequestNumber}</b> has been <b>submitted</b>.</p>
<p>You can track it here: <a href=""{detailsUrl}"">{detailsUrl}</a></p>"
            );
        }

        public async Task ManagerApprovedAsync(MediaAccessRequest req, IUrlHelper url)
        {
            var requester = _ad.GetUser(req.CreatedBySam);
            if (requester == null || string.IsNullOrWhiteSpace(requester.Email)) return;

            var detailsUrl = url.Action("Details", "Requests",
                new { id = req.Id }, protocol: "http") ?? "";

            await _email.SendAsync(
                to: requester.Email,
                subject: $"Update: Request {req.RequestNumber} approved by Manager",
                htmlBody: $@"
<p>Dear {requester.DisplayName},</p>
<p>Your request <b>{req.RequestNumber}</b> was <b>approved by your Line Manager</b> and forwarded to Security.</p>
<p>Track it here: <a href=""{detailsUrl}"">{detailsUrl}</a></p>"
            );
        }

        public async Task RejectedAsync(MediaAccessRequest req, string rejectedBy, string? notes, IUrlHelper url)
        {
            var requester = _ad.GetUser(req.CreatedBySam);
            if (requester == null || string.IsNullOrWhiteSpace(requester.Email)) return;

            var detailsUrl = url.Action("Details", "Requests",
                new { id = req.Id }, protocol: "http") ?? "";

            var notesHtml = string.IsNullOrWhiteSpace(notes) ? "" : $"<p><b>Notes:</b> {System.Net.WebUtility.HtmlEncode(notes)}</p>";

            await _email.SendAsync(
                to: requester.Email,
                subject: $"Update: Request {req.RequestNumber} was rejected",
                htmlBody: $@"
<p>Dear {requester.DisplayName},</p>
<p>Your request <b>{req.RequestNumber}</b> was <b>rejected by {rejectedBy}</b>.</p>
{notesHtml}
<p>You can review it here: <a href=""{detailsUrl}"">{detailsUrl}</a></p>"
            );
        }
    }
}
