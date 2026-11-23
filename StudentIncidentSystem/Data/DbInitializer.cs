using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StudentIncidentSystem.Models;

namespace StudentIncidentSystem.Data
{
    public static class DbInitializer
    {
        // FICTIONAL DEVELOPMENT PASSWORD. Never use this in a real deployment.
        private const string DevPassword = "Password123";

        public static async Task InitializeAsync(IServiceProvider services, bool seedTestAccounts)
        {
            var context = services.GetRequiredService<ApplicationDbContext>();
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

            await context.Database.MigrateAsync();

            foreach (var role in AppRoles.All)
            {
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));
            }

            await SeedCategoriesAsync(context);


                
            if (seedTestAccounts)
            {
                await CreateUserAsync(userManager, "admin@school.test", "Alex Administrator", "EMP-0001", AppRoles.Administrator);
                await CreateUserAsync(userManager, "counselor@school.test", "Carla Counselor", "EMP-0002", AppRoles.Staff);
                await CreateUserAsync(userManager, "counselor2@school.test", "Daniel Dela Cruz", "EMP-0003", AppRoles.Staff);
                await CreateUserAsync(userManager, "student1@school.test", "Sam Student", "STU-1001", AppRoles.Student);
                await CreateUserAsync(userManager, "student2@school.test", "Riley Reyes", "STU-1002", AppRoles.Student);
                await SeedSampleReportsAsync(context, userManager);
            }
        }

        private static async Task SeedSampleReportsAsync(
    ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            if (await context.IncidentReports.AnyAsync()) return;

            var s1 = await userManager.FindByEmailAsync("student1@school.test");
            var s2 = await userManager.FindByEmailAsync("student2@school.test");
            var staff = await userManager.FindByEmailAsync("counselor@school.test");
            if (s1 == null || s2 == null || staff == null) return;

            var categories = await context.IncidentCategories.ToListAsync();
            int CatId(string prefix) =>
                categories.First(c => c.Name.StartsWith(prefix, StringComparison.Ordinal)).Id;

            var now = DateTime.UtcNow;

            var samples = new (ApplicationUser Reporter, string Category, string Title, string Description,
                               string Location, int DaysAgo, IncidentStatus Status)[]
            {
                (s1, "Bullying", "Name-calling in the hallway", "Two students kept calling me names between classes.", "Main hallway", 40, IncidentStatus.Resolved),
                (s1, "Theft", "Missing calculator", "My calculator disappeared from my bag during lunch.", "Room 204", 25, IncidentStatus.UnderReview),
                (s2, "Classroom", "Repeated loud disruption", "A group keeps shouting during lessons.", "Room 105", 12, IncidentStatus.Submitted),
                (s2, "Damaged", "Broken window latch", "The window latch in the reading area is broken.", "Library", 8, IncidentStatus.Referred),
                (s1, "Safety", "Wet floor near cafeteria", "The floor is often wet and slippery after lunch.", "Cafeteria", 3, IncidentStatus.Submitted),
                (s2, "Verbal", "Argument during PE", "Two classmates argued loudly during a game.", "Gymnasium", 70, IncidentStatus.Closed),
                (s1, "Other", "Lost ID lanyard", "I lost my ID lanyard somewhere near the lockers.", "Locker area", 5, IncidentStatus.AdditionalInformationRequested)
            };

            var counter = 0;
            foreach (var s in samples)
            {
                counter++;
                var created = now.AddDays(-s.DaysAgo);
                var updated = created.AddDays(Math.Min(s.DaysAgo, 2));
                var finished = s.Status is IncidentStatus.Resolved or IncidentStatus.Closed;

                var report = new IncidentReport
                {
                    ReferenceNumber = $"INC-{created.Year}-{counter:D6}",
                    ReporterId = s.Reporter.Id,
                    CategoryId = CatId(s.Category),
                    Title = s.Title,
                    Description = s.Description,
                    IncidentDateTime = created.AddHours(-2),
                    Location = s.Location,
                    Status = s.Status,
                    CreatedAtUtc = created,
                    UpdatedAtUtc = updated,
                    ResolvedAtUtc = finished ? updated : null,
                    ClosedAtUtc = s.Status == IncidentStatus.Closed ? updated : null
                };

                report.StatusHistory.Add(new IncidentStatusHistory
                {
                    OldStatus = null,
                    NewStatus = IncidentStatus.Submitted,
                    ChangedById = s.Reporter.Id,
                    ChangedAtUtc = created
                });

                if (s.Status != IncidentStatus.Submitted)
                {
                    report.StatusHistory.Add(new IncidentStatusHistory
                    {
                        OldStatus = IncidentStatus.Submitted,
                        NewStatus = s.Status,
                        ChangedById = staff.Id,
                        ChangedAtUtc = updated,
                        Comment = "Sample status update.",
                        IsCommentVisibleToStudent = true
                    });

                    report.Assignments.Add(new CaseAssignment
                    {
                        AssignedToId = staff.Id,
                        AssignedById = staff.Id,
                        AssignedAtUtc = updated,
                        IsCurrent = true
                    });
                }

                context.IncidentReports.Add(report);
            }

            await context.SaveChangesAsync();
        }

        private static async Task SeedCategoriesAsync(ApplicationDbContext context)
        {
            if (await context.IncidentCategories.AnyAsync()) return;

            context.IncidentCategories.AddRange(
                new IncidentCategory { Name = "Bullying and Harassment", Description = "Repeated unwanted behavior intended to hurt or intimidate." },
                new IncidentCategory { Name = "Verbal or Physical Conflict", Description = "Arguments, fights, or threats between students." },
                new IncidentCategory { Name = "Classroom Disturbance", Description = "Behavior that disrupts learning." },
                new IncidentCategory { Name = "Theft or Missing Belongings", Description = "Lost, stolen, or missing personal or school items." },
                new IncidentCategory { Name = "Damaged School Property", Description = "Vandalism or accidental damage to school property." },
                new IncidentCategory { Name = "Safety Hazard", Description = "Broken equipment, unsafe areas, or other risks." },
                new IncidentCategory { Name = "Violation of School Rules", Description = "Behavior that goes against the school's code of conduct." },
                new IncidentCategory { Name = "Other", Description = "Anything that does not fit the categories above." }
            );
            await context.SaveChangesAsync();
        }

        private static async Task CreateUserAsync(
            UserManager<ApplicationUser> userManager,
            string email, string fullName, string schoolId, string role)
        {
            if (await userManager.FindByEmailAsync(email) != null) return;

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = fullName,
                SchoolIdNumber = schoolId,
                IsActive = true
            };

            var result = await userManager.CreateAsync(user, DevPassword);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not seed user {email}: " +
                    string.Join("; ", result.Errors.Select(e => e.Description)));
            }

            await userManager.AddToRoleAsync(user, role);
        }
    }
}