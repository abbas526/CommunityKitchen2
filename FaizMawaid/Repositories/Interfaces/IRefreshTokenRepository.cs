using FaizMawaid.Models;

namespace FaizMawaid.Repositories.Interfaces
{
    public interface IRefreshTokenRepository
    {
        Task<ulong> CreateAsync(ulong userId, string tokenHash, DateTime expiresAt, string? createdByIp);
        Task<RefreshToken?> GetByTokenHashAsync(string tokenHash);
        /// <summary>Marks a token used/invalid. Pass replacedByTokenHash when this is a rotation (refresh), leave it null for a plain revoke (logout).</summary>
        Task<bool> RevokeAsync(string tokenHash, string? replacedByTokenHash = null);
    }
}
