﻿using System.ComponentModel;

namespace ZhonTai.Admin.Services.Contracts
{
    /// <summary>
    /// 缓存键
    /// </summary>
    public static partial class CacheKey
    {
        /// <summary>
        /// 验证码 admin:captcha:guid
        /// </summary>
        [Description("验证码")]
        public const string CaptchaKey = "admin:captcha:{0}";

        /// <summary>
        /// 密码加密 admin:password:encrypt:guid
        /// </summary>
        [Description("密码加密")]
        public const string PassWordEncryptKey = "admin:password:encrypt:{0}";

        /// <summary>
        /// 用户权限 admin:user:permissions:用户主键
        /// </summary>
        [Description("用户权限")]
        public const string UserPermissions = "admin:user:permissions:{0}";

        /// <summary>
        /// 用户信息 admin:user:info:用户主键
        /// </summary>
        [Description("用户信息")]
        public const string UserInfo = "admin:user:info:{0}";

        /// <summary>
        /// 租户信息 admin:tenant:info:租户主键
        /// </summary>
        [Description("租户信息")]
        public const string TenantInfo = "admin:tenant:info:{0}";
    }
}