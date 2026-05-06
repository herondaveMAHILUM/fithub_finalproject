using System.Security.Claims;
using FitHub_FinalProject.Data;
using FitHub_FinalProject.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitHub_FinalProject.Controllers
{
    [Authorize]
    public class MembershipController : Controller
    {
        private readonly FitHubDbContext _context;

        public MembershipController(FitHubDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string billing = "monthly")
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            if (billing != "monthly" && billing != "annual") billing = "monthly";

            var membership = await _context.Memberships
                .Include(m => m.Plan)
                .FirstOrDefaultAsync(m => m.UserId == userId);

            ViewBag.HasActiveMembership = membership != null && membership.Status == "Active";
            ViewBag.CurrentPlan = membership?.Plan?.Name ?? "";
            ViewBag.NextBillingDate = membership?.NextBillingDate.ToString("MMM dd, yyyy") ?? "";
            ViewBag.Billing = billing;

            return View("~/Views/User/Membership.cshtml");
        }

        [HttpGet]
        public async Task<IActionResult> Subscribe(int planId, string billing = "monthly")
        {
            if (billing != "monthly" && billing != "annual") billing = "monthly";

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var plan = await _context.MembershipPlans
                .FirstOrDefaultAsync(p => p.PlanId == planId && p.IsActive);
            if (plan == null) return RedirectToAction("Index");

            var amount = billing == "annual" ? plan.AnnualPrice : plan.MonthlyPrice;
            var startDate = DateTime.UtcNow;
            var expiryDate = billing == "annual" ? startDate.AddYears(1) : startDate.AddMonths(1);

            var existing = await _context.Memberships.FirstOrDefaultAsync(m => m.UserId == userId);
            string txType;

            if (existing == null)
            {
                _context.Memberships.Add(new Membership
                {
                    UserId = userId,
                    PlanId = planId,
                    BillingCycle = billing,
                    PaymentMethod = "GCash",
                    Status = "Active",
                    StartDate = startDate,
                    ExpiryDate = expiryDate,
                    NextBillingDate = expiryDate,
                    CreatedAt = DateTime.UtcNow
                });
                txType = "Subscription";
            }
            else
            {
                existing.PlanId = planId;
                existing.BillingCycle = billing;
                existing.Status = "Active";
                existing.StartDate = startDate;
                existing.ExpiryDate = expiryDate;
                existing.NextBillingDate = expiryDate;
                txType = "Upgrade";
            }

            _context.Transactions.Add(new Transaction
            {
                UserId = userId,
                Type = txType,
                Amount = amount,
                Status = "Paid",
                Date = DateTime.UtcNow,
                Description = $"FitHub {plan.Name} Plan - {billing}",
                PaymentMethod = "GCash"
            });

            _context.Notifications.Add(new Notification
            {
                UserId = userId,
                Title = $"{txType} Successful",
                Description = $"You are now subscribed to the {plan.Name} plan ({billing}).",
                Type = "Success",
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            return RedirectToAction("Dashboard", "User");
        }

        [HttpGet]
        public async Task<IActionResult> Cancel()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var membership = await _context.Memberships
                .Include(m => m.Plan)
                .FirstOrDefaultAsync(m => m.UserId == userId);
            if (membership != null)
            {
                membership.Status = "Cancelled";
                _context.Notifications.Add(new Notification
                {
                    UserId = userId,
                    Title = "Membership Cancelled",
                    Description = $"Your {membership.Plan?.Name ?? ""} membership has been cancelled.",
                    Type = "Warning",
                    CreatedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Dashboard", "User");
        }
    }
}
