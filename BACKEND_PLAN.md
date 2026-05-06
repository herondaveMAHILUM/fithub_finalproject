# FitHub Backend Implementation Plan (v2 — Audited)

**Project:** ASP.NET Core 10.0 MVC (FitHub_FinalProject)
**Stack:** C#, Razor Views, Entity Framework Core 10, SQL Server LocalDB
**Goal:** Make every page in the app functional — authentication, profile, membership, transactions, dashboard.

---

## What Changed From v1

The v1 plan didn't match the actual views. After auditing every `.cshtml` file, the corrected scope is:

- **The Login/Register/Profile forms have NO `name` attributes** on their inputs. Form posts arrive empty without a tiny view edit. This plan adds `name="..."` to existing inputs only — no CSS, layout, or content changes.
- **Forms post to `Account/...` and to two new controllers** (`MembershipController`, `TransactionController`), not to `User/...` as v1 assumed.
- **ViewBag keys** must match the views exactly (`FullName`, `MembershipPlan`, `Billing`, `HasActiveMembership`, etc., not `ViewBag.User`).
- **Compile-error fixes:** `User.PhoneNumber` (not `Phone`), `User.DateOfBirth` is `DateOnly?` (not `DateTime`), `MembershipPlan.AnnualPrice` (not `YearlyPrice`), `User.AssignedTrainer` (not `Trainer`), billing cycle string is `"annual"` (not `"Yearly"`).
- **`DateTime.UtcNow`** everywhere, matching model defaults.
- **Null-guard the Dashboard** when a stale cookie outlives a deleted user.

---

## Ground Truth Reference

### Models

| Model | Key Fields |
|---|---|
| `User` | `UserId`, `FullName`, `Email`, `PasswordHash`, `PhoneNumber?`, `DateOfBirth (DateOnly?)`, `Gender?`, `Address?`, `ProfilePhotoPath?`, `IsActive`, `CreatedAt`, nav: `Membership`, `AssignedTrainer`, `Transactions`, `Notifications`, `WorkoutPlans` |
| `MembershipPlan` | `PlanId`, `Name`, `Description?`, `MonthlyPrice`, `AnnualPrice`, `IsActive` |
| `Membership` | `MembershipId`, `UserId`, `PlanId`, `BillingCycle ("monthly"\|"annual")`, `Status ("Active"\|"Expired"\|"Cancelled"\|"Frozen")`, `StartDate`, `ExpiryDate`, `NextBillingDate`, `PaymentMethod?`, `CreatedAt` |
| `Transaction` | `TransactionId`, `UserId`, `Description`, `Type ("Subscription"\|"Upgrade"\|"Renewal"\|"Refund")`, `Amount`, `PaymentMethod`, `Status ("Paid"\|"Pending"\|"Failed"\|"Refunded")`, `Date` |
| `Notification` | `NotificationId`, `UserId`, `Title`, `Description?`, `Type ("Info"\|"Warning"\|"Success"\|"Alert")`, `IsRead`, `CreatedAt` |
| `Trainer` | `TrainerId`, `FullName`, `Email?`, `PhoneNumber?`, `PhotoPath?`, `IsActive` |
| `WorkoutPlan` / `WorkoutDay` / `Exercise` | as defined in models |

### Seed Data (already in DB)

- Plan 1 = **Basic** — ₱499 / ₱4,790
- Plan 2 = **Pro** — ₱899 / ₱8,630
- Plan 3 = **Elite** — ₱1,499 / ₱14,390

### View → Controller/Action Map

| View Element | URL Target |
|---|---|
| Login form | `Account/Login (POST)` |
| Register form | `Account/Register (POST)` |
| "Forgot your password" link | `Account/ForgotPassword (GET)` |
| Logout link (dashboard quick action) | `Account/Logout (GET)` |
| Profile "Edit Profile" link | `Account/Profile (GET)` |
| Profile personal-info form | `Account/UpdateProfile (POST)` |
| Profile password form | `Account/ChangePassword (POST)` |
| Profile "Deactivate Account" form | `Account/DeactivateAccount (POST)` |
| Profile "Delete Account" form | `Account/DeleteAccount (POST)` |
| Dashboard "Change Password" quick action | `Account/ChangePassword (GET)` |
| Membership page | `Membership/Index (GET)` |
| "Get Basic/Pro/Elite" anchor | `Membership/Subscribe?planId=X (GET)` |
| "Cancel Membership" anchor | `Membership/Cancel (GET)` |
| Billing toggle form | `Membership/Index?billing=monthly\|annual (GET)` |
| Transaction page | `Transaction/Index (GET)` |
| Transaction filter form | `Transaction/Index (GET)` with query params |
| Transaction "View" link | `Transaction/Details/{id} (GET)` |
| Transaction "Download as PDF/CSV" | `Transaction/Export?format=pdf\|csv (GET)` |
| Home links to Terms / Contact | `Home/Terms (GET)`, `Home/Contact (GET)` |

### ViewBag Contract (what views read)

| View | ViewBag keys |
|---|---|
| `Home/Index` | `TotalMembers`, `TotalEquipmentTypes`, `DaysOpen`, `TotalTrainers`, `Testimonials` |
| `User/Dashboard` | `FullName`, `MemberSince`, `MembershipStatus`, `MembershipPlan`, `ExpiryDate`, `NextBillingDate`, `TodayWorkoutLabel`, `TodayExercises`, `WeeklySchedule` (`IsToday`, `ShortName`, `Focus`), `TrainerName`, `NotificationCount`, `Notifications` (`Type`, `Title`, `Description`, `TimeAgo`), `RecentTransactions` |
| `User/Profile` | `FullName`, `MembershipPlan`, `MemberSince`, `MembershipStatus`, `NextBillingDate`, `MonthlyFee`, `PaymentMethod` |
| `User/Membership` | `HasActiveMembership`, `CurrentPlan`, `NextBillingDate`, `Billing` (`"monthly"\|"annual"`) |
| `User/Transaction` | Model = `List<Transaction>`, plus `TotalPaid`, `LastPaymentAmount`, `LastPaymentDate`, `NextBillingAmount`, `NextBillingDate`, `PaymentMethod`, `Search`, `DateFrom`, `DateTo`, `CurrentPage`, `TotalPages`, `StatusFilter`, `TypeFilter` |

---

## Constraints

- Do not touch CSS, layouts, models, migrations, or seed data.
- View edits are limited to **adding `name="..."` to existing form inputs** — no other markup change.
- Use `async/await` with EF Core async methods everywhere DB is touched.
- Use `DateTime.UtcNow` (matches model defaults).
- Password hashing = SHA-256 (per project rules — no external libraries).
- `[ValidateAntiForgeryToken]` on every POST.
- `[Authorize]` on every controller that handles authenticated user data.
- BillingCycle values are exactly `"monthly"` or `"annual"` (lowercase).

---

# Commit Plan (14 commits)

Each commit below is one self-contained unit. The history shows: setup → auth → public stats → user data pages → polish.

---

## Commit 1 — Configure cookie authentication and middleware ✅ DONE (`823129b`)

**File:** `Program.cs`

Add `using` directive at top:

```csharp
using Microsoft.AspNetCore.Authentication.Cookies;
```

Insert before `builder.Build();`:

```csharp
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();
```

Replace `app.UseAuthorization();` with:

```csharp
app.UseAuthentication();
app.UseAuthorization();
```

**Commit message:** `Configure cookie authentication and middleware`

---

## Commit 2 — Implement login ✅ DONE (`ff5c4ac`)

### View edit — `Views/Account/Login.cshtml`

Add `name` attributes (only — nothing else changes):

| Input | Add |
|---|---|
| Email input | `name="email"` |
| Password input | `name="password"` |
| Remember-me checkbox | `name="rememberMe" value="true"` |

### Controller — `Controllers/AccountController.cs`

Replace the file with:

```csharp
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

        public AccountController(FitHubDbContext context)
        {
            _context = context;
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

        public IActionResult Register() => View();

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
```

**Commit message:** `Implement login with cookie auth and form binding`

---

## Commit 3 — Implement registration ✅ DONE (`720276d`)

### View edit — `Views/Account/Register.cshtml`

Add `name` attributes:

| Input | Add |
|---|---|
| Full Name | `name="fullName"` |
| Email | `name="email"` |
| Phone | `name="phoneNumber"` |
| Date of Birth | `name="dateOfBirth"` |
| Gender select | `name="gender"` |
| Address | `name="address"` |
| Password | `name="password"` |
| Confirm Password | `name="confirmPassword"` |

### AccountController additions

Replace the placeholder `Register()` with:

```csharp
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
    string fullName, string email, string phoneNumber,
    DateOnly? dateOfBirth, string gender, string address,
    string password, string confirmPassword)
{
    if (string.IsNullOrWhiteSpace(fullName) ||
        string.IsNullOrWhiteSpace(email) ||
        string.IsNullOrWhiteSpace(password))
    {
        ModelState.AddModelError("", "Full name, email, and password are required.");
    }

    if (password != confirmPassword)
        ModelState.AddModelError("", "Passwords do not match.");

    if (password?.Length < 8)
        ModelState.AddModelError("", "Password must be at least 8 characters.");

    if (await _context.Users.AnyAsync(u => u.Email == email))
        ModelState.AddModelError("", "An account with this email already exists.");

    if (!ModelState.IsValid)
        return View();

    var user = new User
    {
        FullName = fullName,
        Email = email,
        PhoneNumber = phoneNumber,
        DateOfBirth = dateOfBirth,
        Gender = gender,
        Address = address,
        PasswordHash = HashPassword(password!),
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
```

**Commit message:** `Implement registration with validation and welcome notification`

---

## Commit 4 — Implement logout ✅ DONE (`9dec94a`)

Add to `AccountController`:

```csharp
[Authorize]
public async Task<IActionResult> Logout()
{
    await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return RedirectToAction("Login");
}
```

**Commit message:** `Implement logout`

---

## Commit 5 — Add live stats and stub pages to HomeController ✅ DONE (`20c820e`)

### `Controllers/HomeController.cs`

Inject DbContext and populate real counts. Add stub `Terms` and `Contact` actions linked from views (return `Privacy` view as placeholder, or a friendly redirect — see below).

```csharp
using System.Diagnostics;
using FitHub_FinalProject.Data;
using FitHub_FinalProject.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitHub_FinalProject.Controllers
{
    public class HomeController : Controller
    {
        private readonly FitHubDbContext _context;

        public HomeController(FitHubDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.TotalMembers = await _context.Users.CountAsync(u => u.IsActive);
            ViewBag.TotalTrainers = await _context.Trainers.CountAsync(t => t.IsActive);
            ViewBag.TotalEquipmentTypes = 20;
            ViewBag.DaysOpen = 365;
            ViewBag.Testimonials = null;
            return View();
        }

        public IActionResult Privacy() => View();

        public IActionResult Terms() => View("Privacy");

        public IActionResult Contact()
        {
            TempData["InfoMessage"] = "Contact us at support@fithub.ph";
            return RedirectToAction("Index");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() =>
            View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
```

**Commit message:** `Populate Home with live stats and add Terms/Contact stubs`

---

## Commit 6 — Implement Dashboard ✅ DONE (`e9888cf`)

### `Controllers/UserController.cs`

Strip down to just `Dashboard` (the other actions move to dedicated controllers in later commits). Replace contents with:

```csharp
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

            // auto-expire stale memberships
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

            // Workout
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

            // Notifications
            var rawNotifs = await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(5)
                .ToListAsync();

            ViewBag.Notifications = rawNotifs.Select(n => new
            {
                n.Type,
                n.Title,
                Description = n.Description ?? "",
                TimeAgo = TimeAgo(n.CreatedAt)
            }).ToList();

            ViewBag.NotificationCount = await _context.Notifications
                .CountAsync(n => n.UserId == userId && !n.IsRead);

            // Recent transactions
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
```

**Commit message:** `Implement Dashboard with workout, notifications, and recent transactions`

---

## Commit 7 — Implement Profile (view + update) ✅ DONE (`91fb75a`)

### View edit — `Views/User/Profile.cshtml` (personal-info form only)

Add `name` attributes:

| Input | Add |
|---|---|
| Full Name | `name="fullName"` |
| Email | `name="email"` (read-only field — keep but `disabled` can stay; we won't update it) |
| Phone Number | `name="phoneNumber"` |
| Date of Birth | `name="dateOfBirth"` |
| Gender select | `name="gender"` |
| Address | `name="address"` |

### `AccountController` additions

```csharp
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
    DateOnly? dateOfBirth, string gender, string address)
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

    await _context.SaveChangesAsync();
    TempData["SuccessMessage"] = "Profile updated successfully.";
    return RedirectToAction("Profile");
}
```

**Commit message:** `Implement profile view and update`

---

## Commit 8 — Implement ChangePassword ✅ DONE (`f484988`)

### View edit — `Views/User/Profile.cshtml` (change-password form)

| Input | Add |
|---|---|
| Current Password | `name="currentPassword"` |
| New Password | `name="newPassword"` |
| Confirm New Password | `name="confirmPassword"` |

### `AccountController` additions

```csharp
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
```

**Commit message:** `Implement password change with verification and notification`

---

## Commit 9 — Implement Deactivate / Delete account + ForgotPassword stub ✅ DONE (`15725ea`)

### `AccountController` additions

```csharp
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
        _context.Users.Remove(user); // cascades to memberships, transactions, notifications, workouts
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
```

**Commit message:** `Implement account deactivate/delete and forgot-password stub`

---

## Commit 10 — Add MembershipController ✅ DONE (`e8b1202`)

### New file — `Controllers/MembershipController.cs`

```csharp
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
```

### View edit — `Views/User/Membership.cshtml`

For the three "Get [Plan]" anchor tags, add `asp-route-billing="@ViewBag.Billing"` so the chosen toggle is preserved:

```html
<a asp-controller="Membership" asp-action="Subscribe"
   asp-route-planId="1" asp-route-billing="@ViewBag.Billing">Get Basic</a>
```

(Same for plans 2 and 3.)

**Commit message:** `Add MembershipController with subscribe and cancel`

---

## Commit 11 — Add TransactionController ✅ DONE (`f307363`)

### New file — `Controllers/TransactionController.cs`

```csharp
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

            // summary (across all of user's transactions, unfiltered)
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
```

**Commit message:** `Add TransactionController with filters, pagination, details, and CSV export`

---

## Commit 12 — Build verification and end-to-end smoke test

Run from project root:

```powershell
dotnet build
dotnet run
```

Walk every flow:

1. **Register** a new account — verify auto-login + welcome notification visible on dashboard.
2. **Logout** — verify redirect to Login.
3. **Login** with same credentials — verify dashboard loads.
4. **Dashboard** — confirm Full Name, Member Since, Plan = "—", workout schedule shows 7 day pills, today is highlighted, recent transactions empty.
5. **Membership/Index** — verify both billing toggles, PlanIds 1/2/3 show. Click "Get Pro". Verify redirect to Dashboard, Plan now = "Pro", Status = "Active".
6. **Transaction/Index** — verify the Subscription transaction appears, TotalPaid populated, NextBilling populated. Try search/status/type/date filters. Pagination works (subscribe more times to populate).
7. **Transaction/Export?format=csv** — verify CSV download.
8. **Profile** — update phone/address, verify success message + persistence after refresh.
9. **ChangePassword** — change password, log out, log back in with new password.
10. **DeactivateAccount** — verify forced logout, then login fails (`IsActive = false` excludes the user).
11. Re-register a fresh account, then **DeleteAccount** — verify forced logout, account is gone.
12. Visit `/User/Dashboard` while logged out — confirm redirect to `/Account/Login`.

**Commit message:** `Build and end-to-end smoke test pass`

---

# Commit History Summary (12 commits)

| #  | Commit | Status |
|----|--------|--------|
| 1  | Configure cookie authentication and middleware | ✅ `823129b` |
| 2  | Implement login with cookie auth and form binding | ✅ `ff5c4ac` |
| 3  | Implement registration with validation and welcome notification | ✅ `720276d` |
| 4  | Implement logout | ✅ `9dec94a` |
| 5  | Populate Home with live stats and add Terms/Contact stubs | ✅ `20c820e` |
| 6  | Implement Dashboard with workout, notifications, and recent transactions | ✅ `e9888cf` |
| 7  | Implement profile view and update | ✅ `91fb75a` |
| 8  | Implement password change with verification and notification | ✅ `f484988` |
| 9  | Implement account deactivate/delete and forgot-password stub | ✅ `15725ea` |
| 10 | Add MembershipController with subscribe and cancel | ✅ `e8b1202` |
| 11 | Add TransactionController with filters, pagination, details, and CSV export | ✅ `f307363` |
| 12 | Build and end-to-end smoke test pass | ⬜ |

---

# Known Limitations (Acknowledged)

These are accepted trade-offs for an academic-scope project — not bugs:

- **SHA-256 unsalted** is weak vs. rainbow tables; ASP.NET's built-in `PasswordHasher<T>` is a stronger drop-in if the rules permit it later.
- **`Subscribe` and `Cancel` are GET** to match anchor links in the views; in production these would be POST behind a confirmation form.
- **PDF export returns CSV** — wiring up a real PDF generator is out of scope.
- **No payment gateway** — `Status = "Paid"` is set immediately on subscribe.
- **`ForgotPassword`** is a redirect/notice, not a real recovery flow.
- **Profile photo upload** is in the form but not stored — out of scope.

---

**End of plan.** Following commits 1–12 in order yields a fully working backend, with each commit a coherent unit your professor can step through.
