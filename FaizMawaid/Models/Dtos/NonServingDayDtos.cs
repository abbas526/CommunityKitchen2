namespace FaizMawaid.Models.Dtos
{
    public class CreateNonServingDayRequest
    {
        public DateOnly TheDate { get; set; }
        public string? Reason { get; set; }
        public ulong CreatedByUserId { get; set; }
    }
}
