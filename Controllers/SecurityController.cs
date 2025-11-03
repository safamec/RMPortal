using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RMPortal.Data;
using RMPortal.Services;

[Area("Approvals")]
[Authorize(Roles="RM_Security")]
public class SecurityController : Controller
{
    private readonly AppDbContext _db;
    private readonly RMPortal.Services.IFakeAdService _ad;
    private readonly IEmailService _email;
    private readonly IHubContext<NotificationHub> _hub;

    public SecurityController(AppDbContext db, RMPortal.Services.IFakeAdService ad, IEmailService email, IHubContext<NotificationHub> hub)
    { _db = db; _ad = ad; _email = email; _hub = hub; }

    public async Task<IActionResult> Index()
        => View(await _db.Requests.Where(r => r.Status == RequestStatus.ManagerApproved).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Approve(int id, string? notes)
    {
        var req = await _db.Requests.FindAsync(id);
        if (req is null || req.Status != RequestStatus.ManagerApproved) return BadRequest();

        req.Status = RequestStatus.SecurityApproved;
        req.SecuritySignAt = DateTime.UtcNow;
        _db.RequestDecisions.Add(new RequestDecision {
            MediaAccessRequestId = id, Stage = "Security", Decision = "Approve",
            Notes = notes, DecidedBySam = User.FindFirstValue("sam")
        });
        await _db.SaveChangesAsync();

        // Notify requester
        var requester = _ad.GetUser(req.CreatedBySam);
        if (requester is not null)
            await _email.SendAsync(requester.Email,
                $"Update: {req.RequestNumber} approved by Security",
                $"<p>Your request moved to <b>IT</b> for completion.</p>");

        // Notify IT group
        foreach (var it in _ad.GetUsersInGroup("RM_ITAdmins"))
            await _email.SendAsync(it.Email,
                $"Request {req.RequestNumber} ready for IT completion",
                $"<p>Please perform device enablement and close.</p>");

        await _hub.Clients.Group("RM_ITAdmins").SendAsync("toast", $"Req {req.RequestNumber} ready for IT.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Reject(int id, string? notes)
    {
        var req = await _db.Requests.FindAsync(id);
        if (req is null || req.Status != RequestStatus.ManagerApproved) return BadRequest();

        req.Status = RequestStatus.Rejected;
        _db.RequestDecisions.Add(new RequestDecision {
            MediaAccessRequestId = id, Stage = "Security", Decision = "Reject",
            Notes = notes, DecidedBySam = User.FindFirstValue("sam")
        });
        await _db.SaveChangesAsync();

        var requester = _ad.GetUser(req.CreatedBySam);
        if (requester is not null)
            await _email.SendAsync(requester.Email,
                $"Request {req.RequestNumber} was Rejected by Security",
                $"<p>Reason: {System.Net.WebUtility.HtmlEncode(notes ?? "")}</p>");

        return RedirectToAction(nameof(Index));
    }
}
