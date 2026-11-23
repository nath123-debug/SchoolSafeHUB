using System.ComponentModel.DataAnnotations;

namespace StudentIncidentSystem.Models
{
    public class IncidentStatusHistory
    {
        public int Id { get; set; }

        public int IncidentReportId { get; set; }
        public IncidentReport? IncidentReport { get; set; }

        // Null for the very first entry (when the report is created)
        public IncidentStatus? OldStatus { get; set; }
        public IncidentStatus NewStatus { get; set; }

        [Required]
        public string ChangedById { get; set; } = string.Empty;
        public ApplicationUser? ChangedBy { get; set; }

        public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;

        // Staff comment about the change
        [StringLength(500)]
        public string? Comment { get; set; }

        // Controls whether the student sees the comment.
        // The status change itself is always visible.
        public bool IsCommentVisibleToStudent { get; set; }
    }
}