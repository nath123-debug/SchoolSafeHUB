using System.ComponentModel.DataAnnotations;
using StudentIncidentSystem.Models;

namespace StudentIncidentSystem.ViewModels
{
    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalCount { get; set; }
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
    }

    public record PagerInfo(int Page, int TotalPages, string? Q, IncidentStatus? Status);

    public class CreateReportViewModel
    {
        [Required(ErrorMessage = "Please choose a category.")]
        [Display(Name = "Category")]
        public int? CategoryId { get; set; }

        [Required, StringLength(150)]
        [Display(Name = "Short title")]
        public string Title { get; set; } = string.Empty;

        [Required, StringLength(4000, MinimumLength = 10, ErrorMessage = "Please describe what happened (at least 10 characters).")]
        [Display(Name = "What happened?")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter when it happened.")]
        [Display(Name = "Date and time of incident")]
        [DataType(DataType.DateTime)]
        public DateTime? IncidentDateTime { get; set; }

        [Required, StringLength(200)]
        public string Location { get; set; } = string.Empty;

        [StringLength(1000)]
        [Display(Name = "People involved (if known)")]
        public string? PeopleInvolved { get; set; }

        // Filled by the controller for the dropdown. Not user input.
        public List<CategoryOption> Categories { get; set; } = new();
    }

    public class MyReportsViewModel
    {
        public PagedResult<ReportListItem> Reports { get; set; } = new();
        public string? Search { get; set; }
        public IncidentStatus? StatusFilter { get; set; }
    }

    public class MessageItem
    {
        public string Body { get; set; } = string.Empty;
        public DateTime SentAtUtc { get; set; }
        public bool FromMe { get; set; }
        public bool IsInformationRequest { get; set; }
    }

    public class ReportDetailsViewModel
    {
        public int Id { get; set; }
        public string ReferenceNumber { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public DateTime IncidentDateTime { get; set; }
        public string Location { get; set; } = string.Empty;
        public string? PeopleInvolved { get; set; }
        public IncidentStatus Status { get; set; }
        public string? ResolutionSummary { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public List<StatusUpdateItem> Timeline { get; set; } = new();
        public List<MessageItem> Messages { get; set; } = new();
        public bool CanReply { get; set; }
    }

    public class ReplyViewModel
    {
        [Required, StringLength(2000)]
        [Display(Name = "Your message")]
        public string Body { get; set; } = string.Empty;
    }
}