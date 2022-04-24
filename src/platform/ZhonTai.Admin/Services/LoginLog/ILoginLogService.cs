using ZhonTai.Admin.Core.Dto;
using System.Threading.Tasks;
using ZhonTai.Admin.Services.LoginLog.Dto;
using ZhonTai.Admin.Domain;

namespace ZhonTai.Admin.Services.LoginLog
{
    /// <summary>
    /// ��¼��־�ӿ�
    /// </summary>
    public interface ILoginLogService
    {
        Task<IResultOutput> GetPageAsync(PageInput<LogGetPageDto> input);

        Task<IResultOutput<long>> AddAsync(LoginLogAddInput input);
    }
}