using System.Reflection;
using MyGateway.Host.Core.Configs;
using NLog.Web;

var builder = WebApplication.CreateBuilder(args);

//�����־��Ӧ���򣬱���.net�Դ���־���������̨
builder.Logging.ClearProviders();
//ʹ��NLog��־
builder.Host.UseNLog();

var healthChecks = builder.Configuration.GetSection("GatewayConfig").Get<GatewayConfig>()?.HealthChecks;
//��ӽ������
if (healthChecks != null && healthChecks.Enable)
{
    builder.Services.AddHealthChecks();
}

//��ӿ���
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAnyPolicy", policy =>
    {
        policy
        .AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod();
    });
});

//��Ӵ���
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Add services to the container.

var app = builder.Build();

// Configure the HTTP request pipeline.

//ʹ�ÿ���
app.UseCors("AllowAnyPolicy");

//ʹ�ý������
if (healthChecks != null && healthChecks.Enable)
{
    app.MapHealthChecks(healthChecks.Path);
}

//ʹ�ô���
app.MapReverseProxy();

//��ҳ
app.MapGet("/", async (HttpResponse response) =>
{
    var gatewayConfig = builder.Configuration.GetSection("GatewayConfig").Get<GatewayConfig>();
    var moduleList = gatewayConfig?.ModuleList;

    var html = $"<html><body>";
    if (moduleList?.Count > 0)
    {
        moduleList.ForEach(m =>
        {
            html += $"""<a href='{m.Url}' target="_blank">{m.Name}</a></br>""";
        });
    }
    else
    {
        html += $"The {Assembly.GetEntryAssembly()?.GetName().Name} has started.";
    }
    html += "</body></html>";

    response.ContentType = "text/html;charset=UTF-8";
    await response.WriteAsync(html);
});

app.Run();