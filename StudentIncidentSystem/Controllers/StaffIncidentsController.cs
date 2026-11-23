using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentIncidentSystem.Models;
using StudentIncidentSystem.Services;
using StudentIncidentSystem.ViewModels;

namespace StudentIncidentSystem.Controllers
{
    // Staff only. Every action also checks scope inside StaffCaseService.
    [Authorize(Roles = AppRoles.Staff)]
    public class StaffIncidentsController : Controller
    {
        private readonly StaffCaseService _cases;

        public StaffIncidentsController(StaffCaseService cases)
        {
            _cases = cases;
        }

        private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        // GET /StaffIncidents
        public async Task<IActionResult> Index(
            string? q, IncidentStatus? status, int? categoryId, string? scope, int page = 1)
        {
            return View(await _cases.GetListAsync(UserId, q, status, categoryId, scope, page));
        }

        // GET /StaffIncidents/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var model = await _cases.GetDetailsAsync(id, UserId);
            if (model == null) return NotFound(); // missing OR outside your scope: same answer
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> ChangeStatus(int id, ChangeStatusViewModel form)
        {
            if (!ModelState.IsValid || form.NewStatus == null)
            {
                TempData["Error"] = "Please choose a new status. Comments can be up to 500 characters.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var result = await _cases.ChangeStatusAsync(
                id, UserId, form.NewStatus.Value, form.Comment, form.VisibleToStudent);
            return Finish(result, id);
        }

        [HttpPost]
        public async Task<IActionResult> Assign(int id, AssignViewModel form)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please choose a staff member.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var result = await _cases.AssignAsync(id, UserId, form.AssigneeId);
            if (result.NotFound) return NotFound();

            TempData[result.Success ? "Success" : "Error"] = result.Message;

            // After handing the case to someone else, you no longer have access to it
            if (result.Success && form.AssigneeId != UserId)
                return RedirectToAction(nameof(Index));

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> RequestInfo(int id, StaffMessageViewModel form)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Your request cannot be empty or longer than 2000 characters.";
                return RedirectToAction(nameof(Details), new { id });
            }

            return Finish(await _cases.RequestInfoAsync(id, UserId, form.Body.Trim()), id);
        }

        [HttpPost]
        public async Task<IActionResult> SendMessage(int id, StaffMessageViewModel form)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Your message cannot be empty or longer than 2000 characters.";
                return RedirectToAction(nameof(Details), new { id });
            }

            return Finish(await _cases.SendMessageAsync(id, UserId, form.Body.Trim()), id);
        }

        [HttpPost]
        public async Task<IActionResult> AddNote(int id, NoteViewModel form)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "A note cannot be empty or longer than 4000 characters.";
                return RedirectToAction(nameof(Details), new { id });
            }

            return Finish(await _cases.AddNoteAsync(id, UserId, form.NoteType, form.Content.Trim()), id);
        }

        [HttpPost]
        public async Task<IActionResult> Resolve(int id, ResolveViewModel form)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please write a resolution summary (10 to 2000 characters).";
                return RedirectToAction(nameof(Details), new { id });
            }

            return Finish(await _cases.ResolveAsync(id, UserId, form.ResolutionSummary.Trim()), id);
        }

        private IActionResult Finish(OpResult result, int id)
        {
            if (result.NotFound) return NotFound();
            TempData[result.Success ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
