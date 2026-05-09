using System.Security.Claims;
using FitHub_FinalProject.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace FitHub_FinalProject.Filters
{
    public class NotificationFilter : IAsyncActionFilter
    {
        private readonly FitHubDbContext _context;

        public NotificationFilter(FitHubDbContext context)
        {
            _context = context;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var user = context.HttpContext.User;

            if (user?.Identity?.IsAuthenticated == true)
            {
                var idClaim = user.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(idClaim, out int userId))
                {
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
                    }).Cast<dynamic>().ToList();

                    var unreadCount = await _context.Notifications
                        .CountAsync(n => n.UserId == userId && !n.IsRead);

                    if (context.Controller is Controller controller)
                    {
                        controller.ViewBag.NavNotifications    = notifList;
                        controller.ViewBag.NavNotificationCount = unreadCount;
                    }
                }
            }

            await next();
        }

        private static string TimeAgo(DateTime utc)
        {
            var span = DateTime.UtcNow - utc;
            if (span.TotalMinutes < 1)  return "just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} min ago";
            if (span.TotalHours   < 24) return $"{(int)span.TotalHours} hr ago";
            if (span.TotalDays    < 30) return $"{(int)span.TotalDays} days ago";
            return utc.ToString("MMM dd, yyyy");
        }
    }
}
