using Microsoft.EntityFrameworkCore;
using StudentIncidentSystem.Data;
using StudentIncidentSystem.Models;
using StudentIncidentSystem.ViewModels;

namespace StudentIncidentSystem.Services
{
    public class CategoryAdminService
    {
        private readonly ApplicationDbContext _db;

        public CategoryAdminService(ApplicationDbContext db)
        {
            _db = db;
        }

        public Task<List<CategoryListItem>> GetListAsync() =>
            _db.IncidentCategories.AsNoTracking()
                .OrderByDescending(c => c.IsActive)
                .ThenBy(c => c.Name)
                .Select(c => new CategoryListItem
                {
                    Id = c.Id,
                    Name = c.Name,
                    Description = c.Description,
                    IsActive = c.IsActive,
                    ReportCount = c.Reports.Count(),
                    CreatedAtUtc = c.CreatedAtUtc
                })
                .ToListAsync();

        public Task<CategoryFormViewModel?> GetAsync(int id) =>
            _db.IncidentCategories.AsNoTracking()
                .Where(c => c.Id == id)
                .Select(c => new CategoryFormViewModel { Id = c.Id, Name = c.Name, Description = c.Description })
                .FirstOrDefaultAsync();

        public async Task<OpResult> CreateAsync(string name, string? description)
        {
            name = name.Trim();
            if (await _db.IncidentCategories.AnyAsync(c => c.Name == name))
                return OpResult.Fail("A category with that name already exists.");

            _db.IncidentCategories.Add(new IncidentCategory
            {
                Name = name,
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                IsActive = true
            });

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                _db.ChangeTracker.Clear();
                return OpResult.Fail("A category with that name already exists.");
            }

            return OpResult.Ok($"Category \"{name}\" was added.");
        }

        public async Task<OpResult> UpdateAsync(int id, string name, string? description)
        {
            var category = await _db.IncidentCategories.FirstOrDefaultAsync(c => c.Id == id);
            if (category == null) return OpResult.Missing();

            name = name.Trim();
            if (await _db.IncidentCategories.AnyAsync(c => c.Name == name && c.Id != id))
                return OpResult.Fail("Another category already uses that name.");

            category.Name = name;
            category.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                _db.ChangeTracker.Clear();
                return OpResult.Fail("Another category already uses that name.");
            }

            return OpResult.Ok("Category updated.");
        }

        public async Task<OpResult> SetActiveAsync(int id, bool active)
        {
            var category = await _db.IncidentCategories.FirstOrDefaultAsync(c => c.Id == id);
            if (category == null) return OpResult.Missing();

            if (category.IsActive == active)
                return OpResult.Fail(active ? "That category is already active." : "That category is already inactive.");

            if (!active)
            {
                var othersActive = await _db.IncidentCategories.CountAsync(c => c.IsActive && c.Id != id);
                if (othersActive == 0)
                    return OpResult.Fail("At least one category must stay active so students can submit reports.");
            }

            category.IsActive = active;
            await _db.SaveChangesAsync();

            return OpResult.Ok(active
                ? $"\"{category.Name}\" is active again."
                : $"\"{category.Name}\" was deactivated. Existing reports keep it, but students can no longer choose it.");
        }
    }
}
