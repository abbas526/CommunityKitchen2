using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;

namespace FaizMawaid.Repositories.Interfaces
{
    public interface IAppSettingsRepository
    {
        Task<AppSetting> GetAsync();
        Task<bool> UpdateAsync(UpdateAppSettingsRequest request);
    }
}
