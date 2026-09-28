namespace FaizMawaid.Models
{
    /// <summary>Matches the seeded rows in migration 001_create_roles.sql -- avoids magic numbers in repository code.</summary>
    public static class RoleIds
    {
        public const byte Admin = 1;
        public const byte FamilyHead = 2;
    }
}
