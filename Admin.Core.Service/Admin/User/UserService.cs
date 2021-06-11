using Admin.Core.Common.Attributes;
using Admin.Core.Common.Cache;
using Admin.Core.Common.Configs;
using Admin.Core.Common.Helpers;
using Admin.Core.Common.Input;
using Admin.Core.Common.Output;
using Admin.Core.Model.Admin;
using Admin.Core.Repository.Admin;
using Admin.Core.Service.Admin.Auth.Output;
using Admin.Core.Service.Admin.User.Input;
using Admin.Core.Service.Admin.User.Output;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Admin.Core.Service.Admin.User
{
    /// <summary>
    /// 用户服务
    /// </summary>
    public class UserService : BaseService, IUserService
    {
        private readonly AppConfig _appConfig;
        private readonly IUserRepository _userRepository;
        private readonly IUserRoleRepository _userRoleRepository;
        private readonly IRolePermissionRepository _rolePermissionRepository;
        private readonly ITenantRepository _tenantRepository;

        public UserService(
            AppConfig appConfig,
            IUserRepository userRepository,
            IUserRoleRepository userRoleRepository,
            IRolePermissionRepository rolePermissionRepository,
            ITenantRepository tenantRepository
        )
        {
            _appConfig = appConfig;
            _userRepository = userRepository;
            _userRoleRepository = userRoleRepository;
            _rolePermissionRepository = rolePermissionRepository;
            _tenantRepository = tenantRepository;
        }

        public async Task<ResponseOutput<AuthLoginOutput>> GetLoginUserAsync(long id)
        {
            var output = new ResponseOutput<AuthLoginOutput>();
            var entityDto = await _userRepository.Select.DisableGlobalFilter("Tenant").WhereDynamic(id).ToOneAsync<AuthLoginOutput>();
            if (_appConfig.Tenant)
            {
                var tenant = await _tenantRepository.Select.DisableGlobalFilter("Tenant").WhereDynamic(entityDto.TenantId).ToOneAsync(a => new { a.TenantType, a.DataIsolationType });
                output.Data.TenantType = tenant.TenantType;
                output.Data.DataIsolationType = tenant.DataIsolationType;
            }
            return output.Ok(entityDto);
        }

        public async Task<ResponseOutput<UserGetOutput>> GetAsync(long id)
        {
            var res = new ResponseOutput<UserGetOutput>();

            var entity = await _userRepository.Select
            .WhereDynamic(id)
            .IncludeMany(a => a.Roles.Select(b => new RoleEntity { Id = b.Id }))
            .ToOneAsync();

            var entityDto = Mapper.Map<UserGetOutput>(entity);
            return res.Ok(entityDto);
        }

        public async Task<IResponseOutput> GetBasicAsync()
        {
            if (!(User?.Id > 0))
            {
                return ResponseOutput.NotOk("未登录！");
            }

            var data = await _userRepository.GetAsync<UserUpdateBasicInput>(User.Id);
            return ResponseOutput.Ok(data);
        }

        public async Task<IList<UserPermissionsOutput>> GetPermissionsAsync()
        {
            var key = string.Format(CacheKey.UserPermissions, User.Id);
            var result = await Cache.GetOrSetAsync(key, async () =>
            {
                var userPermissoins = await _rolePermissionRepository.Select
                .InnerJoin<UserRoleEntity>((a, b) => a.RoleId == b.RoleId && b.UserId == User.Id && a.Permission.Type == PermissionType.Api)
                .Include(a => a.Permission.Api)
                .Distinct()
                .ToListAsync(a => new UserPermissionsOutput { HttpMethods = a.Permission.Api.HttpMethods, Path = a.Permission.Api.Path });
                return userPermissoins;
            });
            return result;
        }

        public async Task<IResponseOutput> PageAsync(PageInput<UserEntity> input)
        {
            var list = await _userRepository.Select
            .WhereDynamicFilter(input.DynamicFilter)
            .Count(out var total)
            .OrderByDescending(true, a => a.Id)
            .IncludeMany(a => a.Roles.Select(b => new RoleEntity { Name = b.Name }))
            .Page(input.CurrentPage, input.PageSize)
            .ToListAsync();

            var data = new PageOutput<UserListOutput>()
            {
                List = Mapper.Map<List<UserListOutput>>(list),
                Total = total
            };

            return ResponseOutput.Ok(data);
        }

        [Transaction]
        public async Task<IResponseOutput> AddAsync(UserAddInput input)
        {
            if (input.Password.IsNull())
            {
                input.Password = "111111";
            }

            input.Password = MD5Encrypt.Encrypt32(input.Password);

            var entity = Mapper.Map<UserEntity>(input);
            var user = await _userRepository.InsertAsync(entity);

            if (!(user?.Id > 0))
            {
                return ResponseOutput.NotOk();
            }

            if (input.RoleIds != null && input.RoleIds.Any())
            {
                var roles = input.RoleIds.Select(a => new UserRoleEntity { UserId = user.Id, RoleId = a });
                await _userRoleRepository.InsertAsync(roles);
            }

            return ResponseOutput.Ok();
        }

        [Transaction]
        public async Task<IResponseOutput> UpdateAsync(UserUpdateInput input)
        {
            if (!(input?.Id > 0))
            {
                return ResponseOutput.NotOk();
            }

            var user = await _userRepository.GetAsync(input.Id);
            if (!(user?.Id > 0))
            {
                return ResponseOutput.NotOk("用户不存在！");
            }

            Mapper.Map(input, user);
            await _userRepository.UpdateAsync(user);
            await _userRoleRepository.DeleteAsync(a => a.UserId == user.Id);
            if (input.RoleIds != null && input.RoleIds.Any())
            {
                var roles = input.RoleIds.Select(a => new UserRoleEntity { UserId = user.Id, RoleId = a });
                await _userRoleRepository.InsertAsync(roles);
            }

            return ResponseOutput.Ok();
        }

        public async Task<IResponseOutput> UpdateBasicAsync(UserUpdateBasicInput input)
        {
            var entity = await _userRepository.GetAsync(input.Id);
            entity = Mapper.Map(input, entity);
            var result = (await _userRepository.UpdateAsync(entity)) > 0;

            //清除用户缓存
            await Cache.DelAsync(string.Format(CacheKey.UserInfo, input.Id));

            return ResponseOutput.Result(result);
        }

        public async Task<IResponseOutput> ChangePasswordAsync(UserChangePasswordInput input)
        {
            if (input.ConfirmPassword != input.NewPassword)
            {
                return ResponseOutput.NotOk("新密码和确认密码不一致！");
            }

            var entity = await _userRepository.GetAsync(input.Id);
            var oldPassword = MD5Encrypt.Encrypt32(input.OldPassword);
            if (oldPassword != entity.Password)
            {
                return ResponseOutput.NotOk("旧密码不正确！");
            }

            input.Password = MD5Encrypt.Encrypt32(input.NewPassword);

            entity = Mapper.Map(input, entity);
            var result = (await _userRepository.UpdateAsync(entity)) > 0;

            return ResponseOutput.Result(result);
        }

        public async Task<IResponseOutput> DeleteAsync(long id)
        {
            var result = false;
            if (id > 0)
            {
                result = (await _userRepository.DeleteAsync(m => m.Id == id)) > 0;
            }

            return ResponseOutput.Result(result);
        }

        [Transaction]
        public async Task<IResponseOutput> SoftDeleteAsync(long id)
        {
            var result = await _userRepository.SoftDeleteAsync(id);
            await _userRoleRepository.DeleteAsync(a => a.UserId == id);

            return ResponseOutput.Result(result);
        }

        [Transaction]
        public async Task<IResponseOutput> BatchSoftDeleteAsync(long[] ids)
        {
            var result = await _userRepository.SoftDeleteAsync(ids);
            await _userRoleRepository.DeleteAsync(a => ids.Contains(a.UserId));

            return ResponseOutput.Result(result);
        }
    }
}