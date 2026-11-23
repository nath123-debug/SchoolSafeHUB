using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace StudentIncidentSystem.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required, StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        // Student ID, or employee ID for staff. Optional.
        [StringLength(30)]
        public string? SchoolIdNumber { get; set; }

        // Admins can deactivate accounts instead of deleting them.
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}