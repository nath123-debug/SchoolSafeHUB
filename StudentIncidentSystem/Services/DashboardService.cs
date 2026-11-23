using Microsoft.EntityFrameworkCore;
using StudentIncidentSystem.Data;
using StudentIncidentSystem.Models;
using StudentIncidentSystem.ViewModels;

namespace StudentIncidentSystem.Services
{
    public class DashboardService
    {
        private readonly ApplicationDbContext _db;

        public DashboardService(ApplicationDbContext db)
        {
            _db = db;
        }

        // ---------- STUDENT ----------

        public async Task<StudentDashboardViewModel> GetStudentAsync(string studentId)
        {
            var mine = _db.IncidentReports.AsNoTracking()
                .Where(r => r.ReporterId == studentId);

            return new StudentDashboardViewModel
            {
                TotalReports = await mine.CountAsync(),
                UnderReview = await mine.CountAsync(r => r.Status == IncidentStatus.UnderReview),
                Resolved = await mine.CountAsync(r =>
                    r.Status == IncidentStatus.Resolved || r.Status == IncidentStatus.Closed),

                RecentReports = await mine
                    .OrderByDescending(r => r.UpdatedAtUtc)
                    .Take(5)
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
                    .ToListAsync(),

                // Staff names and hidden comments are NOT sent to students
                RecentUpdates = await _db.IncidentStatusHistories.AsNoTracking()
                    .Where(h => h.IncidentReport!.ReporterId == studentId)
                    .OrderByDescending(h => h.ChangedAtUtc)
                    .Take(5)
                    .Select(h => new StatusUpdateItem
                    {
                        ReferenceNumber = h.IncidentReport!.ReferenceNumber,
                        OldStatus = h.OldStatus,
                        NewStatus = h.NewStatus,
                        ChangedAtUtc = h.ChangedAtUtc,
                        Comment = h.IsCommentVisibleToStudent ? h.Comment : null
                    })
                    .ToListAsync()
            };
        }

        // ---------- STAFF ----------

        // Scope rule: cases assigned to this staff member, plus unassigned cases.
        private IQueryable<IncidentReport> StaffScope(string staffId)
        {
            return _db.IncidentReports.AsNoTracking().Where(r =>
                !r.Assignments.Any(a => a.IsCurrent) ||
                r.Assignments.Any(a => a.IsCurrent && a.AssignedToId == staffId));
        }

        public async Task<StaffDashboardViewModel> GetStaffAsync(
            string staffId, string? search, IncidentStatus? status, int? categoryId)
        {
            var scope = StaffScope(staffId);
            var scopeIds = scope.Select(r => r.Id);

            var cases = scope;
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                cases = cases.Where(r => r.ReferenceNumber.Contains(search) || r.Title.Contains(search));
            }
            if (status.HasValue) cases = cases.Where(r => r.Status == status.Value);
            if (categoryId.HasValue) cases = cases.Where(r => r.CategoryId == categoryId.Value);

            var categoryCounts = await scope
                .GroupBy(r => r.Category!.Name)
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync();

            return new StaffDashboardViewModel
            {
                Submitted = await scope.CountAsync(r => r.Status == IncidentStatus.Submitted),
                UnderReview = await scope.CountAsync(r => r.Status == IncidentStatus.UnderReview),
                AwaitingInformation = await scope.CountAsync(r =>
                    r.Status == IncidentStatus.AdditionalInformationRequested),
                Resolved = await scope.CountAsync(r =>
                    r.Status == IncidentStatus.Resolved || r.Status == IncidentStatus.Closed),

                CategoryChart = new ChartData
                {
                    Labels = categoryCounts.Select(c => c.Name).ToList(),
                    Values = categoryCounts.Select(c => c.Count).ToList()
                },

                RecentActivity = await _db.IncidentStatusHistories.AsNoTracking()
                    .Where(h => scopeIds.Contains(h.IncidentReportId))
                    .OrderByDescending(h => h.ChangedAtUtc)
                    .Take(8)
                    .Select(h => new StatusUpdateItem
                    {
                        ReferenceNumber = h.IncidentReport!.ReferenceNumber,
                        OldStatus = h.OldStatus,
                        NewStatus = h.NewStatus,
                        ChangedAtUtc = h.ChangedAtUtc,
                        ChangedByName = h.ChangedBy!.FullName
                    })
                    .ToListAsync(),

                TotalMatching = await cases.CountAsync(),
                Cases = await cases
                    .OrderByDescending(r => r.UpdatedAtUtc)
                    .Take(10)
                    .Select(r => new ReportListItem
                    {
                        Id = r.Id,
                        ReferenceNumber = r.ReferenceNumber,
                        Title = r.Title,
                        CategoryName = r.Category!.Name,
                        Status = r.Status,
                        AssignedToName = r.Assignments
                            .Where(a => a.IsCurrent)
                            .Select(a => a.AssignedTo!.FullName)
                            .FirstOrDefault(),
                        CreatedAtUtc = r.CreatedAtUtc,
                        UpdatedAtUtc = r.UpdatedAtUtc
                    })
                    .ToListAsync(),

                Search = search,
                StatusFilter = status,
                CategoryFilter = categoryId,
                Categories = await _db.IncidentCategories.AsNoTracking()
                    .OrderBy(c => c.Name)
                    .Select(c => new CategoryOption { Id = c.Id, Name = c.Name })
                    .ToListAsync()
            };
        }

        // ---------- ADMINISTRATOR ----------

        public async Task<AdminDashboardViewModel> GetAdminAsync()
        {
            var reports = _db.IncidentReports.AsNoTracking();

            var categoryCounts = await reports
                .GroupBy(r => r.Category!.Name)
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync();

            var now = DateTime.UtcNow;
            var start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-5);

            var monthlyRaw = await reports
                .Where(r => r.CreatedAtUtc >= start)
                .GroupBy(r => new { r.CreatedAtUtc.Year, r.CreatedAtUtc.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                .ToListAsync();

            var monthly = new ChartData();
            for (int i = 0; i < 6; i++)
            {
                var month = start.AddMonths(i);
                monthly.Labels.Add(month.ToString("MMM yyyy"));
                monthly.Values.Add(
                    monthlyRaw.FirstOrDefault(x => x.Year == month.Year && x.Month == month.Month)?.Count ?? 0);
            }

            return new AdminDashboardViewModel
            {
                TotalUsers = await _db.Users.CountAsync(),
                ActiveUsers = await _db.Users.CountAsync(u => u.IsActive),
                TotalReports = await reports.CountAsync(),
                OpenCases = await reports.CountAsync(r =>
                    r.Status != IncidentStatus.Resolved && r.Status != IncidentStatus.Closed),
                ResolvedCases = await reports.CountAsync(r =>
                    r.Status == IncidentStatus.Resolved || r.Status == IncidentStatus.Closed),
                CategoryChart = new ChartData
                {
                    Labels = categoryCounts.Select(c => c.Name).ToList(),
                    Values = categoryCounts.Select(c => c.Count).ToList()
                },
                MonthlyChart = monthly,
                RecentActivity = await _db.AuditLogs.AsNoTracking()
                    .OrderByDescending(l => l.TimestampUtc)
                    .Take(8)
                    .ToListAsync()
            };
        }
    }
}
