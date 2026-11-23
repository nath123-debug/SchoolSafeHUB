using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentIncidentSystem.Models;
using StudentIncidentSystem.Services;

namespace StudentIncidentSystem.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly DashboardService _dashboard;

        public DashboardController(DashboardService dashboard)
        {
            _dashboard = dashboard;
        }

        // One URL, three dashboards depending on role: Administrator, then Staff, then Student.
        public async Task<IActionResult> Index(string? q, IncidentStatus? status, int? categoryId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            if (User.IsInRole(AppRoles.Administrator))
                return View("Admin", await _dashboard.GetAdminAsync());

            if (User.IsInRole(AppRoles.Staff))
                return View("Staff", await _dashboard.GetStaffAsync(userId, q, status, categoryId));

            if (User.IsInRole(AppRoles.Student))
                return View("Student", await _dashboard.GetStudentAsync(userId));

            return Forbid();
        }
    }
}
