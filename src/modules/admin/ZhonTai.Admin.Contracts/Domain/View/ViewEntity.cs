﻿using FreeSql.DataAnnotations;
using ZhonTai.Admin.Core.Entities;

namespace ZhonTai.Admin.Domain.View;

/// <summary>
/// 视图管理
/// </summary>
[Table(Name = DbConsts.TableNamePrefix + "view", OldName = DbConsts.TableOldNamePrefix + "view")]
[Index("idx_{tablename}_01", nameof(Platform) + "," + nameof(ParentId) + "," + nameof(Label), true)]
public partial class ViewEntity : EntityBase, IChilds<ViewEntity>
{
    /// <summary>
    /// 平台
    /// </summary>
    [Column(StringLength = 20)]
    public string Platform { get; set; }

    /// <summary>
    /// 所属节点
    /// </summary>
	public long ParentId { get; set; }

    /// <summary>
    /// 视图命名
    /// </summary>
    [Column(StringLength = 50)]
    public string Name { get; set; }

    /// <summary>
    /// 视图名称
    /// </summary>
    [Column(StringLength = 500)]
    public string Label { get; set; }

    /// <summary>
    /// 视图路径
    /// </summary>
    [Column(StringLength = 500)]
    public string Path { get; set; }

    /// <summary>
    /// 说明
    /// </summary>
    [Column(StringLength = 500)]
    public string Description { get; set; }

    /// <summary>
    /// 缓存
    /// </summary>
    public bool Cache { get; set; } = true;

    /// <summary>
    /// 排序
    /// </summary>
    public int Sort { get; set; }

    /// <summary>
    /// 启用
    /// </summary>
    public bool Enabled { get; set; } = true;

    [Navigate(nameof(ParentId))]
    public List<ViewEntity> Childs { get; set; }
}