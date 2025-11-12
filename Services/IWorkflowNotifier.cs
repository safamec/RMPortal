using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;                 // IUrlHelper
using Microsoft.Extensions.Logging;            // ILogger<T>
using RMPortal.Models;                         // MediaAccessRequest
using RMPortal.Services;                       // IEmailService, IFakeAdService

namespace RMPortal.Services
{
    public interface IWorkflowNotifier
    {
        Task RequestSubmittedAsync(MediaAccessRequest req, IUrlHelper url);
        Task ManagerApprovedAsync(MediaAccessRequest req, IUrlHelper url);
        Task RejectedAsync(MediaAccessRequest req, string rejectedBy, string? notes, IUrlHelper url);
    }

    public class WorkflowNotifier : IWorkflowNotifier
    {
        private readonly IEmailService _email;
        private readonly IFakeAdService _ad;
        private readonly ILogger<WorkflowNotifier> _log;

        public WorkflowNotifier(IEmailService email, IFakeAdService ad, ILogger<WorkflowNotifier> log)
        {
            _email = email;
            _ad = ad;
            _log = log;
        }

        public async Task RequestSubmittedAsync(MediaAccessRequest req, IUrlHelper url)
        {
            var u = _ad.GetUser(req.CreatedBySam);
            if (string.IsNullOrWhiteSpace(u?.Email))
            {
                _log.LogWarning("Requester has no email for {Req}", req.RequestNumber);
                return;
            }

            var link = url.Action("Details", "Requests",
                                  new { id = req.Id },
                                  url.ActionContext.HttpContext.Request.Scheme) ?? "";

            var res = await _email.SendAsync(
                to: u.Email,
                subject: $"Your request {req.RequestNumber} was submitted",
                htmlBody: $@"
<p>Dear {u.DisplayName},</p>
<p>Your request <b>{req.RequestNumber}</b> has been <b>submitted</b>.</p>
<p><a href=""{link}"">Track it here</a></p>"
            );

            if (!res.Succeeded)
                _log.LogError("Submit email failed for {Req}: {Err}", req.RequestNumber, res.Error);
        }

        public async Task ManagerApprovedAsync(MediaAccessRequest req, IUrlHelper url)
        {
            var u = _ad.GetUser(req.CreatedBySam);
            if (string.IsNullOrWhiteSpace(u?.Email))
            {
                _log.LogWarning("Requester has no email for {Req} (approved)", req.RequestNumber);
                return;
            }

            var link = url.Action("Details", "Requests",
                                  new { id = req.Id },
                                  url.ActionContext.HttpContext.Request.Scheme) ?? "";

            // ✅ لا Security هنا، فقط إشعار المستخدم
            var res = await _email.SendAsync(
                to: u.Email,
                subject: $"Update: Request {req.RequestNumber} approved by Manager",
                htmlBody: $@"
<p>Dear {u.DisplayName},</p>
<p>Your request <b>{req.RequestNumber}</b> was <b>approved by your Line Manager</b> and forwarded to <b>IT Department</b> for action.</p>
<p><a href=""{link}"">View Details</a></p>"
            );

            if (!res.Succeeded)
                _log.LogError("Approve (requester) email failed for {Req}: {Err}", req.RequestNumber, res.Error);
        }

        public async Task RejectedAsync(MediaAccessRequest req, string rejectedBy, string? notes, IUrlHelper url)
        {
            var u = _ad.GetUser(req.CreatedBySam);
            if (string.IsNullOrWhiteSpace(u?.Email))
            {
                _log.LogWarning("Requester has no email for {Req} (rejected)", req.RequestNumber);
                return;
            }

            var link = url.Action("Details", "Requests",
                                  new { id = req.Id },
                                  url.ActionContext.HttpContext.Request.Scheme) ?? "";

            var notesHtml = string.IsNullOrWhiteSpace(notes)
                ? "<p><b>Notes:</b> (no notes)</p>"
                : $"<p><b>Notes:</b> {System.Net.WebUtility.HtmlEncode(notes)}</p>";

            var res = await _email.SendAsync(
                to: u.Email,
                subject: $"Request {req.RequestNumber} was rejected",
                htmlBody: $@"
<p>Dear {u.DisplayName},</p>
<p>Your request <b>{req.RequestNumber}</b> was <b>rejected</b> by {rejectedBy}.</p>
{notesHtml}
<p><a href=""{link}"">View Details</a></p>"
            );

            if (!res.Succeeded)
                _log.LogError("Reject email failed for {Req}: {Err}", req.RequestNumber, res.Error);
        }
    }
}
