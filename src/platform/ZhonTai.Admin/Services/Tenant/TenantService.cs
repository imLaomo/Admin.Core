using System.Linq;
using System.Threading.Tasks;
using ZhonTai.Admin.Core.Attributes;
using ZhonTai.Admin.Core.Repositories;
using ZhonTai.Common.Helpers;
using ZhonTai.Admin.Core.Dto;
using ZhonTai.Admin.Domain.Role;
using ZhonTai.Admin.Domain.RolePermission;
using ZhonTai.Admin.Domain.Tenant;
using ZhonTai.Admin.Domain.User;
using ZhonTai.Admin.Domain.UserRole;
using ZhonTai.Admin.Services.Tenant.Dto;
using ZhonTai.Admin.Domain.Tenant.Dto;
using ZhonTai.DynamicApi;
using ZhonTai.DynamicApi.Attributes;
using Microsoft.AspNetCore.Mvc;

namespace ZhonTai.Admin.Services.Tenant
{
    /// <summary>
    /// �⻧����
    /// </summary>
    [DynamicApi(Area = "admin")]
    public class TenantService : BaseService, ITenantService, IDynamicApi
    {
        private readonly ITenantRepository _tenantRepository;
        private readonly IRoleRepository _roleRepository;
        private readonly IUserRepository _userRepository;
        private readonly IRepositoryBase<UserRoleEntity> _userRoleRepository;
        private readonly IRepositoryBase<RolePermissionEntity> _rolePermissionRepository;

        public TenantService(
            ITenantRepository tenantRepository,
            IRoleRepository roleRepository,
            IUserRepository userRepository,
            IRepositoryBase<UserRoleEntity> userRoleRepository,
            IRepositoryBase<RolePermissionEntity> rolePermissionRepository
        )
        {
            _tenantRepository = tenantRepository;
            _roleRepository = roleRepository;
            _userRepository = userRepository;
            _userRoleRepository = userRoleRepository;
            _rolePermissionRepository = rolePermissionRepository;
        }

        /// <summary>
        /// ��ѯ�⻧
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<IResultOutput> GetAsync(long id)
        {
            var result = await _tenantRepository.GetAsync<TenantGetOutput>(id);
            return ResultOutput.Ok(result);
        }

        /// <summary>
        /// ��ѯ�⻧�б�
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IResultOutput> GetPageAsync(PageInput<TenantGetPageDto> input)
        {
            var key = input.Filter?.Name;

            var list = await _tenantRepository.Select
            .WhereDynamicFilter(input.DynamicFilter)
            .WhereIf(key.NotNull(), a => a.Name.Contains(key))
            .Count(out var total)
            .OrderByDescending(true, c => c.Id)
            .Page(input.CurrentPage, input.PageSize)
            .ToListAsync<TenantListOutput>();

            var data = new PageOutput<TenantListOutput>()
            {
                List = list,
                Total = total
            };

            return ResultOutput.Ok(data);
        }

        /// <summary>
        /// ����
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        [Transaction]
        public async Task<IResultOutput> AddAsync(TenantAddInput input)
        {
            var entity = Mapper.Map<TenantEntity>(input);
            var tenant = await _tenantRepository.InsertAsync(entity);

            var tenantId = tenant.Id;

            //����û�
            var pwd = MD5Encrypt.Encrypt32("111111");
            var user = new UserEntity { TenantId = tenantId, UserName = input.Phone, NickName = input.RealName, Password = pwd, Status = 0 };
            await _userRepository.InsertAsync(user);

            //��ӽ�ɫ
            var role = new RoleEntity { TenantId = tenantId, Code = "plat_admin", Name = "ƽ̨����Ա", Enabled = true };
            await _roleRepository.InsertAsync(role);

            //����û���ɫ
            var userRole = new UserRoleEntity() { UserId = user.Id, RoleId = role.Id };
            await _userRoleRepository.InsertAsync(userRole);

            //�����⻧�û��ͽ�ɫ
            tenant.UserId = user.Id;
            tenant.RoleId = role.Id;
            await _tenantRepository.UpdateAsync(tenant);

            return ResultOutput.Ok();
        }

        /// <summary>
        /// �޸�
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public async Task<IResultOutput> UpdateAsync(TenantUpdateInput input)
        {
            if (!(input?.Id > 0))
            {
                return ResultOutput.NotOk();
            }

            var entity = await _tenantRepository.GetAsync(input.Id);
            if (!(entity?.Id > 0))
            {
                return ResultOutput.NotOk("�⻧�����ڣ�");
            }

            Mapper.Map(input, entity);
            await _tenantRepository.UpdateAsync(entity);
            return ResultOutput.Ok();
        }

        /// <summary>
        /// ����ɾ��
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [Transaction]
        public async Task<IResultOutput> DeleteAsync(long id)
        {
            //ɾ����ɫȨ��
            await _rolePermissionRepository.Where(a => a.Role.TenantId == id).DisableGlobalFilter("Tenant").ToDelete().ExecuteAffrowsAsync();

            //ɾ���û���ɫ
            await _userRoleRepository.Where(a => a.User.TenantId == id).DisableGlobalFilter("Tenant").ToDelete().ExecuteAffrowsAsync();

            //ɾ���û�
            await _userRepository.Where(a => a.TenantId == id).DisableGlobalFilter("Tenant").ToDelete().ExecuteAffrowsAsync();

            //ɾ����ɫ
            await _roleRepository.Where(a => a.TenantId == id).DisableGlobalFilter("Tenant").ToDelete().ExecuteAffrowsAsync();

            //ɾ���⻧
            await _tenantRepository.DeleteAsync(id);

            return ResultOutput.Ok();
        }

        /// <summary>
        /// ɾ��
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [Transaction]
        public async Task<IResultOutput> SoftDeleteAsync(long id)
        {
            //ɾ���û�
            await _userRepository.SoftDeleteAsync(a => a.TenantId == id, "Tenant");

            //ɾ����ɫ
            await _roleRepository.SoftDeleteAsync(a => a.TenantId == id, "Tenant");

            //ɾ���⻧
            var result = await _tenantRepository.SoftDeleteAsync(id);

            return ResultOutput.Result(result);
        }

        /// <summary>
        /// ����ɾ��
        /// </summary>
        /// <param name="ids"></param>
        /// <returns></returns>
        [Transaction]
        public async Task<IResultOutput> BatchSoftDeleteAsync(long[] ids)
        {
            //ɾ���û�
            await _userRepository.SoftDeleteAsync(a => ids.Contains(a.TenantId.Value), "Tenant");

            //ɾ����ɫ
            await _roleRepository.SoftDeleteAsync(a => ids.Contains(a.TenantId.Value), "Tenant");

            //ɾ���⻧
            var result = await _tenantRepository.SoftDeleteAsync(ids);

            return ResultOutput.Result(result);
        }
    }
}