using System.Security.Claims;
using FitHub_FinalProject.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitHub_FinalProject.Components
{
    public class NotificationBellViewComponent : ViewComponent
    {
        private readonly FitHubDbContext _context;

        public NotificationBellViewComponent(FitHubDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var userIdClaim = HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return Content(string.Empty);

            var userId = int.Parse(userIdClaim);

            var rawNotifs = await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(5)
                .ToListAsync();

            var notifications = rawNotifs.Select(n => new Dictionary<string, object?>
            {
                ["Type"] = n.Type,
                ["Title"] = n.Title,
                ["Description"] = n.Description ?? "",
                ["TimeAgo"] = GetTimeAgo(n.CreatedAt)
            }).ToList();

            var unreadCount = await _context.Notifications
                .CountAsync(n => n.UserId == userId && !n.IsRead);

            ViewBag.Notifications = notifications;
            ViewBag.UnreadCount = unreadCount;

            return View();
        }

        private static string GetTimeAgo(DateTime utc)
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
