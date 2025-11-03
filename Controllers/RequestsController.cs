using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RMPortal.Data;
using RMPortal.Models;   // MediaAccessRequest, RequestDecision, RequestStatus
using RMPortal.Services; // IFakeAdService, IEmailService

namespace RMPortal.Controllers
{
    [Authorize]
    public class RequestsController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IFakeAdService _ad;
        private readonly IEmailService _email;
        private readonly IHubContext<NotificationHub> _hub;

        public RequestsController(
            AppDbContext db,
            IFakeAdService ad,
            IEmailService email,
            IHubContext<NotificationHub> hub)
        {
            _db = db; _ad = ad; _email = email; _hub = hub;
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

                // إظهار رقم الطلب داخل الفورم
                RequestNumber = $"RM-{DateTime.UtcNow:yyyyMMddHHmmss}"
            };
            return View(vm);
        }

        // POST: /Requests/Create  (Save أو Submit)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MediaAccessRequest model, string action)
        {
            var sam = GetCurrentSam();

            if (!ModelState.IsValid)
                return View(model);

            // لو مفقود لأي سبب (Refresh) نولّده
            if (string.IsNullOrWhiteSpace(model.RequestNumber))
                model.RequestNumber = $"RM-{DateTime.UtcNow:yyyyMMddHHmmss}";

            model.CreatedBySam = sam;
            model.Status = RequestStatus.Draft;

            _db.Requests.Add(model);
            await _db.SaveChangesAsync();

            if (string.Equals(action, "Submit", StringComparison.OrdinalIgnoreCase))
            {
                // مرّر قيمة checkbox بشكل صريح
                return await Submit(model.Id, model.ConfirmDeclaration);
            }

            // Save Draft => Details
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }

        // POST: /Requests/Submit/{id}
        // ملاحظة: نأخذ confirmDeclaration كـ باراميتر لضمان القراءة الصحيحة من الفورم
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(int id, bool confirmDeclaration)
        {
            var req = await _db.Requests.FindAsync(id);
            var sam = GetCurrentSam();

            // يُسمح بالإرسال مرة واحدة فقط: عندما تكون Draft فقط
            if (req == null || req.CreatedBySam != sam || req.Status != RequestStatus.Draft)
                return Forbid();

            // checkbox لازم يكون مؤشّر عند الإرسال
            if (!confirmDeclaration)
            {
                ModelState.AddModelError("", "You must check the declaration before submitting.");
                return View("Create", req);
            }

            // التواريخ: EndDate مطلوب وأن لا يكون قبل StartDate
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

            // إشعار المدير (اختياري)
            var managerSam = _ad.GetManagerSam(req.CreatedBySam);
            if (!string.IsNullOrWhiteSpace(managerSam))
            {
                var mgr = _ad.GetUser(managerSam);
                if (mgr != null && !string.IsNullOrWhiteSpace(mgr.Email))
                {
                    var approvalsUrl = Url.Action("Index", "Manager", null, Request.Scheme) ?? "";
                    await _email.SendAsync(
                        mgr.Email,
                        $"Request {req.RequestNumber} awaiting your review",
                        $@"<p>Dear {mgr.DisplayName},</p>
                           <p>Request <b>{req.RequestNumber}</b> awaits your approval.</p>
                           <p><a href=""{approvalsUrl}"">Open Approvals</a></p>");
                }

                await _hub.Clients.Group("RM_LineManagers")
                    .SendAsync("toast", $"Req {req.RequestNumber} needs your review.");
            }

            // تأكيد للمستخدم
            var requester = _ad.GetUser(req.CreatedBySam);
            if (requester != null && !string.IsNullOrWhiteSpace(requester.Email))
            {
                var detailsUrl = Url.Action(nameof(Details), "Requests", new { id = req.Id }, Request.Scheme) ?? "";
                await _email.SendAsync(
                    requester.Email,
                    $"Your request {req.RequestNumber} was submitted",
                    $@"<p>Dear {requester.DisplayName},</p>
                       <p>Your request <b>{req.RequestNumber}</b> has been submitted.</p>
                       <p><a href=""{detailsUrl}"">Track it here</a></p>");
            }

            // رسالة نجاح والعودة للـ Home
            TempData["Success"] = $"Request {req.RequestNumber} submitted successfully.";
            // Email confirmation to requester
if (requester != null && !string.IsNullOrWhiteSpace(requester.Email))
{
    var detailsUrl = Url.Action(nameof(Details), "Requests", new { id = req.Id }, Request.Scheme) ?? "";
    await _email.SendAsync(
        requester.Email,
        $"Your request {req.RequestNumber} was submitted successfully ✅",
        $@"<p>Dear {requester.DisplayName},</p>
           <p>Your Removable Media Access Request <b>{req.RequestNumber}</b> has been <b>successfully submitted</b>.</p>
           <p>You can view the status anytime here:</p>
           <p><a href=""{detailsUrl}"">{detailsUrl}</a></p>
           <p>Regards,<br/>RMPortal System</p>"
    );
}

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
