using System.ComponentModel.DataAnnotations;

namespace StudentIncidentSystem.Models
{
    // Simple key/value settings that administrators can change
    public class AppSetting
    {
        [Key, StringLength(100)]
        public string Key { get; set; } = string.Empty;

        [Required, StringLength(500)]
        public string Value { get; set; } = string.Empty;

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
