using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentIncidentSystem.Models;
using StudentIncidentSystem.Services;
using StudentIncidentSystem.ViewModels;

namespace StudentIncidentSystem.Controllers
{
    [Authorize(Roles = AppRoles.Administrator)]
    public class UserManagementController : Controller
    {
        private readonly UserAdminService _users;

        public UserManagementController(UserAdminService users)
        {
            _users = users;
        }

        private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        public async Task<IActionResult> Index(string? q, string? role, string? status, int page = 1)
        {
            return View(await _users.GetListAsync(UserId, q, role, status, page));
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new CreateUserViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateUserViewModel model)
        {
            if (!AppRoles.All.Contains(model.Role))
                ModelState.AddModelError(nameof(model.Role), "Please choose a valid role.");

            if (!ModelState.IsValid) return View(model);

            var result = await _users.CreateAsync(model);
            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.Message);
                return View(model);
            }

            TempData["Success"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(string id)
        {
            var model = await _users.GetEditAsync(id, UserId);
            if (model == null) return NotFound();
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProfile(string id, UpdateProfileViewModel form)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please enter a full name (up to 100 characters). The school ID can be up to 30.";
                return RedirectToAction(nameof(Edit), new { id });
            }
            return Finish(await _users.UpdateProfileAsync(id, form.FullName, form.SchoolIdNumber), id);
        }

        [HttpPost]
        public async Task<IActionResult> SetRole(string id, SetRoleViewModel form)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please choose a role.";
                return RedirectToAction(nameof(Edit), new { id });
            }
            return Finish(await _users.SetRoleAsync(id, UserId, form.Role), id);
        }

        [HttpPost]
        public async Task<IActionResult> Activate(string id) =>
            Finish(await _users.SetActiveAsync(id, UserId, true), id);

        [HttpPost]
        public async Task<IActionResult> Deactivate(string id) =>
            Finish(await _users.SetActiveAsync(id, UserId, false), id);

        [HttpPost]
        public async Task<IActionResult> ResetPassword(string id, ResetPasswordViewModel form)
        {
            if (!ModelState.IsValid || form.NewPassword != form.ConfirmPassword)
            {
                TempData["Error"] = "The passwords must match and be at least 8 characters.";
                return RedirectToAction(nameof(Edit), new { id });
            }
            return Finish(await _users.ResetPasswordAsync(id, form.NewPassword), id);
        }

        private IActionResult Finish(OpResult result, string id)
        {
            if (result.NotFound) return NotFound();
            TempData[result.Success ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(Edit), new { id });
        }
    }
}
