using StudentIncidentSystem.Models;

namespace StudentIncidentSystem.Services
{
    public record OpResult(bool Success, string Message, bool NotFound = false)
    {
        public static OpResult Ok(string message) => new(true, message);
        public static OpResult Fail(string message) => new(false, message);
        public static OpResult Missing() => new(false, "Case not found.", true);
    }

    // The single source of truth for which status changes are allowed.
    public static class StatusTransitions
    {
        private static readonly Dictionary<IncidentStatus, IncidentStatus[]> Map = new()
        {
            [IncidentStatus.Submitted] = new[]
            {
                IncidentStatus.UnderReview,
                IncidentStatus.AdditionalInformationRequested,
                IncidentStatus.Referred
            },
            [IncidentStatus.UnderReview] = new[]
            {
                IncidentStatus.AdditionalInformationRequested,
                IncidentStatus.Referred,
                IncidentStatus.Resolved
            },
            [IncidentStatus.AdditionalInformationRequested] = new[]
            {
                IncidentStatus.UnderReview,
                IncidentStatus.Referred
            },
            [IncidentStatus.Referred] = new[]
            {
                IncidentStatus.UnderReview,
                IncidentStatus.AdditionalInformationRequested,
                IncidentStatus.Resolved
            },
            [IncidentStatus.Resolved] = new[]
            {
                IncidentStatus.Closed,
                IncidentStatus.UnderReview
            },
            [IncidentStatus.Closed] = Array.Empty<IncidentStatus>()
        };

        public static IReadOnlyList<IncidentStatus> From(IncidentStatus current) =>
            Map.TryGetValue(current, out var list) ? list : Array.Empty<IncidentStatus>();

        public static bool IsAllowed(IncidentStatus from, IncidentStatus to) =>
            From(from).Contains(to);
    }
}
