using System.Security.Claims;
using FitHub_FinalProject.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitHub_FinalProject.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly FitHubDbContext _context;

        public AdminController(FitHubDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Dashboard()
        {
            var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var admin = await _context.Users.FirstOrDefaultAsync(u => u.UserId == adminId);

            ViewBag.AdminName = admin?.FullName ?? "Admin";

            ViewBag.TotalMembers = await _context.Users.CountAsync(u => !u.IsAdmin);
            ViewBag.ActiveMemberships = await _context.Memberships.CountAsync(m => m.Status == "Active");
            ViewBag.ExpiredMemberships = await _context.Memberships.CountAsync(m => m.Status == "Expired");

            var totalRevenue = await _context.Transactions
                .Where(t => t.Status == "Paid")
                .SumAsync(t => (decimal?)t.Amount) ?? 0m;
            ViewBag.TotalRevenue = "₱" + totalRevenue.ToString("N2");

            var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var monthlyRevenue = await _context.Transactions
                .Where(t => t.Status == "Paid" && t.Date >= monthStart)
                .SumAsync(t => (decimal?)t.Amount) ?? 0m;
            ViewBag.MonthlyRevenue = "₱" + monthlyRevenue.ToString("N2");

            ViewBag.NewMembersThisMonth = await _context.Users
                .CountAsync(u => !u.IsAdmin && u.CreatedAt >= monthStart);

            ViewBag.RecentMembers = await _context.Users
                .Where(u => !u.IsAdmin)
                .Include(u => u.Membership).ThenInclude(m => m!.Plan)
                .OrderByDescending(u => u.CreatedAt)
                .Take(5)
                .Select(u => new
                {
                    Id = u.UserId,
                    u.FullName,
                    u.Email,
                    MembershipPlan = u.Membership != null && u.Membership.Plan != null ? u.Membership.Plan.Name : "—",
                    DateJoined = u.CreatedAt,
                    Status = u.IsActive ? "Active" : "Inactive"
                })
                .ToListAsync();

            ViewBag.RecentTransactions = await _context.Transactions
                .Include(t => t.User)
                .OrderByDescending(t => t.Date)
                .Take(5)
                .Select(t => new
                {
                    MemberName = t.User.FullName,
                    t.Amount,
                    t.Type,
                    t.Date,
                    t.Status
                })
                .ToListAsync();

            ViewBag.PlanBreakdown = await _context.MembershipPlans
                .Select(p => new
                {
                    PlanName = p.Name,
                    MemberCount = p.Memberships.Count(m => m.Status == "Active"),
                    Revenue = p.Memberships
                        .Where(m => m.Status == "Active")
                        .Sum(m => m.BillingCycle == "annual" ? p.AnnualPrice : p.MonthlyPrice),
                    Status = p.IsActive ? "Active" : "Inactive"
                })
                .ToListAsync();

            return View();
        }

        public IActionResult Members() => View();

        public IActionResult Plans() => View();

        public IActionResult Profile() => View();

        public IActionResult Transactions() => View("Transaction");
    }
}
