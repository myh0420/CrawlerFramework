// <copyright file="ConfigController.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerFramework.CrawlerMonitor.Controllers
{
    using System.Diagnostics;
    // using CrawlerFramework.CrawlerCore.Configuration;
    using CrawlerFramework.CrawlerEntity.Configuration;
    using CrawlerFramework.CrawlerInterFaces.Interfaces.Configuration;
    using Microsoft.AspNetCore.Mvc;

    // [ApiController]
    // [Route("api/[controller]")]

    /// <summary>
    /// 配置控制器.
    /// </summary>
    public class ConfigController : Controller
    {
        /// <summary>
        /// 配置服务.
        /// </summary>
        private readonly IConfigService configService;

        /// <summary>
        /// 配置验证器.
        /// </summary>
        private readonly IConfigValidator configValidator;

        /// <summary>
        /// 初始化 <see cref="ConfigController"/> 类的新实例.
        /// </summary>
        /// <param name="configService">配置服务.</param>
        /// <param name="configValidator">配置验证器.</param>
        public ConfigController(IConfigService configService, IConfigValidator configValidator)
        {
            this.configService = configService ?? throw new ArgumentNullException(nameof(configService), "配置服务参数不能为空");
            this.configValidator = configValidator ?? throw new ArgumentNullException(nameof(configValidator), "配置验证器参数不能为空");
        }

        // [HttpGet]
        // [Route("config")]
        // [Route("Config/Index")]

        /// <summary>
        /// 显示当前配置.
        /// </summary>
        /// <returns>当前配置视图.</returns>
        public IActionResult Index()
        {
            var config = this.configService.GetCurrentConfig();
            return this.View(config);
        }

        // [HttpGet]
        // [Route("config/edit")]
        // public IActionResult Edit()
        // {
        //    var config = _configService.GetCurrentConfig();
        //    return View(config);
        // }

        /// <summary>
        /// 获取当前配置.
        /// </summary>
        /// <returns>当前配置.</returns>
        [HttpGet]
        [Route("/api/config")]
        public IActionResult GetConfig()
        {
            var config = this.configService.GetCurrentConfig();
            return this.Ok(config);
        }

        /// <summary>
        /// 更新配置.
        /// </summary>
        /// <param name="config">要更新的配置.</param>
        /// <returns>更新结果.</returns>
        [HttpPost]
        [Route("/api/config")]
        public async Task<IActionResult> UpdateConfig([FromBody] AppCrawlerConfig config)
        {
            // 验证配置
            var validationResult = this.configValidator.Validate(config);
            if (!validationResult.IsValid)
            {
                return this.BadRequest(new
                {
                    success = false,
                    errors = validationResult.Errors,
                    warnings = validationResult.Warnings,
                });
            }

            try
            {
                await this.configService.SaveConfigAsync(config);
                return this.Ok(new { success = true, message = "Configuration updated successfully" });
            }
            catch (Exception ex)
            {
                return this.StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// 重置配置.
        /// </summary>
        /// <returns>重置结果.</returns>
        [HttpPost("reset")]
        [Route("/api/config/reset")]
        public async Task<IActionResult> ResetConfig()
        {
            try
            {
                var defaultConfig = new AppCrawlerConfig();
                await this.configService.SaveConfigAsync(defaultConfig);
                return this.Ok(new { success = true, message = "Configuration reset to defaults" });
            }
            catch (Exception ex)
            {
                return this.StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// 验证当前配置.
        /// </summary>
        /// <returns>验证结果.</returns>
        [HttpPost("validate")]

        [Route("/api/config/validate")]
        public IActionResult ValidateCurrentConfig()
        {
            var config = this.configService.GetCurrentConfig();
            var result = this.configValidator.Validate(config);

            return this.Ok(new
            {
                isValid = result.IsValid,
                errors = result.Errors,
                warnings = result.Warnings,
            });
        }

        /// <summary>
        /// 重新加载配置.
        /// </summary>
        /// <returns>重新加载结果.</returns>
        [HttpPost("reload")]

        [Route("/api/config/reload")]
        public async Task<IActionResult> ReloadConfig()
        {
            try
            {
                await this.configService.LoadConfigAsync();
                return this.Ok(new { success = true, message = "Configuration reloaded from file" });
            }
            catch (Exception ex)
            {
                return this.StatusCode(500, new { success = false, error = ex.Message });
            }
        }
    }
}