namespace FaizMawaid.Models.Dtos
{
    public class UpdateAppSettingsRequest
    {
        public uint MealVisibilityDays { get; set; }
        public ulong? UpdatedByUserId { get; set; }
    }
}
