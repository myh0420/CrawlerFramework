// CrawlerMonitor/Program.cs
using CrawlerCore.Configuration;
using CrawlerMonitor.Hubs;
using CrawlerServiceDependencyInjection.DependencyInjection;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using System;

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddControllersWithViews();
        builder.Services.AddSignalR();

        // 配置日志
        builder.Services.AddLogging(logging =>
        {
            logging.AddConsole();
            logging.AddDebug();
            logging.SetMinimumLevel(LogLevel.Information);
        });

        // 配置爬虫服务
        ConfigureCrawlerServices(builder.Services);

        // 添加Razor页面服务（如果需要使用Razor页面）
        builder.Services.AddRazorPages();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Home/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();

        // 配置Views目录的静态文件访问
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(
                Path.Combine(builder.Environment.ContentRootPath, "Views")),
            RequestPath = "/Views",
            ContentTypeProvider = new FileExtensionContentTypeProvider
            {
                Mappings =
        {
            [".cshtml"] = "text/plain",
            [".cs"] = "text/plain"
        }
            },
            ServeUnknownFileTypes = true,
            DefaultContentType = "application/octet-stream"
        });

        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthorization();

        // 可选：为特定控制器添加自定义路由
        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");

        app.MapControllerRoute(
            name: "config",
            pattern: "{controller=Config}/{action=Index}");

        
        //app.MapControllerRoute(
        //    name: "testconfig",
        //    pattern: "{controller=TestConfig}/{action=Index}");


        // 调试：列出所有路由
        app.Use(async (context, next) =>
        {            
            if (context.Request.Path == "/debug-routes")
            {
                var endpoints = app.Services.GetService<IEnumerable<EndpointDataSource>>();
                foreach (var endpoint in endpoints?.SelectMany(e => e.Endpoints) ?? [])
                {
                    if (endpoint is RouteEndpoint routeEndpoint)
                    {
                        await context.Response.WriteAsync(
                            $"{routeEndpoint.DisplayName} : {string.Join(", ", routeEndpoint.RoutePattern.Parameters)}\n");
                    }
                }
                return;
            }
            await next();
        });
        // API routes

        app.MapHub<CrawlerHub>("/crawlerHub");
        app.MapControllers();

        app.Run();

        static void ConfigureCrawlerServices(IServiceCollection services)
        {
            // 添加配置服务
            services.AddCrawlerConfiguration();

            services.AddStorageProvider();
            // 添加爬虫核心服务
            services.AddAdvancedCrawler();

            services.AddCrawlerCore();
            
            // 或者使用SQLite: services.AddSQLiteStorage("crawler.db");

            // 添加下载器
            services.AddCrawlerDownloader(useProxies: false);

            // 添加解析器
            services.AddCrawlerParser();

            // 添加调度器
            services.AddCrawlerScheduler();

            // 添加HttpClient
            services.AddHttpClient();
        }
    }
}