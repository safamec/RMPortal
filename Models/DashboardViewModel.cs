namespace RMPortal.Models
{
    public class DashboardViewModel
    {
        public int PendingCount { get; set; }
        public int ApprovedCount { get; set; }
        public int ExpiredCount { get; set; }

        public List<MediaAccessRequest> RecentRequests { get; set; } = new();

        public List<(string Department, int Count)> RequestsByDepartment { get; set; } = new();
        public List<(string Department, int Count)> RejectedByDepartment { get; set; } = new();
    }
}