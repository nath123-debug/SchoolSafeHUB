using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using StudentIncidentSystem.Models;
using StudentIncidentSystem.ViewModels;

namespace StudentIncidentSystem.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly UserManager<ApplicationUser> _users;
        private readonly SignInManager<ApplicationUser> _signIn;

        public ProfileController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn)
        {
            _users = users;
            _signIn = signIn;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _users.GetUserAsync(User);
            if (user == null) return Challenge();

            var roles = await _users.GetRolesAsync(user);
            return View(new ProfileViewModel
            {
                Email = user.Email ?? string.Empty,
                FullName = user.FullName,
                SchoolIdNumber = user.SchoolIdNumber,
                Role = roles.FirstOrDefault(),
                CreatedAtUtc = user.CreatedAtUtc
            });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateDetails(UpdateOwnProfileViewModel form)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please enter your full name (up to 100 characters).";
                return RedirectToAction(nameof(Index));
            }

            var user = await _users.GetUserAsync(User);
            if (user == null) return Challenge();

            user.FullName = form.FullName.Trim();
            var result = await _users.UpdateAsync(user);
            if (!result.Succeeded)
            {
                TempData["Error"] = string.Join(" ", result.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Index));
            }

            await _signIn.RefreshSignInAsync(user); // so the new name shows in the sidebar
            TempData["Success"] = "Your details were updated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel form)
        {
            if (!ModelState.IsValid || form.NewPassword != form.ConfirmPassword)
            {
                TempData["Error"] = "The new passwords must match and be at least 8 characters.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _users.GetUserAsync(User);
            if (user == null) return Challenge();

            // Wrong guesses count toward the account lockout, like at login
            var check = await _signIn.CheckPasswordSignInAsync(user, form.CurrentPassword, lockoutOnFailure: true);
            if (check.IsLockedOut)
            {
                TempData["Error"] = "Too many wrong attempts. Your account is locked for 10 minutes.";
                return RedirectToAction(nameof(Index));
            }
            if (!check.Succeeded)
            {
                TempData["Error"] = "Your current password is not correct.";
                return RedirectToAction(nameof(Index));
            }

            var result = await _users.ChangePasswordAsync(user, form.CurrentPassword, form.NewPassword);
            if (!result.Succeeded)
            {
                TempData["Error"] = string.Join(" ", result.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Index));
            }

            await _signIn.RefreshSignInAsync(user); // keeps this session signed in
            TempData["Success"] = "Your password was changed.";
            return RedirectToAction(nameof(Index));
        }
    }
}
