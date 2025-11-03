using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RMPortal.Data;
using RMPortal.Models;
using RMPortal.Services;

[Authorize(Policy = "IsManager")]
public class ManagerController : Controller
{
    private readonly AppDbContext _db;
    private readonly IFakeAdService _ad;
    private readonly IEmailService _email;

    public ManagerController(AppDbContext db, IFakeAdService ad, IEmailService email)
    {
        _db = db; _ad = ad; _email = email;
    }

    private string CurrentSam => User.FindFirstValue("sam") ?? string.Empty;

    // قائمة الطلبات بانتظار المدير
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        // الأبسط الآن: كل Submitted تظهر لكل مدير
        // لاحقًا نقدر نحفظ ManagerSam في الطلب ونفلتر عليه
        var items = await _db.Requests
            .Where(r => r.Status == RequestStatus.Submitted)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return View(items);
    }

    // عرض التفاصيل (اختياري – نقدر نعيد استخدام View Details الموجودة)
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var req = await _db.Requests.Include(r => r.Decisions).FirstOrDefaultAsync(r => r.Id == id);
        if (req == null) return NotFound();
        return View(req); // أنشئ View أو أعيدي استخدام Requests/Details
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, string? notes)
    {
        var req = await _db.Requests.FindAsync(id);
        if (req == null || req.Status != RequestStatus.Submitted) return BadRequest();

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

        // إشعار الأمن (Security) + المرسل
        await NotifySecurity(req);
        await NotifyRequester(req, "approved by your Line Manager and forwarded to Security.");

        return RedirectToAction(nameof(Index));
    }

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

        await NotifyRequester(req, "rejected by your Line Manager.");
        return RedirectToAction(nameof(Index));
    }

    // Delay → OnHold + إيميل للطالب يخبره بالسبب (ويقدر لاحقاً يعدل ويعيد إرسال — لو أردتِ لاحقاً)
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

        // إيميل للطالب بسبب التأجيل
        await NotifyRequester(req, "put on hold by your Line Manager. Notes: " + (notes ?? "(no notes)"));
        return RedirectToAction(nameof(Index));
    }

    private async Task NotifySecurity(MediaAccessRequest req)
    {
        // كل أعضاء مجموعة الأمن
        var secUsers = _ad.GetUsersInGroup("RM_Security");
        foreach (var u in secUsers.Where(u => !string.IsNullOrWhiteSpace(u.Email)))
        {
            var url = Url.Action("Index", "Security", null, Request.Scheme) ?? "";
            await _email.SendAsync(
                u.Email,
                $"Request {req.RequestNumber} awaiting Security review",
                $@"<p>Dear {u.DisplayName},</p>
                   <p>Request <b>{req.RequestNumber}</b> is ready for your review.</p>
                   <p><a href=""{url}"">Open Security Inbox</a></p>");
        }
    }

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
                   <p>Your request <b>{req.RequestNumber}</b> was {message}</p>
                   <p><a href=""{detailsUrl}"">View Details</a></p>");
        }
    }
}
