using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentIncidentSystem.Models;
using StudentIncidentSystem.Services;
using StudentIncidentSystem.ViewModels;

namespace StudentIncidentSystem.Controllers
{
    [Authorize(Roles = AppRoles.Administrator)]
    public class SettingsController : Controller
    {
        private readonly SettingsService _settings;

        public SettingsController(SettingsService settings)
        {
            _settings = settings;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var s = await _settings.GetAsync();
            return View(new SettingsViewModel
            {
                SchoolName = s.SchoolName,
                MaxFileMb = s.MaxFileMb,
                MaxFilesPerReport = s.MaxFilesPerReport
            });
        }

        [HttpPost]
        public async Task<IActionResult> Index(SettingsViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            await _settings.SaveAsync(new AppSettingsSnapshot(
                model.SchoolName.Trim(), model.MaxFileMb, model.MaxFilesPerReport));

            TempData["Success"] = "Settings saved.";
            return RedirectToAction(nameof(Index));
        }
    }
}
