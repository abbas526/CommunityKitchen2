using FaizMawaid.Models;

namespace FaizMawaid.Repositories.Interfaces
{
    public interface IRefreshTokenRepository
    {
        Task<ulong> CreateAsync(ulong userId, string tokenHash, DateTime expiresAt, string? createdByIp);
        Task<RefreshToken?> GetByTokenHashAsync(string tokenHash);
        /// <summary>Marks a token used/invalid. Pass replacedByTokenHash when this is a rotation (refresh), leave it null for a plain revoke (logout).</summary>
        Task<bool> RevokeAsync(string tokenHash, string? replacedByTokenHash = null);
        /// <summary>Revokes every still-valid refresh token a user holds (used when an account is deactivated or demoted, so they can't keep refreshing a session). Returns how many were revoked.</summary>
        Task<int> RevokeAllForUserAsync(ulong userId);
    }
}
