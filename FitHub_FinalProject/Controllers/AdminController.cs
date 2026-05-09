using System.Security.Claims;
using FitHub_FinalProject.Data;
using FitHub_FinalProject.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitHub_FinalProject.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private const int PageSize = 10;
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

        [HttpGet]
        public async Task<IActionResult> Members(
            string? search, string? plan, string? status, string? gender,
            DateTime? dateFrom, DateTime? dateTo, int page = 1)
        {
            var query = _context.Users
                .Where(u => !u.IsAdmin)
                .Include(u => u.Membership).ThenInclude(m => m!.Plan)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(u => u.FullName.Contains(search) || u.Email.Contains(search));
            if (!string.IsNullOrWhiteSpace(plan))
                query = query.Where(u => u.Membership != null && u.Membership.Plan != null && u.Membership.Plan.Name == plan);
            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(u => u.Membership != null && u.Membership.Status == status);
            if (!string.IsNullOrWhiteSpace(gender))
                query = query.Where(u => u.Gender == gender);
            if (dateFrom.HasValue)
                query = query.Where(u => u.CreatedAt >= dateFrom.Value);
            if (dateTo.HasValue)
                query = query.Where(u => u.CreatedAt <= dateTo.Value.AddDays(1));

            var totalCount = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));
            if (page < 1) page = 1;
            if (page > totalPages) page = totalPages;

            var members = await query
                .OrderByDescending(u => u.CreatedAt)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .Select(u => new
                {
                    Id = u.UserId,
                    u.FullName,
                    u.Email,
                    u.PhoneNumber,
                    MembershipPlan = u.Membership != null && u.Membership.Plan != null ? u.Membership.Plan.Name : "—",
                    Status = u.Membership != null ? u.Membership.Status : (u.IsActive ? "No Plan" : "Inactive"),
                    DateJoined = u.CreatedAt,
                    ExpiryDate = u.Membership != null ? u.Membership.ExpiryDate : DateTime.MinValue
                })
                .ToListAsync();

            ViewBag.TotalMembers = totalCount;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.Search = search;
            ViewBag.PlanFilter = plan;
            ViewBag.StatusFilter = status;
            ViewBag.DateFrom = dateFrom?.ToString("yyyy-MM-dd");
            ViewBag.DateTo = dateTo?.ToString("yyyy-MM-dd");

            return View(members);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMember(int id)
        {
            var member = await _context.Users.FirstOrDefaultAsync(u => u.UserId == id && !u.IsAdmin);
            if (member == null)
            {
                TempData["ErrorMessage"] = "Member not found.";
                return RedirectToAction("Members");
            }

            _context.Users.Remove(member);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Deleted member: {member.FullName}";
            return RedirectToAction("Members");
        }

        [HttpGet]
        public IActionResult AddMember()
        {
            TempData["InfoMessage"] = "Use the public Register page to add new members.";
            return RedirectToAction("Members");
        }

        [HttpGet]
        public async Task<IActionResult> MemberDetails(int id)
        {
            var member = await _context.Users
                .Include(u => u.Membership).ThenInclude(m => m!.Plan)
                .FirstOrDefaultAsync(u => u.UserId == id);
            if (member == null)
            {
                TempData["ErrorMessage"] = "Member not found.";
                return RedirectToAction("Members");
            }
            TempData["InfoMessage"] = $"#{member.UserId} • {member.FullName} • {member.Email} • Plan: {member.Membership?.Plan?.Name ?? "—"} • Status: {(member.IsActive ? "Active" : "Inactive")}";
            return RedirectToAction("Members");
        }

        [HttpGet]
        public IActionResult EditMember(int id)
        {
            TempData["InfoMessage"] = "Member editing is available through the user's own profile page.";
            return RedirectToAction("Members");
        }

        [HttpGet]
        public async Task<IActionResult> ExportMembers(string format = "csv")
        {
            var rows = await _context.Users
                .Where(u => !u.IsAdmin)
                .Include(u => u.Membership).ThenInclude(m => m!.Plan)
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("UserId,FullName,Email,PhoneNumber,Plan,Status,DateJoined");
            foreach (var u in rows)
            {
                var planName = u.Membership?.Plan?.Name ?? "—";
                var memStatus = u.Membership?.Status ?? "No Plan";
                sb.AppendLine($"{u.UserId},\"{u.FullName}\",{u.Email},{u.PhoneNumber},{planName},{memStatus},{u.CreatedAt:yyyy-MM-dd}");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
            var fileName = format == "pdf" ? "members.pdf" : "members.csv";
            return File(bytes, "text/csv", fileName);
        }

        [HttpGet]
        public async Task<IActionResult> Plans(int? edit)
        {
            var plansList = await _context.MembershipPlans
                .OrderBy(p => p.PlanId)
                .Select(p => new
                {
                    Id = p.PlanId,
                    PlanName = p.Name,
                    p.Description,
                    p.MonthlyPrice,
                    p.AnnualPrice,
                    p.Features,
                    p.MaxMembers,
                    MemberCount = p.Memberships.Count(m => m.Status == "Active"),
                    TotalRevenue = p.Memberships
                        .Where(m => m.Status == "Active")
                        .Sum(m => m.BillingCycle == "annual" ? p.AnnualPrice : p.MonthlyPrice),
                    Status = p.IsActive ? "Active" : "Inactive"
                })
                .ToListAsync();

            ViewBag.TotalPlans = plansList.Count;
            ViewBag.TotalSubscribers = plansList.Sum(p => p.MemberCount);
            ViewBag.MostPopularPlan = plansList.OrderByDescending(p => p.MemberCount).Select(p => p.PlanName).FirstOrDefault() ?? "—";
            ViewBag.TotalPlanRevenue = "₱" + plansList.Sum(p => p.TotalRevenue).ToString("N2");

            if (edit.HasValue)
            {
                ViewBag.EditingPlan = plansList.FirstOrDefault(p => p.Id == edit.Value);
            }

            ViewBag.FeatureComparison = null;

            return View(plansList);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPlan(string planName, string description, decimal monthlyPrice, decimal annualPrice, string features, int? maxMembers, string status)
        {
            if (string.IsNullOrWhiteSpace(planName))
            {
                TempData["ErrorMessage"] = "Plan name is required.";
                return RedirectToAction("Plans");
            }

            _context.MembershipPlans.Add(new MembershipPlan
            {
                Name = planName,
                Description = description,
                MonthlyPrice = monthlyPrice,
                AnnualPrice = annualPrice,
                Features = features,
                MaxMembers = maxMembers,
                IsActive = status != "Inactive"
            });

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Plan '{planName}' created.";
            return RedirectToAction("Plans");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePlan(int planId, string planName, string description, decimal monthlyPrice, decimal annualPrice, string features, int? maxMembers, string status)
        {
            var plan = await _context.MembershipPlans.FirstOrDefaultAsync(p => p.PlanId == planId);
            if (plan == null)
            {
                TempData["ErrorMessage"] = "Plan not found.";
                return RedirectToAction("Plans");
            }

            plan.Name = planName;
            plan.Description = description;
            plan.MonthlyPrice = monthlyPrice;
            plan.AnnualPrice = annualPrice;
            plan.Features = features;
            plan.MaxMembers = maxMembers;
            plan.IsActive = status != "Inactive";

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Plan '{planName}' updated.";
            return RedirectToAction("Plans");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePlan(int id)
        {
            var plan = await _context.MembershipPlans.FirstOrDefaultAsync(p => p.PlanId == id);
            if (plan == null)
            {
                TempData["ErrorMessage"] = "Plan not found.";
                return RedirectToAction("Plans");
            }

            plan.IsActive = false;
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Plan '{plan.Name}' deactivated.";
            return RedirectToAction("Plans");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TogglePlanStatus(int id)
        {
            var plan = await _context.MembershipPlans.FirstOrDefaultAsync(p => p.PlanId == id);
            if (plan == null)
            {
                TempData["ErrorMessage"] = "Plan not found.";
                return RedirectToAction("Plans");
            }

            plan.IsActive = !plan.IsActive;
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Plan '{plan.Name}' is now {(plan.IsActive ? "active" : "inactive")}.";
            return RedirectToAction("Plans");
        }

        [HttpGet]
        public IActionResult AddPlan() => RedirectToAction("Plans");

        [HttpGet]
        public IActionResult EditPlan(int id) => RedirectToAction("Plans", new { edit = id });

        public IActionResult Profile() => View();

        [HttpGet]
        public async Task<IActionResult> Transactions(
            string? search, string? status, string? type, string? plan,
            DateTime? dateFrom, DateTime? dateTo, int page = 1)
        {
            var query = _context.Transactions
                .Include(t => t.User).ThenInclude(u => u.Membership).ThenInclude(m => m!.Plan)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(t => t.User.FullName.Contains(search) || t.TransactionId.ToString().Contains(search));
            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(t => t.Status == status);
            if (!string.IsNullOrWhiteSpace(type))
                query = query.Where(t => t.Type == type);
            if (!string.IsNullOrWhiteSpace(plan))
                query = query.Where(t => t.User.Membership != null && t.User.Membership.Plan != null && t.User.Membership.Plan.Name == plan);
            if (dateFrom.HasValue)
                query = query.Where(t => t.Date >= dateFrom.Value);
            if (dateTo.HasValue)
                query = query.Where(t => t.Date <= dateTo.Value.AddDays(1));

            var totalCount = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));
            if (page < 1) page = 1;
            if (page > totalPages) page = totalPages;

            var transactions = await query
                .OrderByDescending(t => t.Date)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .Select(t => new
                {
                    t.TransactionId,
                    MemberId = t.UserId,
                    MemberName = t.User.FullName,
                    Plan = t.User.Membership != null && t.User.Membership.Plan != null ? t.User.Membership.Plan.Name : "—",
                    t.Type,
                    t.Amount,
                    t.PaymentMethod,
                    t.Date,
                    t.Status
                })
                .ToListAsync();

            ViewBag.TotalTransactions = totalCount;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.Search = search;
            ViewBag.StatusFilter = status;
            ViewBag.TypeFilter = type;
            ViewBag.PlanFilter = plan;
            ViewBag.DateFrom = dateFrom?.ToString("yyyy-MM-dd");
            ViewBag.DateTo = dateTo?.ToString("yyyy-MM-dd");

            var totalRevenue = await _context.Transactions
                .Where(t => t.Status == "Paid")
                .SumAsync(t => (decimal?)t.Amount) ?? 0m;
            ViewBag.TotalRevenue = "₱" + totalRevenue.ToString("N2");

            var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var monthlyRevenue = await _context.Transactions
                .Where(t => t.Status == "Paid" && t.Date >= monthStart)
                .SumAsync(t => (decimal?)t.Amount) ?? 0m;
            ViewBag.MonthlyRevenue = "₱" + monthlyRevenue.ToString("N2");

            ViewBag.PendingCount = await _context.Transactions.CountAsync(t => t.Status == "Pending");
            ViewBag.FailedCount = await _context.Transactions.CountAsync(t => t.Status == "Failed");
            ViewBag.RefundedCount = await _context.Transactions.CountAsync(t => t.Status == "Refunded");

            return View("Transaction", transactions);
        }

        [HttpGet]
        public async Task<IActionResult> TransactionDetails(int id)
        {
            var tx = await _context.Transactions
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TransactionId == id);
            if (tx == null)
            {
                TempData["ErrorMessage"] = "Transaction not found.";
                return RedirectToAction("Transactions");
            }
            TempData["InfoMessage"] = $"#{tx.TransactionId} • {tx.User.FullName} • {tx.Type} • ₱{tx.Amount:N2} • {tx.Status} • {tx.Date:MMM dd, yyyy}";
            return RedirectToAction("Transactions");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsPaid(int id)
        {
            var tx = await _context.Transactions.FirstOrDefaultAsync(t => t.TransactionId == id);
            if (tx == null)
            {
                TempData["ErrorMessage"] = "Transaction not found.";
                return RedirectToAction("Transactions");
            }
            tx.Status = "Paid";
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Transaction #{tx.TransactionId} marked as Paid.";
            return RedirectToAction("Transactions");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RefundTransaction(int id)
        {
            var tx = await _context.Transactions.FirstOrDefaultAsync(t => t.TransactionId == id);
            if (tx == null)
            {
                TempData["ErrorMessage"] = "Transaction not found.";
                return RedirectToAction("Transactions");
            }
            tx.Status = "Refunded";
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Transaction #{tx.TransactionId} refunded.";
            return RedirectToAction("Transactions");
        }
    }
}
