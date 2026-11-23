using StudentIncidentSystem.Models;

namespace StudentIncidentSystem.ViewModels
{
    public static class StatusDisplay
    {
        public static string Label(IncidentStatus status) => status switch
        {
            IncidentStatus.Submitted => "Submitted",
            IncidentStatus.UnderReview => "Under Review",
            IncidentStatus.AdditionalInformationRequested => "Additional Info Requested",
            IncidentStatus.Referred => "Referred",
            IncidentStatus.Resolved => "Resolved",
            IncidentStatus.Closed => "Closed",
            _ => status.ToString()
        };

        public static string Badge(IncidentStatus status) => status switch
        {
            IncidentStatus.Submitted => "text-bg-primary",
            IncidentStatus.UnderReview => "text-bg-info",
            IncidentStatus.AdditionalInformationRequested => "text-bg-warning",
            IncidentStatus.Referred => "text-bg-secondary",
            IncidentStatus.Resolved => "text-bg-success",
            IncidentStatus.Closed => "text-bg-dark",
            _ => "text-bg-light"
        };
    }
}
