using System.ComponentModel.DataAnnotations;

namespace StudentIncidentSystem.Models
{
    public class AuditLog
    {
        public long Id { get; set; }

        // Stored as plain text with no foreign key, so the log
        // survives even if an account is later removed.
        [StringLength(450)]
        public string? UserId { get; set; }

        [StringLength(256)]
        public string? UserName { get; set; }

        [Required, StringLength(100)]
        public string Action { get; set; } = string.Empty;

        [StringLength(100)]
        public string? EntityType { get; set; }

        [StringLength(50)]
        public string? EntityId { get; set; }

        [StringLength(1000)]
        public string? Details { get; set; }

        [StringLength(45)]
        public string? IpAddress { get; set; }

        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    }
}