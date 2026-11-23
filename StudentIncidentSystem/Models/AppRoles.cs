namespace StudentIncidentSystem.Models
{
    public static class AppRoles
    {
        public const string Student = "Student";
        public const string Staff = "Staff";                 // Guidance counselor / authorized staff
        public const string Administrator = "Administrator";

        public static readonly string[] All = { Student, Staff, Administrator };
    }
}