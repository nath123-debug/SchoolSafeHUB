using StudentIncidentSystem.Models;

namespace StudentIncidentSystem.ViewModels
{
    public record StatCard(string Title, int Value, string Icon, string Color);

    public record NavItem(string Text, string Icon, string? Controller, string? Action,
                          string? AvailableInStage = null);

    public class ChartData
    {
        public List<string> Labels { get; set; } = new();
        public List<int> Values { get; set; } = new();
        public bool HasData => Values.Any(v => v > 0);
    }

    public class ReportListItem
    {
        public int Id { get; set; }
        public string ReferenceNumber { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public IncidentStatus Status { get; set; }
        public string? AssignedToName { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }

    public class StatusUpdateItem
    {
        public string ReferenceNumber { get; set; } = string.Empty;
        public IncidentStatus? OldStatus { get; set; }
        public IncidentStatus NewStatus { get; set; }
        public DateTime ChangedAtUtc { get; set; }
        public string? Comment { get; set; }
        public string? ChangedByName { get; set; }
    }

    public class CategoryOption
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class StudentDashboardViewModel
    {
        public int TotalReports { get; set; }
        public int UnderReview { get; set; }
        public int Resolved { get; set; }
        public List<ReportListItem> RecentReports { get; set; } = new();
        public List<StatusUpdateItem> RecentUpdates { get; set; } = new();
    }

    public class StaffDashboardViewModel
    {
        public int Submitted { get; set; }
        public int UnderReview { get; set; }
        public int AwaitingInformation { get; set; }
        public int Resolved { get; set; }
        public ChartData CategoryChart { get; set; } = new();
        public List<StatusUpdateItem> RecentActivity { get; set; } = new();

        public List<ReportListItem> Cases { get; set; } = new();
        public int TotalMatching { get; set; }
        public string? Search { get; set; }
        public IncidentStatus? StatusFilter { get; set; }
        public int? CategoryFilter { get; set; }
        public List<CategoryOption> Categories { get; set; } = new();
    }

    public class AdminDashboardViewModel
    {
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int TotalReports { get; set; }
        public int OpenCases { get; set; }
        public int ResolvedCases { get; set; }
        public ChartData CategoryChart { get; set; } = new();
        public ChartData MonthlyChart { get; set; } = new();
        public List<AuditLog> RecentActivity { get; set; } = new();
    }
}
