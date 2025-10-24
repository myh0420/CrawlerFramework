// CrawlerMonitor/Controllers/ConfigController.cs
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using CrawlerCore.Configuration;

namespace CrawlerMonitor.Controllers
{
    //[ApiController]
    //[Route("api/[controller]")]
    public class ConfigController : Controller
    {
        private readonly IConfigService _configService;
        private readonly IConfigValidator _configValidator;
        public ConfigController(IConfigService configService, IConfigValidator configValidator) {
            _configService = configService;
            _configValidator = configValidator;
        }
        //[HttpGet]
        //[Route("config")]
        //[Route("Config/Index")]
        public IActionResult Index()
        {
            var config = _configService.GetCurrentConfig();
            return View(config);
        }

        //[HttpGet]
        //[Route("config/edit")]
        //public IActionResult Edit()
        //{
        //    var config = _configService.GetCurrentConfig();
        //    return View(config);
        //}

        [HttpGet]
        [Route("/api/config")]
        public IActionResult GetConfig()
        {
                var config = _configService.GetCurrentConfig();
                return Ok(config);
        }

        [HttpPost]
        [Route("/api/config")]
        public async Task<IActionResult> UpdateConfig([FromBody] AppCrawlerConfig config)
        {
            // 验证配置
            var validationResult = _configValidator.Validate(config);
            if (!validationResult.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    errors = validationResult.Errors,
                    warnings = validationResult.Warnings
                });
            }

            try
            {
                await _configService.SaveConfigAsync(config);
                return Ok(new { success = true, message = "Configuration updated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        [HttpPost("reset")]
        [Route("/api/config/reset")]
        public async Task<IActionResult> ResetConfig()
        {
            try
            {
                var defaultConfig = new AppCrawlerConfig();
                await _configService.SaveConfigAsync(defaultConfig);
                return Ok(new { success = true, message = "Configuration reset to defaults" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        [HttpPost("validate")]

        [Route("/api/config/validate")]
        public IActionResult ValidateCurrentConfig()
        {
            var config = _configService.GetCurrentConfig();
            var result = _configValidator.Validate(config);

            return Ok(new
            {
                isValid = result.IsValid,
                errors = result.Errors,
                warnings = result.Warnings
            });
        }

        [HttpPost("reload")]

        [Route("/api/config/reload")]
        public async Task<IActionResult> ReloadConfig()
        {
            try
            {
                await _configService.LoadConfigAsync();
                return Ok(new { success = true, message = "Configuration reloaded from file" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }
    }
}