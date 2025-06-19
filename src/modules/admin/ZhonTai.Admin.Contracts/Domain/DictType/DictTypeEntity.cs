﻿using ZhonTai.Admin.Core.Entities;
using FreeSql.DataAnnotations;

namespace ZhonTai.Admin.Domain.DictType;

/// <summary>
/// 数据字典类型
/// </summary>
[Table(Name = DbConsts.TableNamePrefix + "dict_type", OldName = DbConsts.TableOldNamePrefix + "dict_type")]
[Index("idx_{tablename}_01", nameof(Name), true)]
public class DictTypeEntity : EntityBase
{
    /// <summary>
    /// 上级Id
    /// </summary>
    public long ParentId { get; set; }

    /// <summary>
    /// 名称
    /// </summary>
    [Column(StringLength = 50)]
    public string Name { get; set; }

    /// <summary>
    /// 编码
    /// </summary>
    [Column(StringLength = 50)]
    public string Code { get; set; }

    /// <summary>
    /// 描述
    /// </summary>
    [Column(StringLength = 500)]
    public string Description { get; set; }

    /// <summary>
    /// 启用
    /// </summary>
	public bool Enabled { get; set; } = true;

    /// <summary>
    /// 是否树形
    /// </summary>
    public bool IsTree { get; set; }

    /// <summary>
    /// 排序
    /// </summary>
	public int Sort { get; set; }
}