using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RMPortal.Services;
using RMPortal.Services; // <-- required
[Area("Approvals")]
[Authorize(Roles="RM_ITAdmins")]
public class ITController : Controller
{
    private readonly AppDbContext _db;
    private readonly IFakeAdService _ad;
    private readonly IEmailService _email;

    public ITController(AppDbContext db, IFakeAdService ad, IEmailService email)
    { _db = db; _ad = ad; _email = email; }

    public async Task<IActionResult> Index()
        => View(await _db.Requests.Where(r => r.Status == RequestStatus.SecurityApproved).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Complete(int id, string? actionTaken, string? crq)
    {
        var req = await _db.Requests.FindAsync(id);
        if (req is null || req.Status != RequestStatus.SecurityApproved) return BadRequest();

        req.Status = RequestStatus.Completed;
        req.ITSignAt = DateTime.UtcNow;
        _db.RequestDecisions.Add(new RequestDecision {
            MediaAccessRequestId = id, Stage = "IT", Decision = "Complete",
            Notes = $"Action: {actionTaken ?? "-"}, CRQ: {crq ?? "-"}",
            DecidedBySam = User.FindFirstValue("sam")
        });
        await _db.SaveChangesAsync();

        // Final email to requester (and optionally manager + security)
        var user = _ad.GetUser(req.CreatedBySam);
        if (user is not null)
            await _email.SendAsync(user.Email,
                $"Completed: {req.RequestNumber}",
                $"<p>Your request is <b>Completed</b>. Action: {System.Net.WebUtility.HtmlEncode(actionTaken ?? "-")}</p>");

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Reject(int id, string? notes)
    {
        var req = await _db.Requests.FindAsync(id);
        if (req is null || req.Status != RequestStatus.SecurityApproved) return BadRequest();

        req.Status = RequestStatus.Rejected;
        _db.RequestDecisions.Add(new RequestDecision {
            MediaAccessRequestId = id, Stage = "IT", Decision = "Reject",
            Notes = notes, DecidedBySam = User.FindFirstValue("sam")
        });
        await _db.SaveChangesAsync();

        var user = _ad.GetUser(req.CreatedBySam);
        if (user is not null)
            await _email.SendAsync(user.Email,
                $"Request {req.RequestNumber} was Rejected by IT",
                $"<p>Reason: {System.Net.WebUtility.HtmlEncode(notes ?? "")}</p>");

        return RedirectToAction(nameof(Index));
    }
}
