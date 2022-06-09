﻿using AspNetCoreRateLimit;
using Autofac;
using IdentityServer4.AccessTokenValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyModel;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Reflection;
using System.Text;
using Mapster;
using Yitter.IdGenerator;
//using FluentValidation;
//using FluentValidation.AspNetCore;
using ZhonTai.Admin.Core.Auth;
using ZhonTai.Admin.Tools.Cache;
using ZhonTai.Common.Helpers;
using ZhonTai.Admin.Core.Db;
using ZhonTai.Admin.Core.Enums;
using ZhonTai.Admin.Core.Extensions;
using ZhonTai.Admin.Core.Filters;
using ZhonTai.Admin.Core.Logs;
using ZhonTai.Admin.Core.RegisterModules;
using System.IO;
using Microsoft.OpenApi.Any;
using Microsoft.AspNetCore.Mvc.Controllers;
using ZhonTai.Admin.Core.Attributes;
using ZhonTai.Admin.Core.Configs;
using ZhonTai.Admin.Core.Consts;
using MapsterMapper;
using ZhonTai.DynamicApi;
using ZhonTai.ApiUI;
using NLog.Web;
using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc;

namespace ZhonTai.Admin.Core
{
    public class HostApp
    {
        public void Run(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            //使用NLog日志
            builder.Host.UseNLog();

            //添加配置
            builder.Host.ConfigureAppConfiguration((context, builder) =>
            {
                builder.AddJsonFile("./configs/ratelimitconfig.json", optional: true, reloadOnChange: true);
                if (context.HostingEnvironment.EnvironmentName.NotNull())
                {
                    builder.AddJsonFile($"./configs/ratelimitconfig.{context.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true);
                }
                builder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                if (context.HostingEnvironment.EnvironmentName.NotNull())
                {
                    builder.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true);
                }
            });

            var services = builder.Services;
            var env = builder.Environment;
            var configuration = builder.Configuration;

            var configHelper = new ConfigHelper();
            var appConfig = ConfigHelper.Get<AppConfig>("appconfig", env.EnvironmentName) ?? new AppConfig();

            //应用配置
            services.AddSingleton(appConfig);

            //使用Autofac容器
            builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
            //配置Autofac容器
            builder.Host.ConfigureContainer<ContainerBuilder>(builder =>
            {
                // 控制器注入
                builder.RegisterModule(new ControllerModule());

                // 单例注入
                builder.RegisterModule(new SingleInstanceModule(appConfig));

                // 仓储注入
                builder.RegisterModule(new RepositoryModule(appConfig));

                // 服务注入
                builder.RegisterModule(new ServiceModule(appConfig));
            });

            //配置Kestrel服务器
            builder.WebHost.ConfigureKestrel((context, options) =>
            {
                //设置应用服务器Kestrel请求体最大为100MB
                options.Limits.MaxRequestBodySize = 1024 * 1024 * 100;
            });

            //访问地址
            builder.WebHost.UseUrls(appConfig.Urls);

            //配置服务
            ConfigureServices(services, env, configuration, configHelper, appConfig);

            var app = builder.Build();

            //配置中间件
            Configure(app, env, appConfig);

            app.Run();
        }

        /// <summary>
        /// 配置服务
        /// </summary>
        /// <param name="services"></param>
        /// <param name="env"></param>
        /// <param name="configuration"></param>
        /// <param name="configHelper"></param>
        /// <param name="appConfig"></param>
        private void ConfigureServices(IServiceCollection services, IWebHostEnvironment env, IConfiguration configuration, ConfigHelper configHelper, AppConfig appConfig)
        {
            //雪花漂移算法
            YitIdHelper.SetIdGenerator(new IdGeneratorOptions(1) { WorkerIdBitLength = 6 });

            //权限处理
            services.AddScoped<IPermissionHandler, PermissionHandler>();

            // ClaimType不被更改
            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

            //用户信息
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            if (appConfig.IdentityServer.Enable)
            {
                //is4
                services.TryAddSingleton<IUser, UserIdentiyServer>();
            }
            else
            {
                //jwt
                services.TryAddSingleton<IUser, User>();
            }

            //添加数据库
            services.AddDbAsync(env).Wait();

           
            //数据库配置
            var dbConfig = ConfigHelper.Get<DbConfig>("dbconfig", env.EnvironmentName);
            services.AddSingleton(dbConfig);
            //添加IdleBus单例
            var timeSpan = dbConfig.IdleTime > 0 ? TimeSpan.FromMinutes(dbConfig.IdleTime) : TimeSpan.MaxValue;
            var ib = new IdleBus<IFreeSql>(timeSpan);
            services.AddSingleton(ib);

            //上传配置
            var uploadConfig = ConfigHelper.Load("uploadconfig", env.EnvironmentName, true);
            services.Configure<UploadConfig>(uploadConfig);

            #region Mapster 映射配置

            Assembly[] assemblies = DependencyContext.Default.RuntimeLibraries
                .Where(a => appConfig.AssemblyNames.Contains(a.Name) || a.Name == "ZhonTai.Admin")
                .Select(o => Assembly.Load(new AssemblyName(o.Name))).ToArray();
            services.AddScoped<IMapper>(sp => new Mapper());
            TypeAdapterConfig.GlobalSettings.Scan(assemblies);

            #endregion Mapster 映射配置

            #region Cors 跨域
            services.AddCors(options =>
            {
                options.AddPolicy(AdminConsts.RequestPolicyName, policy =>
                {
                    var hasOrigins = appConfig.CorUrls?.Length > 0;
                    if (hasOrigins)
                    {
                        policy.WithOrigins(appConfig.CorUrls);
                    }
                    else
                    {
                        policy.AllowAnyOrigin();
                    }
                    policy
                    .AllowAnyHeader()
                    .AllowAnyMethod();

                    if (hasOrigins)
                    {
                        policy.AllowCredentials();
                    }
                });

                //允许任何源访问Api策略，使用时在控制器或者接口上增加特性[EnableCors(AdminConsts.AllowAnyPolicyName)]
                options.AddPolicy(AdminConsts.AllowAnyPolicyName, policy =>
                {
                    policy
                    .AllowAnyOrigin()
                    .AllowAnyHeader()
                    .AllowAnyMethod();
                });
            });

            #endregion Cors 跨域

            #region 身份认证授权

            var jwtConfig = ConfigHelper.Get<JwtConfig>("jwtconfig", env.EnvironmentName);
            services.TryAddSingleton(jwtConfig);

            if (appConfig.IdentityServer.Enable)
            {
                //is4
                services.AddAuthentication(options =>
                {
                    options.DefaultScheme = IdentityServerAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = nameof(ResponseAuthenticationHandler); //401
                    options.DefaultForbidScheme = nameof(ResponseAuthenticationHandler);    //403
                })
                .AddJwtBearer(options =>
                {
                    options.Authority = appConfig.IdentityServer.Url;
                    options.RequireHttpsMetadata = false;
                    options.Audience = "admin.server.api";
                })
                .AddScheme<AuthenticationSchemeOptions, ResponseAuthenticationHandler>(nameof(ResponseAuthenticationHandler), o => { });
            }
            else
            {
                //jwt
                services.AddAuthentication(options =>
                {
                    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = nameof(ResponseAuthenticationHandler); //401
                    options.DefaultForbidScheme = nameof(ResponseAuthenticationHandler);    //403
                })
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = jwtConfig.Issuer,
                        ValidAudience = jwtConfig.Audience,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtConfig.SecurityKey)),
                        ClockSkew = TimeSpan.Zero
                    };
                })
                .AddScheme<AuthenticationSchemeOptions, ResponseAuthenticationHandler>(nameof(ResponseAuthenticationHandler), o => { });
            }

            #endregion 身份认证授权

            #region Swagger Api文档

            if (env.IsDevelopment() || appConfig.Swagger.Enable)
            {
                services.AddSwaggerGen(options =>
                {
                    typeof(ApiVersion).GetEnumNames().ToList().ForEach(version =>
                    {
                        options.SwaggerDoc(version, new OpenApiInfo
                        {
                            Version = version,
                            Title = "ZhonTai.Admin.Host"
                        });
                        //c.OrderActionsBy(o => o.RelativePath);
                    });

                    options.SchemaFilter<EnumSchemaFilter>();

                    options.CustomOperationIds(apiDesc =>
                    {
                        var controllerAction = apiDesc.ActionDescriptor as ControllerActionDescriptor;
                        return controllerAction.ControllerName + "-" + controllerAction.ActionName;
                    });

                    options.ResolveConflictingActions(apiDescription => apiDescription.First());
                    options.CustomSchemaIds(x => x.FullName);
                    options.DocInclusionPredicate((docName, description) => true);

                    string[] xmlFiles = Directory.GetFiles(AppContext.BaseDirectory, "*.xml");
                    if (xmlFiles.Length > 0)
                    {
                        foreach (var xmlFile in xmlFiles)
                        {
                            options.IncludeXmlComments(xmlFile, true);
                        }
                    }

                    var server = new OpenApiServer()
                    {
                        Url = appConfig.Swagger.Url,
                        Description = ""
                    };
                    server.Extensions.Add("extensions", new OpenApiObject
                    {
                        ["copyright"] = new OpenApiString(appConfig.ApiUI.Footer.Content)
                    });
                    options.AddServer(server);

                    #region 添加设置Token的按钮

                    if (appConfig.IdentityServer.Enable)
                    {
                        //添加Jwt验证设置
                        options.AddSecurityRequirement(new OpenApiSecurityRequirement()
                        {
                            {
                                new OpenApiSecurityScheme
                                {
                                    Reference = new OpenApiReference
                                    {
                                        Id = "oauth2",
                                        Type = ReferenceType.SecurityScheme
                                    }
                                },
                                new List<string>()
                            }
                        });

                        //统一认证
                        options.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
                        {
                            Type = SecuritySchemeType.OAuth2,
                            Description = "oauth2登录授权",
                            Flows = new OpenApiOAuthFlows
                            {
                                Implicit = new OpenApiOAuthFlow
                                {
                                    AuthorizationUrl = new Uri($"{appConfig.IdentityServer.Url}/connect/authorize"),
                                    Scopes = new Dictionary<string, string>
                                    {
                                        { "admin.server.api", "admin后端api" }
                                    }
                                }
                            }
                        });
                    }
                    else
                    {
                        //添加Jwt验证设置
                        options.AddSecurityRequirement(new OpenApiSecurityRequirement()
                        {
                            {
                                new OpenApiSecurityScheme
                                {
                                    Reference = new OpenApiReference
                                    {
                                        Id = "Bearer",
                                        Type = ReferenceType.SecurityScheme
                                    }
                                },
                                new List<string>()
                            }
                        });

                        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                        {
                            Description = "Value: Bearer {token}",
                            Name = "Authorization",
                            In = ParameterLocation.Header,
                            Type = SecuritySchemeType.ApiKey
                        });
                    }

                    #endregion 添加设置Token的按钮
                });
            }

            #endregion Swagger Api文档

            #region 操作日志

            if (appConfig.Log.Operation)
            {
                services.AddScoped<ILogHandler, LogHandler>();
            }

            #endregion 操作日志

            #region 控制器
            void controllersAction(MvcOptions options)
            {
                options.Filters.Add<ControllerExceptionFilter>();
                options.Filters.Add<ValidateInputFilter>();
                options.Filters.Add<ValidatePermissionAttribute>();
                if (appConfig.Log.Operation)
                {
                    options.Filters.Add<ControllerLogFilter>();
                }
                //禁止去除ActionAsync后缀
                //options.SuppressAsyncSuffixInActionNames = false;
            }
            
            var mvcBuilder = appConfig.AppType switch
            {
                AppType.Controllers => services.AddControllers(controllersAction),
                AppType.ControllersWithViews => services.AddControllersWithViews(controllersAction),
                AppType.MVC => services.AddMvc(controllersAction),
                _ => services.AddControllers(controllersAction)
            };
            
            //.AddFluentValidation(config =>
            //{
            //    var assembly = Assembly.LoadFrom(Path.Combine(basePath, "ZhonTai.Admin.Host.dll"));
            //    config.RegisterValidatorsFromAssembly(assembly);
            //})
            mvcBuilder.AddNewtonsoftJson(options =>
            {
                //忽略循环引用
                options.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
                //使用驼峰 首字母小写
                options.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
                //设置时间格式
                options.SerializerSettings.DateFormatString = "yyyy-MM-dd HH:mm:ss";
            })
            .AddControllersAsServices();

            #endregion 控制器

            services.AddHttpClient();

            #region 缓存

            var cacheConfig = ConfigHelper.Get<CacheConfig>("cacheconfig", env.EnvironmentName);
            if (cacheConfig.Type == CacheType.Redis)
            {
                var csredis = new CSRedis.CSRedisClient(cacheConfig.Redis.ConnectionString);
                RedisHelper.Initialization(csredis);
                services.AddSingleton<ICacheTool, RedisCacheTool>();
            }
            else
            {
                services.AddMemoryCache();
                services.AddSingleton<ICacheTool, MemoryCacheTool>();
            }

            #endregion 缓存

            #region IP限流

            if (appConfig.RateLimit)
            {
                services.AddIpRateLimit(configuration, cacheConfig);
            }

            #endregion IP限流

            //阻止NLog接收状态消息
            services.Configure<ConsoleLifetimeOptions>(opts => opts.SuppressStatusMessages = true);

            //性能分析
            if (appConfig.MiniProfiler)
            {
                services.AddMiniProfiler();
            }

            //动态api
            services.AddDynamicApi(options =>
            {
                Assembly[] assemblies = DependencyContext.Default.RuntimeLibraries
                .Where(a => a.Name.EndsWith("Service"))
                .Select(o => Assembly.Load(new AssemblyName(o.Name))).ToArray();
                options.AddAssemblyOptions(assemblies);
            });
        }

        /// <summary>
        /// 配置中间件
        /// </summary>
        /// <param name="app"></param>
        /// <param name="env"></param>
        /// <param name="appConfig"></param>
        private static void Configure(IApplicationBuilder app, IWebHostEnvironment env, AppConfig appConfig)
        {
            //IP限流
            if (appConfig.RateLimit)
            {
                app.UseIpRateLimiting();
            }

            //性能分析
            if (appConfig.MiniProfiler)
            {
                app.UseMiniProfiler();
            }

            //异常
            app.UseExceptionHandler("/Error");

            //静态文件
            app.UseDefaultFiles();
            app.UseStaticFiles();
            app.UseUploadConfig();

            //路由
            app.UseRouting();

            //跨域
            app.UseCors(AdminConsts.RequestPolicyName);

            //认证
            app.UseAuthentication();

            //授权
            app.UseAuthorization();

            //配置端点
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            #region Swagger Api文档
            if (env.IsDevelopment() || appConfig.Swagger.Enable)
            {
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    typeof(ApiVersion).GetEnumNames().OrderByDescending(e => e).ToList().ForEach(version =>
                    {
                        c.SwaggerEndpoint($"/swagger/{version}/swagger.json", $"ZhonTai.Admin.Host {version}");
                    });
                    c.RoutePrefix = "";//直接根目录访问，如果是IIS发布可以注释该语句，并打开launchSettings.launchUrl
                    c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);//折叠Api
                    //c.DefaultModelsExpandDepth(-1);//不显示Models
                    if (appConfig.MiniProfiler)
                    {
                        c.InjectJavascript("/swagger/mini-profiler.js?v=4.2.22+2.0");
                        c.InjectStylesheet("/swagger/mini-profiler.css?v=4.2.22+2.0");
                    }
                });
            }
            #endregion Swagger Api文档

            #region 新版Api文档
            if (env.IsDevelopment() || appConfig.ApiUI.Enable)
            {
                app.UseApiUI(options =>
                {
                    options.RoutePrefix = "swagger";
                    typeof(ApiVersion).GetEnumNames().OrderByDescending(e => e).ToList().ForEach(version =>
                    {
                        options.SwaggerEndpoint($"/swagger/{version}/swagger.json", $"ZhonTai.Host {version}");
                    });
                });
            } 
            #endregion

            //数据库日志
            //var log = LogManager.GetLogger("db");
            //var ei = new LogEventInfo(LogLevel.Error, "", "错误信息");
            //ei.Properties["id"] = YitIdHelper.NextId();
            //log.Log(ei);


        }
    }
}