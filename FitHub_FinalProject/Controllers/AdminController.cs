using Microsoft.AspNetCore.Mvc;

namespace FitHub_FinalProject.Controllers
{
    public class AdminController : Controller
    {
        public IActionResult Dashboard()
        {
            return View();
        }

        public IActionResult Members()
        {
            return View();
        }

        public IActionResult Plans()
        {
            return View();
        }

        public IActionResult Profile()
        {
            return View();
        }

        public IActionResult Transaction()
        {
            return View();
        }
    }
}
