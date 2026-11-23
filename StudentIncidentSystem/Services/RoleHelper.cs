using System.Security.Claims;
using StudentIncidentSystem.Models;

namespace StudentIncidentSystem.Services
{
    public static class RoleHelper
    {
        public static string Of(ClaimsPrincipal user) =>
            user.IsInRole(AppRoles.Administrator) ? AppRoles.Administrator :
            user.IsInRole(AppRoles.Staff) ? AppRoles.Staff :
            user.IsInRole(AppRoles.Student) ? AppRoles.Student : string.Empty;
    }
}
