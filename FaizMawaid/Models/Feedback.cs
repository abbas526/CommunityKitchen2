namespace FaizMawaid.Models
{
    public enum FeedbackStatus
    {
        Open,
        Responded
    }

    /// <summary>
    /// Maps to Feedback -- a private message between one family and the Admin team.
    /// A Family Head submits Message; any Admin can add ResponseText once, which
    /// flips Status to Responded. Visible only to that family and to Admins.
    /// </summary>
    public class Feedback
    {
        public ulong Id { get; set; }
        public ulong FamilyId { get; set; }
        public ulong CreatedByUserId { get; set; }
        public string Message { get; set; } = string.Empty;
        public FeedbackStatus Status { get; set; }
        public string? ResponseText { get; set; }
        public ulong? RespondedByAdminUserId { get; set; }
        public DateTime? RespondedAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
