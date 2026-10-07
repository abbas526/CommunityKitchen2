namespace FaizMawaid.Models.Dtos
{
    /// <summary>Admin-only: creates a login account directly (e.g. another Admin, or a Family Head without going through self-registration). The Admin chooses the initial password; MustChangePassword is always forced true so the real owner sets their own at first login.</summary>
    public class CreateUserRequest
    {
        public byte RoleId { get; set; }
        public string Email { get; set; } = string.Empty;
        /// <summary>Plaintext, over HTTPS -- hashed server-side (UsersController.Create) before anything is persisted. Never stored as-is.</summary>
        public string Password { get; set; } = string.Empty;
        /// <summary>Set by the server (UsersController.Create) after hashing Password; not meant to be supplied by the caller.</summary>
        public string PasswordHash { get; set; } = string.Empty;
        public bool MustChangePassword { get; set; } = true;
        public string FullName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? SabilNumber { get; set; }
    }

    public class UpdateUserRequest
    {
        public string FullName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? SabilNumber { get; set; }
    }

    /// <summary>SuperAdmin-only: the new role for an Admin/SuperAdmin account (RoleIds.Admin or RoleIds.SuperAdmin).</summary>
    public class ChangeUserRoleRequest
    {
        public byte RoleId { get; set; }
    }

    /// <summary>How many of the allowed SuperAdmin seats are used (active SuperAdmins) out of the maximum.</summary>
    public class SuperAdminSeatsResponse
    {
        public int Used { get; set; }
        public int Max { get; set; }
    }
}
