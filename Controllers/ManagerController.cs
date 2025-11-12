using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RMPortal.Data;
using RMPortal.Models;
using RMPortal.Services; // IFakeAdService, IEmailService, IWorkflowNotifier

[Authorize(Policy = "IsManager")]
public class ManagerController : Controller
{
    private readonly AppDbContext _db;
    private readonly IFakeAdService _ad;
    private readonly IEmailService _email;
    private readonly IWorkflowNotifier _notifier;

    public ManagerController(AppDbContext db, IFakeAdService ad, IEmailService email, IWorkflowNotifier notifier)
    {
        _db = db;
        _ad = ad;
        _email = email;
        _notifier = notifier;
    }

    private string CurrentSam => User.FindFirstValue("sam") ?? string.Empty;

    // الطلبات بانتظار المدير
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var items = await _db.Requests
            .Where(r => r.Status == RequestStatus.Submitted)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return View(items);
    }

    // التفاصيل
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var req = await _db.Requests
            .Include(r => r.Decisions)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (req == null) return NotFound();
        return View(req);
    }

    // ======= APPROVE: إلى قسم IT مباشرة + بريد للمستخدم عبر Notifier فقط =======
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, string? notes)
    {
        var req = await _db.Requests.FindAsync(id);
        if (req == null || req.Status != RequestStatus.Submitted) return BadRequest();

        // تغيير الحالة إلى ManagerApproved
        req.Status = RequestStatus.ManagerApproved;
        req.ManagerSignAt = DateTime.UtcNow;

        _db.RequestDecisions.Add(new RequestDecision
        {
            MediaAccessRequestId = req.Id,
            Stage = "Manager",
            Decision = "Approved",
            Notes = notes,
            DecidedBySam = CurrentSam,
            DecidedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        // بريد المستخدم (موحّد عبر الـ Notifier فقط)
        await _notifier.ManagerApprovedAsync(req, Url);

        // إشعار IT
        await NotifyIT(req);

        return RedirectToAction(nameof(Index));
    }

    // ======= REJECT: بريد رفض للمستخدم عبر Notifier فقط =======
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string? notes)
    {
        var req = await _db.Requests.FindAsync(id);
        if (req == null || (req.Status != RequestStatus.Submitted && req.Status != RequestStatus.OnHold))
            return BadRequest();

        req.Status = RequestStatus.Rejected;

        _db.RequestDecisions.Add(new RequestDecision
        {
            MediaAccessRequestId = req.Id,
            Stage = "Manager",
            Decision = "Rejected",
            Notes = notes,
            DecidedBySam = CurrentSam,
            DecidedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        // بريد المستخدم (موحّد عبر الـ Notifier فقط)
        await _notifier.RejectedAsync(req, rejectedBy: "Line Manager", notes, Url);

        return RedirectToAction(nameof(Index));
    }

    // ======= DELAY: تعليق الطلب مؤقتًا (يبقى بريد بسيط للمستخدم هنا) =======
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delay(int id, string? notes)
    {
        var req = await _db.Requests.FindAsync(id);
        if (req == null || req.Status != RequestStatus.Submitted) return BadRequest();

        req.Status = RequestStatus.OnHold;

        _db.RequestDecisions.Add(new RequestDecision
        {
            MediaAccessRequestId = req.Id,
            Stage = "Manager",
            Decision = "Delayed",
            Notes = notes,
            DecidedBySam = CurrentSam,
            DecidedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        // بريد توضيحي للمستخدم (هذا الحدث فقط يبقى هنا)
        await NotifyRequester(req, "put on hold by your Line Manager. This is not a rejection. Notes: " + (notes ?? "(no notes)"));
        return RedirectToAction(nameof(Index));
    }

    // ===== Helpers =====

    // إشعار IT (RM_ITAdmins)
    private async Task NotifyIT(MediaAccessRequest req)
    {
        var itUsers = _ad.GetUsersInGroup("RM_ITAdmins");
        foreach (var u in itUsers.Where(u => !string.IsNullOrWhiteSpace(u.Email)))
        {
            var url = Url.Action("Index", "IT", new { area = "Approvals" }, Request.Scheme) ?? "";
            await _email.SendAsync(
                u.Email,
                $"Request {req.RequestNumber} awaiting IT action",
                $@"<p>Dear {u.DisplayName},</p>
                   <p>Request <b>{req.RequestNumber}</b> has been approved by Line Manager and is ready for IT action.</p>
                   <p><a href=""{url}"">Open IT Inbox</a></p>");
        }
    }

    // بريد بسيط للمستخدم (يُستخدم حاليًا في Delay فقط)
    private async Task NotifyRequester(MediaAccessRequest req, string message)
    {
        var requester = _ad.GetUser(req.CreatedBySam);
        if (requester != null && !string.IsNullOrWhiteSpace(requester.Email))
        {
            var detailsUrl = Url.Action("Details", "Requests", new { id = req.Id }, Request.Scheme) ?? "";
            await _email.SendAsync(
                requester.Email,
                $"Update on your request {req.RequestNumber}",
                $@"<p>Dear {requester.DisplayName},</p>
                   <p>Your request <b>{req.RequestNumber}</b> was {System.Net.WebUtility.HtmlEncode(message)}</p>
                   <p><a href=""{detailsUrl}"">View Details</a></p>");
        }
    }
}
