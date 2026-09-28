namespace FaizMawaid.Models.Dtos
{
    /// <summary>Family Head cancels their own thaali (or an Admin enters it on their behalf).</summary>
    public class CreateThaaliCancellationRequest
    {
        public ulong FamilyId { get; set; }
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public string? Reason { get; set; }
        public ulong CreatedByUserId { get; set; }
    }

    public class ReinstateThaaliCancellationRequest
    {
        public ulong ReinstatedByUserId { get; set; }
    }
}
