using Microsoft.AspNetCore.Mvc;

namespace FitHub_FinalProject.Controllers
{
    public class UserController : Controller
    {
        public IActionResult Dashboard()
        {
            return View();
        }

        public IActionResult Membership()
        {
            return View();
        }

        public IActionResult Profile()
        {
            return View();
        }
    }
}
