using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RMPortal.Data;
using RMPortal.Models;   // MediaAccessRequest, RequestDecision, RequestStatus
using RMPortal.Services; // IFakeAdService, IEmailService, IWorkflowNotifier

namespace RMPortal.Controllers
{
    [Authorize]
    public class RequestsController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IFakeAdService _ad;
        private readonly IHubContext<NotificationHub> _hub;
        private readonly IWorkflowNotifier _notifier;

        public RequestsController(
            AppDbContext db,
            IFakeAdService ad,
            IHubContext<NotificationHub> hub,
            IWorkflowNotifier notifier)
        {
            _db = db;
            _ad = ad;
            _hub = hub;
            _notifier = notifier;
        }

        // GET: /Requests/Create
        public IActionResult Create()
        {
            var sam = GetCurrentSam();
            var adUser = _ad.GetUser(sam);

            var vm = new MediaAccessRequest
            {
                EmploymentStatus = "EMPLOYEE",
                Name       = adUser?.DisplayName ?? "",
                Department = adUser?.Department,
                LoginName  = adUser?.Sam ?? sam,
                RequestNumber = $"RM-{DateTime.UtcNow:yyyyMMddHHmmss}"
            };
            return View(vm);
        }

        // POST: /Requests/Create (Save or Submit)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MediaAccessRequest model, string action)
        {
            var sam = GetCurrentSam();

            if (!ModelState.IsValid)
                return View(model);

            if (string.IsNullOrWhiteSpace(model.RequestNumber))
                model.RequestNumber = $"RM-{DateTime.UtcNow:yyyyMMddHHmmss}";

            model.CreatedBySam = sam;
            model.Status = RequestStatus.Draft;

            _db.Requests.Add(model);
            await _db.SaveChangesAsync();

            if (string.Equals(action, "Submit", StringComparison.OrdinalIgnoreCase))
            {
                return await Submit(model.Id, model.ConfirmDeclaration);
            }

            return RedirectToAction(nameof(Details), new { id = model.Id });
        }

        // POST: /Requests/Submit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(int id, bool confirmDeclaration)
        {
            var req = await _db.Requests.FindAsync(id);
            var sam = GetCurrentSam();

            // يُسمح بالإرسال مرة واحدة فقط عندما تكون Draft ومن نفس المستخدِم
            if (req == null || req.CreatedBySam != sam || req.Status != RequestStatus.Draft)
                return Forbid();

            if (!confirmDeclaration)
            {
                ModelState.AddModelError("", "You must check the declaration before submitting.");
                return View("Create", req);
            }

            if (!req.EndDate.HasValue || (req.StartDate.HasValue && req.EndDate < req.StartDate))
            {
                ModelState.AddModelError("", "End Date is required and must be on/after Start Date.");
                return View("Create", req);
            }

            // الانتقال إلى Submitted + ختم توقيع مقدم الطلب
            req.Status = RequestStatus.Submitted;
            req.RequesterSignAt = DateTime.UtcNow;

            _db.RequestDecisions.Add(new RequestDecision
            {
                MediaAccessRequestId = req.Id,
                Stage = "Requester",
                Decision = "Submitted",
                DecidedBySam = sam,
                DecidedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();

            // إشعار موحّد للطالب (بدون أي ذكر لـ Security)
            await _notifier.RequestSubmittedAsync(req, Url);

            // إرسال إيميل للمدير مع روابط Approve/Reject (من الـ WorkflowNotifier)
            if (_notifier is WorkflowNotifier wf)
            {
                await wf.SendManagerReviewEmailAsync(req, Url);
            }

            // إشعار المدير عبر توست لمجموعة المدراء (بدون إيميل من الكنترولر)
            var managerSam = _ad.GetManagerSam(req.CreatedBySam);
            if (!string.IsNullOrWhiteSpace(managerSam))
            {
                await _hub.Clients.Group("RM_LineManagers")
                    .SendAsync("toast", $"Req {req.RequestNumber} needs your review.");
            }

            TempData["Success"] = $"Request {req.RequestNumber} submitted successfully.";
            return RedirectToAction("Index", "Home");
        }

        // GET: /Requests/Details/{id}
        public async Task<IActionResult> Details(int id)
        {
            var req = await _db.Requests
                .Include(r => r.Decisions)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (req == null)
                return NotFound();

            return View(req);
        }

        private string GetCurrentSam() => User.FindFirstValue("sam") ?? string.Empty;
    }
}
