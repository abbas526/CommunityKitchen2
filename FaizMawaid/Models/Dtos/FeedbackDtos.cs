namespace FaizMawaid.Models.Dtos
{
    /// <summary>A Family Head submitting a new piece of feedback/question.</summary>
    public class CreateFeedbackRequest
    {
        public ulong FamilyId { get; set; }
        public ulong CreatedByUserId { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>An Admin adding (or revising) the response to a feedback item.</summary>
    public class RespondFeedbackRequest
    {
        public string ResponseText { get; set; } = string.Empty;
        public ulong RespondedByAdminUserId { get; set; }
    }
}
