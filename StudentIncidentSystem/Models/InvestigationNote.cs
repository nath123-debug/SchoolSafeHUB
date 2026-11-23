using System.ComponentModel.DataAnnotations;

namespace StudentIncidentSystem.Models
{
    // PRIVATE to authorized staff. Never shown to students.
    public class InvestigationNote
    {
        public int Id { get; set; }

        public int IncidentReportId { get; set; }
        public IncidentReport? IncidentReport { get; set; }

        [Required]
        public string AuthorId { get; set; } = string.Empty;
        public ApplicationUser? Author { get; set; }

        public NoteType NoteType { get; set; } = NoteType.Investigation;

        [Required, StringLength(4000)]
        public string Content { get; set; } = string.Empty;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}