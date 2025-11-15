using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RMPortal.Data;
using RMPortal.Models;   // MediaAccessRequest, RequestDecision, RequestStatus
using RMPortal.Services; // IWorkflowNotifier

namespace RMPortal.Controllers
{
    /// <summary>
    /// كل روابط الموافقة / الرفض عن طريق الإيميل
    /// للمدير + السيكيورتي + IT في كنترولر واحد.
    /// </summary>
    [AllowAnonymous]
    public class WorkflowEmailController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IWorkflowNotifier _notifier;

        public WorkflowEmailController(AppDbContext db, IWorkflowNotifier notifier)
        {
            _db = db;
            _notifier = notifier;
        }

        // ================= 1) Manager via Email =================
        // مثال رابط:
        // /WorkflowEmail/ManagerAction?reqId=5&emailAction=approve
        [HttpGet]
        public async Task<IActionResult> ManagerAction(int reqId, string emailAction)
        {
            var req = await _db.Requests.FindAsync(reqId);
            if (req == null)
                return Content("Request not found.");

            string decisionNotes;

            switch (emailAction?.ToLower())
            {
                case "approve":
                    req.Status = RequestStatus.ManagerApproved;
                    req.ManagerSignAt = DateTime.UtcNow;
                    decisionNotes = "Approved by Line Manager via email link.";
                    break;

                case "reject":
                    req.Status = RequestStatus.Rejected;
                    decisionNotes = "Rejected by Line Manager via email link.";
                    break;

                default:
                    return Content("Invalid action.");
            }

            _db.RequestDecisions.Add(new RequestDecision
            {
                MediaAccessRequestId = req.Id,
                Stage = "Manager",
                Decision = req.Status.ToString(),
                Notes = decisionNotes,
                DecidedBySam = "(via email)",
                DecidedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();

            if (req.Status == RequestStatus.ManagerApproved)
            {
                await _notifier.ManagerApprovedAsync(req, Url);
            }
            else
            {
                await _notifier.RejectedAsync(req, "Line Manager", decisionNotes, Url);
            }

            return Content($"✅ Request {req.RequestNumber} has been {emailAction} successfully by Manager.");
        }

        // ================= 2) Security via Email =================
        // /WorkflowEmail/SecurityAction?reqId=5&emailAction=approve
        [HttpGet]
        public async Task<IActionResult> SecurityAction(int reqId, string emailAction)
        {
            var req = await _db.Requests.FindAsync(reqId);
            if (req == null)
                return Content("Request not found.");

            string decisionNotes;

            switch (emailAction?.ToLower())
            {
                case "approve":
                    req.Status = RequestStatus.SecurityApproved;
                    decisionNotes = "Approved by Security via email link.";
                    break;

                case "reject":
                    req.Status = RequestStatus.Rejected;
                    decisionNotes = "Rejected by Security via email link.";
                    break;

                default:
                    return Content("Invalid action.");
            }

            _db.RequestDecisions.Add(new RequestDecision
            {
                MediaAccessRequestId = req.Id,
                Stage = "Security",
                Decision = req.Status.ToString(),
                Notes = decisionNotes,
                DecidedBySam = "(via email)",
                DecidedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();

            if (_notifier is IExtendedWorkflowNotifier ext &&
                req.Status == RequestStatus.SecurityApproved)
            {
                await ext.SecurityApprovedAsync(req, Url);
            }
            else
            {
                await _notifier.RejectedAsync(req, "Security", decisionNotes, Url);
            }

            return Content($"✅ Request {req.RequestNumber} has been {emailAction} successfully by Security.");
        }

        // ================= 3) IT via Email =================
        // /WorkflowEmail/ITAction?reqId=5&emailAction=complete
        [HttpGet]
        public async Task<IActionResult> ITAction(int reqId, string emailAction)
        {
            var req = await _db.Requests.FindAsync(reqId);
            if (req == null)
                return Content("Request not found.");

            string decisionNotes;

            switch (emailAction?.ToLower())
            {
                case "complete":
                case "approve":
                    req.Status = RequestStatus.Completed;
                    decisionNotes = "Completed by IT via email link.";
                    break;

                case "reject":
                    req.Status = RequestStatus.Rejected;
                    decisionNotes = "Rejected by IT via email link.";
                    break;

                default:
                    return Content("Invalid action.");
            }

            _db.RequestDecisions.Add(new RequestDecision
            {
                MediaAccessRequestId = req.Id,
                Stage = "IT",
                Decision = req.Status.ToString(),
                Notes = decisionNotes,
                DecidedBySam = "(via email)",
                DecidedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();

            if (_notifier is IExtendedWorkflowNotifier ext &&
                req.Status == RequestStatus.Completed)
            {
                await ext.ITCompletedAsync(req, Url);
            }
            else
            {
                await _notifier.RejectedAsync(req, "IT Department", decisionNotes, Url);
            }

            return Content($"✅ Request {req.RequestNumber} has been {emailAction} successfully by IT.");
        }
    }
}
