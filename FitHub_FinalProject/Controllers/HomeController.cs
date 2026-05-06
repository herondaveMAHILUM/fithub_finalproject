using System.Diagnostics;
using FitHub_FinalProject.Data;
using FitHub_FinalProject.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitHub_FinalProject.Controllers
{
    public class HomeController : Controller
    {
        private readonly FitHubDbContext _context;

        public HomeController(FitHubDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.TotalMembers = await _context.Users.CountAsync(u => u.IsActive);
            ViewBag.TotalTrainers = await _context.Trainers.CountAsync(t => t.IsActive);
            ViewBag.TotalEquipmentTypes = 20;
            ViewBag.DaysOpen = 365;
            ViewBag.Testimonials = null;
            return View();
        }

        public IActionResult Privacy() => View();

        public IActionResult Terms() => View("Privacy");

        public IActionResult Contact()
        {
            TempData["InfoMessage"] = "Contact us at support@fithub.ph";
            return RedirectToAction("Index");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() =>
            View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
