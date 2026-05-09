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
        private readonly IWebHostEnvironment _env;

        public AccountController(FitHubDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
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
            string firstName, string lastName, string email, string phoneNumber,
            DateOnly? dateOfBirth, string gender, string address,
            string password, string confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(firstName) ||
                string.IsNullOrWhiteSpace(lastName) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                ModelState.AddModelError("", "First name, last name, email, and password are required.");
            }

            var fullName = $"{firstName.Trim()} {lastName.Trim()}";

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
                FullName = fullName.Trim(),
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

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await _context.Users
                .Include(u => u.Membership).ThenInclude(m => m!.Plan)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return RedirectToAction("Login");
            }

            ViewBag.FullName = user.FullName;
            ViewBag.Email = user.Email;
            ViewBag.PhoneNumber = user.PhoneNumber;
            ViewBag.DateOfBirth = user.DateOfBirth?.ToString("yyyy-MM-dd");
            ViewBag.Gender = user.Gender;
            ViewBag.Address = user.Address;
            ViewBag.ProfilePhoto = user.ProfilePhotoPath ?? "/images/default-avatar.png";

            ViewBag.MembershipPlan = user.Membership?.Plan?.Name ?? "—";
            ViewBag.MembershipStatus = user.Membership?.Status ?? "No membership";
            ViewBag.MemberSince = user.CreatedAt.ToString("MMMM yyyy");
            ViewBag.NextBillingDate = user.Membership?.NextBillingDate.ToString("MMM dd, yyyy") ?? "—";
            ViewBag.MonthlyFee = user.Membership?.Plan != null
                ? "₱" + user.Membership.Plan.MonthlyPrice.ToString("N2")
                : "—";
            ViewBag.PaymentMethod = user.Membership?.PaymentMethod ?? "—";

            return View("~/Views/User/Profile.cshtml");
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(
            string fullName, string phoneNumber,
            DateOnly? dateOfBirth, string gender, string address,
            IFormFile? ProfilePhoto)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null) return RedirectToAction("Login");

            if (string.IsNullOrWhiteSpace(fullName))
            {
                TempData["ErrorMessage"] = "Full name is required.";
                return RedirectToAction("Profile");
            }

            user.FullName = fullName;
            user.PhoneNumber = phoneNumber;
            user.DateOfBirth = dateOfBirth;
            user.Gender = gender;
            user.Address = address;

            if (ProfilePhoto != null && ProfilePhoto.Length > 0)
            {
                if (ProfilePhoto.Length > 2 * 1024 * 1024)
                {
                    TempData["ErrorMessage"] = "Profile photo must be 2MB or smaller.";
                    return RedirectToAction("Profile");
                }

                var ext = Path.GetExtension(ProfilePhoto.FileName).ToLowerInvariant();
                if (ext != ".jpg" && ext != ".jpeg" && ext != ".png")
                {
                    TempData["ErrorMessage"] = "Profile photo must be a JPG or PNG file.";
                    return RedirectToAction("Profile");
                }

                var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "profile");
                Directory.CreateDirectory(uploadsDir);

                var fileName = $"{user.UserId}{ext}";
                var filePath = Path.Combine(uploadsDir, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await ProfilePhoto.CopyToAsync(stream);
                }

                user.ProfilePhotoPath = $"/uploads/profile/{fileName}";
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Profile updated successfully.";
            return RedirectToAction("Profile");
        }

        [Authorize]
        [HttpGet]
        public IActionResult ChangePassword()
            => Redirect("/Account/Profile#change-password");

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(
            string currentPassword, string newPassword, string confirmPassword)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null) return RedirectToAction("Login");

            if (HashPassword(currentPassword ?? "") != user.PasswordHash)
            {
                TempData["ErrorMessage"] = "Current password is incorrect.";
                return RedirectToAction("Profile");
            }

            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
            {
                TempData["ErrorMessage"] = "New password must be at least 8 characters.";
                return RedirectToAction("Profile");
            }

            if (newPassword != confirmPassword)
            {
                TempData["ErrorMessage"] = "New passwords do not match.";
                return RedirectToAction("Profile");
            }

            user.PasswordHash = HashPassword(newPassword);

            _context.Notifications.Add(new Notification
            {
                UserId = user.UserId,
                Title = "Password Changed",
                Description = "Your password was changed successfully.",
                Type = "Success",
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Password changed successfully.";
            return RedirectToAction("Profile");
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeactivateAccount()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user != null)
            {
                user.IsActive = false;
                await _context.SaveChangesAsync();
            }

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAccount(string confirm)
        {
            if (confirm != "true") return RedirectToAction("Profile");

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user != null)
            {
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
            }

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            TempData["InfoMessage"] = "Password recovery is currently disabled. Please contact support@fithub.ph.";
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
