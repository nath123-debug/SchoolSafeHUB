using System.ComponentModel.DataAnnotations;

namespace StudentIncidentSystem.Models
{
    public class IncidentCategory
    {
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        // Admins deactivate categories instead of deleting them,
        // so old reports keep their category.
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public ICollection<IncidentReport> Reports { get; set; } = new List<IncidentReport>();
    }
}