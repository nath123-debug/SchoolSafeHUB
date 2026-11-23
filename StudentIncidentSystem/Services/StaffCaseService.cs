using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StudentIncidentSystem.Data;
using StudentIncidentSystem.Models;
using StudentIncidentSystem.ViewModels;

namespace StudentIncidentSystem.Services
{
    public class StaffCaseService
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _users;

        public StaffCaseService(ApplicationDbContext db, UserManager<ApplicationUser> users)
        {
            _db = db;
            _users = users;
        }

        // SCOPE RULE: cases assigned to this staff member, plus unassigned cases.
        private static IQueryable<IncidentReport> InScope(IQueryable<IncidentReport> q, string staffId) =>
            q.Where(r => !r.Assignments.Any(a => a.IsCurrent) ||
                         r.Assignments.Any(a => a.IsCurrent && a.AssignedToId == staffId));

        // ---------- LIST ----------

        public async Task<StaffIndexViewModel> GetListAsync(
            string staffId, string? search, IncidentStatus? status, int? categoryId,
            string? scope, int page, int pageSize = 10)
        {
            scope = scope is "mine" or "unassigned" ? scope : "all";

            var query = InScope(_db.IncidentReports.AsNoTracking(), staffId);

            if (scope == "mine")
                query = query.Where(r => r.Assignments.Any(a => a.IsCurrent && a.AssignedToId == staffId));
            else if (scope == "unassigned")
                query = query.Where(r => !r.Assignments.Any(a => a.IsCurrent));

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                query = query.Where(r => r.ReferenceNumber.Contains(search) || r.Title.Contains(search));
            }
            if (status.HasValue) query = query.Where(r => r.Status == status.Value);
            if (categoryId.HasValue) query = query.Where(r => r.CategoryId == categoryId.Value);

            var total = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            page = Math.Clamp(page, 1, totalPages);

            var items = await query
                .OrderByDescending(r => r.UpdatedAtUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
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
                .ToListAsync();

            return new StaffIndexViewModel
            {
                Cases = new PagedResult<ReportListItem>
                {
                    Items = items, Page = page, PageSize = pageSize, TotalCount = total
                },
                Search = search,
                StatusFilter = status,
                CategoryFilter = categoryId,
                Scope = scope,
                Categories = await _db.IncidentCategories.AsNoTracking()
                    .OrderBy(c => c.Name)
                    .Select(c => new CategoryOption { Id = c.Id, Name = c.Name })
                    .ToListAsync()
            };
        }

        // ---------- DETAILS ----------

        // Returns null if the case does not exist OR is outside this staff member's scope.
        public async Task<StaffCaseDetailsViewModel?> GetDetailsAsync(int id, string staffId)
        {
            var r = await InScope(_db.IncidentReports.AsNoTracking(), staffId)
                .Where(x => x.Id == id)
                .Select(x => new
                {
                    x.Id, x.ReferenceNumber, x.Title, x.Description, x.IncidentDateTime,
                    x.Location, x.PeopleInvolved, x.Status, x.ResolutionSummary,
                    x.CreatedAtUtc, x.UpdatedAtUtc, x.ReporterId,
                    CategoryName = x.Category!.Name,
                    ReporterName = x.Reporter!.FullName,
                    ReporterSchoolId = x.Reporter!.SchoolIdNumber,
                    AssignedToId = x.Assignments.Where(a => a.IsCurrent)
                        .Select(a => a.AssignedToId).FirstOrDefault(),
                    AssignedToName = x.Assignments.Where(a => a.IsCurrent)
                        .Select(a => a.AssignedTo!.FullName).FirstOrDefault()
                })
                .FirstOrDefaultAsync();

            if (r == null) return null;

            var reporterId = r.ReporterId;

            var timeline = await _db.IncidentStatusHistories.AsNoTracking()
                .Where(h => h.IncidentReportId == id)
                .OrderBy(h => h.ChangedAtUtc)
                .Select(h => new StaffTimelineItem
                {
                    OldStatus = h.OldStatus,
                    NewStatus = h.NewStatus,
                    ChangedAtUtc = h.ChangedAtUtc,
                    ChangedByName = h.ChangedBy!.FullName,
                    Comment = h.Comment,
                    CommentVisibleToStudent = h.IsCommentVisibleToStudent
                })
                .ToListAsync();

            var messages = await _db.CaseMessages.AsNoTracking()
                .Where(m => m.IncidentReportId == id)
                .OrderBy(m => m.SentAtUtc)
                .Select(m => new StaffMessageItem
                {
                    Body = m.Body,
                    SentAtUtc = m.SentAtUtc,
                    SenderName = m.Sender!.FullName,
                    FromStudent = m.SenderId == reporterId,
                    IsInformationRequest = m.IsInformationRequest
                })
                .ToListAsync();

            var notes = await _db.InvestigationNotes.AsNoTracking()
                .Where(n => n.IncidentReportId == id)
                .OrderByDescending(n => n.CreatedAtUtc)
                .Select(n => new StaffNoteItem
                {
                    AuthorName = n.Author!.FullName,
                    NoteType = n.NoteType,
                    Content = n.Content,
                    CreatedAtUtc = n.CreatedAtUtc
                })
                .ToListAsync();

            var staffUsers = await _users.GetUsersInRoleAsync(AppRoles.Staff);
            var options = staffUsers
                .Where(u => u.IsActive)
                .OrderBy(u => u.FullName)
                .Select(u => new StaffOption { Id = u.Id, Name = u.FullName })
                .ToList();

            var mine = r.AssignedToId == staffId;
            var closed = r.Status == IncidentStatus.Closed;

            return new StaffCaseDetailsViewModel
            {
                Id = r.Id,
                CurrentUserId = staffId,
                ReferenceNumber = r.ReferenceNumber,
                Title = r.Title,
                Description = r.Description,
                CategoryName = r.CategoryName,
                IncidentDateTime = r.IncidentDateTime,
                Location = r.Location,
                PeopleInvolved = r.PeopleInvolved,
                Status = r.Status,
                ResolutionSummary = r.ResolutionSummary,
                CreatedAtUtc = r.CreatedAtUtc,
                UpdatedAtUtc = r.UpdatedAtUtc,
                ReporterName = r.ReporterName,
                ReporterSchoolId = r.ReporterSchoolId,
                AssignedToId = r.AssignedToId,
                AssignedToName = r.AssignedToName,

                // Resolved and "info requested" have their own forms, so they are not in this list
                AllowedStatuses = mine
                    ? StatusTransitions.From(r.Status)
                        .Where(s => s != IncidentStatus.Resolved &&
                                    s != IncidentStatus.AdditionalInformationRequested)
                        .ToList()
                    : new List<IncidentStatus>(),

                CanRequestInfo = mine && (r.Status == IncidentStatus.AdditionalInformationRequested ||
                                          StatusTransitions.IsAllowed(r.Status, IncidentStatus.AdditionalInformationRequested)),
                CanSendMessage = mine && !closed,
                CanResolve = mine && StatusTransitions.IsAllowed(r.Status, IncidentStatus.Resolved),
                CanAssign = !closed && (r.AssignedToId == null || mine),

                Timeline = timeline,
                Messages = messages,
                Notes = notes,
                StaffOptions = options
            };
        }

        // ---------- ACTIONS ----------

        // Loads a case for editing. The staff member must be in scope AND hold the case.
        private async Task<(IncidentReport? Report, OpResult? Error)> LoadForActionAsync(int id, string staffId)
        {
            var report = await InScope(_db.IncidentReports, staffId)
                .Include(r => r.Assignments.Where(a => a.IsCurrent))
                .FirstOrDefaultAsync(r => r.Id == id);

            if (report == null) return (null, OpResult.Missing());

            var current = report.Assignments.FirstOrDefault(a => a.IsCurrent);
            if (current == null || current.AssignedToId != staffId)
                return (null, OpResult.Fail("Assign this case to yourself before making changes."));

            return (report, null);
        }

        private void ApplyStatus(IncidentReport report, IncidentStatus target, string staffId,
                                 string? comment, bool visibleToStudent, DateTime now)
        {
            var hasComment = !string.IsNullOrWhiteSpace(comment);

            _db.IncidentStatusHistories.Add(new IncidentStatusHistory
            {
                IncidentReportId = report.Id,
                OldStatus = report.Status,
                NewStatus = target,
                ChangedById = staffId,
                ChangedAtUtc = now,
                Comment = hasComment ? comment!.Trim() : null,
                IsCommentVisibleToStudent = hasComment && visibleToStudent
            });

            report.Status = target;
            report.UpdatedAtUtc = now;

            if (target == IncidentStatus.Resolved) report.ResolvedAtUtc = now;
            else if (target == IncidentStatus.Closed) report.ClosedAtUtc = now;
            else
            {
                // Reopened or moved sideways: no longer resolved or closed
                report.ResolvedAtUtc = null;
                report.ClosedAtUtc = null;
            }
        }

        public async Task<OpResult> ChangeStatusAsync(
            int id, string staffId, IncidentStatus target, string? comment, bool visibleToStudent)
        {
            var (report, error) = await LoadForActionAsync(id, staffId);
            if (report == null) return error!;

            if (target == IncidentStatus.Resolved)
                return OpResult.Fail("Use the Resolve form to resolve a case.");
            if (target == IncidentStatus.AdditionalInformationRequested)
                return OpResult.Fail("Use the Request information form to ask the student for more information.");

            if (!StatusTransitions.IsAllowed(report.Status, target))
                return OpResult.Fail(
                    $"A case that is {StatusDisplay.Label(report.Status)} cannot be changed to {StatusDisplay.Label(target)}.");

            ApplyStatus(report, target, staffId, comment, visibleToStudent, DateTime.UtcNow);
            await _db.SaveChangesAsync();
            return OpResult.Ok($"Status changed to {StatusDisplay.Label(target)}.");
        }

        public async Task<OpResult> RequestInfoAsync(int id, string staffId, string body)
        {
            var (report, error) = await LoadForActionAsync(id, staffId);
            if (report == null) return error!;

            var now = DateTime.UtcNow;

            if (report.Status != IncidentStatus.AdditionalInformationRequested)
            {
                if (!StatusTransitions.IsAllowed(report.Status, IncidentStatus.AdditionalInformationRequested))
                    return OpResult.Fail(
                        $"You cannot request information while the case is {StatusDisplay.Label(report.Status)}.");

                ApplyStatus(report, IncidentStatus.AdditionalInformationRequested, staffId,
                            "Additional information requested.", true, now);
            }
            else
            {
                report.UpdatedAtUtc = now;
            }

            _db.CaseMessages.Add(new CaseMessage
            {
                IncidentReportId = report.Id,
                SenderId = staffId,
                Body = body,
                IsInformationRequest = true,
                SentAtUtc = now
            });

            await _db.SaveChangesAsync();
            return OpResult.Ok("Your request was sent to the student.");
        }

        public async Task<OpResult> SendMessageAsync(int id, string staffId, string body)
        {
            var (report, error) = await LoadForActionAsync(id, staffId);
            if (report == null) return error!;

            if (report.Status == IncidentStatus.Closed)
                return OpResult.Fail("This case is closed, so new messages cannot be added.");

            var now = DateTime.UtcNow;
            _db.CaseMessages.Add(new CaseMessage
            {
                IncidentReportId = report.Id,
                SenderId = staffId,
                Body = body,
                IsInformationRequest = false,
                SentAtUtc = now
            });
            report.UpdatedAtUtc = now;

            await _db.SaveChangesAsync();
            return OpResult.Ok("Your message was sent to the student.");
        }

        public async Task<OpResult> AddNoteAsync(int id, string staffId, NoteType type, string content)
        {
            var (report, error) = await LoadForActionAsync(id, staffId);
            if (report == null) return error!;

            var now = DateTime.UtcNow;
            _db.InvestigationNotes.Add(new InvestigationNote
            {
                IncidentReportId = report.Id,
                AuthorId = staffId,
                NoteType = type,
                Content = content,
                CreatedAtUtc = now
            });
            report.UpdatedAtUtc = now;

            await _db.SaveChangesAsync();
            return OpResult.Ok("Private note saved. Students cannot see it.");
        }

        public async Task<OpResult> ResolveAsync(int id, string staffId, string summary)
        {
            var (report, error) = await LoadForActionAsync(id, staffId);
            if (report == null) return error!;

            if (!StatusTransitions.IsAllowed(report.Status, IncidentStatus.Resolved))
                return OpResult.Fail(
                    $"A case that is {StatusDisplay.Label(report.Status)} cannot be resolved.");

            report.ResolutionSummary = summary;
            ApplyStatus(report, IncidentStatus.Resolved, staffId, "Case resolved.", true, DateTime.UtcNow);

            await _db.SaveChangesAsync();
            return OpResult.Ok("Case marked as Resolved. The student can now see your resolution summary.");
        }

        public async Task<OpResult> AssignAsync(int id, string actingStaffId, string assigneeId)
        {
            var report = await InScope(_db.IncidentReports, actingStaffId)
                .Include(r => r.Assignments.Where(a => a.IsCurrent))
                .FirstOrDefaultAsync(r => r.Id == id);

            if (report == null) return OpResult.Missing();
            if (report.Status == IncidentStatus.Closed)
                return OpResult.Fail("Closed cases cannot be reassigned.");

            var current = report.Assignments.FirstOrDefault(a => a.IsCurrent);

            if (current != null && current.AssignedToId != actingStaffId)
                return OpResult.Fail("Only the staff member who holds this case can reassign it.");
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

                // End the old assignment first, so the "one current assignment" index is never broken
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
                    AssignedById = actingStaffId,
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

            return assigneeId == actingStaffId
                ? OpResult.Ok("The case is now assigned to you.")
                : OpResult.Ok($"The case was assigned to {assignee.FullName}.");
        }
    }
}
