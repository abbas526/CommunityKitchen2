namespace FaizMawaid.Models.Dtos
{
    public class CreateMealPlanTemplateRequest
    {
        public string Name { get; set; } = string.Empty;
        public string MealDescription { get; set; } = string.Empty;
        public byte SortOrder { get; set; }
        public ulong CreatedByUserId { get; set; }
    }

    public class UpdateMealPlanTemplateRequest
    {
        public string Name { get; set; } = string.Empty;
        public string MealDescription { get; set; } = string.Empty;
        public byte SortOrder { get; set; }
    }
}
