using Admin.Core.Common.Attributes;
using Admin.Core.Common.BaseModel;
using Admin.Core.Common.Cache;
using Admin.Core.Common.Configs;
using Admin.Core.Common.Output;
using Admin.Core.Model.Admin;
using Admin.Core.Repository.Admin;
using Admin.Core.Service.Admin.Permission.Input;
using Admin.Core.Service.Admin.Permission.Output;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Admin.Core.Service.Admin.Permission
{
    public class PermissionService : BaseService, IPermissionService
    {
        private readonly AppConfig _appConfig;
        private readonly IPermissionRepository _permissionRepository;
        private readonly IRoleRepository _roleRepository;
        private readonly IRolePermissionRepository _rolePermissionRepository;
        private readonly ITenantRepository _tenantRepository;

        public PermissionService(
            AppConfig appConfig,
            IPermissionRepository permissionRepository,
            IRoleRepository roleRepository,
            IRolePermissionRepository rolePermissionRepository,
            ITenantRepository tenantRepository
        )
        {
            _appConfig = appConfig;
            _permissionRepository = permissionRepository;
            _roleRepository = roleRepository;
            _rolePermissionRepository = rolePermissionRepository;
            _tenantRepository = tenantRepository;
        }

        public async Task<IResponseOutput> GetAsync(long id)
        {
            var result = await _permissionRepository.GetAsync(id);

            return ResponseOutput.Ok(result);
        }

        public async Task<IResponseOutput> GetGroupAsync(long id)
        {
            var result = await _permissionRepository.GetAsync<PermissionGetGroupOutput>(id);
            return ResponseOutput.Ok(result);
        }

        public async Task<IResponseOutput> GetMenuAsync(long id)
        {
            var result = await _permissionRepository.GetAsync<PermissionGetMenuOutput>(id);
            return ResponseOutput.Ok(result);
        }

        public async Task<IResponseOutput> GetApiAsync(long id)
        {
            var result = await _permissionRepository.GetAsync<PermissionGetApiOutput>(id);
            return ResponseOutput.Ok(result);
        }

        public async Task<IResponseOutput> GetDotAsync(long id)
        {
            var result = await _permissionRepository.GetAsync<PermissionGetDotOutput>(id);
            return ResponseOutput.Ok(result);
        }

        public async Task<IResponseOutput> ListAsync(string key, DateTime? start, DateTime? end)
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
                .ToListAsync(a => new PermissionListOutput { ApiPath = a.Api.Path });

            return ResponseOutput.Ok(data);
        }

        public async Task<IResponseOutput> AddGroupAsync(PermissionAddGroupInput input)
        {
            var entity = Mapper.Map<PermissionEntity>(input);
            var id = (await _permissionRepository.InsertAsync(entity)).Id;

            return ResponseOutput.Ok(id > 0);
        }

        public async Task<IResponseOutput> AddMenuAsync(PermissionAddMenuInput input)
        {
            var entity = Mapper.Map<PermissionEntity>(input);
            var id = (await _permissionRepository.InsertAsync(entity)).Id;

            return ResponseOutput.Ok(id > 0);
        }

        public async Task<IResponseOutput> AddApiAsync(PermissionAddApiInput input)
        {
            var entity = Mapper.Map<PermissionEntity>(input);
            var id = (await _permissionRepository.InsertAsync(entity)).Id;

            return ResponseOutput.Ok(id > 0);
        }

        public async Task<IResponseOutput> AddDotAsync(PermissionAddDotInput input)
        {
            var entity = Mapper.Map<PermissionEntity>(input);
            var id = (await _permissionRepository.InsertAsync(entity)).Id;

            return ResponseOutput.Ok(id > 0);
        }

        public async Task<IResponseOutput> UpdateGroupAsync(PermissionUpdateGroupInput input)
        {
            var result = false;
            if (input != null && input.Id > 0)
            {
                var entity = await _permissionRepository.GetAsync(input.Id);
                entity = Mapper.Map(input, entity);
                result = (await _permissionRepository.UpdateAsync(entity)) > 0;
            }

            return ResponseOutput.Result(result);
        }

        public async Task<IResponseOutput> UpdateMenuAsync(PermissionUpdateMenuInput input)
        {
            var result = false;
            if (input != null && input.Id > 0)
            {
                var entity = await _permissionRepository.GetAsync(input.Id);
                entity = Mapper.Map(input, entity);
                result = (await _permissionRepository.UpdateAsync(entity)) > 0;
            }

            return ResponseOutput.Result(result);
        }

        public async Task<IResponseOutput> UpdateApiAsync(PermissionUpdateApiInput input)
        {
            var result = false;
            if (input != null && input.Id > 0)
            {
                var entity = await _permissionRepository.GetAsync(input.Id);
                entity = Mapper.Map(input, entity);
                result = (await _permissionRepository.UpdateAsync(entity)) > 0;
            }

            return ResponseOutput.Result(result);
        }

        public async Task<IResponseOutput> UpdateDotAsync(PermissionUpdateDotInput input)
        {
            var result = false;
            if (input != null && input.Id > 0)
            {
                var entity = await _permissionRepository.GetAsync(input.Id);
                entity = Mapper.Map(input, entity);
                result = (await _permissionRepository.UpdateAsync(entity)) > 0;
            }

            return ResponseOutput.Result(result);
        }

        public async Task<IResponseOutput> DeleteAsync(long id)
        {
            var result = false;
            if (id > 0)
            {
                result = (await _permissionRepository.DeleteAsync(m => m.Id == id)) > 0;
            }

            return ResponseOutput.Result(result);
        }

        public async Task<IResponseOutput> SoftDeleteAsync(long id)
        {
            var result = await _permissionRepository.SoftDeleteAsync(id);
            return ResponseOutput.Result(result);
        }

        [Transaction]
        public async Task<IResponseOutput> AssignAsync(PermissionAssignInput input)
        {
            //����Ȩ�޵�ʱ���жϽ�ɫ�Ƿ����
            var exists = await _roleRepository.Select.DisableGlobalFilter("Tenant").WhereDynamic(input.RoleId).AnyAsync();
            if (!exists)
            {
                return ResponseOutput.NotOk("�ý�ɫ�����ڻ��ѱ�ɾ����");
            }

            //��ѯ��ɫȨ��
            var permissionIds = await _rolePermissionRepository.Select.Where(d => d.RoleId == input.RoleId).ToListAsync(m => m.PermissionId);

            //����ɾ��Ȩ��
            var deleteIds = permissionIds.Where(d => !input.PermissionIds.Contains(d));
            if (deleteIds.Count() > 0)
            {
                await _rolePermissionRepository.DeleteAsync(m => m.RoleId == input.RoleId && deleteIds.Contains(m.PermissionId));
            }

            //��������Ȩ��
            var insertRolePermissions = new List<RolePermissionEntity>();
            var insertPermissionIds = input.PermissionIds.Where(d => !permissionIds.Contains(d));
            if (insertPermissionIds.Count() > 0)
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

            //���Ȩ��
            await Cache.DelByPatternAsync(CacheKey.UserPermissions);

            return ResponseOutput.Ok();
        }

        public async Task<IResponseOutput> GetPermissionList()
        {
            var permissions = await _permissionRepository.Select
                .WhereIf(_appConfig.Tenant && User.TenantType == TenantType.Tenant && User.DataIsolationType == DataIsolationType.Share, a =>
                    _permissionRepository.Orm.Select<RolePermissionEntity>()
                    .InnerJoin<TenantEntity>((b, c) => b.RoleId == c.RoleId && c.Id == User.TenantId)
                    .DisableGlobalFilter("Tenant")
                    .Where(b => b.PermissionId == a.Id)
                    .Any()
                )
                .OrderBy(a => a.ParentId)
                .OrderBy(a => a.Sort)
                .ToListAsync(a => new { a.Id, a.ParentId, a.Label, a.Type });

            var apis = permissions
                .Where(a => new[] { PermissionType.Api, PermissionType.Dot }.Contains(a.Type))
                .Select(a => new { a.Id, a.ParentId, a.Label });

            var menus = permissions
                .Where(a => (new[] { PermissionType.Group, PermissionType.Menu }).Contains(a.Type))
                .Select(a => new
                {
                    a.Id,
                    a.ParentId,
                    a.Label,
                    Apis = apis.Where(b => b.ParentId == a.Id).Select(b => new { b.Id, b.Label })
                });

            return ResponseOutput.Ok(menus);
        }

        public async Task<IResponseOutput> GetRolePermissionList(long roleId = 0)
        {
            var permissionIds = await _rolePermissionRepository
                .Select.Where(d => d.RoleId == roleId)
                .ToListAsync(a => a.PermissionId);

            return ResponseOutput.Ok(permissionIds);
        }
    }
}