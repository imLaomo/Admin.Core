using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ZhonTai.Admin.Core.Attributes;
using ZhonTai.Admin.Core.Entities;
using ZhonTai.Admin.Core.Configs;
using ZhonTai.Admin.Core.Dto;
using ZhonTai.Admin.Core.Repositories;
using ZhonTai.Admin.Services.Permission.Dto;
using ZhonTai.Admin.Domain.Permission;
using ZhonTai.Admin.Domain.RolePermission;
using ZhonTai.Admin.Domain.TenantPermission;
using ZhonTai.Admin.Domain.UserRole;
using ZhonTai.Admin.Domain.PermissionApi;
using ZhonTai.Admin.Domain.Role;
using ZhonTai.Admin.Domain.Api;
using ZhonTai.Admin.Domain.User;
using ZhonTai.Admin.Services.Contracts;
using ZhonTai.DynamicApi;
using ZhonTai.DynamicApi.Attributes;
using ZhonTai.Admin.Core.Db;

namespace ZhonTai.Admin.Services.Permission
{
    /// <summary>
    /// Ȩ�޷���
    /// </summary>
    [DynamicApi(Area = "admin")]
    public class PermissionService : BaseService, IPermissionService, IDynamicApi
    {
        private readonly AppConfig _appConfig;
        private readonly IPermissionRepository _permissionRepository;
        private readonly IRoleRepository _roleRepository;
        private readonly IRepositoryBase<RolePermissionEntity> _rolePermissionRepository;
        private readonly IRepositoryBase<TenantPermissionEntity> _tenantPermissionRepository;
        private readonly IRepositoryBase<UserRoleEntity> _userRoleRepository;
        private readonly IRepositoryBase<PermissionApiEntity> _permissionApiRepository;

        public PermissionService(
            AppConfig appConfig,
            IPermissionRepository permissionRepository,
            IRoleRepository roleRepository,
            IRepositoryBase<RolePermissionEntity> rolePermissionRepository,
            IRepositoryBase<TenantPermissionEntity> tenantPermissionRepository,
            IRepositoryBase<UserRoleEntity> userRoleRepository,
            IRepositoryBase<PermissionApiEntity> permissionApiRepository
        )
        {
            _appConfig = appConfig;
            _permissionRepository = permissionRepository;
            _roleRepository = roleRepository;
            _rolePermissionRepository = rolePermissionRepository;
            _tenantPermissionRepository = tenantPermissionRepository;
            _userRoleRepository = userRoleRepository;
            _permissionApiRepository = permissionApiRepository;
        }

        /// <summary>
        /// ���Ȩ���¹������û�Ȩ�޻���
        /// </summary>
        /// <param name="permissionIds"></param>
        /// <returns></returns>
        private async Task ClearUserPermissionsAsync(List<long> permissionIds)
        {
            var userIds = await _userRoleRepository.Select.Where(a =>
                _rolePermissionRepository
                .Where(b => b.RoleId == a.RoleId && permissionIds.Contains(b.PermissionId))
                .Any()
            ).ToListAsync(a => a.UserId);
            foreach (var userId in userIds)
            {
                await Cache.DelAsync(string.Format(CacheKey.UserPermissions, userId));
            }
        }

        /// <summary>
        /// ��ѯȨ��
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<IResultOutput> GetAsync(long id)
        {
            var result = await _permissionRepository.GetAsync(id);

            return ResultOutput.Ok(result);
        }

        /// <summary>
        /// ��ѯ����
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<IResultOutput> GetGroupAsync(long id)
        {
            var result = await _permissionRepository.GetAsync<PermissionGetGroupOutput>(id);
            return ResultOutput.Ok(result);
        }

        /// <summary>
        /// ��ѯ�˵�
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<IResultOutput> GetMenuAsync(long id)
        {
            var result = await _permissionRepository.GetAsync<PermissionGetMenuOutput>(id);
            return ResultOutput.Ok(result);
        }

        /// <summary>
        /// ��ѯ�ӿ�
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<IResultOutput> GetApiAsync(long id)
        {
            var result = await _permissionRepository.GetAsync<PermissionGetApiOutput>(id);
            return ResultOutput.Ok(result);
        }

        /// <summary>
        /// ��ѯȨ�޵�
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<IResultOutput> GetDotAsync(long id)
        {
            var output = await _permissionRepository.Select
            .WhereDynamic(id)
            .IncludeMany(a => a.Apis.Select(b => new ApiEntity { Id = b.Id }))
            .ToOneAsync();

            return ResultOutput.Ok(Mapper.Map<PermissionGetDotOutput>(output));
        }

        /// <summary>
        /// ��ѯȨ���б�
        /// </summary>
        /// <param name="key"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        public async Task<IResultOutput> GetListAsync(string key, DateTime? start, DateTime? end)
        {
            if (end.HasValue)
            {
                end = end.Value.AddDays(1);
            }

            var data = await _permissionRepository
                .WhereIf(key.NotNull(), a => a.Path.Contains(key) || a.Label.Contains(key))
                .WhereIf(start.HasValue && end.HasValue, a => a.CreatedTime.Value.BetweenEnd(start.Value, end.Value))
                .OrderBy(a => a.ParentId)
                .OrderBy(a => a.Sort)
                .ToListAsync(a=> new PermissionListOutput { ApiPaths = string.Join(";", _permissionApiRepository.Where(b=>b.PermissionId == a.Id).ToList(b => b.Api.Path)) });

            return ResultOutput.Ok(data);
        }

        /// <summary>
        /// ��ѯ��ɫȨ��-Ȩ���б�
        /// </summary>
        /// <returns></returns>
        public async Task<IResultOutput> GetPermissionList()
        {
            var permissions = await _permissionRepository.Select
                .WhereIf(_appConfig.Tenant && User.TenantType == TenantType.Tenant, a =>
                    _tenantPermissionRepository
                    .Where(b => b.PermissionId == a.Id && b.TenantId == User.TenantId)
                    .Any()
                )
                .OrderBy(a => a.ParentId)
                .OrderBy(a => a.Sort)
                .ToListAsync(a => new { a.Id, a.ParentId, a.Label, a.Type });

            var apis = permissions
                .Where(a => a.Type == PermissionTypeEnum.Dot)
                .Select(a => new { a.Id, a.ParentId, a.Label });

            var menus = permissions
                .Where(a => (new[] { PermissionTypeEnum.Group, PermissionTypeEnum.Menu }).Contains(a.Type))
                .Select(a => new
                {
                    a.Id,
                    a.ParentId,
                    a.Label,
                    Apis = apis.Where(b => b.ParentId == a.Id).Select(b => new { b.Id, b.Label })
                });

            return ResultOutput.Ok(menus);
        }

        /// <summary>
        /// ��ѯ��ɫȨ���б�
        /// </summary>
        /// <param name="roleId"></param>
        /// <returns></returns>
        public async Task<IResultOutput> GetRolePermissionList(long roleId = 0)
        {
            var permissionIds = await _rolePermissionRepository
                .Select.Where(d => d.RoleId == roleId)
                .ToListAsync(a => a.PermissionId);

            return ResultOutput.Ok(permissionIds);
        }

        /// <summary>
        /// ��ѯ�⻧Ȩ���б�
        /// </summary>
        /// <param name="tenantId"></param>
        /// <returns></returns>
        public async Task<IResultOutput> GetTenantPermissionList(long tenantId)
        {
            var permissionIds = await _tenantPermissionRepository
                .Select.Where(d => d.TenantId == tenantId)
                .ToListAsync(a => a.PermissionId);

            return ResultOutput.Ok(permissionIds);
        }

        /// <summary>
        /// ��������
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public async Task<IResultOutput> AddGroupAsync(PermissionAddGroupInput input)
        {
            var entity = Mapper.Map<PermissionEntity>(input);
            var id = (await _permissionRepository.InsertAsync(entity)).Id;

            return ResultOutput.Ok(id > 0);
        }

        /// <summary>
        /// �����˵�
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public async Task<IResultOutput> AddMenuAsync(PermissionAddMenuInput input)
        {
            var entity = Mapper.Map<PermissionEntity>(input);
            var id = (await _permissionRepository.InsertAsync(entity)).Id;

            return ResultOutput.Ok(id > 0);
        }

        /// <summary>
        /// �����ӿ�
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public async Task<IResultOutput> AddApiAsync(PermissionAddApiInput input)
        {
            var entity = Mapper.Map<PermissionEntity>(input);
            var id = (await _permissionRepository.InsertAsync(entity)).Id;

            return ResultOutput.Ok(id > 0);
        }

        /// <summary>
        /// ����Ȩ�޵�
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        [Transaction]
        public async Task<IResultOutput> AddDotAsync(PermissionAddDotInput input)
        {
            var entity = Mapper.Map<PermissionEntity>(input);
            var id = (await _permissionRepository.InsertAsync(entity)).Id;

            if (input.ApiIds != null && input.ApiIds.Any())
            {
                var permissionApis = input.ApiIds.Select(a => new PermissionApiEntity { PermissionId = id, ApiId = a });
                await _permissionApiRepository.InsertAsync(permissionApis);
            }

            return ResultOutput.Ok(id > 0);
        }

        /// <summary>
        /// �޸ķ���
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public async Task<IResultOutput> UpdateGroupAsync(PermissionUpdateGroupInput input)
        {
            var result = false;
            if (input != null && input.Id > 0)
            {
                var entity = await _permissionRepository.GetAsync(input.Id);
                entity = Mapper.Map(input, entity);
                result = (await _permissionRepository.UpdateAsync(entity)) > 0;
            }

            return ResultOutput.Result(result);
        }

        /// <summary>
        /// �޸Ĳ˵�
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public async Task<IResultOutput> UpdateMenuAsync(PermissionUpdateMenuInput input)
        {
            var result = false;
            if (input != null && input.Id > 0)
            {
                var entity = await _permissionRepository.GetAsync(input.Id);
                entity = Mapper.Map(input, entity);
                result = (await _permissionRepository.UpdateAsync(entity)) > 0;
            }

            return ResultOutput.Result(result);
        }

        /// <summary>
        /// �޸Ľӿ�
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public async Task<IResultOutput> UpdateApiAsync(PermissionUpdateApiInput input)
        {
            var result = false;
            if (input != null && input.Id > 0)
            {
                var entity = await _permissionRepository.GetAsync(input.Id);
                entity = Mapper.Map(input, entity);
                result = (await _permissionRepository.UpdateAsync(entity)) > 0;
            }

            return ResultOutput.Result(result);
        }

        /// <summary>
        /// �޸�Ȩ�޵�
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        [Transaction]
        public async Task<IResultOutput> UpdateDotAsync(PermissionUpdateDotInput input)
        {
            if (!(input?.Id > 0))
            {
                return ResultOutput.NotOk();
            }

            var entity = await _permissionRepository.GetAsync(input.Id);
            if (!(entity?.Id > 0))
            {
                return ResultOutput.NotOk("Ȩ�޵㲻���ڣ�");
            }

            Mapper.Map(input, entity);
            await _permissionRepository.UpdateAsync(entity);

            await _permissionApiRepository.DeleteAsync(a => a.PermissionId == entity.Id);

            if (input.ApiIds != null && input.ApiIds.Any())
            {
                var permissionApis = input.ApiIds.Select(a => new PermissionApiEntity { PermissionId = entity.Id, ApiId = a });
                await _permissionApiRepository.InsertAsync(permissionApis);
            }

            //����û�Ȩ�޻���
            await ClearUserPermissionsAsync(new List<long> { entity.Id });

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
            //�ݹ��ѯ����Ȩ�޵�
            var ids = _permissionRepository.Select
            .Where(a => a.Id == id)
            .AsTreeCte()
            .ToList(a => a.Id);

            //ɾ��Ȩ�޹����ӿ�
            await _permissionApiRepository.DeleteAsync(a => ids.Contains(a.PermissionId));

            //ɾ�����Ȩ��
            await _permissionRepository.DeleteAsync(a => ids.Contains(a.Id));

            //����û�Ȩ�޻���
            await ClearUserPermissionsAsync(ids);

            return ResultOutput.Ok();
        }

        /// <summary>
        /// ɾ��
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<IResultOutput> SoftDeleteAsync(long id)
        {
            //�ݹ��ѯ����Ȩ�޵�
            var ids = _permissionRepository.Select
            .Where(a => a.Id == id)
            .AsTreeCte()
            .ToList(a => a.Id);

            //ɾ��Ȩ��
            await _permissionRepository.SoftDeleteAsync(a => ids.Contains(a.Id));

            //����û�Ȩ�޻���
            await ClearUserPermissionsAsync(ids);

            return ResultOutput.Ok();
        }

        /// <summary>
        /// �����ɫȨ��
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        [Transaction]
        public async Task<IResultOutput> AssignAsync(PermissionAssignInput input)
        {
            //����Ȩ�޵�ʱ���жϽ�ɫ�Ƿ����
            var exists = await _roleRepository.Select.DisableGlobalFilter("Tenant").WhereDynamic(input.RoleId).AnyAsync();
            if (!exists)
            {
                return ResultOutput.NotOk("�ý�ɫ�����ڻ��ѱ�ɾ����");
            }

            //��ѯ��ɫȨ��
            var permissionIds = await _rolePermissionRepository.Select.Where(d => d.RoleId == input.RoleId).ToListAsync(m => m.PermissionId);

            //����ɾ��Ȩ��
            var deleteIds = permissionIds.Where(d => !input.PermissionIds.Contains(d));
            if (deleteIds.Any())
            {
                await _rolePermissionRepository.DeleteAsync(m => m.RoleId == input.RoleId && deleteIds.Contains(m.PermissionId));
            }

            //��������Ȩ��
            var insertRolePermissions = new List<RolePermissionEntity>();
            var insertPermissionIds = input.PermissionIds.Where(d => !permissionIds.Contains(d));

            //��ֹ�⻧�Ƿ���Ȩ
            if (_appConfig.Tenant && User.TenantType == TenantType.Tenant)
            {
                var masterDb = ServiceProvider.GetRequiredService<IFreeSql>();
                var tenantPermissionIds = await masterDb.GetRepositoryBase<TenantPermissionEntity>().Select.Where(d => d.TenantId == User.TenantId).ToListAsync(m => m.PermissionId);
                insertPermissionIds = insertPermissionIds.Where(d => tenantPermissionIds.Contains(d));
            }

            if (insertPermissionIds.Any())
            {
                foreach (var permissionId in insertPermissionIds)
                {
                    insertRolePermissions.Add(new RolePermissionEntity()
                    {
                        RoleId = input.RoleId,
                        PermissionId = permissionId,
                    });
                }
                await _rolePermissionRepository.InsertAsync(insertRolePermissions);
            }

            //�����ɫ�¹������û�Ȩ�޻���
            var userIds = await _userRoleRepository.Select.Where(a => a.RoleId == input.RoleId).ToListAsync(a => a.UserId);
            foreach (var userId in userIds)
            {
                await Cache.DelAsync(string.Format(CacheKey.UserPermissions, userId));
            }

            return ResultOutput.Ok();
        }

        /// <summary>
        /// �����⻧Ȩ��
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        [Transaction]
        public async Task<IResultOutput> SaveTenantPermissionsAsync(PermissionSaveTenantPermissionsInput input)
        {
            //����⻧db
            var ib = ServiceProvider.GetRequiredService<IdleBus<IFreeSql>>();
            var tenantDb = ib.GetTenantFreeSql(ServiceProvider, input.TenantId);

            //��ѯ�⻧Ȩ��
            var permissionIds = await _tenantPermissionRepository.Select.Where(d => d.TenantId == input.TenantId).ToListAsync(m => m.PermissionId);

            //����ɾ���⻧Ȩ��
            var deleteIds = permissionIds.Where(d => !input.PermissionIds.Contains(d));
            if (deleteIds.Any())
            {
                await _tenantPermissionRepository.DeleteAsync(m => m.TenantId == input.TenantId && deleteIds.Contains(m.PermissionId));
                //ɾ���⻧�¹����Ľ�ɫȨ��
                await tenantDb.GetRepositoryBase<RolePermissionEntity>().DeleteAsync(a => deleteIds.Contains(a.PermissionId));
            }

            //���������⻧Ȩ��
            var tenatPermissions = new List<TenantPermissionEntity>();
            var insertPermissionIds = input.PermissionIds.Where(d => !permissionIds.Contains(d));
            if (insertPermissionIds.Any())
            {
                foreach (var permissionId in insertPermissionIds)
                {
                    tenatPermissions.Add(new TenantPermissionEntity()
                    {
                        TenantId = input.TenantId,
                        PermissionId = permissionId,
                    });
                }
                await _tenantPermissionRepository.InsertAsync(tenatPermissions);
            }

            //����⻧�������û�Ȩ�޻���
            var userIds = await tenantDb.GetRepositoryBase<UserEntity>().Select.Where(a => a.TenantId == input.TenantId).ToListAsync(a => a.Id);
            if(userIds.Any())
            {
                foreach (var userId in userIds)
                {
                    await Cache.DelAsync(string.Format(CacheKey.UserPermissions, userId));
                }
            }

            return ResultOutput.Ok();
        }
    }
}