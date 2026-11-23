using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using StudentIncidentSystem.Services;

namespace StudentIncidentSystem.ViewComponents
{
    // Shows the "Supporting files" card. It checks access itself and renders nothing if denied.
    public class AttachmentsViewComponent : ViewComponent
    {
        private readonly AttachmentService _files;

        public AttachmentsViewComponent(AttachmentService files)
        {
            _files = files;
        }

        public async Task<IViewComponentResult> InvokeAsync(int reportId)
        {
            var userId = UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Content(string.Empty);

            var panel = await _files.GetPanelAsync(reportId, userId, RoleHelper.Of(UserClaimsPrincipal));
            if (panel == null) return Content(string.Empty);

            return View(panel);
        }
    }
}
