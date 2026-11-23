using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StudentIncidentSystem.Data;
using StudentIncidentSystem.Models;
using StudentIncidentSystem.ViewModels;

namespace StudentIncidentSystem.Services
{
    public enum ReplyResult { Sent, NotFound, Closed }

    public class IncidentService
    {
        private readonly ApplicationDbContext _db;

        public IncidentService(ApplicationDbContext db)
        {
            _db = db;
        }

        // ---------- Categories ----------

        public Task<List<CategoryOption>> GetActiveCategoriesAsync() =>
            _db.IncidentCategories.AsNoTracking()
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .Select(c => new CategoryOption { Id = c.Id, Name = c.Name })
                .ToListAsync();

        public Task<bool> CategoryIsActiveAsync(int id) =>
            _db.IncidentCategories.AnyAsync(c => c.Id == id && c.IsActive);

        // ---------- Create ----------

        public async Task<IncidentReport> CreateReportAsync(string reporterId, CreateReportViewModel m)
        {
            for (var attempt = 1; ; attempt++)
            {
                var now = DateTime.UtcNow;

                var report = new IncidentReport
                {
                    ReferenceNumber = await NextReferenceNumberAsync(now.Year),
                    ReporterId = reporterId,
                    CategoryId = m.CategoryId!.Value,
                    Title = m.Title.Trim(),
                    Description = m.Description.Trim(),
                    IncidentDateTime = m.IncidentDateTime!.Value,
                    Location = m.Location.Trim(),
                    PeopleInvolved = string.IsNullOrWhiteSpace(m.PeopleInvolved) ? null : m.PeopleInvolved.Trim(),
                    Status = IncidentStatus.Submitted,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                };

                // First history entry. Saved in the same transaction as the report.
                report.StatusHistory.Add(new IncidentStatusHistory
                {
                    OldStatus = null,
                    NewStatus = IncidentStatus.Submitted,
                    ChangedById = reporterId,
                    ChangedAtUtc = now
                });

                _db.IncidentReports.Add(report);

                try
                {
                    await _db.SaveChangesAsync();
                    return report;
                }
                catch (DbUpdateException ex) when (IsDuplicateKey(ex) && attempt < 5)
                {
                    // Another report took this number a moment ago. Try the next one.
                    _db.ChangeTracker.Clear();
                }
            }
        }

        private async Task<string> NextReferenceNumberAsync(int year)
        {
            var prefix = $"INC-{year}-";

            var last = await _db.IncidentReports
                .Where(r => r.ReferenceNumber.StartsWith(prefix))
                .OrderByDescending(r => r.ReferenceNumber) // fixed-width, so text order = numeric order
                .Select(r => r.ReferenceNumber)
                .FirstOrDefaultAsync();

            var next = 1;
            if (last != null && int.TryParse(last.AsSpan(prefix.Length), out var n))
                next = n + 1;

            return $"{prefix}{next:D6}";
        }

        private static bool IsDuplicateKey(DbUpdateException ex) =>
            ex.InnerException is SqlException { Number: 2601 or 2627 };

        // ---------- My reports ----------

        public async Task<PagedResult<ReportListItem>> GetMyReportsAsync(
            string studentId, string? search, IncidentStatus? status, int page, int pageSize = 10)
        {
            var query = _db.IncidentReports.AsNoTracking().Where(r => r.ReporterId == studentId);

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                query = query.Where(r => r.ReferenceNumber.Contains(search) || r.Title.Contains(search));
            }
            if (status.HasValue)
                query = query.Where(r => r.Status == status.Value);

            var total = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            page = Math.Clamp(page, 1, totalPages);

            var items = await query
                .OrderByDescending(r => r.CreatedAtUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new ReportListItem
                {
                    Id = r.Id,
                    ReferenceNumber = r.ReferenceNumber,
                    Title = r.Title,
                    CategoryName = r.Category!.Name,
                    Status = r.Status,
                    CreatedAtUtc = r.CreatedAtUtc,
                    UpdatedAtUtc = r.UpdatedAtUtc
                })
                .ToListAsync();

            return new PagedResult<ReportListItem>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = total
            };
        }

        // ---------- Details (student view) ----------

        // Returns null if the report does not exist OR belongs to someone else.
        public async Task<ReportDetailsViewModel?> GetStudentDetailsAsync(int id, string studentId)
        {
            var r = await _db.IncidentReports.AsNoTracking()
                .Where(x => x.Id == id && x.ReporterId == studentId)
                .Select(x => new
                {
                    x.Id,
                    x.ReferenceNumber,
                    x.Title,
                    x.Description,
                    x.IncidentDateTime,
                    x.Location,
                    x.PeopleInvolved,
                    x.Status,
                    x.ResolutionSummary,
                    x.CreatedAtUtc,
                    x.UpdatedAtUtc,
                    CategoryName = x.Category!.Name
                })
                .FirstOrDefaultAsync();

            if (r == null) return null;

            var timeline = await _db.IncidentStatusHistories.AsNoTracking()
                .Where(h => h.IncidentReportId == id)
                .OrderBy(h => h.ChangedAtUtc)
                .Select(h => new StatusUpdateItem
                {
                    ReferenceNumber = r.ReferenceNumber,
                    OldStatus = h.OldStatus,
                    NewStatus = h.NewStatus,
                    ChangedAtUtc = h.ChangedAtUtc,
                    // Hidden comments never leave the database for student views
                    Comment = h.IsCommentVisibleToStudent ? h.Comment : null
                })
                .ToListAsync();

            var messages = await _db.CaseMessages.AsNoTracking()
                .Where(m => m.IncidentReportId == id)
                .OrderBy(m => m.SentAtUtc)
                .Select(m => new MessageItem
                {
                    Body = m.Body,
                    SentAtUtc = m.SentAtUtc,
                    FromMe = m.SenderId == studentId,
                    IsInformationRequest = m.IsInformationRequest
                })
                .ToListAsync();

            var finished = r.Status is IncidentStatus.Resolved or IncidentStatus.Closed;

            return new ReportDetailsViewModel
            {
                Id = r.Id,
                ReferenceNumber = r.ReferenceNumber,
                Title = r.Title,
                Description = r.Description,
                CategoryName = r.CategoryName,
                IncidentDateTime = r.IncidentDateTime,
                Location = r.Location,
                PeopleInvolved = r.PeopleInvolved,
                Status = r.Status,
                ResolutionSummary = finished ? r.ResolutionSummary : null,
                CreatedAtUtc = r.CreatedAtUtc,
                UpdatedAtUtc = r.UpdatedAtUtc,
                Timeline = timeline,
                Messages = messages,
                CanReply = r.Status != IncidentStatus.Closed
            };
        }

        // ---------- Student reply ----------

        public async Task<ReplyResult> AddStudentReplyAsync(int id, string studentId, string body)
        {
            var report = await _db.IncidentReports
                .FirstOrDefaultAsync(r => r.Id == id && r.ReporterId == studentId);

            if (report == null) return ReplyResult.NotFound;
            if (report.Status == IncidentStatus.Closed) return ReplyResult.Closed;

            var now = DateTime.UtcNow;

            _db.CaseMessages.Add(new CaseMessage
            {
                IncidentReportId = report.Id,
                SenderId = studentId,
                Body = body,
                IsInformationRequest = false,
                SentAtUtc = now
            });

            // Answering a request for information puts the case back in review
            if (report.Status == IncidentStatus.AdditionalInformationRequested)
            {
                _db.IncidentStatusHistories.Add(new IncidentStatusHistory
                {
                    IncidentReportId = report.Id,
                    OldStatus = report.Status,
                    NewStatus = IncidentStatus.UnderReview,
                    ChangedById = studentId,
                    ChangedAtUtc = now,
                    Comment = "Student provided additional information.",
                    IsCommentVisibleToStudent = true
                });
                report.Status = IncidentStatus.UnderReview;
            }

            report.UpdatedAtUtc = now;
            await _db.SaveChangesAsync();
            return ReplyResult.Sent;
        }
    }
}