using Microsoft.EntityFrameworkCore;
using StudentIncidentSystem.Data;
using StudentIncidentSystem.Models;
using StudentIncidentSystem.ViewModels;

namespace StudentIncidentSystem.Services
{
    public class AuditService
    {
        public static readonly string[] Outcomes = { "Success", "Failed", "Denied", "NotFound", "Error" };

        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<AuditService> _logger;

        public AuditService(IServiceScopeFactory scopes, ILogger<AuditService> logger)
        {
            _scopes = scopes;
            _logger = logger;
        }

        // Uses its own database scope, and never throws: a logging problem must not break a request.
        public async Task LogAsync(string? userId, string? userName, string action,
            string? entityType, string? entityId, string? details, string? ipAddress)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                db.AuditLogs.Add(new AuditLog
                {
                    UserId = Cut(userId, 450),
                    UserName = Cut(userName, 256),
                    Action = Cut(action, 100) ?? "Unknown",
                    EntityType = Cut(entityType, 100),
                    EntityId = Cut(entityId, 50),
                    Details = Cut(details, 1000),
                    IpAddress = Cut(ipAddress, 45),
                    TimestampUtc = DateTime.UtcNow
                });

                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not write audit log entry {Action}", action);
            }
        }

        public async Task<PagedResult<AuditLog>> QueryAsync(
            string? q, string? outcome, DateTime? fromDate, DateTime? toDate, int page, int pageSize = 20)
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var query = db.AuditLogs.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();
                query = query.Where(l => l.Action.Contains(q) ||
                                         l.UserName!.Contains(q) ||
                                         l.EntityId!.Contains(q) ||
                                         l.Details!.Contains(q));
            }
            if (!string.IsNullOrEmpty(outcome))
                query = query.Where(l => l.Details!.StartsWith(outcome));
            if (fromDate.HasValue)
            {
                var start = fromDate.Value.Date;
                query = query.Where(l => l.TimestampUtc >= start);
            }
            if (toDate.HasValue)
            {
                var end = toDate.Value.Date.AddDays(1);
                query = query.Where(l => l.TimestampUtc < end);
            }

            var total = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            page = Math.Clamp(page, 1, totalPages);

            var items = await query
                .OrderByDescending(l => l.TimestampUtc).ThenByDescending(l => l.Id)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .ToListAsync();

            return new PagedResult<AuditLog> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
        }

        private static string? Cut(string? s, int max) =>
            s == null ? null : (s.Length <= max ? s : s[..max]);
    }
}
