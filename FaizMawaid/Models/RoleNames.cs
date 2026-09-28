namespace FaizMawaid.Models
{
    /// <summary>
    /// The exact Roles.Name values seeded by migration 001, as strings -- used with
    /// [Authorize(Roles = ...)], which matches role claims by name, not by RoleIds.Id.
    /// Kept in sync with RoleIds by hand since there are only two roles.
    /// </summary>
    public static class RoleNames
    {
        public const string Admin = "Admin";
        public const string FamilyHead = "FamilyHead";
    }
}
