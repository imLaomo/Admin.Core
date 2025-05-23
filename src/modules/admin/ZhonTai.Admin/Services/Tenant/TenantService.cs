﻿using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Mapster;
using Yitter.IdGenerator;
using ZhonTai.Admin.Core.Attributes;
using ZhonTai.Admin.Core.Consts;
using ZhonTai.Admin.Core.Configs;
using ZhonTai.Admin.Core.Dto;
using ZhonTai.Admin.Core.Helpers;
using ZhonTai.Admin.Domain.Role;
using ZhonTai.Admin.Domain.RolePermission;
using ZhonTai.Admin.Domain.Tenant;
using ZhonTai.Admin.Domain.User;
using ZhonTai.Admin.Domain.UserRole;
using ZhonTai.Admin.Domain.Org;
using ZhonTai.Admin.Domain.UserStaff;
using ZhonTai.Admin.Domain.UserOrg;
using ZhonTai.Admin.Domain.Pkg;
using ZhonTai.Admin.Domain.TenantPkg;
using ZhonTai.Admin.Services.Tenant.Dto;
using ZhonTai.Admin.Services.Pkg;
using ZhonTai.Common.Helpers;
using ZhonTai.DynamicApi;
using ZhonTai.DynamicApi.Attributes;
using ZhonTai.Admin.Resources;
using ZhonTai.Admin.Core;
using ZhonTai.Admin.Services.Auth.Dto;
using ZhonTai.Admin.Services.Auth;
using ZhonTai.Admin.Core.Validators;
using ZhonTai.Admin.Core.Auth;

namespace ZhonTai.Admin.Services.Tenant;

/// <summary>
/// 租户服务
/// </summary>
[Order(50)]
[DynamicApi(Area = AdminConsts.AreaName)]
public class TenantService : BaseService, ITenantService, IDynamicApi
{
    private readonly ITenantRepository _tenantRep;
    private readonly ITenantPkgRepository _tenantPkgRep;
    private readonly IRoleRepository _roleRep;
    private readonly IUserRepository _userRep;
    private readonly IOrgRepository _orgRep;
    private readonly AppConfig _appConfig;
    private readonly Lazy<IUserRoleRepository> _userRoleRep;
    private readonly Lazy<IRolePermissionRepository> _rolePermissionRep;
    private readonly Lazy<IUserStaffRepository> _userStaffRep;
    private readonly Lazy<IUserOrgRepository> _userOrgRep;
    private readonly Lazy<IPasswordHasher<UserEntity>> _passwordHasher;
    private readonly Lazy<UserHelper> _userHelper;
    private readonly AdminLocalizer _adminLocalizer;

    public TenantService(
        ITenantRepository tenantRep,
        ITenantPkgRepository tenantPkgRep,
        IRoleRepository roleRep,
        IUserRepository userRep,
        IOrgRepository orgRep,
        IOptions<AppConfig> appConfig,
        Lazy<IUserRoleRepository> userRoleRep,
        Lazy<IRolePermissionRepository> rolePermissionRep,
        Lazy<IUserStaffRepository> userStaffRep,
        Lazy<IUserOrgRepository> userOrgRep,
        Lazy<IPasswordHasher<UserEntity>> passwordHasher,
        Lazy<UserHelper> userHelper,
        AdminLocalizer adminLocalizer
    )
    {
        _tenantRep = tenantRep;
        _tenantPkgRep = tenantPkgRep;
        _roleRep = roleRep;
        _userRep = userRep;
        _orgRep = orgRep;
        _appConfig = appConfig.Value;
        _userRoleRep = userRoleRep;
        _rolePermissionRep = rolePermissionRep;
        _userStaffRep = userStaffRep;
        _userOrgRep = userOrgRep;
        _passwordHasher = passwordHasher;
        _userHelper = userHelper;
        _adminLocalizer = adminLocalizer;
    }

    /// <summary>
    /// 查询
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public async Task<TenantGetOutput> GetAsync(long id)
    {
        using (_tenantRep.DataFilter.Disable(FilterNames.Tenant))
        {
            var tenant = await _tenantRep.Select
            .WhereDynamic(id)
            .IncludeMany(a => a.Pkgs.Select(b => new PkgEntity { Id = b.Id, Name = b.Name }))
            .FirstAsync(a => new TenantGetOutput
            {
                Name = a.Org.Name,
                Code = a.Org.Code,
                Pkgs = a.Pkgs,
                UserName = a.User.UserName,
                RealName = a.User.Name,
                Phone = a.User.Mobile,
                Email = a.User.Email,
            });
            return tenant;
        }
    }

    /// <summary>
    /// 查询分页
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<PageOutput<TenantGetPageOutput>> GetPageAsync(PageInput<TenantGetPageInput> input)
    {
        using var _ = _tenantRep.DataFilter.Disable(FilterNames.Tenant);

        var key = input.Filter?.Name;

        var list = await _tenantRep.Select
        .WhereDynamicFilter(input.DynamicFilter)
        .WhereIf(key.NotNull(), a => a.Org.Name.Contains(key))
        .Count(out var total)
        .OrderByDescending(true, a => a.Id)
        .IncludeMany(a => a.Pkgs.Select(b => new PkgEntity { Name = b.Name }))
        .Page(input.CurrentPage, input.PageSize)
        .ToListAsync(a => new TenantGetPageOutput
        {
            Name = a.Org.Name,
            Code = a.Org.Code,
            UserName = a.User.UserName,
            RealName = a.User.Name,
            Phone = a.User.Mobile,
            Email = a.User.Email,
            Pkgs = a.Pkgs,
        });

        var data = new PageOutput<TenantGetPageOutput>()
        {
            List = Mapper.Map<List<TenantGetPageOutput>>(list),
            Total = total
        };
         
        return data;
    }

    /// <summary>
    /// 新增
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [AdminTransaction]
    public virtual async Task<long> AddAsync(TenantAddInput input)
    {
        if (input.Password.IsNull())
        {
            input.Password = _appConfig.DefaultPassword;
        }
        _userHelper.Value.CheckPassword(input.Password);

        using var _t = _tenantRep.DataFilter.Disable(FilterNames.Tenant);
        using var _o = _orgRep.DataFilter.Disable(FilterNames.Tenant);
        using var _u = _userRep.DataFilter.Disable(FilterNames.Tenant);

        var existsOrg = await _orgRep.Select
        .Where(a => a.Name == input.Name && a.ParentId == 0)
        .WhereIf(input.Code.NotNull(), a => a.Code == input.Code)
        .FirstAsync(a => new { a.Name, a.Code });

        if (existsOrg != null)
        {
            if (existsOrg.Name == input.Name)
            {
                throw ResultOutput.Exception(_adminLocalizer["企业名称已存在"]);
            }

            if (input.Code.NotNull() && existsOrg.Code == input.Code)
            {
                throw ResultOutput.Exception(_adminLocalizer["企业编码已存在"]);
            }
        }

        Expression<Func<UserEntity, bool>> where = (a => a.UserName == input.UserName);
        where = where.Or(input.Phone.NotNull(), a => a.Mobile == input.Phone)
            .Or(input.Email.NotNull(), a => a.Email == input.Email);

        var existsUser = await _userRep.Select.Where(where)
            .FirstAsync(a => new { a.UserName, a.Mobile, a.Email });

        if (existsUser != null)
        {
            if (existsUser.UserName == input.UserName)
            {
                throw ResultOutput.Exception(_adminLocalizer["企业账号已存在"]);
            }

            if (input.Phone.NotNull() && existsUser.Mobile == input.Phone)
            {
                throw ResultOutput.Exception(_adminLocalizer["企业手机号已存在"]);
            }

            if (input.Email.NotNull() && existsUser.Email == input.Email)
            {
                throw ResultOutput.Exception(_adminLocalizer["企业邮箱已存在"]);
            }
        }

        //租户Id
        long tenantId = input.Id > 0 ? input.Id : YitIdHelper.NextId();

        //添加租户套餐
        if (input.PkgIds != null && input.PkgIds.Any())
        {
            var pkgs = input.PkgIds.Select(pkgId => new TenantPkgEntity
            {
                TenantId = tenantId,
                PkgId = pkgId
            }).ToList();

            await _tenantPkgRep.InsertAsync(pkgs);
        }

        //添加部门
        var org = new OrgEntity
        {
            TenantId = tenantId,
            Name = input.Name,
            Code = input.Code,
            ParentId = 0,
            MemberCount = 1,
            Sort = 1,
            Enabled = true
        };
        await _orgRep.InsertAsync(org);

        //添加用户
        var user = new UserEntity
        {
            TenantId = tenantId,
            UserName = input.UserName,
            Name = input.RealName,
            Mobile = input.Phone,
            Email = input.Email,
            Type = UserType.TenantAdmin,
            OrgId = org.Id,
            Enabled = true
        };
        if (_appConfig.PasswordHasher)
        {
            user.Password = _passwordHasher.Value.HashPassword(user, input.Password);
            user.PasswordEncryptType = PasswordEncryptType.PasswordHasher;
        }
        else
        {
            user.Password = MD5Encrypt.Encrypt32(input.Password);
            user.PasswordEncryptType = PasswordEncryptType.MD5Encrypt32;
        }
        await _userRep.InsertAsync(user);

        long userId = user.Id;

        //添加用户员工
        var emp = new UserStaffEntity
        {
            Id = userId,
            TenantId = tenantId
        };
        await _userStaffRep.Value.InsertAsync(emp);

        //添加用户部门
        var userOrg = new UserOrgEntity
        {
            UserId = userId,
            OrgId = org.Id
        };
        await _userOrgRep.Value.InsertAsync(userOrg);

        //添加角色分组和角色
        var roleId = YitIdHelper.NextId();
        var jobGroupId = YitIdHelper.NextId();
        var roles = new List<RoleEntity>
        {
            new()
            {
                Id= jobGroupId,
                ParentId = 0,
                TenantId = tenantId,
                Type = RoleType.Group,
                Name = "岗位",
                Sort = 1
            },
            new()
            {
                Id = roleId,
                TenantId = tenantId,
                Type = RoleType.Role,
                Name = "主管理员",
                Code = "main-admin",
                ParentId = jobGroupId,
                DataScope = DataScope.All,
                Sort = 1
            },
            new()
            {
                TenantId = tenantId,
                Type = RoleType.Role,
                Name = "普通员工",
                Code = "emp",
                ParentId = jobGroupId,
                DataScope = DataScope.Self,
                Sort = 1
            }
        };
        await _roleRep.InsertAsync(roles);

        //添加用户角色
        var userRole = new UserRoleEntity()
        {
            UserId = userId,
            RoleId = roleId
        };
        await _userRoleRep.Value.InsertAsync(userRole);

        //添加租户
        var tenant = input.Adapt<TenantEntity>();
        tenant.Id = tenantId;
        tenant.UserId = userId;
        tenant.OrgId = org.Id;
        await _tenantRep.InsertAsync(tenant);

        return tenant.Id;
    }

    /// <summary>
    /// 注册
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [AdminTransaction]
    [NonAction]
    public virtual async Task<long> RegAsync(TenantRegInput input)
    {
        if (input.Password.IsNull())
        {
            input.Password = _appConfig.DefaultPassword;
        }
        _userHelper.Value.CheckPassword(input.Password);

        using var _t = _tenantRep.DataFilter.Disable(FilterNames.Tenant);
        using var _o = _orgRep.DataFilter.Disable(FilterNames.Tenant);
        using var _u = _userRep.DataFilter.Disable(FilterNames.Tenant);
 
        Expression<Func<UserEntity, bool>> where = (a => a.UserName == input.UserName);
        where = where.Or(input.Mobile.NotNull(), a => a.Mobile == input.Mobile)
            .Or(input.Email.NotNull(), a => a.Email == input.Email);

        //检查用户
        var existsUser = await _userRep.Select.Where(where)
            .FirstAsync(a => new { a.UserName, a.Mobile, a.Email });

        if (existsUser != null)
        {
            if (existsUser.UserName == input.UserName)
            {
                throw ResultOutput.Exception(_adminLocalizer["企业账号已注册"]);
            }

            if (input.Mobile.NotNull() && existsUser.Mobile == input.Mobile)
            {
                throw ResultOutput.Exception(_adminLocalizer["企业手机号已注册"]);
            }

            if (input.Email.NotNull() && existsUser.Email == input.Email)
            {
                throw ResultOutput.Exception(_adminLocalizer["企业邮箱已注册"]);
            }
        }

        //检查部门
        var existsOrg = await _orgRep.Select
       .Where(a => a.Name == input.Name && a.ParentId == 0)
       .WhereIf(input.Code.NotNull(), a => a.Code == input.Code)
       .FirstAsync(a => new { a.Id, a.TenantId });

        var hasTenant = existsOrg?.TenantId > 0;

        //租户Id
        long tenantId = hasTenant ? existsOrg.TenantId.Value : (input.Id > 0 ? input.Id : YitIdHelper.NextId());

        //添加部门
        var orgId = existsOrg?.Id > 0 ? existsOrg.Id : YitIdHelper.NextId();
        if (existsOrg == null)
        {
            var org = new OrgEntity
            {
                Id = orgId,
                TenantId = tenantId,
                Name = input.Name,
                Code = input.Code,
                ParentId = 0,
                MemberCount = 1,
                Sort = 1,
                Enabled = true
            };
            await _orgRep.InsertAsync(org);
        }
       
        //添加用户
        var user = new UserEntity
        {
            TenantId = tenantId,
            UserName = input.UserName,
            Name = input.RealName,
            Mobile = input.Mobile,
            Email = input.Email,
            Type = hasTenant ? UserType.DefaultUser : UserType.TenantAdmin,
            OrgId = orgId,
            Enabled = true
        };
        if (_appConfig.PasswordHasher)
        {
            user.Password = _passwordHasher.Value.HashPassword(user, input.Password);
            user.PasswordEncryptType = PasswordEncryptType.PasswordHasher;
        }
        else
        {
            user.Password = MD5Encrypt.Encrypt32(input.Password);
            user.PasswordEncryptType = PasswordEncryptType.MD5Encrypt32;
        }
        await _userRep.InsertAsync(user);

        long userId = user.Id;

        //添加用户员工
        var emp = new UserStaffEntity
        {
            Id = userId,
            TenantId = tenantId,
        };
        await _userStaffRep.Value.InsertAsync(emp);

        //添加用户部门
        var userOrg = new UserOrgEntity
        {
            UserId = userId,
            OrgId = orgId
        };
        await _userOrgRep.Value.InsertAsync(userOrg);

        //添加角色分组和角色
        if (!hasTenant)
        {
            var roleId = YitIdHelper.NextId();
            var jobGroupId = YitIdHelper.NextId();
            var roles = new List<RoleEntity>
            {
                new()
                {
                    Id= jobGroupId,
                    ParentId = 0,
                    TenantId = tenantId,
                    Type = RoleType.Group,
                    Name = "岗位",
                    Sort = 1
                },
                new()
                {
                    Id = roleId,
                    TenantId = tenantId,
                    Type = RoleType.Role,
                    Name = "主管理员",
                    Code = "main-admin",
                    ParentId = jobGroupId,
                    DataScope = DataScope.All,
                    Sort = 1
                },
                new()
                {
                    TenantId = tenantId,
                    Type = RoleType.Role,
                    Name = "普通员工",
                    Code = "emp",
                    ParentId = jobGroupId,
                    DataScope = DataScope.Self,
                    Sort = 1
                }
            };
            await _roleRep.InsertAsync(roles);

            //添加用户角色
            var userRole = new UserRoleEntity()
            {
                UserId = userId,
                RoleId = roleId
            };
            await _userRoleRep.Value.InsertAsync(userRole);
        }

        //添加租户
        if(!hasTenant)
        {
            var tenant = input.Adapt<TenantEntity>();
            tenant.Id = tenantId;
            tenant.UserId = userId;
            tenant.OrgId = orgId;
            await _tenantRep.InsertAsync(tenant);

            //添加租户套餐
            if (input.PkgIds != null && input.PkgIds.Length != 0)
            {
                var pkgs = input.PkgIds.Select(pkgId => new TenantPkgEntity
                {
                    TenantId = tenantId,
                    PkgId = pkgId
                }).ToList();

                await _tenantPkgRep.InsertAsync(pkgs);
            }
        }

        return tenantId;
    }

    /// <summary>
    /// 修改
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public async Task UpdateAsync(TenantUpdateInput input)
    {
        using var _t = _tenantRep.DataFilter.Disable(FilterNames.Tenant);
        using var _o = _orgRep.DataFilter.Disable(FilterNames.Tenant);
        using var _u = _userRep.DataFilter.Disable(FilterNames.Tenant);

        var tenant = await _tenantRep.GetAsync(input.Id);
        if (!(tenant?.Id > 0))
        {
            throw ResultOutput.Exception(_adminLocalizer["租户不存在"]);
        }

        var existsOrg = await _orgRep.Select
            .Where(a => a.Id != tenant.OrgId && a.ParentId == 0 && (a.Name == input.Name || a.Code == input.Code))
            .FirstAsync(a => new { a.Name, a.Code });

        if (existsOrg != null)
        {
            if (existsOrg.Name == input.Name)
            {
                throw ResultOutput.Exception(_adminLocalizer["企业名称已存在"]);
            }

            if (existsOrg.Code == input.Code)
            {
                throw ResultOutput.Exception(_adminLocalizer["企业编码已存在"]);
            }
        }

        Expression<Func<UserEntity, bool>> where = (a => a.UserName == input.UserName);
        where = where.Or(input.Phone.NotNull(), a => a.Mobile == input.Phone)
            .Or(input.Email.NotNull(), a => a.Email == input.Email);

        var existsUser = await _userRep.Select.Where(a => a.Id != tenant.UserId).Where(where)
            .FirstAsync(a => new { a.Id, a.Name, a.UserName, a.Mobile, a.Email });

        if (existsUser != null)
        {
            if (existsUser.UserName == input.UserName)
            {
                throw ResultOutput.Exception(_adminLocalizer["企业账号已存在"]);
            }

            if (input.Phone.NotNull() && existsUser.Mobile == input.Phone)
            {
                throw ResultOutput.Exception(_adminLocalizer["企业手机号已存在"]);
            }

            if (input.Email.NotNull() && existsUser.Email == input.Email)
            {
                throw ResultOutput.Exception(_adminLocalizer["企业邮箱已存在"]);
            }
        }

        //更新用户
        await _userRep.UpdateDiy.DisableGlobalFilter(FilterNames.Tenant).SetSource(
        new UserEntity()
        {
            Id = tenant.UserId,
            Name = input.RealName,
            UserName = input.UserName,
            Mobile = input.Phone,
            Email = input.Email
        })
        .UpdateColumns(a => new { a.Name, a.UserName, a.Mobile, a.Email, a.ModifiedTime }).ExecuteAffrowsAsync();

        //更新部门
        await _orgRep.UpdateDiy.DisableGlobalFilter(FilterNames.Tenant).SetSource(
        new OrgEntity()
        {
            Id = tenant.OrgId,
            Name = input.Name,
            Code = input.Code
        })
        .UpdateColumns(a => new { a.Name, a.Code, a.ModifiedTime }).ExecuteAffrowsAsync();

        //更新租户
        await _tenantRep.UpdateDiy.DisableGlobalFilter(FilterNames.Tenant).SetSource(
        new TenantEntity()
        {
            Id = tenant.Id,
            Domain = input.Domain,
            Description = input.Description,
        })
        .UpdateColumns(a => new { a.Description, a.Domain, a.ModifiedTime }).ExecuteAffrowsAsync();

        //更新租户套餐
        await _tenantPkgRep.DeleteAsync(a => a.TenantId == tenant.Id);
        if (input.PkgIds != null && input.PkgIds.Any())
        {
            var pkgs = input.PkgIds.Select(pkgId => new TenantPkgEntity
            {
                TenantId = tenant.Id,
                PkgId = pkgId
            }).ToList();

            await _tenantPkgRep.InsertAsync(pkgs);

            //清除租户下所有用户权限缓存
            await LazyGetRequiredService<PkgService>().ClearUserPermissionsAsync(new List<long> { tenant.Id });
        }
    }

    /// <summary>
    /// 彻底删除
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [AdminTransaction]
    public virtual async Task DeleteAsync(long id)
    {
        using (_tenantRep.DataFilter.Disable(FilterNames.Tenant))
        {
            var tenantType = await _tenantRep.Select.WhereDynamic(id).ToOneAsync(a => a.TenantType);
            if (tenantType == TenantType.Platform)
            {
                throw ResultOutput.Exception(_adminLocalizer["平台租户禁止删除"]);
            }

            //删除角色权限
            await _rolePermissionRep.Value.Where(a => a.Role.TenantId == id).DisableGlobalFilter(FilterNames.Tenant).ToDelete().ExecuteAffrowsAsync();

            //删除用户角色
            await _userRoleRep.Value.Where(a => a.User.TenantId == id).DisableGlobalFilter(FilterNames.Tenant).ToDelete().ExecuteAffrowsAsync();

            //删除员工
            await _userStaffRep.Value.Where(a => a.TenantId == id).DisableGlobalFilter(FilterNames.Tenant).ToDelete().ExecuteAffrowsAsync();

            //删除用户部门
            await _userOrgRep.Value.Where(a => a.User.TenantId == id).DisableGlobalFilter(FilterNames.Tenant).ToDelete().ExecuteAffrowsAsync();

            //删除部门
            await _orgRep.Where(a => a.TenantId == id).DisableGlobalFilter(FilterNames.Tenant).ToDelete().ExecuteAffrowsAsync();

            //删除用户
            await _userRep.Where(a => a.TenantId == id && a.Type != UserType.Member).DisableGlobalFilter(FilterNames.Tenant).ToDelete().ExecuteAffrowsAsync();

            //删除角色
            await _roleRep.Where(a => a.TenantId == id).DisableGlobalFilter(FilterNames.Tenant).ToDelete().ExecuteAffrowsAsync();

            //删除租户套餐
            await _tenantPkgRep.DeleteAsync(a => a.TenantId == id);

            //删除租户
            await _tenantRep.DeleteAsync(id);

            //清除租户下所有用户权限缓存
            await LazyGetRequiredService<PkgService>().ClearUserPermissionsAsync(new List<long> { id });
        }
    }

    /// <summary>
    /// 删除
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [AdminTransaction]
    public virtual async Task SoftDeleteAsync(long id)
    {
        using (_tenantRep.DataFilter.Disable(FilterNames.Tenant))
        {
            var tenantType = await _tenantRep.Select.WhereDynamic(id).ToOneAsync(a => a.TenantType);
            if (tenantType == TenantType.Platform)
            {
                throw ResultOutput.Exception(_adminLocalizer["平台租户禁止删除"]);
            }

            //删除部门
            await _orgRep.SoftDeleteAsync(a => a.TenantId == id, FilterNames.Tenant);

            //删除用户
            await _userRep.SoftDeleteAsync(a => a.TenantId == id && a.Type != UserType.Member, FilterNames.Tenant);

            //删除角色
            await _roleRep.SoftDeleteAsync(a => a.TenantId == id, FilterNames.Tenant);

            //删除租户
            var result = await _tenantRep.SoftDeleteAsync(id);

            //清除租户下所有用户权限缓存
            await LazyGetRequiredService<PkgService>().ClearUserPermissionsAsync(new List<long> { id });
        }
    }

    /// <summary>
    /// 批量删除
    /// </summary>
    /// <param name="ids"></param>
    /// <returns></returns>
    [AdminTransaction]
    public virtual async Task BatchSoftDeleteAsync(long[] ids)
    {
        using (_tenantRep.DataFilter.Disable(FilterNames.Tenant))
        {
            var tenantType = await _tenantRep.Select.WhereDynamic(ids).ToOneAsync(a => a.TenantType);
            if (tenantType == TenantType.Platform)
            {
                throw ResultOutput.Exception(_adminLocalizer["平台租户禁止删除"]);
            }

            //删除部门
            await _orgRep.SoftDeleteAsync(a => ids.Contains(a.TenantId.Value), FilterNames.Tenant);

            //删除用户
            await _userRep.SoftDeleteAsync(a => ids.Contains(a.TenantId.Value) && a.Type != UserType.Member, FilterNames.Tenant);

            //删除角色
            await _roleRep.SoftDeleteAsync(a => ids.Contains(a.TenantId.Value), FilterNames.Tenant);

            //删除租户
            var result = await _tenantRep.SoftDeleteAsync(ids);

            //清除租户下所有用户权限缓存
            await LazyGetRequiredService<PkgService>().ClearUserPermissionsAsync(ids.ToList());
        }
    }

    /// <summary>
    /// 设置启用
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public async Task SetEnableAsync(TenantSetEnableInput input)
    {
        var entity = await _tenantRep.GetAsync(input.TenantId);
        if (entity.TenantType == TenantType.Platform)
        {
            throw ResultOutput.Exception(_adminLocalizer["平台租户禁止禁用"]);
        }
        entity.Enabled = input.Enabled;
        await _tenantRep.UpdateAsync(entity);
    }

    /// <summary>
    /// 一键登录
    /// </summary>
    /// <returns></returns>
    [HttpPost]
    public async Task<TokenInfo> OneClickLoginAsync([ValidateRequired] long tenantId)
    {
        if (!(tenantId > 0))
        {
            throw ResultOutput.Exception(_adminLocalizer["请选择租户"]);
        }

        var userRep = _userRep;
        using var _ = userRep.DataFilter.DisableAll();

        var authLoginOutput = await userRep.Select
            .Where(a => a.Tenant.Id == tenantId && a.Tenant.UserId == a.Id)
            .ToOneAsync(a=> new AuthLoginOutput
            {
                Tenant = new AuthLoginTenantModel
                {
                    DbKey = a.Tenant.DbKey,
                    Enabled = a.Tenant.Enabled,
                    TenantType = a.Tenant.TenantType,
                }
            });

        if (authLoginOutput == null)
        {
            throw ResultOutput.Exception(_adminLocalizer["超级管理员不存在"]);
        }

        var tokenInfo = AppInfo.GetRequiredService<IAuthService>().GetTokenInfo(authLoginOutput);

        return tokenInfo;
    }
}