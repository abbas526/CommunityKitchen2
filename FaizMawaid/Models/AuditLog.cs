namespace FaizMawaid.Models
{
    /// <summary>Maps to AuditLogs -- a generic append-only trail of who did what.</summary>
    public class AuditLog
    {
        public ulong Id { get; set; }
        public ulong? UserId { get; set; }
        public string Action { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public ulong? EntityId { get; set; }
        /// <summary>Raw JSON text (the Metadata column is JSON in MySQL); serialize/deserialize at the call site.</summary>
        public string? MetadataJson { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
