using System.Security.Claims;
using System.Text;
using FitHub_FinalProject.Data;
using FitHub_FinalProject.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitHub_FinalProject.Controllers
{
    [Authorize]
    public class TransactionController : Controller
    {
        private const int PageSize = 10;
        private readonly FitHubDbContext _context;

        public TransactionController(FitHubDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            string? search, string? status, string? type,
            DateTime? dateFrom, DateTime? dateTo, int page = 1)
        {
            if (User.IsInRole("Admin")) return RedirectToAction("Dashboard", "Admin");

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var query = _context.Transactions.Where(t => t.UserId == userId);

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(t => t.Description.Contains(search));
            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(t => t.Status == status);
            if (!string.IsNullOrWhiteSpace(type))
                query = query.Where(t => t.Type == type);
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
                .ToListAsync();

            var allPaid = await _context.Transactions
                .Where(t => t.UserId == userId && t.Status == "Paid")
                .ToListAsync();

            ViewBag.TotalPaid = "₱" + allPaid.Sum(t => t.Amount).ToString("N2");

            var lastPaid = allPaid.OrderByDescending(t => t.Date).FirstOrDefault();
            ViewBag.LastPaymentAmount = lastPaid != null ? "₱" + lastPaid.Amount.ToString("N2") : "—";
            ViewBag.LastPaymentDate = lastPaid?.Date.ToString("MMM dd, yyyy") ?? "—";

            var membership = await _context.Memberships
                .Include(m => m.Plan)
                .FirstOrDefaultAsync(m => m.UserId == userId);

            if (membership?.Plan != null)
            {
                var nextAmount = membership.BillingCycle == "annual"
                    ? membership.Plan.AnnualPrice
                    : membership.Plan.MonthlyPrice;
                ViewBag.NextBillingAmount = "₱" + nextAmount.ToString("N2");
                ViewBag.NextBillingDate = membership.NextBillingDate.ToString("MMM dd, yyyy");
                ViewBag.PaymentMethod = membership.PaymentMethod ?? "—";
            }
            else
            {
                ViewBag.NextBillingAmount = "—";
                ViewBag.NextBillingDate = "—";
                ViewBag.PaymentMethod = "—";
            }

            ViewBag.Search = search;
            ViewBag.StatusFilter = status;
            ViewBag.TypeFilter = type;
            ViewBag.DateFrom = dateFrom?.ToString("yyyy-MM-dd");
            ViewBag.DateTo = dateTo?.ToString("yyyy-MM-dd");
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            return View("~/Views/User/Transaction.cshtml", transactions);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var tx = await _context.Transactions
                .FirstOrDefaultAsync(t => t.TransactionId == id && t.UserId == userId);

            if (tx == null)
            {
                TempData["ErrorMessage"] = "Transaction not found.";
                return RedirectToAction("Index");
            }

            TempData["InfoMessage"] =
                $"#{tx.TransactionId} • {tx.Date:MMM dd, yyyy} • {tx.Description} • ₱{tx.Amount:N2} • {tx.Status}";
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> Export(string format = "csv")
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var rows = await _context.Transactions
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.Date)
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("TransactionId,Date,Description,Type,Amount,PaymentMethod,Status");
            foreach (var t in rows)
            {
                sb.AppendLine($"{t.TransactionId},{t.Date:yyyy-MM-dd},\"{t.Description}\",{t.Type},{t.Amount},{t.PaymentMethod},{t.Status}");
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var fileName = format == "pdf" ? "transactions.pdf" : "transactions.csv";
            return File(bytes, "text/csv", fileName);
        }
    }
}
