namespace FaizMawaid.Models
{
    /// <summary>Matches the seeded rows in migration 001_create_roles.sql -- avoids magic numbers in repository code.</summary>
    public static class RoleIds
    {
        public const byte Admin = 1;
        public const byte FamilyHead = 2;
        /// <summary>Added by migration 026_add_superadmin_role.sql.</summary>
        public const byte SuperAdmin = 3;

        /// <summary>The most ACTIVE SuperAdmin accounts allowed at once. Enforced transactionally in UserRepository.</summary>
        public const int MaxActiveSuperAdmins = 2;
    }
}
