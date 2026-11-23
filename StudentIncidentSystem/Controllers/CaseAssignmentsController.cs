using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentIncidentSystem.Models;
using StudentIncidentSystem.Services;
using StudentIncidentSystem.ViewModels;

namespace StudentIncidentSystem.Controllers
{
    [Authorize(Roles = AppRoles.Administrator)]
    public class CaseAssignmentsController : Controller
    {
        private readonly CaseAssignmentService _assignments;

        public CaseAssignmentsController(CaseAssignmentService assignments)
        {
            _assignments = assignments;
        }

        private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        public async Task<IActionResult> Index(string? q, IncidentStatus? status, string? show, int page = 1)
        {
            return View(await _assignments.GetListAsync(q, status, show, page));
        }

        [HttpPost]
        public async Task<IActionResult> Assign(int id, AdminAssignViewModel form)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please choose a staff member.";
                return RedirectToAction(nameof(Index));
            }

            var result = await _assignments.AssignAsync(id, form.AssigneeId, UserId);
            if (result.NotFound) return NotFound();

            TempData[result.Success ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(Index));
        }
    }
}
