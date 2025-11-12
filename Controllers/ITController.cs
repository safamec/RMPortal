using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RMPortal.Data;
using RMPortal.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace RMPortal.Areas.Approvals.Controllers
{
    [Area("Approvals")]
    [Authorize(Roles = "RM_ITAdmins")]
    public class ITController : Controller
    {
        private readonly AppDbContext _context;

        public ITController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // إحصائيات خاصة بقسم الـ IT فقط
            var pendingCount = await _context.Requests
                .CountAsync(r => r.Status == RequestStatus.ManagerApproved);

            var completedCount = await _context.Requests
                .CountAsync(r => r.Status == RequestStatus.Completed);

            var rejectedCount = await _context.Requests
                .CountAsync(r => r.Status == RequestStatus.Rejected);

            // أحدث الطلبات التي تخص الـ IT
            var recent = await _context.Requests
                .Where(r => r.Status == RequestStatus.ManagerApproved
                         || r.Status == RequestStatus.Completed
                         || r.Status == RequestStatus.Rejected)
                .OrderByDescending(r => r.CreatedAt)
                .Take(10)
                .ToListAsync();

            // عدد الطلبات لكل قسم
            var byDept = await _context.Requests
                .Where(r => r.Department != null)
                .GroupBy(r => r.Department!)
                .Select(g => new { Department = g.Key, Count = g.Count() })
                .ToListAsync();

            var model = new DashboardViewModel
            {
                PendingCount = pendingCount,
                ApprovedCount = completedCount,
                ExpiredCount = rejectedCount, // نستخدمها فقط للعرض كعدد الطلبات المرفوضة
                RecentRequests = recent,
                RequestsByDepartment = byDept
                    .Select(x => (x.Department ?? "غير محدد", x.Count))
                    .ToList(),
                RejectedByDepartment = new()
            };

            return View(model);
        }
    }
}
