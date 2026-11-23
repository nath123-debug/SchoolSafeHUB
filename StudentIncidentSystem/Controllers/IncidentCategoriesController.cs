using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentIncidentSystem.Models;
using StudentIncidentSystem.Services;
using StudentIncidentSystem.ViewModels;

namespace StudentIncidentSystem.Controllers
{
    [Authorize(Roles = AppRoles.Administrator)]
    public class IncidentCategoriesController : Controller
    {
        private readonly CategoryAdminService _categories;

        public IncidentCategoriesController(CategoryAdminService categories)
        {
            _categories = categories;
        }

        public async Task<IActionResult> Index()
        {
            return View(await _categories.GetListAsync());
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View("Form", new CategoryFormViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> Create(CategoryFormViewModel model)
        {
            if (!ModelState.IsValid) return View("Form", model);

            var result = await _categories.CreateAsync(model.Name, model.Description);
            if (!result.Success)
            {
                ModelState.AddModelError(nameof(model.Name), result.Message);
                return View("Form", model);
            }

            TempData["Success"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var model = await _categories.GetAsync(id);
            if (model == null) return NotFound();
            return View("Form", model);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(int id, CategoryFormViewModel model)
        {
            model.Id = id;
            if (!ModelState.IsValid) return View("Form", model);

            var result = await _categories.UpdateAsync(id, model.Name, model.Description);
            if (result.NotFound) return NotFound();
            if (!result.Success)
            {
                ModelState.AddModelError(nameof(model.Name), result.Message);
                return View("Form", model);
            }

            TempData["Success"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Activate(int id) => await Toggle(id, true);

        [HttpPost]
        public async Task<IActionResult> Deactivate(int id) => await Toggle(id, false);

        private async Task<IActionResult> Toggle(int id, bool active)
        {
            var result = await _categories.SetActiveAsync(id, active);
            if (result.NotFound) return NotFound();
            TempData[result.Success ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(Index));
        }
    }
}
