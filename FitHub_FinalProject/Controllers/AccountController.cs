using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FitHub_FinalProject.Data;
using FitHub_FinalProject.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitHub_FinalProject.Controllers
{
    public class AccountController : Controller
    {
        private readonly FitHubDbContext _context;

        public AccountController(FitHubDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Dashboard", "User");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password, bool rememberMe)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ModelState.AddModelError("", "Email and password are required.");
                return View();
            }

            var hashed = HashPassword(password);
            var user = await _context.Users.FirstOrDefaultAsync(u =>
                u.Email == email && u.PasswordHash == hashed && u.IsActive);

            if (user == null)
            {
                ModelState.AddModelError("", "Invalid email or password.");
                return View();
            }

            await SignInUserAsync(user, rememberMe);
            return RedirectToAction("Dashboard", "User");
        }

        public IActionResult Register() => View();

        private async Task SignInUserAsync(User user, bool isPersistent)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new(ClaimTypes.Name, user.FullName),
                new(ClaimTypes.Email, user.Email)
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties { IsPersistent = isPersistent });
        }

        private static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }
    }
}
