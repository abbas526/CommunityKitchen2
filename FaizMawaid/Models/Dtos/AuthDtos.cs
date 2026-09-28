namespace FaizMawaid.Models.Dtos
{
    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class RefreshTokenRequest
    {
        public string RefreshToken { get; set; } = string.Empty;
    }

    /// <summary>Returned by login, refresh, and the one-time bootstrap-admin endpoint -- everything the UI needs to store as "who am I" plus the token pair.</summary>
    public class LoginResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public DateTime AccessTokenExpiresAt { get; set; }
        public string RefreshToken { get; set; } = string.Empty;
        public ulong UserId { get; set; }
        public byte RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? SabilNumber { get; set; }
        /// <summary>Set only for a FamilyHead login -- the Family record this user heads, so the UI doesn't need a second lookup.</summary>
        public ulong? FamilyId { get; set; }
        public bool MustChangePassword { get; set; }
    }

    /// <summary>One-time-use: creates the very first Admin account when none exists yet.</summary>
    public class BootstrapAdminRequest
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string Password { get; set; } = string.Empty;
    }

    public class ChangePasswordRequest
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    /// <summary>Admin-only: sets a temporary password for another user, communicated out-of-band (phone/in person) -- there's no email sender configured. MustChangePassword is always forced true afterward.</summary>
    public class AdminResetPasswordRequest
    {
        public ulong UserId { get; set; }
        public string NewPassword { get; set; } = string.Empty;
    }
}
