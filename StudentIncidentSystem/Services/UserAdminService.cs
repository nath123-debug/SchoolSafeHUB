using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StudentIncidentSystem.Data;
using StudentIncidentSystem.Models;
using StudentIncidentSystem.ViewModels;

namespace StudentIncidentSystem.Services
{
    public class UserAdminService
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _users;

        public UserAdminService(ApplicationDbContext db, UserManager<ApplicationUser> users)
        {
            _db = db;
            _users = users;
        }

        // ---------- LIST ----------

        public async Task<UserIndexViewModel> GetListAsync(
            string currentUserId, string? search, string? role, string? status,
            int page, int pageSize = 10)
        {
            var query = _db.Users.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                query = query.Where(u => u.Email!.Contains(search) ||
                                         u.FullName.Contains(search) ||
                                         (u.SchoolIdNumber != null && u.SchoolIdNumber.Contains(search)));
            }

            if (!string.IsNullOrEmpty(role) && AppRoles.All.Contains(role))
            {
                var roleId = await _db.Roles.Where(r => r.Name == role)
                    .Select(r => r.Id).FirstOrDefaultAsync();
                query = query.Where(u => _db.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == roleId));
            }
            else
            {
                role = null;
            }

            if (status == "active") query = query.Where(u => u.IsActive);
            else if (status == "inactive") query = query.Where(u => !u.IsActive);
            else status = null;

            var total = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            page = Math.Clamp(page, 1, totalPages);

            var now = DateTimeOffset.UtcNow;

            var items = await query
                .OrderBy(u => u.FullName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new UserListItem
                {
                    Id = u.Id,
                    Email = u.Email ?? string.Empty,
                    FullName = u.FullName,
                    SchoolIdNumber = u.SchoolIdNumber,
                    IsActive = u.IsActive,
                    CreatedAtUtc = u.CreatedAtUtc,
                    IsLockedOut = u.LockoutEnd != null && u.LockoutEnd > now,
                    Role = (from ur in _db.UserRoles
                            join r in _db.Roles on ur.RoleId equals r.Id
                            where ur.UserId == u.Id
                            select r.Name).FirstOrDefault()
                })
                .ToListAsync();

            return new UserIndexViewModel
            {
                Users = new PagedResult<UserListItem>
                {
                    Items = items, Page = page, PageSize = pageSize, TotalCount = total
                },
                Search = search,
                Role = role,
                Status = status,
                CurrentUserId = currentUserId
            };
        }

        // ---------- DETAILS ----------

        public async Task<EditUserViewModel?> GetEditAsync(string id, string currentUserId)
        {
            var user = await _users.FindByIdAsync(id);
            if (user == null) return null;

            var roles = await _users.GetRolesAsync(user);
            var openCases = roles.Contains(AppRoles.Staff) ? await OpenCaseCountAsync(id) : 0;

            return new EditUserViewModel
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                FullName = user.FullName,
                SchoolIdNumber = user.SchoolIdNumber,
                Role = roles.FirstOrDefault(),
                IsActive = user.IsActive,
                IsLockedOut = await _users.IsLockedOutAsync(user),
                IsSelf = user.Id == currentUserId,
                OpenCaseCount = openCases,
                CreatedAtUtc = user.CreatedAtUtc
            };
        }

        // Cases this staff member holds that are not Closed yet
        private Task<int> OpenCaseCountAsync(string staffId) =>
            _db.CaseAssignments.CountAsync(a =>
                a.IsCurrent && a.AssignedToId == staffId &&
                a.IncidentReport!.Status != IncidentStatus.Closed);

        // ---------- ACTIONS ----------

        public async Task<OpResult> CreateAsync(CreateUserViewModel m)
        {
            if (!AppRoles.All.Contains(m.Role))
                return OpResult.Fail("Please choose a valid role.");

            var user = new ApplicationUser
            {
                UserName = m.Email.Trim(),
                Email = m.Email.Trim(),
                EmailConfirmed = true,
                FullName = m.FullName.Trim(),
                SchoolIdNumber = string.IsNullOrWhiteSpace(m.SchoolIdNumber) ? null : m.SchoolIdNumber.Trim(),
                IsActive = true
            };

            var result = await _users.CreateAsync(user, m.Password);
            if (!result.Succeeded)
                return OpResult.Fail(string.Join(" ", result.Errors.Select(e => e.Description)));

            await _users.AddToRoleAsync(user, m.Role);
            return OpResult.Ok($"Account created for {user.FullName} as {m.Role}.");
        }

        public async Task<OpResult> UpdateProfileAsync(string id, string fullName, string? schoolId)
        {
            var user = await _users.FindByIdAsync(id);
            if (user == null) return OpResult.Missing();

            user.FullName = fullName.Trim();
            user.SchoolIdNumber = string.IsNullOrWhiteSpace(schoolId) ? null : schoolId.Trim();

            var result = await _users.UpdateAsync(user);
            return result.Succeeded
                ? OpResult.Ok("Account details updated.")
                : OpResult.Fail(string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        public async Task<OpResult> SetRoleAsync(string id, string actingUserId, string role)
        {
            if (!AppRoles.All.Contains(role))
                return OpResult.Fail("Please choose a valid role.");

            var user = await _users.FindByIdAsync(id);
            if (user == null) return OpResult.Missing();

            if (id == actingUserId)
                return OpResult.Fail("You cannot change your own role.");

            var current = (await _users.GetRolesAsync(user)).ToList();
            if (current.Count == 1 && current[0] == role)
                return OpResult.Fail("That user already has this role.");

            if (current.Contains(AppRoles.Staff) && role != AppRoles.Staff)
            {
                var open = await OpenCaseCountAsync(id);
                if (open > 0)
                    return OpResult.Fail(
                        $"{user.FullName} still holds {open} open case(s). Reassign them on the Case assignments page first.");
            }

            if (current.Count > 0)
            {
                var removed = await _users.RemoveFromRolesAsync(user, current);
                if (!removed.Succeeded)
                    return OpResult.Fail(string.Join(" ", removed.Errors.Select(e => e.Description)));
            }

            var added = await _users.AddToRoleAsync(user, role);
            if (!added.Succeeded)
            {
                if (current.Count > 0) await _users.AddToRolesAsync(user, current); // put the old roles back
                return OpResult.Fail(string.Join(" ", added.Errors.Select(e => e.Description)));
            }

            // Forces the user's old login cookie to expire, so the new role applies
            await _users.UpdateSecurityStampAsync(user);
            return OpResult.Ok($"{user.FullName} is now {role}.");
        }

        public async Task<OpResult> SetActiveAsync(string id, string actingUserId, bool active)
        {
            var user = await _users.FindByIdAsync(id);
            if (user == null) return OpResult.Missing();

            if (!active && id == actingUserId)
                return OpResult.Fail("You cannot deactivate your own account.");

            if (user.IsActive == active)
                return OpResult.Fail(active ? "This account is already active." : "This account is already deactivated.");

            if (!active && await _users.IsInRoleAsync(user, AppRoles.Staff))
            {
                var open = await OpenCaseCountAsync(id);
                if (open > 0)
                    return OpResult.Fail(
                        $"{user.FullName} still holds {open} open case(s). Reassign them on the Case assignments page first.");
            }

            user.IsActive = active;
            var result = await _users.UpdateAsync(user);
            if (!result.Succeeded)
                return OpResult.Fail(string.Join(" ", result.Errors.Select(e => e.Description)));

            if (!active) await _users.UpdateSecurityStampAsync(user); // ends any open session soon

            return OpResult.Ok(active
                ? $"{user.FullName}'s account was activated."
                : $"{user.FullName}'s account was deactivated.");
        }

        public async Task<OpResult> ResetPasswordAsync(string id, string newPassword)
        {
            var user = await _users.FindByIdAsync(id);
            if (user == null) return OpResult.Missing();

            var token = await _users.GeneratePasswordResetTokenAsync(user);
            var result = await _users.ResetPasswordAsync(user, token, newPassword);
            if (!result.Succeeded)
                return OpResult.Fail(string.Join(" ", result.Errors.Select(e => e.Description)));

            await _users.SetLockoutEndDateAsync(user, null);
            await _users.ResetAccessFailedCountAsync(user);

            return OpResult.Ok($"Password reset for {user.FullName}. Share the temporary password securely.");
        }
    }
}
