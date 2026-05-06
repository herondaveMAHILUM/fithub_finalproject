using System.Security.Claims;
using FitHub_FinalProject.Data;
using FitHub_FinalProject.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitHub_FinalProject.Controllers
{
    [Authorize]
    public class UserController : Controller
    {
        private readonly FitHubDbContext _context;

        public UserController(FitHubDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Dashboard()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var expiring = await _context.Memberships
                .FirstOrDefaultAsync(m => m.UserId == userId && m.Status == "Active");
            if (expiring != null && expiring.ExpiryDate < DateTime.UtcNow)
            {
                expiring.Status = "Expired";
                await _context.SaveChangesAsync();
            }

            var user = await _context.Users
                .Include(u => u.Membership).ThenInclude(m => m!.Plan)
                .Include(u => u.AssignedTrainer)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return RedirectToAction("Login", "Account");
            }

            ViewBag.FullName = user.FullName;
            ViewBag.MemberSince = user.CreatedAt.ToString("MMMM yyyy");
            ViewBag.MembershipStatus = user.Membership?.Status ?? "No active membership";
            ViewBag.MembershipPlan = user.Membership?.Plan?.Name ?? "—";
            ViewBag.ExpiryDate = user.Membership?.ExpiryDate.ToString("MMM dd, yyyy") ?? "—";
            ViewBag.NextBillingDate = user.Membership?.NextBillingDate.ToString("MMM dd, yyyy") ?? "—";
            ViewBag.TrainerName = user.AssignedTrainer?.FullName;

            var plan = await _context.WorkoutPlans
                .Include(wp => wp.WorkoutDays).ThenInclude(wd => wd.Exercises)
                .FirstOrDefaultAsync(wp => wp.UserId == userId && wp.IsActive);

            var todayDow = (int)DateTime.UtcNow.DayOfWeek;
            var todayDay = plan?.WorkoutDays.FirstOrDefault(d => d.DayOfWeek == todayDow);
            ViewBag.TodayWorkoutLabel = DateTime.UtcNow.ToString("dddd, MMM dd");
            ViewBag.TodayExercises = todayDay?.Exercises.ToList() ?? new List<Exercise>();

            string[] shortNames = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
            ViewBag.WeeklySchedule = Enumerable.Range(0, 7).Select(i => new
            {
                ShortName = shortNames[i],
                Focus = plan?.WorkoutDays.FirstOrDefault(d => d.DayOfWeek == i)?.Focus ?? "Rest",
                IsToday = i == todayDow
            }).ToList();

            var rawNotifs = await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(5)
                .ToListAsync();

            var notifList = rawNotifs.Select(n => new
            {
                n.Type,
                n.Title,
                Description = n.Description ?? "",
                TimeAgo = TimeAgo(n.CreatedAt)
            }).ToList();

            ViewBag.Notifications = notifList;
            ViewBag.NavNotifications = notifList;

            var unreadCount = await _context.Notifications
                .CountAsync(n => n.UserId == userId && !n.IsRead);

            ViewBag.NotificationCount = unreadCount;
            ViewBag.NavNotificationCount = unreadCount;

            ViewBag.RecentTransactions = await _context.Transactions
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.Date)
                .Take(5)
                .Select(t => new
                {
                    Date = t.Date.ToString("MMM dd, yyyy"),
                    t.Description,
                    Amount = "₱" + t.Amount.ToString("N2"),
                    t.Status
                })
                .ToListAsync();

            return View();
        }

        private static string TimeAgo(DateTime utc)
        {
            var span = DateTime.UtcNow - utc;
            if (span.TotalMinutes < 1) return "just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} min ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours} hr ago";
            if (span.TotalDays < 30) return $"{(int)span.TotalDays} days ago";
            return utc.ToString("MMM dd, yyyy");
        }
    }
}
