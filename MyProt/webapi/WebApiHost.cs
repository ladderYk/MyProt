using Carter;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyProt.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyProt.webapi
{
    public static class WebApiHost
    {
        private static WebApplication? _app;

        public static async void Start()
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseKestrel(o => o.ListenAnyIP(5000));

            // 注册与 WPF 共享的服务实例
            builder.Services.AddSingleton<IProtocolService>(App.ProtocolService);
            //builder.Services.AddSingleton<IDeviceService>(App.DeviceService);
            //builder.Services.AddSingleton<ITagService>(App.TagService);

            builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

            var app = builder.Build();
            app.UseCors();

            // ===== 添加静态文件支持 =====
            // 1. 启用默认文档（自动查找 index.html）
            app.UseDefaultFiles();
            // 2. 提供 wwwroot 中的静态文件
            app.UseStaticFiles();

            // 3. 映射 API 端点
            app.MapProtocolEndpoints();
            //app.MapDevices();
            //app.MapTags();
            app.MapGet("/api/health", () => Results.Ok("Web API is running"));
            app.MapFallbackToFile("index.html");

            _app = app;
            await app.StartAsync();
        }

        public static async Task StopAsync()
        {
            if (_app != null)
                await _app.StopAsync();
        }
    }
}
