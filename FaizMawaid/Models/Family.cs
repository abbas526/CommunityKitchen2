namespace FaizMawaid.Models
{
    public enum RegistrationStatus
    {
        Pending,
        Approved,
        Rejected
    }

    /// <summary>
    /// Maps to the Families table -- one row per home. ThaaliSizeId holds the CURRENT
    /// size; the full history of changes lives in FamilySizeHistory.
    ///
    /// A row can also represent a SUB-FAMILY: an additional household (e.g. a married
    /// child staying in a nearby flat or building) that an Admin has linked to a primary
    /// family so it can take its own Thaali. A sub-family has ParentFamilyId set to its
    /// primary family's Id, has no FamilyHeadUserId of its own (it has no separate login --
    /// the primary family's head manages it), and carries SubFamilyLabel to identify it
    /// in reports/UI (there's no User row to derive a name from). A primary family always
    /// has ParentFamilyId = null.
    /// </summary>
    public class Family
    {
        public ulong Id { get; set; }
        /// <summary>Null only for a sub-family row, which has no login of its own.</summary>
        public ulong? FamilyHeadUserId { get; set; }
        /// <summary>Set only on a sub-family row: the primary family it's linked to.</summary>
        public ulong? ParentFamilyId { get; set; }
        /// <summary>Short admin-provided label identifying a sub-family (e.g. "Son's family - Building B, Flat 12"). Null for a primary family.</summary>
        public string? SubFamilyLabel { get; set; }
        public string? Address { get; set; }
        /// <summary>Optional -- a family that picks its thaali up from the kitchen itself has no Area.</summary>
        public byte? AreaId { get; set; }
        public byte? NumberOfMembers { get; set; }
        /// <summary>False = the family is in the system but only receives a meal on a Special Day (see MealPlan.IsSpecialDay). Admin-only to change after registration.</summary>
        public bool TakesRegularMeal { get; set; } = true;
        public byte ThaaliSizeId { get; set; }
        public RegistrationStatus RegistrationStatus { get; set; }
        public ulong? ApprovedByAdminUserId { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
