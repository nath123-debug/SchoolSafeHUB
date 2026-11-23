using System.ComponentModel.DataAnnotations;

namespace StudentIncidentSystem.Models
{
    // Messages between the student and staff. Visible to both.
    public class CaseMessage
    {
        public int Id { get; set; }

        public int IncidentReportId { get; set; }
        public IncidentReport? IncidentReport { get; set; }

        [Required]
        public string SenderId { get; set; } = string.Empty;
        public ApplicationUser? Sender { get; set; }

        [Required, StringLength(2000)]
        public string Body { get; set; } = string.Empty;

        // True when staff are asking the student for more information
        public bool IsInformationRequest { get; set; }

        public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? ReadAtUtc { get; set; }
    }
}