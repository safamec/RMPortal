using Microsoft.AspNetCore.Mvc;
using RMPortal.Services;

public class WorkflowNotifier : IWorkflowNotifier
{
    private readonly IEmailService _email;
    private readonly IFakeAdService _ad;
    private readonly ILogger<WorkflowNotifier> _log;

    public WorkflowNotifier(IEmailService email, IFakeAdService ad, ILogger<WorkflowNotifier> log)
    {
        _email = email; _ad = ad; _log = log;
    }

    public async Task RequestSubmittedAsync(MediaAccessRequest req, IUrlHelper url)
    {
        var u = _ad.GetUser(req.CreatedBySam);
        if (string.IsNullOrWhiteSpace(u?.Email)) { _log.LogWarning("Requester has no email"); return; }

        var link = url.Action("Details", "Requests", new { id = req.Id },
                              url.ActionContext.HttpContext.Request.Scheme) ?? "";
        var res = await _email.SendAsync(
            u.Email,
            $"Your request {req.RequestNumber} was submitted",
            $@"<p>Dear {u.DisplayName},</p>
               <p>Your request <b>{req.RequestNumber}</b> has been submitted.</p>
               <p><a href=""{link}"">Track it here</a></p>");
        if (!res.Succeeded) _log.LogError("Submit email failed: {Err}", res.Error);
    }

    public async Task ManagerApprovedAsync(MediaAccessRequest req, IUrlHelper url)
    {
        var u = _ad.GetUser(req.CreatedBySam);
        if (!string.IsNullOrWhiteSpace(u?.Email))
        {
            var link = url.Action("Details", "Requests", new { id = req.Id },
                                  url.ActionContext.HttpContext.Request.Scheme) ?? "";
            var res1 = await _email.SendAsync(
                u.Email,
                $"Request {req.RequestNumber} approved",
                $@"<p>Dear {u.DisplayName},</p>
                   <p>Your request <b>{req.RequestNumber}</b> was approved by your Line Manager and forwarded to Security.</p>
                   <p><a href=""{link}"">View Details</a></p>");
            if (!res1.Succeeded) _log.LogError("Approve email (requester) failed: {Err}", res1.Error);
        }

        // Security group
        foreach (var s in _ad.GetUsersInGroup("RM_Security").Where(x => !string.IsNullOrWhiteSpace(x.Email)))
        {
            var inbox = url.Action("Index", "Security", null, url.ActionContext.HttpContext.Request.Scheme) ?? "";
            var res2 = await _email.SendAsync(
                s.Email,
                $"Request {req.RequestNumber} awaiting Security review",
                $@"<p>Dear {s.DisplayName},</p>
                   <p>Request <b>{req.RequestNumber}</b> is ready for your review.</p>
                   <p><a href=""{inbox}"">Open Security Inbox</a></p>");
            if (!res2.Succeeded) _log.LogError("Approve email (security) failed: {Err}", res2.Error);
        }
    }

    public async Task RejectedAsync(MediaAccessRequest req, string rejectedBy, string? notes, IUrlHelper url)
    {
        var u = _ad.GetUser(req.CreatedBySam);
        if (string.IsNullOrWhiteSpace(u?.Email)) { _log.LogWarning("Requester has no email"); return; }

        var link = url.Action("Details", "Requests", new { id = req.Id },
                              url.ActionContext.HttpContext.Request.Scheme) ?? "";
        var res = await _email.SendAsync(
            u.Email,
            $"Request {req.RequestNumber} was rejected",
            $@"<p>Dear {u.DisplayName},</p>
               <p>Your request <b>{req.RequestNumber}</b> was rejected by {rejectedBy}.</p>
               <p><b>Notes:</b> {(string.IsNullOrWhiteSpace(notes) ? "(no notes)" : notes)}</p>
               <p><a href=""{link}"">View Details</a></p>");
        if (!res.Succeeded) _log.LogError("Reject email failed: {Err}", res.Error);
    }
}
