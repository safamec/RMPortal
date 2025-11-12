using Microsoft.AspNetCore.Mvc;
using RMPortal.Data;
using RMPortal.Models;
using System.Linq;

namespace RMPortal.Controllers
{
    public class DashboardController : Controller
    {
        private readonly AppDbContext _context;

        public DashboardController(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            // إحصائيات رئيسية
            var pendingCount = _context.Requests
                .Count(r => r.Status == RequestStatus.Submitted || r.Status == RequestStatus.OnHold);

            var approvedCount = _context.Requests
                .Count(r => r.Status == RequestStatus.ManagerApproved || 
                            r.Status == RequestStatus.SecurityApproved || 
                            r.Status == RequestStatus.Completed);

            var expiredCount = _context.Requests
                .Count(r => r.EndDate < DateTime.UtcNow);

            // أحدث الطلبات
            var recent = _context.Requests
                .OrderByDescending(r => r.CreatedAt)
                .Take(10)
                .ToList();

            // عدد الطلبات لكل قسم
            var requestsByDept = _context.Requests
                .Where(r => r.Department != null)
                .AsEnumerable()
                .GroupBy(r => r.Department!)
                .Select(g => new { Department = g.Key, Count = g.Count() })
                .ToList();

            // عدد الطلبات المرفوضة
            var rejectedByDept = _context.Requests
                .Where(r => r.Status == RequestStatus.Rejected && r.Department != null)
                .AsEnumerable()
                .GroupBy(r => r.Department!)
                .Select(g => new { Department = g.Key, Count = g.Count() })
                .ToList();

            var model = new DashboardViewModel
            {
                PendingCount = pendingCount,
                ApprovedCount = approvedCount,
                ExpiredCount = expiredCount,
                RecentRequests = recent,
                RequestsByDepartment = requestsByDept
                    .Select(x => (x.Department ?? "غير محدد", x.Count))
                    .ToList(),
                RejectedByDepartment = rejectedByDept
                    .Select(x => (x.Department ?? "غير محدد", x.Count))
                    .ToList()
            };

            return View(model);
        }
    }
}