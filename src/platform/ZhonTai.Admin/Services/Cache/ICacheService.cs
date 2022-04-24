using System.Threading.Tasks;
using ZhonTai.Admin.Core.Dto;

namespace ZhonTai.Admin.Services.Cache
{
    /// <summary>
    /// ����ӿ�
    /// </summary>
    public interface ICacheService
    {
        /// <summary>
        /// �����б�
        /// </summary>
        /// <returns></returns>
        IResultOutput GetList();

        /// <summary>
        /// �������
        /// </summary>
        /// <param name="cacheKey"></param>
        /// <returns></returns>
        Task<IResultOutput> ClearAsync(string cacheKey);
    }
}