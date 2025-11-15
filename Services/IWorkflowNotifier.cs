using System.Linq;
using System.Net;                           // WebUtility.HtmlEncode
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;             // IUrlHelper
using Microsoft.Extensions.Logging;         // ILogger<T>
using RMPortal.Models;                      // MediaAccessRequest
using RMPortal.Services;                    // IEmailService, IFakeAdService

namespace RMPortal.Services
{
    public interface IWorkflowNotifier
    {
        // بعد ما الطالب يضغط Submit
        Task RequestSubmittedAsync(MediaAccessRequest req, IUrlHelper url);

        // بعد ما المدير يوافق (من الشاشة أو من الإيميل)
        Task ManagerApprovedAsync(MediaAccessRequest req, IUrlHelper url);

        // أي مرحلة ترفض (Manager / Security / IT)
        Task RejectedAsync(MediaAccessRequest req, string rejectedBy, string? notes, IUrlHelper url);
    }

    // توسيع للواجهة عشان Security + IT
    public interface IExtendedWorkflowNotifier : IWorkflowNotifier
    {
        // بعد ما Security يوافق
        Task SecurityApprovedAsync(MediaAccessRequest req, IUrlHelper url);

        // بعد ما IT يكمّل الطلب
        Task ITCompletedAsync(MediaAccessRequest req, IUrlHelper url);
    }

    public class WorkflowNotifier : IExtendedWorkflowNotifier
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

        // ================== 1) Requester: Submitted ==================
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
<p>Your request <b>{req.RequestNumber}</b> has been <b>submitted</b> and is awaiting Line Manager review.</p>
<p><a href=""{link}"">Track it here</a></p>"
            );

            if (!res.Succeeded)
                _log.LogError("Submit email failed for {Req}: {Err}", req.RequestNumber, res.Error);
        }

        // ========== (جديد) إيميل للـ Manager مع روابط Approve/Reject ==========
      public async Task SendManagerReviewEmailAsync(MediaAccessRequest req, IUrlHelper url)
{
    // 1) نجيب بيانات المدير من AD
    var managerSam = _ad.GetManagerSam(req.CreatedBySam);
    if (string.IsNullOrWhiteSpace(managerSam))
    {
        _log.LogWarning("No manager found for {Req}", req.RequestNumber);
        return;
    }

    var mgr = _ad.GetUser(managerSam);
    if (mgr == null || string.IsNullOrWhiteSpace(mgr.Email))
    {
        _log.LogWarning("Manager has no email for {Req}", req.RequestNumber);
        return;
    }

    // 2) إنشاء توكن للرابط (ممكن نخليه حتى لو ما ذكرنا انتهاء الصلاحية في الإيميل)
    req.EmailActionToken = Guid.NewGuid().ToString("N");
    req.TokenExpiresAt = DateTime.UtcNow.AddDays(2);

    var scheme = url.ActionContext.HttpContext.Request.Scheme;

    // 3) روابط الموافقة / الرفض
    var approveUrl = url.Action(
        "ManagerAction",
        "WorkflowEmail",
        new { reqId = req.Id, emailAction = "approve", token = req.EmailActionToken },
        scheme
    ) ?? "";

    var rejectUrl = url.Action(
        "ManagerAction",
        "WorkflowEmail",
        new { reqId = req.Id, emailAction = "reject", token = req.EmailActionToken },
        scheme
    ) ?? "";

    // 4) نص الإيميل بدون جملة انتهاء صلاحية الرابط
    var htmlBody = $@"
<p>Dear {mgr.DisplayName},</p>

<p>A new Removable Media Access Request <b>{req.RequestNumber}</b> has been submitted and is awaiting your decision.</p>

<p>
    <b>Requester:</b> {req.Name}<br/>
    <b>Department:</b> {req.Department}<br/>
    <b>From:</b> {req.StartDate:yyyy-MM-dd} &nbsp; <b>To:</b> {req.EndDate:yyyy-MM-dd}
</p>

<p>Please choose one of the following actions:</p>

<p>
    <a href=""{approveUrl}"">✅ Approve</a> &nbsp;&nbsp;|&nbsp;&nbsp;
    <a href=""{rejectUrl}"">❌ Reject</a>
</p>
";

    var res = await _email.SendAsync(
        to: mgr.Email,
        subject: $"Request {req.RequestNumber} awaiting your approval",
        htmlBody: htmlBody
    );

    if (!res.Succeeded)
        _log.LogError("Manager review email failed for {Req}: {Err}", req.RequestNumber, res.Error);
}

        // ================== 2) Requester: Manager Approved ==================
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

        // ================== 3) Requester: Rejected (أي مرحلة) ==================
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
                : $"<p><b>Notes:</b> {WebUtility.HtmlEncode(notes)}</p>";

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

        // ================== 4) Security Approved ==================
        public async Task SecurityApprovedAsync(MediaAccessRequest req, IUrlHelper url)
        {
            // 4.1 إشعار المستخدِم
            var requester = _ad.GetUser(req.CreatedBySam);
            if (string.IsNullOrWhiteSpace(requester?.Email))
            {
                _log.LogWarning("Requester has no email for {Req} (security approved)", req.RequestNumber);
            }
            else
            {
                var detailsUrl = url.Action("Details", "Requests",
                                            new { id = req.Id },
                                            url.ActionContext.HttpContext.Request.Scheme) ?? "";

                var res = await _email.SendAsync(
                    to: requester.Email,
                    subject: $"Update: Request {req.RequestNumber} approved by Security",
                    htmlBody: $@"
<p>Dear {requester.DisplayName},</p>
<p>Your request <b>{req.RequestNumber}</b> was <b>approved by Security</b> and forwarded to <b>IT Department</b> for final action.</p>
<p><a href=""{detailsUrl}"">View Details</a></p>"
                );

                if (!res.Succeeded)
                    _log.LogError("SecurityApproved (requester) email failed for {Req}: {Err}", req.RequestNumber, res.Error);
            }

            // 4.2 إشعار مجموعة IT (RM_ITAdmins)
            var itUsers = _ad.GetUsersInGroup("RM_ITAdmins");
            if (itUsers != null)
            {
                var itInboxUrl = url.Action("Index", "IT",
                    new { area = "Approvals" },   // غيّريها إذا ما عندك Area
                    url.ActionContext.HttpContext.Request.Scheme) ?? "";

                foreach (var it in itUsers.Where(x => !string.IsNullOrWhiteSpace(x.Email)))
                {
                    var res = await _email.SendAsync(
                        to: it.Email,
                        subject: $"Request {req.RequestNumber} awaiting IT action",
                        htmlBody: $@"
<p>Dear {it.DisplayName},</p>
<p>Request <b>{req.RequestNumber}</b> has been approved by Security and is ready for IT action.</p>
<p><a href=""{itInboxUrl}"">Open IT Inbox</a></p>"
                    );

                    if (!res.Succeeded)
                        _log.LogError("SecurityApproved (IT) email failed for {Req}: {Err}", req.RequestNumber, res.Error);
                }
            }
        }

        // ================== 5) IT Completed ==================
        public async Task ITCompletedAsync(MediaAccessRequest req, IUrlHelper url)
        {
            var requester = _ad.GetUser(req.CreatedBySam);
            if (string.IsNullOrWhiteSpace(requester?.Email))
            {
                _log.LogWarning("Requester has no email for {Req} (IT completed)", req.RequestNumber);
                return;
            }

            var detailsUrl = url.Action("Details", "Requests",
                                        new { id = req.Id },
                                        url.ActionContext.HttpContext.Request.Scheme) ?? "";

            var validityText = req.EndDate.HasValue
                ? $" until <b>{req.EndDate:yyyy-MM-dd}</b>"
                : "";

            var res = await _email.SendAsync(
                to: requester.Email,
                subject: $"Completed: Request {req.RequestNumber}",
                htmlBody: $@"
<p>Dear {requester.DisplayName},</p>
<p>Your request <b>{req.RequestNumber}</b> has been <b>completed</b> by IT Department.</p>
<p>USB access has been enabled{validityText} in line with the Removable Media Procedure.</p>
<p><a href=""{detailsUrl}"">View Details</a></p>"
            );

            if (!res.Succeeded)
                _log.LogError("ITCompleted (requester) email failed for {Req}: {Err}", req.RequestNumber, res.Error);
        }
    }
}
