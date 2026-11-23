using System.ComponentModel.DataAnnotations;
using StudentIncidentSystem.Models;

namespace StudentIncidentSystem.ViewModels
{
    // One pager used by every list in this stage. Query holds the current filters.
    public record GenericPagerInfo(int Page, int TotalPages, Dictionary<string, string> Query);

    // ---------- Attachments ----------

    public class AttachmentItem
    {
        public int Id { get; set; }
        public string OriginalFileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public DateTime UploadedAtUtc { get; set; }
    }

    public class AttachmentsPanelViewModel
    {
        public int ReportId { get; set; }
        public List<AttachmentItem> Items { get; set; } = new();
        public bool CanUpload { get; set; }
        public bool IsStudent { get; set; }
        public bool IsClosed { get; set; }
        public int RemainingSlots { get; set; }
        public int MaxFileMb { get; set; }
        public int MaxFiles { get; set; }
    }

    // ---------- Reports ----------

    public class ReportFilter
    {
        public string? Q { get; set; }
        public IncidentStatus? Status { get; set; }
        public int? CategoryId { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public string? AssignedTo { get; set; }
    }

    public record CountRow(string Label, int Count);

    public class ReportRow
    {
        public int Id { get; set; }
        public string ReferenceNumber { get; set; } = string.Empty;
        public string? Title { get; set; }          // always null for administrators
        public string CategoryName { get; set; } = string.Empty;
        public IncidentStatus Status { get; set; }
        public string? AssignedToName { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? ResolvedAtUtc { get; set; }
    }

    public class ReportsViewModel
    {
        public ReportFilter Filter { get; set; } = new();
        public bool IsAdmin { get; set; }
        public PagedResult<ReportRow> Rows { get; set; } = new();
        public int Total { get; set; }
        public int OpenCount { get; set; }
        public int ResolvedCount { get; set; }
        public List<CountRow> ByStatus { get; set; } = new();
        public List<CountRow> ByCategory { get; set; } = new();
        public List<CountRow> ByMonth { get; set; } = new();
        public List<CategoryOption> Categories { get; set; } = new();
        public List<StaffOption> AssignOptions { get; set; } = new();
    }

    // ---------- Audit logs ----------

    public class AuditLogIndexViewModel
    {
        public PagedResult<AuditLog> Logs { get; set; } = new();
        public string? Search { get; set; }
        public string? Outcome { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
    }

    // ---------- Profile ----------

    public class ProfileViewModel
    {
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? SchoolIdNumber { get; set; }
        public string? Role { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    public class UpdateOwnProfileViewModel
    {
        [Required, StringLength(100)]
        public string FullName { get; set; } = string.Empty;
    }

    public class ChangePasswordViewModel
    {
        [Required]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required, StringLength(100, MinimumLength = 8)]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    // ---------- Settings ----------

    public class SettingsViewModel
    {
        [Required, StringLength(60)]
        [Display(Name = "School or system name")]
        public string SchoolName { get; set; } = string.Empty;

        [Range(1, 10, ErrorMessage = "Choose a size from 1 to 10 MB.")]
        [Display(Name = "Maximum file size (MB)")]
        public int MaxFileMb { get; set; } = 5;

        [Range(1, 10, ErrorMessage = "Choose from 1 to 10 files.")]
        [Display(Name = "Maximum files per report")]
        public int MaxFilesPerReport { get; set; } = 5;
    }
}
