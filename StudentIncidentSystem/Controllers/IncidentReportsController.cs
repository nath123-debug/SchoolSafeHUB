using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentIncidentSystem.Models;
using StudentIncidentSystem.Services;
using StudentIncidentSystem.ViewModels;

namespace StudentIncidentSystem.Controllers
{
    // Only students may use these pages. Staff use StaffIncidents (Stage 8).
    [Authorize(Roles = AppRoles.Student)]
    public class IncidentReportsController : Controller
    {
        private readonly IncidentService _incidents;

        public IncidentReportsController(IncidentService incidents)
        {
            _incidents = incidents;
        }

        private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        // GET /IncidentReports
        public async Task<IActionResult> Index(string? q, IncidentStatus? status, int page = 1)
        {
            var model = new MyReportsViewModel
            {
                Reports = await _incidents.GetMyReportsAsync(UserId, q, status, page),
                Search = q,
                StatusFilter = status
            };
            return View(model);
        }

        // GET /IncidentReports/Create
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var model = new CreateReportViewModel
            {
                Categories = await _incidents.GetActiveCategoriesAsync()
            };
            return View(model);
        }

        // POST /IncidentReports/Create  (anti-forgery is checked automatically)
        [HttpPost]
        public async Task<IActionResult> Create(CreateReportViewModel model)
        {
            if (model.CategoryId.HasValue && !await _incidents.CategoryIsActiveAsync(model.CategoryId.Value))
                ModelState.AddModelError(nameof(model.CategoryId), "Please choose a valid category.");

            if (model.IncidentDateTime.HasValue)
            {
                // 14-hour allowance covers time zones ahead of UTC
                if (model.IncidentDateTime.Value > DateTime.UtcNow.AddHours(14))
                    ModelState.AddModelError(nameof(model.IncidentDateTime), "The incident date cannot be in the future.");
                else if (model.IncidentDateTime.Value < DateTime.UtcNow.AddYears(-2))
                    ModelState.AddModelError(nameof(model.IncidentDateTime), "The incident date is too far in the past.");
            }

            if (!ModelState.IsValid)
            {
                model.Categories = await _incidents.GetActiveCategoriesAsync();
                return View(model);
            }

            var report = await _incidents.CreateReportAsync(UserId, model);

            TempData["Success"] = $"Your report was submitted. Your reference number is {report.ReferenceNumber}.";
            return RedirectToAction(nameof(Details), new { id = report.Id });
        }

        // GET /IncidentReports/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var model = await _incidents.GetStudentDetailsAsync(id, UserId);
            if (model == null) return NotFound(); // missing OR not yours: same answer
            return View(model);
        }

        // POST /IncidentReports/Reply/5
        [HttpPost]
        public async Task<IActionResult> Reply(int id, ReplyViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Your message cannot be empty or longer than 2000 characters.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var result = await _incidents.AddStudentReplyAsync(id, UserId, model.Body.Trim());

            switch (result)
            {
                case ReplyResult.NotFound:
                    return NotFound();
                case ReplyResult.Closed:
                    TempData["Error"] = "This case is closed, so new messages cannot be added.";
                    break;
                default:
                    TempData["Success"] = "Your message was sent.";
                    break;
            }
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}