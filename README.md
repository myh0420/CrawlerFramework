CrawlerFramework/
├── CrawlerCore/                      // 核心爬虫引擎
├── CrawlerScheduler/                 // 任务调度器
├── CrawlerDownloader/                // 下载组件
├── CrawlerParser/                    // 内容解析器
├── CrawlerStorage/                   // 存储模块
├── CrawlerMonitor/                   // 监控界面
├── CrawlerConsole/                   // 控制台应用
├── CrawlerInterFaces/                // 接口定义
├── CrawlerEntity/                    // 实体模型
└── CrawlerServiceDependencyInjection/ // 依赖注入服务

# 网络爬虫框架使用说明和功能示例

## 1. 框架概述


本爬虫框架是一个功能完整、可扩展的企业级网络爬虫解决方案，集成了多种爬虫工具的优点，提供了高性能、可靠性和易用性。

### 主要特性
- 🚀 **高性能**：异步并发处理，连接复用，内存优化，编译时正则表达式
- 🛡️ **可靠性**：完善的错误处理、重试机制、遵守robots.txt
- 🔧 **可配置**：灵活的配置系统、可插拔组件
- 📊 **监控**：（部分实现）基础架构搭建，Web界面框架（开发中）
- 🔌 **可扩展**：模块化设计，易于扩展新功能
- 📝 **内容解析**：支持HTML、纯文本、JSON等多种内容类型，安全的HTML文档处理
- ⏱️ **域名请求节流**：基于域名的动态延迟调整，支持请求类型感知节流
- 📡 **分布式任务调度**：支持多节点部署，防止重复处理
- 🎯 **优先级队列优化**：基于深度、内容类型、域重要性和等待时间的智能优先级计算
- 🔄 **URL归一化**：（计划实现）URL标准化处理，避免重复处理相同内容
- 📈 **动态延迟调整**：根据服务器响应自动调整延迟，防止服务器压力过大

## 2. 快速开始

### 2.1 基本爬虫使用

```csharp
// Program.cs
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using CrawlerCore;
using CrawlerServiceDependencyInjection.DependencyInjection;

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
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="9.0.0" />
    <PackageReference Include="Microsoft.Extensions.Logging.Console" Version="9.0.0" />
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

本框架提供了强大的配置系统，支持多种配置方式和动态更新：

### 配置系统特性
- **JSON配置文件**：所有配置都存储在 appsettings.json 中
- **Web配置界面**：（部分实现）基础界面框架，配置热重载功能
- **配置验证**：实时验证配置的有效性
- **配置热重载**：支持配置文件变化自动重载
- **默认值管理**：提供合理的默认配置
- **类型安全**：强类型配置模型

### 配置使用方式
- **启动应用**：应用会自动加载 appsettings.json 配置文件
- **访问配置界面**：基础界面框架已搭建，完整功能正在开发中
- **修改配置**：目前需手动编辑配置文件
- **保存配置**：配置文件变更会自动重载
- **验证配置**：实时验证配置的有效性

### 配置文件位置
- **控制台应用**：CrawlerConsole/appsettings.json
- **Web监控应用**：CrawlerMonitor/appsettings.json

### 配置系统优势
- **无硬编码**：所有配置都来自外部文件
- **可视化配置**：用户友好的Web界面
- **即时生效**：大部分配置修改无需重启应用
- **配置验证**：避免错误的配置导致运行时错误
- **版本控制友好**：配置文件可以纳入版本控制

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

### 4.2 域名请求节流和动态延迟调整

```csharp
// 配置域名请求节流
var services = new ServiceCollection();
services.AddLogging(builder => builder.AddConsole());

// 添加带有域名节流功能的爬虫服务
services.AddCrawlerCore()
    .AddFileSystemStorage("throttled_crawler_data")
    .AddCrawlerDownloader()
    .AddCrawlerParser()
    .AddCrawlerScheduler()
    .AddDomainDelayManager(options =>
    {
        // 设置默认延迟
        options.DefaultDelay = TimeSpan.FromSeconds(1);
        // 设置最小和最大延迟限制
        options.MinDelay = TimeSpan.FromMilliseconds(100);
        options.MaxDelay = TimeSpan.FromSeconds(10);
        // 设置请求类型特定延迟
        options.RequestTypeDelays = new Dictionary<string, TimeSpan>
        {
            { "html", TimeSpan.FromSeconds(1) },
            { "pdf", TimeSpan.FromSeconds(2) },
            { "image", TimeSpan.FromMilliseconds(500) },
            { "api", TimeSpan.FromSeconds(0.5) }
        };
    });

var serviceProvider = serviceProvider = services.BuildServiceProvider();
var crawler = serviceProvider.GetRequiredService<CrawlerEngine>();
var delayManager = serviceProvider.GetRequiredService<IDomainDelayManager>();

// 手动调整特定域名的延迟
delayManager.SetDelay("example.com", TimeSpan.FromSeconds(3));
delayManager.SetDelay("example.com", "pdf", TimeSpan.FromSeconds(5));

// 启动爬虫
await crawler.StartAsync(config);
```

### 4.3 URL归一化和优先级队列优化

```csharp
// 配置URL归一化和优先级队列
var services = new ServiceCollection();
services.AddLogging(builder => builder.AddConsole());

// 添加带优化功能的爬虫服务
services.AddAdvancedCrawler(config =>
{
    config.EnableUrlNormalization = true;
    config.RespectRobotsTxt = true;
})
.AddCrawlerHttpClient()
.AddFileSystemStorage("optimized_crawler_data")
.AddCrawlerDownloader()
.AddCrawlerParser()
.AddCrawlerScheduler(options =>
{
    // 设置优先级队列选项
    options.HighPriorityDomains = new[] { "example.com", "news.example.com" };
    options.ContentTypePriorities = new Dictionary<string, int>
    {
        { "html", 10 },
        { "pdf", 5 },
        { "image", 1 },
        { "api", 7 }
    };
    // 设置等待时间影响优先级的阈值
    options.PriorityIncreaseThreshold = TimeSpan.FromMinutes(5);
});

var serviceProvider = services.BuildServiceProvider();
var crawler = serviceProvider.GetRequiredService<CrawlerEngine>();

// 启动爬虫
await crawler.StartAsync(config);
```

### 4.4 分布式任务调度

```csharp
// 配置分布式爬虫
var services = new ServiceCollection();
services.AddLogging(builder => builder.AddConsole());

// 添加分布式爬虫服务
services.AddAdvancedCrawler(config =>
{
    config.EnableDistributedScheduling = true;
    config.MachineName = "crawler-node-1"; // 唯一机器名，用于生成唯一任务ID
})
.AddCrawlerHttpClient()
.AddFileSystemStorage("distributed_crawler_data")
.AddCrawlerDownloader()
.AddCrawlerParser()
.AddCrawlerScheduler();

var serviceProvider = services.BuildServiceProvider();
var crawler = serviceProvider.GetRequiredService<CrawlerEngine>();

// 启动爬虫
await crawler.StartAsync(config);
```

### 4.5 代理和User-Agent轮换

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

### 4.6 数据导出功能

**注**：数据导出功能目前处于开发阶段，基础接口已定义，完整功能正在实现中。

```csharp
// 数据导出示例（计划实现）
var storage = new FileSystemStorage("crawler_data", logger);
await storage.InitializeAsync();

// 获取爬取的数据
var results = await storage.GetByDomainAsync("example.com", 100);

// 导出功能正在开发中
```

### 4.7 监控和健康检查

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

## 5. API参考文档

### 5.1 CrawlerEngine - 核心爬虫引擎

CrawlerEngine是整个爬虫框架的核心组件，负责协调和管理爬取任务的执行流程。

#### 构造函数
```csharp
public CrawlerEngine(
    IScheduler scheduler,
    IDownloader downloader,
    IParser parser,
    IStorageProvider storage,
    ILogger<CrawlerEngine> logger,
    IMetadataStore? metadataStore = null,
    AntiBotDetectionService? antiBotService = null,
    AdaptiveRetryStrategy? retryStrategy = null,
    RobotsTxtParser? robotsTxtParser = null,
    CrawlerMetrics? metrics = null,
    IPluginLoader? pluginLoader = null,
    bool enableAutoStop = true,
    TimeSpan? autoStopTimeout = null)
```

**参数说明：**
- `scheduler`: 任务调度器，负责管理爬取请求队列和优先级
- `downloader`: 下载器，负责从网络获取网页内容
- `parser`: 解析器，负责解析下载的内容并提取信息
- `storage`: 存储提供器，负责存储爬取结果和元数据
- `logger`: 日志记录器，用于记录运行时信息和错误
- `metadataStore`: 元数据存储，用于存储爬取任务的元数据（可选）
- `antiBotService`: 反机器人服务，用于检测和应对反爬机制（可选）
- `retryStrategy`: 重试策略，用于处理下载失败的情况（可选）
- `robotsTxtParser`: Robots.txt解析器，用于遵守网站的爬取规则（可选）
- `metrics`: 指标服务，用于收集和报告爬取性能数据（可选）
- `pluginLoader`: 插件加载器，用于加载和管理爬虫插件（可选）
- `enableAutoStop`: 是否启用自动停止功能，当任务队列为空时自动停止（默认：true）
- `autoStopTimeout`: 自动停止超时时间，当任务队列为空超过此时间时自动停止（默认：30秒）

#### 核心方法

**启动爬虫**
```csharp
// 使用并发任务数启动
public Task StartAsync(int workerCount = 5, string? jobId = null)

// 使用高级配置启动
public Task StartAsync(AdvancedCrawlConfiguration config, string? jobId = null)
```

**停止爬虫**
```csharp
public Task StopAsync(bool saveState = true)
```

**暂停和恢复爬虫**
```csharp
public Task PauseAsync()
public Task ResumeAsync()
```

**添加种子URL**
```csharp
public Task AddSeedUrlsAsync(IEnumerable<string> urls)
```

**获取统计信息**
```csharp
public Task<Dictionary<string, object>> GetStatisticsAsync()
public Task<CrawlState> GetCurrentCrawlStateAsync()
```

#### 事件
- `OnCrawlCompleted`: 爬取完成事件
- `OnCrawlError`: 爬取错误事件
- `OnUrlDiscovered`: URL发现事件
- `OnStatusChanged`: 爬虫状态改变事件

### 5.2 PriorityScheduler - 优先级调度器

PriorityScheduler负责管理爬取请求的队列和优先级，并支持域名请求节流功能。

#### 构造函数
```csharp
public PriorityScheduler(
    IUrlFilter urlFilter,
    IDomainDelayManager delayManager,
    ILogger<PriorityScheduler> logger)
```

**参数说明：**
- `urlFilter`: URL过滤器，用于判断是否允许处理特定URL
- `delayManager`: 域名延迟管理器，用于控制对同一域名的请求频率
- `logger`: 日志记录器，用于记录运行时信息和错误

#### 核心方法

**添加URL到队列**
```csharp
public Task<bool> AddUrlAsync(CrawlRequest request)
public Task<int> AddUrlsAsync(IEnumerable<CrawlRequest> requests)
```

**获取下一个待处理URL**
```csharp
public async Task<CrawlRequest?> GetNextAsync()
```

**记录域名性能数据**
```csharp
public void RecordDomainPerformance(string domain, long downloadTimeMs, bool isSuccess)
```

#### 优先级计算逻辑

PriorityScheduler使用多维度因素计算URL优先级：
1. **基础优先级**：由请求本身提供的优先级值
2. **深度调整**：深度越大，优先级越低（每增加1层深度降低10优先级）
3. **URL模式**：特定内容类型（如文章、PDF）获得更高优先级
4. **域名重要性**：高优先级域名获得额外15优先级
5. **域名性能**：
   - 下载速度越快，优先级越高
   - 错误率越高，优先级越低
   - 连续错误的域名进一步降低优先级
6. **队列等待时间**：避免饥饿，长时间等待的请求优先级会逐渐提高

## 6. 存储和数据处理

### 6.1 文件系统存储

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

### 6.2 SQLite存储

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

## 7. 自定义扩展

### 7.1 自定义内容提取器

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

### 7.2 高级解析器特性

AdvancedParser支持多种内容类型的解析处理：

#### 支持的内容类型
- **HTML**: 完整的HTML文档解析和内容提取
- **纯文本**: 直接提取文本内容，无需HTML解析
- **JSON**: 专门处理JSON数据格式
- **其他类型**: 作为原始数据保存

#### 性能优化
- **编译时正则表达式**: 使用`GeneratedRegexAttribute`在编译时生成正则表达式，提高性能
- **安全的文档处理**: 创建HTML文档副本进行操作，避免修改原始文档状态
- **智能文本清理**: 自动移除多余空白字符，提供整洁的文本内容

#### 使用示例
```csharp
// 解析器会自动根据Content-Type处理不同内容
var parseResult = await parser.ParseAsync(downloadResult);

// 对于HTML内容
string htmlTitle = parseResult.Title; // 页面标题
string htmlContent = parseResult.TextContent; // 清理后的文本内容

// 对于纯文本内容
string plainText = parseResult.TextContent; // 直接提取的文本

// 对于JSON内容
string jsonData = parseResult.ExtractedData["json"] as string; // JSON数据
```

**注**：AI辅助解析功能目前处于计划阶段，尚未实现。

### 7.3 自定义下载器

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

## 8. Web监控界面

**注**：Web监控界面已实现基本功能，包括实时状态监控、爬虫控制和数据可视化。

### 8.1 启动监控服务（已实现）

```csharp
// Program.cs for Web Monitor (已实现)
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

### 7.2 监控界面功能（已实现）

访问 `http://localhost:5000` 将可以看到：

- **实时状态**：爬虫运行状态、内存使用、运行时间
- **性能图表**：内存使用、URL处理数量、处理时间
- **活动日志**：实时显示爬虫活动
- **URL列表**：已爬取的URL详情
- **控制面板**：启动、停止、暂停、恢复爬虫

## 8. 部署和运维

### 8.1 Docker部署

Docker部署功能将在未来版本中提供。

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