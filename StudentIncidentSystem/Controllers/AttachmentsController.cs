using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StudentIncidentSystem.Models;
using StudentIncidentSystem.Services;

namespace StudentIncidentSystem.Controllers
{
    [Authorize]
    public class AttachmentsController : Controller
    {
        private readonly AttachmentService _files;

        public AttachmentsController(AttachmentService files)
        {
            _files = files;
        }

        private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        // GET /Attachments/Download/5  (404 if missing OR not yours: same answer)
        [HttpGet]
        public async Task<IActionResult> Download(int id)
        {
            var download = await _files.GetDownloadAsync(id, UserId, RoleHelper.Of(User));
            if (download == null) return NotFound();
            if (!System.IO.File.Exists(download.PhysicalPath)) return NotFound();

            Response.Headers["X-Content-Type-Options"] = "nosniff";
            Response.Headers["Content-Security-Policy"] = "default-src 'none'; sandbox";
            Response.Headers["Cache-Control"] = "private, no-store";

            // Passing a file name forces a download instead of opening it in the browser
            return PhysicalFile(download.PhysicalPath, download.ContentType, download.DownloadName);
        }

        [HttpPost]
        [Authorize(Roles = AppRoles.Student)]
        public async Task<IActionResult> Upload(int reportId, List<IFormFile> files)
        {
            var result = await _files.UploadAsync(reportId, UserId, files);
            if (result.NotFound) return NotFound();

            if (result.Saved > 0)
                TempData["Success"] = result.Saved == 1 ? "1 file was uploaded." : $"{result.Saved} files were uploaded.";

            if (result.Errors.Count > 0)
            {
                var message = string.Join(" ", result.Errors.Take(3));
                TempData["Error"] = message.Length > 600 ? message[..600] + "..." : message;
            }

            return RedirectToAction("Details", "IncidentReports", new { id = reportId }, "evidence");
        }
    }
}
