namespace FaizMawaid.Models
{
    /// <summary>Outcome of a repository write that touches a SuperAdmin "seat" (the max-2-active-SuperAdmins rule).</summary>
    public enum UserChangeResult
    {
        Ok,
        NotFound,
        /// <summary>Making this change would put the number of ACTIVE SuperAdmins above RoleIds.MaxActiveSuperAdmins.</summary>
        SeatsFull,
        /// <summary>Making this change would leave zero ACTIVE SuperAdmins -- the lockout guard.</summary>
        WouldLeaveNoSuperAdmin
    }

    /// <summary>Result of creating a user through a seat-checked path; UserId is only meaningful when Result is Ok.</summary>
    public class UserCreateResult
    {
        public UserChangeResult Result { get; set; }
        public ulong UserId { get; set; }
    }
}
