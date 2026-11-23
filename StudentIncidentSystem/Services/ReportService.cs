using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StudentIncidentSystem.Data;
using StudentIncidentSystem.Models;
using StudentIncidentSystem.ViewModels;

namespace StudentIncidentSystem.Services
{
    public class ReportService
    {
        private const int MaxExportRows = 10000;

        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _users;

        public ReportService(ApplicationDbContext db, UserManager<ApplicationUser> users)
        {
            _db = db;
            _users = users;
        }

        // PERMISSIONS: administrators see statistics for everything (without contents).
        // Staff see only cases assigned to them plus unassigned cases.
        private IQueryable<IncidentReport> Build(ReportFilter f, string userId, bool isAdmin)
        {
            var q = _db.IncidentReports.AsNoTracking().AsQueryable();

            if (!isAdmin)
                q = q.Where(r => !r.Assignments.Any(a => a.IsCurrent) ||
                                 r.Assignments.Any(a => a.IsCurrent && a.AssignedToId == userId));

            if (!string.IsNullOrWhiteSpace(f.Q))
            {
                var s = f.Q.Trim();
                q = q.Where(r => r.ReferenceNumber.Contains(s));
            }
            if (f.Status.HasValue) q = q.Where(r => r.Status == f.Status.Value);
            if (f.CategoryId.HasValue) q = q.Where(r => r.CategoryId == f.CategoryId.Value);

            if (f.From.HasValue)
            {
                var start = f.From.Value.Date;
                q = q.Where(r => r.CreatedAtUtc >= start);
            }
            if (f.To.HasValue)
            {
                var end = f.To.Value.Date.AddDays(1);
                q = q.Where(r => r.CreatedAtUtc < end);
            }

            if (f.AssignedTo == "unassigned")
                q = q.Where(r => !r.Assignments.Any(a => a.IsCurrent));
            else if (!string.IsNullOrEmpty(f.AssignedTo))
            {
                var staffId = f.AssignedTo;
                q = q.Where(r => r.Assignments.Any(a => a.IsCurrent && a.AssignedToId == staffId));
            }

            return q;
        }

        // Administrators never get the title column loaded at all
        private static IQueryable<ReportRow> ToRows(IQueryable<IncidentReport> q, bool isAdmin)
        {
            if (isAdmin)
            {
                return q.Select(r => new ReportRow
                {
                    Id = r.Id,
                    ReferenceNumber = r.ReferenceNumber,
                    Title = null,
                    CategoryName = r.Category!.Name,
                    Status = r.Status,
                    AssignedToName = r.Assignments.Where(a => a.IsCurrent)
                        .Select(a => a.AssignedTo!.FullName).FirstOrDefault(),
                    CreatedAtUtc = r.CreatedAtUtc,
                    ResolvedAtUtc = r.ResolvedAtUtc
                });
            }

            return q.Select(r => new ReportRow
            {
                Id = r.Id,
                ReferenceNumber = r.ReferenceNumber,
                Title = r.Title,
                CategoryName = r.Category!.Name,
                Status = r.Status,
                AssignedToName = r.Assignments.Where(a => a.IsCurrent)
                    .Select(a => a.AssignedTo!.FullName).FirstOrDefault(),
                CreatedAtUtc = r.CreatedAtUtc,
                ResolvedAtUtc = r.ResolvedAtUtc
            });
        }

        public async Task<ReportsViewModel> GetAsync(
            ReportFilter f, string userId, bool isAdmin, int page, int pageSize)
        {
            var query = Build(f, userId, isAdmin);

            var total = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            page = Math.Clamp(page, 1, totalPages);

            var paged = query
                .OrderByDescending(r => r.CreatedAtUtc).ThenByDescending(r => r.Id)
                .Skip((page - 1) * pageSize).Take(pageSize);
            var rows = await ToRows(paged, isAdmin).ToListAsync();

            // Summaries cover ALL matching cases, not just the current page
            var statusCounts = await query.GroupBy(r => r.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            var byStatus = Enum.GetValues<IncidentStatus>()
                .Select(s => new CountRow(StatusDisplay.Label(s),
                    statusCounts.FirstOrDefault(x => x.Status == s)?.Count ?? 0))
                .ToList();

            var resolved = statusCounts
                .Where(x => x.Status is IncidentStatus.Resolved or IncidentStatus.Closed)
                .Sum(x => x.Count);

            var categoryCounts = await query.GroupBy(r => r.Category!.Name)
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .ToListAsync();
            var byCategory = categoryCounts
                .OrderByDescending(x => x.Count).ThenBy(x => x.Name)
                .Select(x => new CountRow(x.Name, x.Count))
                .ToList();

            var monthRaw = await query
                .GroupBy(r => new { r.CreatedAtUtc.Year, r.CreatedAtUtc.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                .ToListAsync();
            var byMonth = monthRaw
                .OrderBy(x => x.Year).ThenBy(x => x.Month)
                .TakeLast(24)
                .Select(x => new CountRow(new DateTime(x.Year, x.Month, 1).ToString("MMM yyyy"), x.Count))
                .ToList();

            List<StaffOption> assignOptions;
            if (isAdmin)
            {
                assignOptions = (await _users.GetUsersInRoleAsync(AppRoles.Staff))
                    .Where(u => u.IsActive)
                    .OrderBy(u => u.FullName)
                    .Select(u => new StaffOption { Id = u.Id, Name = u.FullName })
                    .ToList();
            }
            else
            {
                var me = await _users.FindByIdAsync(userId);
                assignOptions = me == null
                    ? new List<StaffOption>()
                    : new List<StaffOption> { new StaffOption { Id = me.Id, Name = me.FullName + " (me)" } };
            }

            return new ReportsViewModel
            {
                Filter = f,
                IsAdmin = isAdmin,
                Rows = new PagedResult<ReportRow>
                {
                    Items = rows, Page = page, PageSize = pageSize, TotalCount = total
                },
                Total = total,
                ResolvedCount = resolved,
                OpenCount = total - resolved,
                ByStatus = byStatus,
                ByCategory = byCategory,
                ByMonth = byMonth,
                Categories = await _db.IncidentCategories.AsNoTracking()
                    .OrderBy(c => c.Name)
                    .Select(c => new CategoryOption { Id = c.Id, Name = c.Name })
                    .ToListAsync(),
                AssignOptions = assignOptions
            };
        }

        // ---------- CSV ----------

        public async Task<byte[]> ExportCsvAsync(ReportFilter f, string userId, bool isAdmin)
        {
            var query = Build(f, userId, isAdmin)
                .OrderByDescending(r => r.CreatedAtUtc).ThenByDescending(r => r.Id)
                .Take(MaxExportRows);
            var rows = await ToRows(query, isAdmin).ToListAsync();

            var sb = new StringBuilder();

            var header = new List<string> { "Reference" };
            if (!isAdmin) header.Add("Title");
            header.AddRange(new[] { "Category", "Status", "Assigned to", "Submitted (UTC)", "Resolved (UTC)" });
            sb.AppendLine(string.Join(",", header.Select(Csv)));

            foreach (var r in rows)
            {
                var cells = new List<string?> { r.ReferenceNumber };
                if (!isAdmin) cells.Add(r.Title);
                cells.Add(r.CategoryName);
                cells.Add(StatusDisplay.Label(r.Status));
                cells.Add(r.AssignedToName ?? "Unassigned");
                cells.Add(r.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm"));
                cells.Add(r.ResolvedAtUtc?.ToString("yyyy-MM-dd HH:mm") ?? string.Empty);
                sb.AppendLine(string.Join(",", cells.Select(Csv)));
            }

            // UTF-8 with a byte-order mark, so Excel shows special characters correctly
            var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            return encoding.GetPreamble().Concat(encoding.GetBytes(sb.ToString())).ToArray();
        }

        // Quotes every cell, and defuses cells that Excel could run as formulas
        private static string Csv(string? value)
        {
            value ??= string.Empty;
            if (value.Length > 0 && "=+-@\t\r".Contains(value[0]))
                value = "'" + value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
