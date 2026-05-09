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
            if (User.IsInRole("Admin")) return RedirectToAction("Dashboard", "Admin");

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

        // ─── NEW: Show checkout / payment form ────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Checkout(int planId, string billing = "monthly")
        {
            if (billing != "monthly" && billing != "annual") billing = "monthly";

            var plan = await _context.MembershipPlans
                .FirstOrDefaultAsync(p => p.PlanId == planId && p.IsActive);

            if (plan == null) return RedirectToAction("Index");

            var amount = billing == "annual" ? plan.AnnualPrice : plan.MonthlyPrice;

            ViewBag.Plan    = plan;
            ViewBag.Billing = billing;
            ViewBag.Amount  = amount;

            return View("~/Views/User/Checkout.cshtml");
        }

        // ─── NEW: Process simulated payment (POST) ────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessPayment(int planId, string billing, string paymentMethod)
        {
            if (billing != "monthly" && billing != "annual") billing = "monthly";

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var plan = await _context.MembershipPlans
                .FirstOrDefaultAsync(p => p.PlanId == planId && p.IsActive);

            if (plan == null) return RedirectToAction("Index");

            // Validate payment method
            var validMethods = new[] { "GCash", "Maya", "Card" };
            if (!validMethods.Contains(paymentMethod)) paymentMethod = "GCash";

            var amount     = billing == "annual" ? plan.AnnualPrice : plan.MonthlyPrice;
            var startDate  = DateTime.UtcNow;
            var expiryDate = billing == "annual" ? startDate.AddYears(1) : startDate.AddMonths(1);

            var existing = await _context.Memberships.FirstOrDefaultAsync(m => m.UserId == userId);
            string txType;

            if (existing == null)
            {
                _context.Memberships.Add(new Membership
                {
                    UserId          = userId,
                    PlanId          = planId,
                    BillingCycle    = billing,
                    PaymentMethod   = paymentMethod,
                    Status          = "Active",
                    StartDate       = startDate,
                    ExpiryDate      = expiryDate,
                    NextBillingDate = expiryDate,
                    CreatedAt       = DateTime.UtcNow
                });
                txType = "Subscription";
            }
            else
            {
                existing.PlanId          = planId;
                existing.BillingCycle    = billing;
                existing.PaymentMethod   = paymentMethod;
                existing.Status          = "Active";
                existing.StartDate       = startDate;
                existing.ExpiryDate      = expiryDate;
                existing.NextBillingDate = expiryDate;
                txType = existing.PlanId == planId ? "Renewal" : "Upgrade";
            }

            // Generate a realistic-looking reference number
            var refNumber = "FH" + DateTime.UtcNow.ToString("yyyyMMdd") + Random.Shared.Next(100000, 999999).ToString();

            var transaction = new Transaction
            {
                UserId        = userId,
                Type          = txType,
                Amount        = amount,
                Status        = "Paid",
                Date          = DateTime.UtcNow,
                Description   = $"FitHub {plan.Name} Plan - {char.ToUpper(billing[0]) + billing[1..]}",
                PaymentMethod = paymentMethod
            };

            _context.Transactions.Add(transaction);

            _context.Notifications.Add(new Notification
            {
                UserId      = userId,
                Title       = $"{txType} Successful",
                Description = $"You are now subscribed to the {plan.Name} plan ({billing}) via {paymentMethod}.",
                Type        = "Success",
                CreatedAt   = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            // Pass receipt data via TempData so the success page can display it
            TempData["Receipt_TransactionId"] = transaction.TransactionId.ToString();
            TempData["Receipt_RefNumber"]     = refNumber;
            TempData["Receipt_PlanName"]      = plan.Name;
            TempData["Receipt_Billing"]       = billing;
            TempData["Receipt_Amount"]        = amount.ToString("N2");
            TempData["Receipt_Method"]        = paymentMethod;
            TempData["Receipt_Type"]          = txType;
            TempData["Receipt_Date"]          = DateTime.UtcNow.ToString("MMM dd, yyyy hh:mm tt");
            TempData["Receipt_Expiry"]        = expiryDate.ToString("MMM dd, yyyy");

            return RedirectToAction("PaymentSuccess");
        }

        // ─── NEW: Payment success / receipt page ─────────────────────────────
        [HttpGet]
        public IActionResult PaymentSuccess()
        {
            if (TempData["Receipt_TransactionId"] == null)
                return RedirectToAction("Index");

            return View("~/Views/User/PaymentSuccess.cshtml");
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
                    UserId      = userId,
                    Title       = "Membership Cancelled",
                    Description = $"Your {membership.Plan?.Name ?? ""} membership has been cancelled.",
                    Type        = "Warning",
                    CreatedAt   = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Dashboard", "User");
        }
    }
}
