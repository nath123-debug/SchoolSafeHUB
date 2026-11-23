using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentIncidentSystem.Models;
using StudentIncidentSystem.Services;
using StudentIncidentSystem.ViewModels;

namespace StudentIncidentSystem.Controllers
{
    [Authorize(Roles = AppRoles.Administrator)]
    public class AuditLogsController : Controller
    {
        private readonly AuditService _audit;

        public AuditLogsController(AuditService audit)
        {
            _audit = audit;
        }

        public async Task<IActionResult> Index(
            string? q, string? outcome,
            [FromQuery(Name = "from")] DateTime? fromDate,
            [FromQuery(Name = "to")] DateTime? toDate,
            int page = 1)
        {
            if (!AuditService.Outcomes.Contains(outcome)) outcome = null;

            var logs = await _audit.QueryAsync(q, outcome, fromDate, toDate, page);
            return View(new AuditLogIndexViewModel
            {
                Logs = logs, Search = q, Outcome = outcome, From = fromDate, To = toDate
            });
        }
    }
}
