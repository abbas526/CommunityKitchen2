namespace FaizMawaid.Models
{
    /// <summary>
    /// The exact Roles.Name values seeded by migration 001, as strings -- used with
    /// [Authorize(Roles = ...)], which matches role claims by name, not by RoleIds.Id.
    /// Kept in sync with RoleIds by hand since there are only three roles.
    /// A SuperAdmin's access token carries BOTH the SuperAdmin and the Admin role claim
    /// (see Services/TokenService.cs), so every existing [Authorize(Roles = Admin)] endpoint
    /// keeps working for a SuperAdmin; SuperAdmin-only endpoints use RoleNames.SuperAdmin.
    /// </summary>
    public static class RoleNames
    {
        public const string Admin = "Admin";
        public const string FamilyHead = "FamilyHead";
        public const string SuperAdmin = "SuperAdmin";
    }
}
