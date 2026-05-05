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

        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Dashboard", "User");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(
            string fullName, string email, string phoneNumber,
            DateOnly? dateOfBirth, string gender, string address,
            string password, string confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(fullName) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                ModelState.AddModelError("", "Full name, email, and password are required.");
            }

            if (password != confirmPassword)
                ModelState.AddModelError("", "Passwords do not match.");

            if (!string.IsNullOrEmpty(password) && password.Length < 8)
                ModelState.AddModelError("", "Password must be at least 8 characters.");

            if (!string.IsNullOrWhiteSpace(email) && await _context.Users.AnyAsync(u => u.Email == email))
                ModelState.AddModelError("", "An account with this email already exists.");

            if (!ModelState.IsValid)
                return View();

            var user = new User
            {
                FullName = fullName,
                Email = email,
                PhoneNumber = phoneNumber,
                DateOfBirth = dateOfBirth,
                Gender = gender,
                Address = address,
                PasswordHash = HashPassword(password),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _context.Notifications.Add(new Notification
            {
                UserId = user.UserId,
                Title = "Welcome to FitHub!",
                Description = "Your account has been created. Start by browsing our membership plans.",
                Type = "Success",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            await SignInUserAsync(user, isPersistent: false);
            return RedirectToAction("Dashboard", "User");
        }

        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

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
