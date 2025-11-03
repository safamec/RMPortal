using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RMPortal.Services;

[Authorize(Roles="RM_LineManagers")]
public class ManagerController : Controller
{
    private readonly AppDbContext _db;
    private readonly IFakeAdService _ad;
    private readonly IEmailService _email;

    public ManagerController(AppDbContext db, IFakeAdService ad, IEmailService email)
    {
        _db = db; _ad = ad; _email = email;
    }

    // Dashboard: list submitted requests waiting manager
    public async Task<IActionResult> Index()
    {
        var list = await _db.Requests
            .Where(r => r.Status == RequestStatus.Submitted)
            .ToListAsync();

        return View(list);
    }

    [HttpPost]
    public async Task<IActionResult> Approve(int id)
    {
        var req = await _db.Requests.FindAsync(id);
        if (req == null || req.Status != RequestStatus.Submitted)
            return NotFound();

        req.Status = RequestStatus.ManagerApproved;
        req.ManagerSignAt = DateTime.UtcNow;

        _db.RequestDecisions.Add(new RequestDecision {
            MediaAccessRequestId = id,
            Stage = "Manager",
            Decision = "Approved",
            DecidedBySam = req.CreatedBySam,
            DecidedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> Reject(int id)
    {
        var req = await _db.Requests.FindAsync(id);
        if (req == null || req.Status != RequestStatus.Submitted)
            return NotFound();

        req.Status = RequestStatus.Rejected;

        _db.RequestDecisions.Add(new RequestDecision {
            MediaAccessRequestId = id,
            Stage = "Manager",
            Decision = "Rejected",
            DecidedBySam = req.CreatedBySam,
            DecidedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return RedirectToAction("Index");
    }
}
