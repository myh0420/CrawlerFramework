using CrawlerCore.Configuration;
using CrawlerMonitor.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace CrawlerMonitor.Controllers
{
    public class HomeController(
        ILogger<HomeController> logger,
        IConfigService configService,
        IConfigValidator configValidator
    ) : Controller
    {
        private readonly ILogger<HomeController> _logger = logger;
        private readonly IConfigService _configService = configService;
        private readonly IConfigValidator _configValidator = configValidator;

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult Config()
        {
            var config = _configService.GetCurrentConfig();
            return View(config);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
