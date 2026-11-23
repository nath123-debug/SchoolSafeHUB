using System.ComponentModel.DataAnnotations;
using StudentIncidentSystem.Models;

namespace StudentIncidentSystem.ViewModels
{
    public record UserPagerInfo(int Page, int TotalPages, string? Q, string? Role, string? Status);
    public record AssignmentPagerInfo(int Page, int TotalPages, string? Q, IncidentStatus? Status, string? Show);

    // ---------- Users ----------

    public class UserListItem
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? SchoolIdNumber { get; set; }
        public string? Role { get; set; }
        public bool IsActive { get; set; }
        public bool IsLockedOut { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    public class UserIndexViewModel
    {
        public PagedResult<UserListItem> Users { get; set; } = new();
        public string? Search { get; set; }
        public string? Role { get; set; }
        public string? Status { get; set; }
        public string CurrentUserId { get; set; } = string.Empty;
    }

    public class CreateUserViewModel
    {
        [Required, StringLength(100)]
        [Display(Name = "Full name")]
        public string FullName { get; set; } = string.Empty;

        [StringLength(30)]
        [Display(Name = "School ID number")]
        public string? SchoolIdNumber { get; set; }

        [Required, EmailAddress, StringLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Role { get; set; } = AppRoles.Staff;

        [Required, DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
        [Display(Name = "Temporary password")]
        public string Password { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
        [Display(Name = "Confirm password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class EditUserViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? SchoolIdNumber { get; set; }
        public string? Role { get; set; }
        public bool IsActive { get; set; }
        public bool IsLockedOut { get; set; }
        public bool IsSelf { get; set; }
        public int OpenCaseCount { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    public class UpdateProfileViewModel
    {
        [Required, StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [StringLength(30)]
        public string? SchoolIdNumber { get; set; }
    }

    public class SetRoleViewModel
    {
        [Required]
        public string Role { get; set; } = string.Empty;
    }

    public class ResetPasswordViewModel
    {
        [Required, StringLength(100, MinimumLength = 8)]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    // ---------- Categories ----------

    public class CategoryListItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public int ReportCount { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    public class CategoryFormViewModel
    {
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }
    }

    // ---------- Case assignments (admin) ----------

    public class AssignmentListItem
    {
        public int Id { get; set; }
        public string ReferenceNumber { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public IncidentStatus Status { get; set; }
        public string? AssignedToId { get; set; }
        public string? AssignedToName { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }

    public class AssignmentIndexViewModel
    {
        public PagedResult<AssignmentListItem> Cases { get; set; } = new();
        public string? Search { get; set; }
        public IncidentStatus? StatusFilter { get; set; }
        public string Show { get; set; } = "all";
        public List<StaffOption> StaffOptions { get; set; } = new();
    }

    public class AdminAssignViewModel
    {
        [Required]
        public string AssigneeId { get; set; } = string.Empty;
    }
}
