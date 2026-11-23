using System.ComponentModel.DataAnnotations;

namespace StudentIncidentSystem.Models
{
    public class IncidentAttachment
    {
        public int Id { get; set; }

        public int IncidentReportId { get; set; }
        public IncidentReport? IncidentReport { get; set; }

        // Name the user uploaded (display only, never used to store the file)
        [Required, StringLength(255)]
        public string OriginalFileName { get; set; } = string.Empty;

        // Random name used on disk
        [Required, StringLength(100)]
        public string StoredFileName { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string ContentType { get; set; } = string.Empty;

        public long FileSizeBytes { get; set; }

        [Required]
        public string UploadedById { get; set; } = string.Empty;
        public ApplicationUser? UploadedBy { get; set; }

        public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
    }
}