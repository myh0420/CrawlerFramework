CrawlerFramework/
├── CrawlerCore/           // 核心爬虫引擎
├── CrawlerScheduler/      // 任务调度器
├── CrawlerDownloader/     // 下载组件
├── CrawlerParser/        // 内容解析器
├── CrawlerStorage/       // 存储模块
├── CrawlerMonitor/       // 监控界面
└── CrawlerConsole/       // 控制台应用

# 网络爬虫框架使用说明和功能示例

## 1. 框架概述

本爬虫框架是一个功能完整、可扩展的企业级网络爬虫解决方案，集成了多种爬虫工具的优点，提供了高性能、可靠性和易用性。

### 主要特性
- 🚀 **高性能**：异步并发处理，连接复用，内存优化
- 🛡️ **可靠性**：完善的错误处理、重试机制、遵守robots.txt
- 🔧 **可配置**：灵活的配置系统、可插拔组件
- 📊 **监控完善**：实时监控、指标收集、健康检查
- 🔌 **可扩展**：模块化设计，易于扩展新功能
- 主要特性：
JSON配置文件：所有配置都存储在 appsettings.json 中

Web配置界面：通过浏览器修改配置，无需重启应用

配置验证：实时验证配置的有效性

配置热重载：支持配置文件变化自动重载

默认值管理：提供合理的默认配置

类型安全：强类型配置模型

使用方式：
启动应用：应用会自动加载 appsettings.json 配置文件

访问配置界面：打开 http://localhost:5000/config

修改配置：在Web界面中修改各项设置

保存配置：点击保存，配置会立即生效并保存到文件

验证配置：使用验证功能检查配置是否正确

配置文件位置：
控制台应用：CrawlerConsole/appsettings.json

Web监控应用：CrawlerMonitor/appsettings.json

优势：
无硬编码：所有配置都来自外部文件

可视化配置：用户友好的Web界面

即时生效：大部分配置修改无需重启应用

配置验证：避免错误的配置导致运行时错误

版本控制友好：配置文件可以纳入版本控制

这个配置系统让爬虫框架的使用变得更加简单和灵活，用户只需修改配置文件或通过Web界面就能调整所有爬虫行为。

## 2. 快速开始

### 2.1 基本爬虫使用

```csharp
// Program.cs
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using CrawlerCore;
using CrawlerCore.DependencyInjection;

class Program
{
    static async Task Main(string[] args)
    {
        // 配置依赖注入
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        
        // 添加爬虫服务
        services.AddCrawlerCore()
                .AddFileSystemStorage("crawler_data")
                .AddCrawlerDownloader()
                .AddCrawlerParser()
                .AddCrawlerScheduler();

        var serviceProvider = services.BuildServiceProvider();
        var crawler = serviceProvider.GetRequiredService<CrawlerEngine>();

        // 配置爬虫
        var config = new CrawlConfiguration
        {
            MaxConcurrentTasks = 5,
            MaxDepth = 2,
            MaxPages = 100,
            RequestDelay = TimeSpan.FromMilliseconds(1000),
            AllowedDomains = new[] { "example.com" }
        };

        // 注册事件
        crawler.OnCrawlCompleted += (s, e) => 
        {
            Console.WriteLine($"Completed: {e.Result.Request.Url}");
        };

        // 启动爬虫
        await crawler.StartAsync(config);
        
        // 添加种子URL
        await crawler.AddSeedUrlsAsync(new[] 
        {
            "https://example.com",
            "https://example.com/news"
        });

        Console.WriteLine("Press any key to stop...");
        Console.ReadKey();

        await crawler.StopAsync();
    }
}
```

### 2.2 项目文件配置

```xml
<!-- CrawlerConsole.csproj -->
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net6.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="6.0.0" />
    <PackageReference Include="Microsoft.Extensions.Logging.Console" Version="6.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\CrawlerCore\CrawlerCore.csproj" />
    <ProjectReference Include="..\CrawlerDownloader\CrawlerDownloader.csproj" />
    <ProjectReference Include="..\CrawlerParser\CrawlerParser.csproj" />
    <ProjectReference Include="..\CrawlerScheduler\CrawlerScheduler.csproj" />
    <ProjectReference Include="..\CrawlerStorage\CrawlerStorage.csproj" />
  </ItemGroup>

</Project>
```

## 3. 核心配置详解

### 3.1 基本配置

```csharp
var config = new CrawlConfiguration
{
    // 并发控制
    MaxConcurrentTasks = 10,          // 最大并发任务数
    MaxDepth = 3,                     // 最大爬取深度
    MaxPages = 1000,                  // 最大页面数
    
    // 请求控制
    RequestDelay = TimeSpan.FromMilliseconds(500), // 请求间隔
    TimeoutSeconds = 30,              // 超时时间
    
    // 域名限制
    AllowedDomains = new[] { "example.com", "test.com" },
    BlockedPatterns = new[] { "/admin", "/login" },
    
    // 行为控制
    RespectRobotsTxt = true,          // 遵守robots.txt
    FollowRedirects = true            // 跟随重定向
};
```

### 3.2 高级配置

```csharp
var advancedConfig = new AdvancedCrawlConfiguration
{
    // 基础配置
    MaxConcurrentTasks = 10,
    MaxDepth = 3,
    MaxPages = 5000,
    
    // 性能优化
    MemoryLimitMB = 500,
    MaxQueueSize = 10000,
    EnableCompression = true,
    
    // 反爬虫功能
    EnableAntiBotDetection = true,
    RespectRobotsTxt = true,
    
    // 重试策略
    RetryPolicy = new RetryPolicy
    {
        MaxRetries = 3,
        InitialDelay = TimeSpan.FromSeconds(1),
        BackoffMultiplier = 2.0,
        MaxDelay = TimeSpan.FromMinutes(5)
    },
    
    // 代理设置
    ProxySettings = new ProxySettings
    {
        Enabled = true,
        ProxyUrls = new[] 
        { 
            "http://proxy1.example.com:8080",
            "http://proxy2.example.com:8080" 
        },
        RotationStrategy = ProxyRotationStrategy.RoundRobin
    },
    
    // 监控设置
    MonitoringSettings = new MonitoringSettings
    {
        EnableMetrics = true,
        EnableTracing = false,
        MetricsIntervalSeconds = 30
    },
    
    // 数据清洗
    DataCleaningSettings = new DataCleaningSettings
    {
        RemoveDuplicateContent = true,
        RemoveScriptsAndStyles = true,
        NormalizeText = true,
        MinContentLength = 100
    }
};
```

## 4. 高级功能示例

### 4.1 使用高级配置和反爬虫功能

```csharp
// 高级爬虫示例
var services = new ServiceCollection();
services.AddLogging(builder => builder.AddConsole());

// 添加高级爬虫服务
services.AddAdvancedCrawler(config =>
{
    config.EnableAntiBotDetection = true;
    config.RespectRobotsTxt = true;
    config.RetryPolicy.MaxRetries = 5;
})
.AddCrawlerHttpClient()
.AddFileSystemStorage("advanced_crawler_data")
.AddCrawlerDownloader(useProxies: false)
.AddCrawlerParser()
.AddCrawlerScheduler();

var serviceProvider = services.BuildServiceProvider();
var crawler = serviceProvider.GetRequiredService<CrawlerEngine>();

// 使用高级配置启动
await crawler.StartAsync(advancedConfig);
```

### 4.2 代理和User-Agent轮换

```csharp
// 配置代理和User-Agent轮换
var proxyConfig = new AdvancedCrawlConfiguration
{
    MaxConcurrentTasks = 5,
    ProxySettings = new ProxySettings
    {
        Enabled = true,
        ProxyUrls = new[] 
        {
            "192.168.1.100:8080",
            "192.168.1.101:8080:username:password" // 带认证的代理
        }
    }
};

// 自定义User-Agent
var downloader = new AdvancedDownloader(
    logger: logger,
    config: proxyConfig,
    proxyManager: new ProxyManager()
);

// 添加自定义User-Agent
var userAgentService = new RotatingUserAgentService();
userAgentService.AddUserAgent("MyCustomBot/1.0 (+http://mybot.com)");
```

### 4.3 数据导出功能

```csharp
// 数据导出示例
var storage = new FileSystemStorage("crawler_data", logger);
await storage.InitializeAsync();

// 获取爬取的数据
var results = await storage.GetByDomainAsync("example.com", 100);

// 导出为不同格式
var exportService = new DataExportService();

// JSON格式
await exportService.ExportAsync(results, "data.json");

// CSV格式  
await exportService.ExportAsync(results, "data.csv");

// 筛选和转换数据
var exportData = results.Select(r => new
{
    Url = r.Request.Url,
    Title = r.ParseResult?.Title,
    StatusCode = r.DownloadResult.StatusCode,
    ContentLength = r.DownloadResult.RawData?.Length,
    ProcessedAt = r.ProcessedAt
});

await exportService.ExportAsync(exportData, "summary.csv");
```

### 4.4 监控和健康检查

```csharp
// 监控示例
crawler.OnStatusChanged += (s, e) =>
{
    Console.WriteLine($"[{DateTime.Now}] Status: {e.PreviousStatus} -> {e.CurrentStatus}");
    Console.WriteLine($"Message: {e.Message}");
};

crawler.OnUrlDiscovered += (s, e) =>
{
    Console.WriteLine($"Discovered: {e.DiscoveredUrl} (Depth: {e.Depth})");
};

crawler.OnCrawlCompleted += (s, e) =>
{
    var result = e.Result;
    Console.WriteLine($"Completed: {result.Request.Url} " +
                     $"(Status: {result.DownloadResult.StatusCode}, " +
                     $"Time: {result.DownloadResult.DownloadTimeMs}ms)");
};

crawler.OnCrawlError += (s, e) =>
{
    Console.WriteLine($"Error: {e.Request.Url}");
    Console.WriteLine($"Exception: {e.Exception.Message}");
};

// 获取统计信息
var statistics = await crawler.GetStatisticsAsync();
Console.WriteLine($"Total URLs: {statistics.TotalUrlsProcessed}");
Console.WriteLine($"Success Rate: {(double)statistics.SuccessCount / statistics.TotalUrlsProcessed:P2}");
```

## 5. 存储和数据处理

### 5.1 文件系统存储

```csharp
// 文件系统存储配置
var fileStorage = new FileSystemStorage("crawler_data", logger);
await fileStorage.InitializeAsync();

// 基本操作
await fileStorage.SaveAsync(crawlResult);
var results = await fileStorage.GetByDomainAsync("example.com");
var specificResult = await fileStorage.GetByUrlAsync("https://example.com/page1");

// 统计和备份
var stats = await fileStorage.GetStatisticsAsync();
await fileStorage.BackupAsync("backup.zip");

// 清理数据
await fileStorage.ClearAllAsync();
```

### 5.2 SQLite存储

```csharp
// SQLite存储配置
var sqliteStorage = new SQLiteStorage("crawler.db", logger);
await sqliteStorage.InitializeAsync();

// 高级查询
var recentResults = await sqliteStorage.GetByDomainAsync("example.com", 50);
var urlState = await sqliteStorage.GetUrlStateAsync("https://example.com");

// 数据库维护
await sqliteStorage.BackupAsync("crawler_backup.db");
var dbStats = await sqliteStorage.GetStatisticsAsync();
```

## 6. 自定义扩展

### 6.1 自定义内容提取器

```csharp
// 自定义提取器示例
public class ProductExtractor : IContentExtractor
{
    public string Name => "ProductExtractor";

    public Task<ExtractionResult> ExtractAsync(HtmlDocument htmlDocument, DownloadResult downloadResult)
    {
        var result = new ExtractionResult();
        
        // 提取产品信息
        var productNodes = htmlDocument.DocumentNode.SelectNodes("//div[@class='product']");
        if (productNodes != null)
        {
            var products = new List<object>();
            foreach (var node in productNodes)
            {
                var product = new
                {
                    Name = node.SelectSingleNode(".//h3")?.InnerText.Trim(),
                    Price = node.SelectSingleNode(".//span[@class='price']")?.InnerText.Trim(),
                    Description = node.SelectSingleNode(".//p[@class='description']")?.InnerText.Trim()
                };
                products.Add(product);
            }
            result.Data["Products"] = products;
        }

        // 提取分页链接
        var paginationNodes = htmlDocument.DocumentNode.SelectNodes("//a[@class='page-link']");
        if (paginationNodes != null)
        {
            foreach (var node in paginationNodes)
            {
                var href = node.GetAttributeValue("href", "");
                if (!string.IsNullOrEmpty(href))
                {
                    var absoluteUrl = new Uri(new Uri(downloadResult.Url), href).ToString();
                    result.Links.Add(absoluteUrl);
                }
            }
        }

        return Task.FromResult(result);
    }
}

// 注册自定义提取器
services.AddSingleton<IContentExtractor, ProductExtractor>();
```

### 6.2 自定义下载器

```csharp
// 自定义下载器示例
public class CustomDownloader : IDownloader
{
    private readonly ILogger<CustomDownloader> _logger;
    private readonly HttpClient _httpClient;

    public int ConcurrentRequests { get; set; } = 5;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    public CustomDownloader(ILogger<CustomDownloader> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
    }

    public async Task<DownloadResult> DownloadAsync(CrawlRequest request)
    {
        try
        {
            // 自定义请求逻辑
            var httpRequest = new HttpRequestMessage(HttpMethod.Get, request.Url);
            httpRequest.Headers.Add("Custom-Header", "CustomValue");
            
            var response = await _httpClient.SendAsync(httpRequest);
            var content = await response.Content.ReadAsStringAsync();

            return new DownloadResult
            {
                Url = request.Url,
                Content = content,
                StatusCode = (int)response.StatusCode,
                IsSuccess = response.IsSuccessStatusCode
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Download failed for {Url}", request.Url);
            return new DownloadResult
            {
                Url = request.Url,
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public Task ShutdownAsync() => Task.CompletedTask;
}
```

## 7. Web监控界面

### 7.1 启动监控服务

```csharp
// Program.cs for Web Monitor
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

// 配置爬虫服务
builder.Services.AddAdvancedCrawler()
                .AddCrawlerHttpClient()
                .AddFileSystemStorage("crawler_data")
                .AddCrawlerDownloader()
                .AddCrawlerParser()
                .AddCrawlerScheduler();

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<CrawlerHub>("/crawlerHub");

app.Run();
```

### 7.2 监控界面功能

访问 `http://localhost:5000` 可以看到：

- **实时状态**：爬虫运行状态、内存使用、运行时间
- **性能图表**：内存使用、URL处理数量、处理时间
- **活动日志**：实时显示爬虫活动
- **URL列表**：已爬取的URL详情
- **控制面板**：启动、停止、暂停、恢复爬虫

## 8. 部署和运维

### 8.1 Docker部署

```dockerfile
# Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish "CrawlerMonitor/CrawlerMonitor.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CrawlerMonitor.dll"]
```

```yaml
# docker-compose.yml
version: '3.8'
services:
  crawler:
    build: .
    ports:
      - "8080:80"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
    volumes:
      - crawler_data:/app/data
    restart: unless-stopped

volumes:
  crawler_data:
```

### 8.2 生产环境配置

```json
// appsettings.Production.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "System": "Warning"
    }
  },
  "CrawlerConfig": {
    "MaxConcurrentTasks": 10,
    "MaxDepth": 3,
    "RequestDelay": "00:00:01",
    "StorageType": "SQLite",
    "ConnectionString": "Data Source=/data/crawler.db"
  }
}
```

## 9. 故障排除

### 9.1 常见问题解决

1. **内存泄漏**
   ```csharp
   // 定期清理
   services.AddSingleton<ObjectPool<HttpClient>>();
   // 监控内存使用
   var memory = GC.GetTotalMemory(false) / 1024 / 1024;
   ```

2. **连接超时**
   ```csharp
   var config = new AdvancedCrawlConfiguration
   {
       TimeoutSeconds = 60,
       RetryPolicy = new RetryPolicy { MaxRetries = 3 }
   };
   ```

3. **反爬虫封锁**
   ```csharp
   config.EnableAntiBotDetection = true;
   config.ProxySettings.Enabled = true;
   config.RequestDelay = TimeSpan.FromSeconds(2);
   ```

### 9.2 日志配置

```csharp
// 详细日志配置
services.AddLogging(builder =>
{
    builder.AddConsole()
           .AddDebug()
           .AddFile("logs/crawler-{Date}.txt", LogLevel.Information)
           .SetMinimumLevel(LogLevel.Debug);
});
```

## 10. 性能优化建议

1. **连接池优化**
   ```csharp
   services.AddHttpClient("CrawlerClient")
           .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
           {
               MaxConnectionsPerServer = 100,
               UseProxy = false
           });
   ```

2. **内存管理**
   ```csharp
   // 使用对象池
   var httpClientPool = new HttpClientPool(maxClients: 20);
   // 定期清理缓存
   await storage.ClearAllAsync();
   ```

3. **数据库优化**
   ```sql
   -- 为SQLite创建索引
   CREATE INDEX IX_CrawlResults_Url ON CrawlResults(Url);
   CREATE INDEX IX_CrawlResults_ProcessedAt ON CrawlResults(ProcessedAt);
   ```

这个框架提供了完整的企业级爬虫解决方案，可以根据具体需求进行配置和扩展。建议从基本配置开始，逐步启用高级功能。