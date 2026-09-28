namespace FaizMawaid.Models.Dtos
{
    public class CreateAreaRequest
    {
        public string Name { get; set; } = string.Empty;
        public byte SortOrder { get; set; }
    }

    public class UpdateAreaRequest
    {
        public string Name { get; set; } = string.Empty;
        public byte SortOrder { get; set; }
    }
}
