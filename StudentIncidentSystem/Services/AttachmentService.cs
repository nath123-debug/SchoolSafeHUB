using System.IO.Compression;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using StudentIncidentSystem.Data;
using StudentIncidentSystem.Models;
using StudentIncidentSystem.ViewModels;

namespace StudentIncidentSystem.Services
{
    public record UploadResult(int Saved, List<string> Errors, bool NotFound = false);
    public record AttachmentDownload(string PhysicalPath, string ContentType, string DownloadName);

    public class AttachmentService
    {
        private sealed record FileKind(string Ext, string ContentType, string[] Extensions);

        private static readonly FileKind Jpeg = new(".jpg", "image/jpeg", new[] { ".jpg", ".jpeg" });
        private static readonly FileKind Png = new(".png", "image/png", new[] { ".png" });
        private static readonly FileKind Gif = new(".gif", "image/gif", new[] { ".gif" });
        private static readonly FileKind Webp = new(".webp", "image/webp", new[] { ".webp" });
        private static readonly FileKind Pdf = new(".pdf", "application/pdf", new[] { ".pdf" });
        private static readonly FileKind Docx = new(".docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document", new[] { ".docx" });

        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".pdf", ".docx" };

        private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        private static readonly byte[] Gif87 = Encoding.ASCII.GetBytes("GIF87a");
        private static readonly byte[] Gif89 = Encoding.ASCII.GetBytes("GIF89a");
        private static readonly byte[] Riff = Encoding.ASCII.GetBytes("RIFF");
        private static readonly byte[] WebpTag = Encoding.ASCII.GetBytes("WEBP");
        private static readonly byte[] PdfSignature = Encoding.ASCII.GetBytes("%PDF-");
        private static readonly byte[] ZipSignature = { 0x50, 0x4B, 0x03, 0x04 };

        private readonly ApplicationDbContext _db;
        private readonly SettingsService _settings;
        private readonly string _root;

        public AttachmentService(ApplicationDbContext db, IWebHostEnvironment env,
                                 IConfiguration config, SettingsService settings)
        {
            _db = db;
            _settings = settings;

            // Outside wwwroot, so the web server can never serve these files directly
            var configured = config["Uploads:Path"];
            _root = string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(env.ContentRootPath, "App_Data", "Uploads")
                : Path.GetFullPath(configured);
        }

        // ---------- Who may see a report's files ----------

        // Students: their own reports. Staff: cases in their scope. Everyone else (including administrators): no.
        public async Task<bool> CanViewAsync(int reportId, string userId, string role)
        {
            if (role == AppRoles.Student)
                return await _db.IncidentReports.AnyAsync(r => r.Id == reportId && r.ReporterId == userId);

            if (role == AppRoles.Staff)
                return await _db.IncidentReports.AnyAsync(r => r.Id == reportId &&
                    (!r.Assignments.Any(a => a.IsCurrent) ||
                     r.Assignments.Any(a => a.IsCurrent && a.AssignedToId == userId)));

            return false;
        }

        public async Task<AttachmentsPanelViewModel?> GetPanelAsync(int reportId, string userId, string role)
        {
            if (!await CanViewAsync(reportId, userId, role)) return null;

            var snap = await _settings.GetAsync();

            var items = await _db.IncidentAttachments.AsNoTracking()
                .Where(a => a.IncidentReportId == reportId)
                .OrderBy(a => a.UploadedAtUtc)
                .Select(a => new AttachmentItem
                {
                    Id = a.Id,
                    OriginalFileName = a.OriginalFileName,
                    ContentType = a.ContentType,
                    FileSizeBytes = a.FileSizeBytes,
                    UploadedAtUtc = a.UploadedAtUtc
                })
                .ToListAsync();

            var status = await _db.IncidentReports.AsNoTracking()
                .Where(r => r.Id == reportId).Select(r => r.Status).FirstAsync();

            var isStudent = role == AppRoles.Student;
            var closed = status == IncidentStatus.Closed;
            var remaining = Math.Max(0, snap.MaxFilesPerReport - items.Count);

            return new AttachmentsPanelViewModel
            {
                ReportId = reportId,
                Items = items,
                IsStudent = isStudent,
                IsClosed = closed,
                CanUpload = isStudent && !closed && remaining > 0,
                RemainingSlots = remaining,
                MaxFileMb = snap.MaxFileMb,
                MaxFiles = snap.MaxFilesPerReport
            };
        }

        // ---------- Download ----------

        public async Task<AttachmentDownload?> GetDownloadAsync(int attachmentId, string userId, string role)
        {
            var a = await _db.IncidentAttachments.AsNoTracking()
                .Where(x => x.Id == attachmentId)
                .Select(x => new { x.IncidentReportId, x.OriginalFileName, x.StoredFileName, x.ContentType })
                .FirstOrDefaultAsync();

            if (a == null) return null;
            if (!await CanViewAsync(a.IncidentReportId, userId, role)) return null;

            // Only the file-name part is used, so a stored value can never point outside the folder
            var stored = Path.GetFileName(a.StoredFileName);
            return new AttachmentDownload(Path.Combine(_root, stored), a.ContentType, a.OriginalFileName);
        }

        // ---------- Upload ----------

        public async Task<UploadResult> UploadAsync(int reportId, string studentId, IEnumerable<IFormFile> files)
        {
            var report = await _db.IncidentReports
                .FirstOrDefaultAsync(r => r.Id == reportId && r.ReporterId == studentId);
            if (report == null) return new UploadResult(0, new List<string>(), true);

            var errors = new List<string>();

            if (report.Status == IncidentStatus.Closed)
            {
                errors.Add("This case is closed, so new files cannot be added.");
                return new UploadResult(0, errors);
            }

            var chosen = files.Where(f => f.Length > 0).ToList();
            if (chosen.Count == 0)
            {
                errors.Add("Please choose at least one file.");
                return new UploadResult(0, errors);
            }

            var snap = await _settings.GetAsync();
            var maxBytes = (long)snap.MaxFileMb * 1024 * 1024;

            var existing = await _db.IncidentAttachments.CountAsync(a => a.IncidentReportId == reportId);
            var slots = snap.MaxFilesPerReport - existing;

            Directory.CreateDirectory(_root);

            var written = new List<string>();
            var now = DateTime.UtcNow;

            foreach (var file in chosen)
            {
                var name = SanitizeName(file.FileName);
                var ext = Path.GetExtension(name).ToLowerInvariant();
                var shown = ShortName(name);

                if (slots <= 0)
                {
                    errors.Add($"\"{shown}\" was not added: a report can have at most {snap.MaxFilesPerReport} files.");
                    continue;
                }
                if (!AllowedExtensions.Contains(ext))
                {
                    errors.Add($"\"{shown}\" is not an allowed type. Use JPG, PNG, GIF, WEBP, PDF, or DOCX.");
                    continue;
                }
                if (file.Length > maxBytes)
                {
                    errors.Add($"\"{shown}\" is larger than {snap.MaxFileMb} MB.");
                    continue;
                }

                byte[] bytes;
                using (var ms = new MemoryStream())
                {
                    await file.CopyToAsync(ms);
                    bytes = ms.ToArray();
                }

                // Trust the content, never the file name or the browser's content type
                var kind = Detect(bytes);
                if (kind == null || !kind.Extensions.Contains(ext))
                {
                    errors.Add($"\"{shown}\" is not a real {ext.TrimStart('.').ToUpperInvariant()} file, so it was not added.");
                    continue;
                }

                var storedName = Guid.NewGuid().ToString("N") + kind.Ext;
                var path = Path.Combine(_root, storedName);
                await File.WriteAllBytesAsync(path, bytes);
                written.Add(path);

                _db.IncidentAttachments.Add(new IncidentAttachment
                {
                    IncidentReportId = reportId,
                    OriginalFileName = DisplayName(name),
                    StoredFileName = storedName,
                    ContentType = kind.ContentType,
                    FileSizeBytes = bytes.Length,
                    UploadedById = studentId,
                    UploadedAtUtc = now
                });
                slots--;
            }

            if (written.Count > 0)
            {
                report.UpdatedAtUtc = now;
                try
                {
                    await _db.SaveChangesAsync();
                }
                catch (Exception)
                {
                    foreach (var path in written)
                    {
                        try { File.Delete(path); } catch { /* best effort */ }
                    }
                    _db.ChangeTracker.Clear();
                    errors.Add("The files could not be saved. Please try again.");
                    return new UploadResult(0, errors);
                }
            }

            return new UploadResult(written.Count, errors);
        }

        // ---------- Content checks ----------

        private static FileKind? Detect(byte[] b)
        {
            if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return Jpeg;
            if (StartsWith(b, PngSignature)) return Png;
            if (StartsWith(b, Gif87) || StartsWith(b, Gif89)) return Gif;
            if (b.Length >= 12 && StartsWith(b, Riff) && b.AsSpan(8, 4).SequenceEqual(WebpTag)) return Webp;
            if (StartsWith(b, PdfSignature)) return Pdf;
            if (StartsWith(b, ZipSignature) && IsPlainDocx(b)) return Docx;
            return null;
        }

        private static bool StartsWith(byte[] data, byte[] signature) =>
            data.Length >= signature.Length && data.AsSpan(0, signature.Length).SequenceEqual(signature);

        // A real Word document has word/document.xml. Macro-enabled files are rejected.
        private static bool IsPlainDocx(byte[] data)
        {
            try
            {
                using var ms = new MemoryStream(data);
                using var zip = new ZipArchive(ms, ZipArchiveMode.Read);

                if (zip.GetEntry("[Content_Types].xml") == null) return false;
                if (zip.GetEntry("word/document.xml") == null) return false;
                if (zip.Entries.Any(e => e.FullName.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase)))
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        // ---------- File names (display only, never used on disk) ----------

        private static string SanitizeName(string? name)
        {
            var n = Path.GetFileName(name ?? string.Empty);
            var invalid = Path.GetInvalidFileNameChars();
            var cleaned = new string(n.Where(c => !char.IsControl(c) && !invalid.Contains(c)).ToArray()).Trim();
            return cleaned.Length == 0 ? "attachment" : cleaned;
        }

        private static string DisplayName(string name)
        {
            if (name.Length <= 100) return name;
            var ext = Path.GetExtension(name);
            if (ext.Length > 10) ext = string.Empty;
            return name[..(100 - ext.Length)] + ext;
        }

        private static string ShortName(string name) =>
            name.Length <= 40 ? name : name[..37] + "...";
    }
}
