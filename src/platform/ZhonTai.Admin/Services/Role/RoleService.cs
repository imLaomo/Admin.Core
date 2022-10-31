﻿using System.Linq;
using System.Threading.Tasks;
using ZhonTai.Admin.Core.Repositories;
using ZhonTai.Admin.Core.Dto;
using ZhonTai.Admin.Domain.Role;
using ZhonTai.Admin.Domain.RolePermission;
using ZhonTai.Admin.Services.Role.Dto;
using ZhonTai.Admin.Domain.Role.Dto;
using ZhonTai.DynamicApi;
using ZhonTai.DynamicApi.Attributes;
using Microsoft.AspNetCore.Mvc;
using ZhonTai.Admin.Core.Consts;
using ZhonTai.Admin.Core.Attributes;
using ZhonTai.Admin.Domain.UserRole;
using ZhonTai.Admin.Domain.User;
using ZhonTai.Admin.Domain;
using ZhonTai.Admin.Domain.Org;
using ZhonTai.Admin.Domain.RoleOrg;

namespace ZhonTai.Admin.Services.Role;

/// <summary>
/// 角色服务
/// </summary>
[DynamicApi(Area = AdminConsts.AreaName)]
public class RoleService : BaseService, IRoleService, IDynamicApi
{
    private IRoleRepository _roleRepository => LazyGetRequiredService<IRoleRepository>();
    private IUserRepository _userRepository => LazyGetRequiredService<IUserRepository>();
    private IUserRoleRepository _userRoleRepository => LazyGetRequiredService<IUserRoleRepository>();
    private IRolePermissionRepository _rolePermissionRepository => LazyGetRequiredService<IRolePermissionRepository>();
    private IRoleOrgRepository _roleOrgRepository => LazyGetRequiredService<IRoleOrgRepository>();

    public RoleService()
    {
    }

    /// <summary>
    /// 查询角色
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public async Task<IResultOutput> GetAsync(long id)
    {
        var roleEntity = await _roleRepository.Select
        .WhereDynamic(id)
        .IncludeMany(a => a.Orgs.Select(b => new OrgEntity { Id = b.Id }))
        .ToOneAsync(a => new RoleGetOutput { Orgs = a.Orgs });

        var output = Mapper.Map<RoleGetOutput>(roleEntity);

        return ResultOutput.Ok(output);
    }

    /// <summary>
    /// 查询角色列表
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public async Task<IResultOutput> GetListAsync([FromQuery]RoleGetListInput input)
    {
        var list = await _roleRepository.Select
        .WhereIf(input.Name.NotNull(), a => a.Name.Contains(input.Name))
        .OrderBy(a => new {a.ParentId, a.Sort})
        .ToListAsync<RoleGetListOutput>();

        return ResultOutput.Ok(list);
    }

    /// <summary>
    /// 查询角色列表
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<IResultOutput> GetPageAsync(PageInput<RoleGetPageDto> input)
    {
        var key = input.Filter?.Name;

        var list = await _roleRepository.Select
        .WhereDynamicFilter(input.DynamicFilter)
        .WhereIf(key.NotNull(), a => a.Name.Contains(key))
        .Count(out var total)
        .OrderByDescending(true, c => c.Id)
        .Page(input.CurrentPage, input.PageSize)
        .ToListAsync<RoleGetPageOutput>();

        var data = new PageOutput<RoleGetPageOutput>()
        {
            List = list,
            Total = total
        };

        return ResultOutput.Ok(data);
    }

    /// <summary>
    /// 查询角色用户列表
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public async Task<IResultOutput> GetRoleUserListAsync([FromQuery] UserGetRoleUserListInput input)
    {
        var list = await _userRepository.Select.From<UserRoleEntity>()
            .InnerJoin(a => a.t2.UserId == a.t1.Id)
            .Where(a => a.t2.RoleId == input.RoleId)
            .WhereIf(input.Name.NotNull(), a => a.t1.Name.Contains(input.Name))
            .OrderByDescending(a => a.t1.Id)
            .ToListAsync<UserGetRoleUserListOutput>();

        return ResultOutput.Ok(list);
    }


    /// <summary>
    /// 新增角色用户
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public async Task<IResultOutput> AddRoleUserAsync(RoleAddRoleUserListInput input)
    {
        var roleId = input.RoleId;
        var userIds = await _userRoleRepository.Select.Where(a => a.RoleId == roleId).ToListAsync(a => a.UserId);
        var insertUserIds = input.UserIds.Except(userIds);
        if (insertUserIds != null && insertUserIds.Any())
        {
            var userRoleList = insertUserIds.Select(userId => new UserRoleEntity 
            { 
                UserId = userId, 
                RoleId = roleId 
            }).ToList();
            await _userRoleRepository.InsertAsync(userRoleList);
        }

        var clearUserIds = userIds.Concat(input.UserIds).Distinct();
        foreach (var userId in clearUserIds)
        {
            await Cache.DelAsync(CacheKeys.DataPermission + userId);
        }

        return ResultOutput.Ok();
    }

    /// <summary>
    /// 移除角色用户
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<IResultOutput> RemoveRoleUserAsync(RoleAddRoleUserListInput input)
    {
        var userIds = input.UserIds;
        if (userIds != null && userIds.Any())
        {
            await _userRoleRepository.Where(a => a.RoleId == input.RoleId && input.UserIds.Contains(a.UserId)).ToDelete().ExecuteAffrowsAsync();
        }

        foreach (var userId in userIds)
        {
            await Cache.DelAsync(CacheKeys.DataPermission + userId);
        }

        return ResultOutput.Ok();
    }

    private async Task AddRoleOrgAsync(long roleId, long[] orgIds)
    {
        if (orgIds != null && orgIds.Any())
        {
            var roleOrgs = orgIds.Select(orgId => new RoleOrgEntity 
            { 
                RoleId = roleId, 
                OrgId = orgId 
            }).ToList();
            await _roleOrgRepository.InsertAsync(roleOrgs);
        }
    }

    /// <summary>
    /// 添加
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public async Task<IResultOutput> AddAsync(RoleAddInput input)
    {
        if (await _roleRepository.Select.AnyAsync(a => a.ParentId == input.ParentId && a.Name == input.Name))
        {
            return ResultOutput.NotOk($"此{(input.ParentId == 0 ? "分组" : "角色")}已存在");
        }

        if (input.Code.NotNull() && await _roleRepository.Select.AnyAsync(a => a.ParentId == input.ParentId && a.Code == input.Code))
        {
            return ResultOutput.NotOk($"此{(input.ParentId == 0 ? "分组" : "角色")}编码已存在");
        }

        var entity = Mapper.Map<RoleEntity>(input);
        if (entity.Sort == 0)
        {
            var sort = await _roleRepository.Select.Where(a => a.ParentId == input.ParentId).MaxAsync(a => a.Sort);
            entity.Sort = sort + 1;
        }

        await _roleRepository.InsertAsync(entity);
        if (input.DataScope == DataScope.Custom)
        {
            await AddRoleOrgAsync(entity.Id, input.OrgIds);
        }

        return ResultOutput.Ok();
    }

    /// <summary>
    /// 修改
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public async Task<IResultOutput> UpdateAsync(RoleUpdateInput input)
    {
        if (!(input?.Id > 0))
        {
            return ResultOutput.NotOk();
        }

        var entity = await _roleRepository.GetAsync(input.Id);
        if (!(entity?.Id > 0))
        {
            return ResultOutput.NotOk("角色不存在");
        }

        if (await _roleRepository.Select.AnyAsync(a => a.ParentId == input.ParentId && a.Id != input.Id && a.Name == input.Name))
        {
            return ResultOutput.NotOk($"此{(input.ParentId == 0 ? "分组" : "角色")}已存在");
        }

        if (input.Code.NotNull() && await _roleRepository.Select.AnyAsync(a => a.ParentId == input.ParentId && a.Id != input.Id && a.Code == input.Code))
        {
            return ResultOutput.NotOk($"此{(input.ParentId == 0 ? "分组" : "角色")}编码已存在");
        }

        Mapper.Map(input, entity);
        await _roleRepository.UpdateAsync(entity);
        await _roleOrgRepository.DeleteAsync(a => a.RoleId == entity.Id);
        if (input.DataScope == DataScope.Custom)
        {
            await AddRoleOrgAsync(entity.Id, input.OrgIds);
        }

        var userIds = await _userRoleRepository.Select.Where(a => a.RoleId == entity.Id).ToListAsync(a => a.UserId);
        foreach (var userId in userIds)
        {
            await Cache.DelAsync(CacheKeys.DataPermission + userId);
        }

        return ResultOutput.Ok();
    }

    /// <summary>
    /// 彻底删除
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [AdminTransaction]
    public virtual async Task<IResultOutput> DeleteAsync(long id)
    {
        var userIds = await _userRoleRepository.Select.Where(a => a.RoleId == id).ToListAsync(a => a.UserId);

        //删除用户角色
        await _userRoleRepository.DeleteAsync(a => a.UserId == id);
        //删除角色权限
        await _rolePermissionRepository.DeleteAsync(a => a.RoleId == id);
        //删除角色
        await _roleRepository.DeleteAsync(m => m.Id == id);
        
        foreach (var userId in userIds)
        {
            await Cache.DelAsync(CacheKeys.DataPermission + userId);
        }

        return ResultOutput.Ok();
    }

    /// <summary>
    /// 批量彻底删除
    /// </summary>
    /// <param name="ids"></param>
    /// <returns></returns>
    [AdminTransaction]
    public virtual async Task<IResultOutput> BatchDeleteAsync(long[] ids)
    {
        var userIds = await _userRoleRepository.Select.Where(a => ids.Contains(a.RoleId)).ToListAsync(a => a.UserId);
        //删除用户角色
        await _userRoleRepository.DeleteAsync(a => ids.Contains(a.RoleId));
        //删除角色权限
        await _rolePermissionRepository.DeleteAsync(a => ids.Contains(a.RoleId));
        //删除角色
        await _roleRepository.DeleteAsync(a => ids.Contains(a.Id));

        foreach (var userId in userIds)
        {
            await Cache.DelAsync(CacheKeys.DataPermission + userId);
        }

        return ResultOutput.Ok();
    }

    /// <summary>
    /// 删除
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [AdminTransaction]
    public virtual async Task<IResultOutput> SoftDeleteAsync(long id)
    {
        var userIds = await _userRoleRepository.Select.Where(a => a.RoleId == id).ToListAsync(a => a.UserId);
        await _userRoleRepository.DeleteAsync(a => a.RoleId == id);
        await _rolePermissionRepository.DeleteAsync(a => a.RoleId == id);
        await _roleRepository.SoftDeleteAsync(id);
        foreach (var userId in userIds)
        {
            await Cache.DelAsync(CacheKeys.DataPermission + userId);
        }
        return ResultOutput.Ok();
    }

    /// <summary>
    /// 批量删除
    /// </summary>
    /// <param name="ids"></param>
    /// <returns></returns>
    [AdminTransaction]
    public virtual async Task<IResultOutput> BatchSoftDeleteAsync(long[] ids)
    {
        var userIds = await _userRoleRepository.Select.Where(a => ids.Contains(a.RoleId)).ToListAsync(a => a.UserId);
        await _userRoleRepository.DeleteAsync(a => ids.Contains(a.RoleId));
        await _rolePermissionRepository.DeleteAsync(a => ids.Contains(a.RoleId));
        await _roleRepository.SoftDeleteAsync(ids);
        foreach (var userId in userIds)
        {
            await Cache.DelAsync(CacheKeys.DataPermission + userId);
        }
        return ResultOutput.Ok();
    }
}