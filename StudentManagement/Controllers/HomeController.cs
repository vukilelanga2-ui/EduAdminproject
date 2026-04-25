using Microsoft.AspNetCore.Mvc;
using StudentManagement.Models;
using StudentManagement.Services;

namespace StudentManagement.Controllers
{
    public class HomeController : Controller
    {
        private readonly ICosmosDbService _cosmosDbService;
        private readonly ILogger<HomeController> _logger;

        public HomeController(ICosmosDbService cosmosDbService, ILogger<HomeController> logger)
        {
            _cosmosDbService = cosmosDbService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var stats = await _cosmosDbService.GetDashboardStatsAsync();

                var model = new DashboardViewModel
                {
                    TotalStudents = stats.TotalStudents,
                    ActiveStudents = stats.ActiveStudents,
                    InactiveStudents = stats.InactiveStudents,
                    GraduatedStudents = stats.GraduatedStudents,
                    SuspendedStudents = stats.SuspendedStudents,
                    AverageGPA = stats.AverageGPA,
                    RecentStudents = stats.RecentStudents,
                    StudentsByCourse = stats.StudentsByCourse
                };

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard");
                TempData["Error"] = "Unable to load dashboard data. Please try again.";
                return View(new DashboardViewModel());
            }
        }

        public IActionResult Error()
        {
            return View();
        }
    }
}
