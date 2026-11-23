using System.ComponentModel.DataAnnotations;
using StudentIncidentSystem.Models;

namespace StudentIncidentSystem.ViewModels
{
    public record StaffPagerInfo(int Page, int TotalPages, string? Q,
                                 IncidentStatus? Status, int? CategoryId, string? Scope);

    public class StaffIndexViewModel
    {
        public PagedResult<ReportListItem> Cases { get; set; } = new();
        public string? Search { get; set; }
        public IncidentStatus? StatusFilter { get; set; }
        public int? CategoryFilter { get; set; }
        public string Scope { get; set; } = "all";
        public List<CategoryOption> Categories { get; set; } = new();
    }

    public class StaffMessageItem
    {
        public string Body { get; set; } = string.Empty;
        public DateTime SentAtUtc { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public bool FromStudent { get; set; }
        public bool IsInformationRequest { get; set; }
    }

    public class StaffNoteItem
    {
        public string AuthorName { get; set; } = string.Empty;
        public NoteType NoteType { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
    }

    public class StaffTimelineItem
    {
        public IncidentStatus? OldStatus { get; set; }
        public IncidentStatus NewStatus { get; set; }
        public DateTime ChangedAtUtc { get; set; }
        public string ChangedByName { get; set; } = string.Empty;
        public string? Comment { get; set; }
        public bool CommentVisibleToStudent { get; set; }
    }

    public class StaffOption
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class StaffCaseDetailsViewModel
    {
        public int Id { get; set; }
        public string CurrentUserId { get; set; } = string.Empty;
        public string ReferenceNumber { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public DateTime IncidentDateTime { get; set; }
        public string Location { get; set; } = string.Empty;
        public string? PeopleInvolved { get; set; }
        public IncidentStatus Status { get; set; }
        public string? ResolutionSummary { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }

        public string ReporterName { get; set; } = string.Empty;
        public string? ReporterSchoolId { get; set; }

        public string? AssignedToId { get; set; }
        public string? AssignedToName { get; set; }
        public bool IsUnassigned => AssignedToId == null;
        public bool IsAssignedToMe => AssignedToId == CurrentUserId;

        public List<IncidentStatus> AllowedStatuses { get; set; } = new();
        public bool CanRequestInfo { get; set; }
        public bool CanSendMessage { get; set; }
        public bool CanResolve { get; set; }
        public bool CanAssign { get; set; }

        public List<StaffTimelineItem> Timeline { get; set; } = new();
        public List<StaffMessageItem> Messages { get; set; } = new();
        public List<StaffNoteItem> Notes { get; set; } = new();
        public List<StaffOption> StaffOptions { get; set; } = new();
    }

    public class ChangeStatusViewModel
    {
        [Required]
        public IncidentStatus? NewStatus { get; set; }

        [StringLength(500)]
        public string? Comment { get; set; }

        public bool VisibleToStudent { get; set; }
    }

    public class AssignViewModel
    {
        [Required]
        public string AssigneeId { get; set; } = string.Empty;
    }

    public class StaffMessageViewModel
    {
        [Required, StringLength(2000)]
        public string Body { get; set; } = string.Empty;
    }

    public class NoteViewModel
    {
        public NoteType NoteType { get; set; } = NoteType.Investigation;

        [Required, StringLength(4000)]
        public string Content { get; set; } = string.Empty;
    }

    public class ResolveViewModel
    {
        [Required, StringLength(2000, MinimumLength = 10,
            ErrorMessage = "Please write a resolution summary (at least 10 characters).")]
        public string ResolutionSummary { get; set; } = string.Empty;
    }
}
