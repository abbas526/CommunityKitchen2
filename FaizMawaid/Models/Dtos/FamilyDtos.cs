namespace FaizMawaid.Models.Dtos
{
    /// <summary>Self-registration payload: creates a FamilyHead user and a Pending Family together, in one transaction.</summary>
    public class RegisterFamilyRequest
    {
        public string Email { get; set; } = string.Empty;
        /// <summary>Assigned by the Community Head; required for a Family Head registration.</summary>
        public string SabilNumber { get; set; } = string.Empty;
        /// <summary>Plaintext, over HTTPS -- hashed server-side (FamiliesController.Register) before anything is persisted. Never stored as-is.</summary>
        public string Password { get; set; } = string.Empty;
        /// <summary>Set by the server (FamiliesController.Register) after hashing Password; not meant to be supplied by the caller.</summary>
        public string PasswordHash { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Address { get; set; }
        /// <summary>Optional -- left null when the family will pick their thaali up from the kitchen themselves.</summary>
        public byte? AreaId { get; set; }
        public byte? NumberOfMembers { get; set; }
        public byte ThaaliSizeId { get; set; }
    }

    public class RegisterFamilyResponse
    {
        public ulong UserId { get; set; }
        public ulong FamilyId { get; set; }
    }

    public class ApproveFamilyRequest
    {
        public ulong ApprovedByAdminUserId { get; set; }
    }

    public class RejectFamilyRequest
    {
        public ulong RejectedByAdminUserId { get; set; }
        public string? Reason { get; set; }
    }

    public class UpdateFamilyRequest
    {
        public string? Address { get; set; }
        /// <summary>Optional -- left null when the family will pick their thaali up from the kitchen themselves.</summary>
        public byte? AreaId { get; set; }
        public byte? NumberOfMembers { get; set; }
    }

    public class ChangeThaaliSizeRequest
    {
        public byte NewThaaliSizeId { get; set; }
        public ulong ChangedByUserId { get; set; }
        public DateOnly EffectiveFromDate { get; set; }
    }

    /// <summary>
    /// Admin-only payload for linking a sub-family (an additional household, e.g. a
    /// married child in a nearby flat/building) to an existing primary family. The
    /// sub-family is created already Approved and active -- an Admin is vouching for
    /// it directly, so it doesn't go through the Pending review queue.
    /// </summary>
    public class CreateSubFamilyRequest
    {
        /// <summary>Short label identifying this household, e.g. "Son's family - Building B, Flat 12".</summary>
        public string SubFamilyLabel { get; set; } = string.Empty;
        public string? Address { get; set; }
        public byte? NumberOfMembers { get; set; }
        public byte ThaaliSizeId { get; set; }
        public ulong CreatedByAdminUserId { get; set; }
    }

    /// <summary>One row of a bulk member/family upload -- the client parses an Excel file into
    /// these client-side and posts the whole batch. ThaaliSizeName is matched against the
    /// system's defined Thaali Sizes by name (case-insensitive) rather than by id, since a
    /// human is filling in a spreadsheet, not looking up numeric ids.</summary>
    public class BulkFamilyImportItem
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string SabilNumber { get; set; } = string.Empty;
        /// <summary>Plaintext, over HTTPS -- hashed server-side before anything is persisted. Never stored as-is.</summary>
        public string Password { get; set; } = string.Empty;
        public string? Address { get; set; }
        /// <summary>Optional. Matched against the Areas master table by name (case-insensitive); a blank value leaves the family with no Area (self-pickup).</summary>
        public string? AreaName { get; set; }
        public byte? NumberOfMembers { get; set; }
        public string ThaaliSizeName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Admin-only bulk import: every row is created already Approved and active (the Admin is
    /// vouching for every row by uploading it), matching how Sub-Families are created today.
    /// MustChangePassword is forced true for every imported account since the Admin (not the
    /// member) chose the password.
    /// </summary>
    public class BulkFamilyImportRequest
    {
        public List<BulkFamilyImportItem> Items { get; set; } = new();
        public ulong ImportedByAdminUserId { get; set; }
    }

    /// <summary>Status is "Created" or "Skipped" (see Message for why).</summary>
    public class BulkFamilyImportRowResult
    {
        public string Email { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? Message { get; set; }
    }

    public class BulkFamilyImportResponse
    {
        public int CreatedCount { get; set; }
        public int SkippedCount { get; set; }
        public List<BulkFamilyImportRowResult> Rows { get; set; } = new();
    }
}
