using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StudentIncidentSystem.Data;
using StudentIncidentSystem.Models;
using StudentIncidentSystem.ViewModels;

namespace StudentIncidentSystem.Services
{
    // Lets administrators assign cases WITHOUT reading them:
    // only reference, category, status, assignee and dates are ever loaded here.
    public class CaseAssignmentService
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _users;

        public CaseAssignmentService(ApplicationDbContext db, UserManager<ApplicationUser> users)
        {
            _db = db;
            _users = users;
        }

        public async Task<AssignmentIndexViewModel> GetListAsync(
            string? search, IncidentStatus? status, string? show, int page, int pageSize = 10)
        {
            var query = _db.IncidentReports.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                query = query.Where(r => r.ReferenceNumber.Contains(search));
            }
            if (status.HasValue) query = query.Where(r => r.Status == status.Value);

            if (show == "unassigned")
                query = query.Where(r => !r.Assignments.Any(a => a.IsCurrent));
            else
                show = "all";

            var total = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            page = Math.Clamp(page, 1, totalPages);

            var items = await query
                .OrderByDescending(r => r.CreatedAtUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new AssignmentListItem
                {
                    Id = r.Id,
                    ReferenceNumber = r.ReferenceNumber,
                    CategoryName = r.Category!.Name,
                    Status = r.Status,
                    AssignedToId = r.Assignments.Where(a => a.IsCurrent)
                        .Select(a => a.AssignedToId).FirstOrDefault(),
                    AssignedToName = r.Assignments.Where(a => a.IsCurrent)
                        .Select(a => a.AssignedTo!.FullName).FirstOrDefault(),
                    CreatedAtUtc = r.CreatedAtUtc,
                    UpdatedAtUtc = r.UpdatedAtUtc
                })
                .ToListAsync();

            var staff = (await _users.GetUsersInRoleAsync(AppRoles.Staff))
                .Where(u => u.IsActive)
                .OrderBy(u => u.FullName)
                .Select(u => new StaffOption { Id = u.Id, Name = u.FullName })
                .ToList();

            return new AssignmentIndexViewModel
            {
                Cases = new PagedResult<AssignmentListItem>
                {
                    Items = items, Page = page, PageSize = pageSize, TotalCount = total
                },
                Search = search,
                StatusFilter = status,
                Show = show,
                StaffOptions = staff
            };
        }

        public async Task<OpResult> AssignAsync(int id, string assigneeId, string adminId)
        {
            var report = await _db.IncidentReports
                .Include(r => r.Assignments.Where(a => a.IsCurrent))
                .FirstOrDefaultAsync(r => r.Id == id);

            if (report == null) return OpResult.Missing();
            if (report.Status == IncidentStatus.Closed)
                return OpResult.Fail("Closed cases cannot be reassigned.");

            var current = report.Assignments.FirstOrDefault(a => a.IsCurrent);
            if (current?.AssignedToId == assigneeId)
                return OpResult.Fail("This case is already assigned to that staff member.");

            var assignee = await _users.FindByIdAsync(assigneeId);
            if (assignee == null || !assignee.IsActive ||
                !await _users.IsInRoleAsync(assignee, AppRoles.Staff))
                return OpResult.Fail("Please choose an active staff member.");

            var now = DateTime.UtcNow;

            try
            {
                await using var tx = await _db.Database.BeginTransactionAsync();

                // End the old assignment first so the "one current assignment" index is never broken
                if (current != null)
                {
                    current.IsCurrent = false;
                    current.EndedAtUtc = now;
                    await _db.SaveChangesAsync();
                }

                _db.CaseAssignments.Add(new CaseAssignment
                {
                    IncidentReportId = report.Id,
                    AssignedToId = assigneeId,
                    AssignedById = adminId,
                    AssignedAtUtc = now,
                    IsCurrent = true
                });
                report.UpdatedAtUtc = now;
                await _db.SaveChangesAsync();

                await tx.CommitAsync();
            }
            catch (DbUpdateException)
            {
                _db.ChangeTracker.Clear();
                return OpResult.Fail("Someone else just changed this case's assignment. Please reload and try again.");
            }

            return OpResult.Ok($"{report.ReferenceNumber} was assigned to {assignee.FullName}.");
        }
    }
}
