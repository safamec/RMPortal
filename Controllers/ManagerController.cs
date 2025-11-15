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
    private readonly IWorkflowNotifier _notifier;

    public ManagerController(AppDbContext db, IFakeAdService ad, IWorkflowNotifier notifier)
    {
        _db = db;
        _ad = ad;
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

        if (req == null)
            return NotFound();

        return View(req);
    }

    // ========== APPROVE ==========
    // Manager Approved → goes to Security
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, string? notes)
    {
        var req = await _db.Requests.FindAsync(id);
        if (req == null || req.Status != RequestStatus.Submitted)
            return BadRequest();

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

        // إيميل موحّد (Template) عبر الـ Notifier
        await _notifier.ManagerApprovedAsync(req, Url);

        // (لا يوجد إرسال إيميل مباشر من هنا بعد الآن)

        return RedirectToAction(nameof(Index));
    }

    // ========== REJECT ==========
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

        // Template رفض موحّد عبر الـ Notifier
        await _notifier.RejectedAsync(req, rejectedBy: "Line Manager", notes, Url);

        // (لا يوجد إرسال إيميل مباشر من هنا بعد الآن)

        return RedirectToAction(nameof(Index));
    }

    // ========== DELAY / ON-HOLD ==========
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delay(int id, string? notes)
    {
        var req = await _db.Requests.FindAsync(id);
        if (req == null || req.Status != RequestStatus.Submitted)
            return BadRequest();

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

        // (لا يوجد إرسال إيميل Delay من الكنترولر بعد الآن)

        return RedirectToAction(nameof(Index));
    }
}
