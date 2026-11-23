using System.ComponentModel.DataAnnotations;

namespace StudentIncidentSystem.Models
{
    public class CaseAssignment
    {
        public int Id { get; set; }

        public int IncidentReportId { get; set; }
        public IncidentReport? IncidentReport { get; set; }

        [Required]
        public string AssignedToId { get; set; } = string.Empty;
        public ApplicationUser? AssignedTo { get; set; }

        [Required]
        public string AssignedById { get; set; } = string.Empty;
        public ApplicationUser? AssignedBy { get; set; }

        public DateTime AssignedAtUtc { get; set; } = DateTime.UtcNow;

        // Only one assignment per report can be current.
        // Reassigning sets the old row to false and keeps it as history.
        public bool IsCurrent { get; set; } = true;
        public DateTime? EndedAtUtc { get; set; }
    }
}