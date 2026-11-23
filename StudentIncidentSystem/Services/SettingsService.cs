using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using StudentIncidentSystem.Data;
using StudentIncidentSystem.Models;

namespace StudentIncidentSystem.Services
{
    public record AppSettingsSnapshot(string SchoolName, int MaxFileMb, int MaxFilesPerReport)
    {
        public static AppSettingsSnapshot Defaults => new("Incident System", 5, 5);
    }

    public class SettingsService
    {
        private const string CacheKey = "app-settings";
        private const string SchoolNameKey = "SchoolName";
        private const string MaxFileMbKey = "MaxFileMb";
        private const string MaxFilesKey = "MaxFilesPerReport";

        private readonly IServiceScopeFactory _scopes;
        private readonly IMemoryCache _cache;
        private readonly ILogger<SettingsService> _logger;

        public SettingsService(IServiceScopeFactory scopes, IMemoryCache cache, ILogger<SettingsService> logger)
        {
            _scopes = scopes;
            _cache = cache;
            _logger = logger;
        }

        public async Task<AppSettingsSnapshot> GetAsync()
        {
            if (_cache.TryGetValue(CacheKey, out AppSettingsSnapshot? cached) && cached != null)
                return cached;

            var defaults = AppSettingsSnapshot.Defaults;
            try
            {
                using var scope = _scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var rows = await db.AppSettings.AsNoTracking().ToDictionaryAsync(s => s.Key, s => s.Value);

                var snapshot = new AppSettingsSnapshot(
                    rows.TryGetValue(SchoolNameKey, out var name) && !string.IsNullOrWhiteSpace(name) ? name : defaults.SchoolName,
                    ReadInt(rows, MaxFileMbKey, defaults.MaxFileMb),
                    ReadInt(rows, MaxFilesKey, defaults.MaxFilesPerReport));

                _cache.Set(CacheKey, snapshot, TimeSpan.FromMinutes(5));
                return snapshot;
            }
            catch (Exception ex)
            {
                // The page must still work if settings cannot be read
                _logger.LogWarning(ex, "Could not read application settings. Using defaults.");
                return defaults;
            }
        }

        public async Task SaveAsync(AppSettingsSnapshot s)
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            await UpsertAsync(db, SchoolNameKey, s.SchoolName.Trim());
            await UpsertAsync(db, MaxFileMbKey, Math.Clamp(s.MaxFileMb, 1, 10).ToString());
            await UpsertAsync(db, MaxFilesKey, Math.Clamp(s.MaxFilesPerReport, 1, 10).ToString());

            await db.SaveChangesAsync();
            _cache.Remove(CacheKey);
        }

        private static async Task UpsertAsync(ApplicationDbContext db, string key, string value)
        {
            var row = await db.AppSettings.FirstOrDefaultAsync(x => x.Key == key);
            if (row == null)
                db.AppSettings.Add(new AppSetting { Key = key, Value = value });
            else
            {
                row.Value = value;
                row.UpdatedAtUtc = DateTime.UtcNow;
            }
        }

        private static int ReadInt(Dictionary<string, string> rows, string key, int fallback)
        {
            if (rows.TryGetValue(key, out var text) && int.TryParse(text, out var n))
                return Math.Clamp(n, 1, 10);
            return fallback;
        }
    }
}
