namespace FaizMawaid.Models
{
    /// <summary>Maps to RefreshTokens (migration 008). Only TokenHash (SHA-256 of the raw token) is ever stored -- see Services/TokenService.cs.</summary>
    public class RefreshToken
    {
        public ulong Id { get; set; }
        public ulong UserId { get; set; }
        public string TokenHash { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedByIp { get; set; }
        public DateTime? RevokedAt { get; set; }
        public string? ReplacedByTokenHash { get; set; }
    }
}
