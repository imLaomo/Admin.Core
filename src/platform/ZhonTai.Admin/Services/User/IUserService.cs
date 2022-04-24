using System.Collections.Generic;
using System.Threading.Tasks;
using ZhonTai.Admin.Core.Dto;
using ZhonTai.Admin.Services.Auth.Dto;
using ZhonTai.Admin.Services.User.Dto;

namespace ZhonTai.Admin.Services.User
{
    /// <summary>
    /// �û��ӿ�
    /// </summary>
    public interface IUserService
    {
        Task<ResultOutput<AuthLoginOutput>> GetLoginUserAsync(long id);

        Task<IResultOutput> GetAsync(long id);

        Task<IResultOutput> GetSelectAsync();

        Task<IResultOutput> GetPageAsync(PageInput input);

        Task<IResultOutput> AddAsync(UserAddInput input);

        Task<IResultOutput> UpdateAsync(UserUpdateInput input);

        Task<IResultOutput> DeleteAsync(long id);

        Task<IResultOutput> SoftDeleteAsync(long id);

        Task<IResultOutput> BatchSoftDeleteAsync(long[] ids);

        Task<IResultOutput> ChangePasswordAsync(UserChangePasswordInput input);

        Task<IResultOutput> UpdateBasicAsync(UserUpdateBasicInput input);

        Task<IResultOutput> GetBasicAsync();

        Task<IList<UserPermissionsOutput>> GetPermissionsAsync();
    }
}