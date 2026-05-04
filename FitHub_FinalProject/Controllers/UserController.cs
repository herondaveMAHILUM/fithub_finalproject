using Microsoft.AspNetCore.Mvc;

namespace FitHub_FinalProject.Controllers
{
    public class UserController : Controller
    {
        public IActionResult Dashboard()
        {
            ViewBag.FullName = null;
            ViewBag.MemberSince = null;
            ViewBag.MembershipStatus = null;
            ViewBag.MembershipPlan = null;
            ViewBag.ExpiryDate = null;
            ViewBag.NextBillingDate = null;

            ViewBag.TrainerName = null;

            ViewBag.TodayWorkoutLabel = null;
            ViewBag.TodayExercises = null;
            ViewBag.WeeklySchedule = null;

            ViewBag.Notifications = null;
            ViewBag.NotificationCount = 0;

            ViewBag.RecentTransactions = null;

            return View();
        }

        public IActionResult Membership()
        {
            return View();
        }

        public IActionResult Profile()
        {
            ViewBag.FullName = null;
            ViewBag.MembershipPlan = null;
            ViewBag.MembershipStatus = null;
            ViewBag.MemberSince = null;
            ViewBag.NextBillingDate = null;
            ViewBag.MonthlyFee = null;
            ViewBag.PaymentMethod = null;

            return View();
        }

        public IActionResult Transaction()
        {
            ViewBag.TotalPaid = null;
            ViewBag.LastPaymentAmount = null;
            ViewBag.LastPaymentDate = null;
            ViewBag.NextBillingAmount = null;
            ViewBag.NextBillingDate = null;
            ViewBag.PaymentMethod = null;

            return View();
        }
    }
}
