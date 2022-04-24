using System.Threading.Tasks;
using ZhonTai.Admin.Core.Dto;
using ZhonTai.Admin.Services.OprationLog.Dto;
using ZhonTai.Admin.Domain;

namespace ZhonTai.Admin.Services.OprationLog
{
    /// <summary>
    /// ������־�ӿ�
    /// </summary>
    public interface IOprationLogService
    {
        Task<IResultOutput> GetPageAsync(PageInput<LogGetPageDto> input);

        Task<IResultOutput> AddAsync(OprationLogAddInput input);
    }
}