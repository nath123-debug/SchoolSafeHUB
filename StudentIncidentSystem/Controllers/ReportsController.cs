using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentIncidentSystem.Models;
using StudentIncidentSystem.Services;
using StudentIncidentSystem.ViewModels;

namespace StudentIncidentSystem.Controllers
{
    // Policy "CanViewReports" = Staff or Administrator (defined in Program.cs)
    [Authorize(Policy = "CanViewReports")]
    public class ReportsController : Controller
    {
        private readonly ReportService _reports;

        public ReportsController(ReportService reports)
        {
            _reports = reports;
        }

        private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        private bool IsAdmin => User.IsInRole(AppRoles.Administrator);

        public async Task<IActionResult> Index(ReportFilter filter, int page = 1)
        {
            return View(await _reports.GetAsync(filter, UserId, IsAdmin, page, 15));
        }

        public async Task<IActionResult> ExportCsv(ReportFilter filter)
        {
            var bytes = await _reports.ExportCsvAsync(filter, UserId, IsAdmin);
            return File(bytes, "text/csv", $"incident-report-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv");
        }

        public async Task<IActionResult> Print(ReportFilter filter)
        {
            return View(await _reports.GetAsync(filter, UserId, IsAdmin, 1, 500));
        }
    }
}
