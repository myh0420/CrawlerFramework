// <copyright file="ConfigController.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerMonitor.Controllers
{
    using System.Diagnostics;
    using CrawlerCore.Configuration;
    using Microsoft.AspNetCore.Mvc;

    // [ApiController]
    // [Route("api/[controller]")]

    /// <summary>
    /// 配置控制器.
    /// </summary>
    public class ConfigController(IConfigService configService, IConfigValidator configValidator) : Controller
    {
        private readonly IConfigService configService = configService;
        private readonly IConfigValidator configValidator = configValidator;

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