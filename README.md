# FitHub

----

## Quick start (first time setup)

Follow these steps in order.

### 1. Install the tools you need

You need three things on your machine:

**a) .NET 10 SDK**
Download and install from https://dotnet.microsoft.com/download

After installing, open a new terminal and check it works:
```
dotnet --version
```
You should see `10.0.x` or similar.

**b) SQL Server LocalDB**
This is the database engine. You probably already have it if you have Visual Studio installed.
Check by running:
```
sqllocaldb info
```
If you see `mssqllocaldb` or `MSSQLLocalDB` in the output, you're good. If not, install it from:
https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/sql-server-express-localdb

**c) EF Core CLI tool**
Run this once on your machine — it installs the `dotnet ef` command we use to set up the database:
```
dotnet tool install --global dotnet-ef
```

### 2. Clone the repo

```
git clone https://github.com/herondaveMAHILUM/fithub_finalproject.git
cd fithub_finalproject
```

### 3. Create the database

From the repo root (the folder you just cd'd into), run:

```
dotnet ef database update --project FitHub_FinalProject
```

This creates a database called `FitHubDb` on your local machine and sets up all the tables. It also pre-loads the three membership plans (Basic / Pro / Elite).

You only need to do this once. If anyone adds new migrations later, you just run the same command again to pick them up.

### 4. Run the app

```
cd FitHub_FinalProject
dotnet run --launch-profile https
```

Open https://localhost:7120 in your browser. You'll get a "this site is not secure" warning the first time — that's just because it's a local dev cert. Click "Advanced" → "Proceed".

To stop the server, hit `Ctrl+C` in the terminal.

---

## How to use the app

There are no pre-made accounts. **Register a new account** through the Sign Up page when you first open the site.

Once registered you can:
- View your dashboard (membership status, today's workout, notifications, recent transactions)
- Browse membership plans and subscribe to one (Basic / Pro / Elite, monthly or annual)
- Cancel your membership anytime
- View your transaction history with filters and CSV export
- Edit your profile info (name, phone, DOB, gender, address)
- Change your password
- Deactivate or delete your account

**IF you want to wipe the database and start fresh**
```
dotnet ef database drop --project FitHub_FinalProject --force
dotnet ef database update --project FitHub_FinalProject
```

---

## Project layout

| Folder | What's in it |
|---|---|
| `Controllers/` | `Account`, `Home`, `User`, `Membership`, `Transaction` — handle all incoming requests |
| `Models/` | Entity classes (`User`, `Membership`, `Transaction`, etc.) |
| `Data/FitHubDbContext.cs` | EF Core database context + the seed data for membership plans |
| `Migrations/` | Auto-generated EF migrations — don't edit these by hand |
| `Views/` | Razor pages, organized by controller |
| `wwwroot/` | Static files (CSS, images, JavaScript) |
| `Program.cs` | App startup, dependency injection, auth setup |
| `appsettings.json` | Connection string lives here |

---

## Database connection string

If the default LocalDB instance doesn't work for you, edit this line in `FitHub_FinalProject/appsettings.json`:

```
"FitHubDb": "Server=(localdb)\\mssqllocaldb;Database=FitHubDb;Trusted_Connection=True;MultipleActiveResultSets=true"
```

Change `(localdb)\\mssqllocaldb` to whatever your local SQL Server instance is named.
