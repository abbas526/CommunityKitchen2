namespace FaizMawaid.Models
{
    /// <summary>
    /// Maps to the Users table. Both Admins and Family Heads are rows here,
    /// distinguished by RoleId. PasswordHash is a real PBKDF2 hash (see
    /// Services/PasswordHasherService.cs) as of 2026-09-24's token-based auth --
    /// never a plaintext or client-supplied value.
    /// </summary>
    public class User
    {
        public ulong Id { get; set; }
        public byte RoleId { get; set; }
        public string Email { get; set; } = string.Empty;
        /// <summary>The Community Head's assigned identifier for this family. Unique when set; Admin accounts typically leave it null.</summary>
        public string? SabilNumber { get; set; }
        public string PasswordHash { get; set; } = string.Empty;
        /// <summary>True when whoever set this password wasn't the account owner (an Admin creating the account or resetting the password) -- the UI forces a change at next login while this is true.</summary>
        public bool MustChangePassword { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
    }
}
