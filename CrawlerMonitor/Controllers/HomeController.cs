// <copyright file="HomeController.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerMonitor.Controllers
{
    using System.Diagnostics;
    using CrawlerCore.Configuration;
    using CrawlerMonitor.Models;
    using Microsoft.AspNetCore.Mvc;

    /// <summary>
    /// 首页控制器.
    /// </summary>
    public class HomeController : Controller
    {
        /// <summary>
        /// 日志记录器.
        /// </summary>
        private readonly ILogger<HomeController> logger;

        /// <summary>
        /// 配置服务.
        /// </summary>
        private readonly IConfigService configService;

        /// <summary>
        /// 配置验证器.
        /// </summary>
        private readonly IConfigValidator configValidator;

        /// <summary>
        /// 初始化 <see cref="HomeController"/> 类的新实例.
        /// </summary>
        /// <param name="logger">日志记录器.</param>
        /// <param name="configService">配置服务.</param>
        /// <param name="configValidator">配置验证器.</param>
        public HomeController(
            ILogger<HomeController> logger,
            IConfigService configService,
            IConfigValidator configValidator)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger), "日志记录器参数不能为空");
            this.configService = configService ?? throw new ArgumentNullException(nameof(configService), "配置服务参数不能为空");
            this.configValidator = configValidator ?? throw new ArgumentNullException(nameof(configValidator), "配置验证器参数不能为空");
        }

        /// <summary>
        /// 首页.
        /// </summary>
        /// <returns>首页视图.</returns>
        public IActionResult Index()
        {
            return this.View();
        }

        /// <summary>
        /// 隐私政策.
        /// </summary>
        /// <returns>隐私政策视图.</returns>
        public IActionResult Privacy()
        {
            return this.View();
        }

        /// <summary>
        /// 配置页面.
        /// </summary>
        /// <returns>配置页面视图.</returns>
        public IActionResult Config()
        {
            var config = this.configService.GetCurrentConfig();
            return this.View(config);
        }

        /// <summary>
        /// 错误页面.
        /// </summary>
        /// <returns>错误页面视图.</returns>
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return this.View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? this.HttpContext.TraceIdentifier });
        }

        /// <summary>
        /// 错误统计页面.
        /// </summary>
        /// <returns>错误统计页面视图.</returns>
        public IActionResult ErrorStats()
        {
            return this.View();
        }
    }
}
