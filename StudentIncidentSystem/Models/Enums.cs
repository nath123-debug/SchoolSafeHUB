using StudentIncidentSystem.Models;

namespace StudentIncidentSystem.Models
{
    public enum IncidentStatus
    {
        Submitted = 0,
        UnderReview = 1,
        AdditionalInformationRequested = 2,
        Referred = 3,
        Resolved = 4,
        Closed = 5
    }

    public enum NoteType
    {
        Investigation = 0,
        ActionTaken = 1,
        Resolution = 2
    }
}