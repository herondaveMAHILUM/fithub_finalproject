using Microsoft.EntityFrameworkCore;
using FitHub_FinalProject.Models;

namespace FitHub_FinalProject.Data
{
    public class FitHubDbContext : DbContext
    {
        public FitHubDbContext(DbContextOptions<FitHubDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<MembershipPlan> MembershipPlans { get; set; }
        public DbSet<Membership> Memberships { get; set; }
        public DbSet<Trainer> Trainers { get; set; }
        public DbSet<WorkoutPlan> WorkoutPlans { get; set; }
        public DbSet<WorkoutDay> WorkoutDays { get; set; }
        public DbSet<Exercise> Exercises { get; set; }
        public DbSet<Transactions> Transactions { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<AdminActivity> AdminActivities { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<Membership>()
                .HasOne(m => m.User)
                .WithOne(u => u.Membership)
                .HasForeignKey<Membership>(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<User>()
                .HasOne(u => u.AssignedTrainer)
                .WithMany(t => t.AssignedMembers)
                .HasForeignKey("TrainerId")
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Transactions>()
                .HasOne(t => t.User)
                .WithMany(u => u.Transactions)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<WorkoutPlan>()
                .HasOne(wp => wp.User)
                .WithMany(u => u.WorkoutPlans)
                .HasForeignKey(wp => wp.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<WorkoutPlan>()
                .HasOne(wp => wp.Trainer)
                .WithMany(t => t.WorkoutPlans)
                .HasForeignKey(wp => wp.TrainerId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<WorkoutDay>()
                .HasOne(wd => wd.WorkoutPlan)
                .WithMany(wp => wp.WorkoutDays)
                .HasForeignKey(wd => wd.WorkoutPlanId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Exercise>()
                .HasOne(e => e.WorkoutDay)
                .WithMany(wd => wd.Exercises)
                .HasForeignKey(e => e.WorkoutDayId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Membership>()
                .HasOne(m => m.Plan)
                .WithMany(p => p.Memberships)
                .HasForeignKey(m => m.PlanId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AdminActivity>()
                .HasOne(a => a.Admin)
                .WithMany()
                .HasForeignKey(a => a.AdminUserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<User>().HasData(
                new User
                {
                    UserId = 1,
                    FullName = "FitHub Admin",
                    Email = "admin@fithub.ph",
                    PasswordHash = "JAvlGPq9JyTdtvBO6x2llnRI1+gxwIyPqCKAn3THIKk=",
                    IsActive = true,
                    IsAdmin = true,
                    CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                }
            );

            modelBuilder.Entity<MembershipPlan>().HasData(
                new MembershipPlan
                {
                    PlanId = 1,
                    Name = "Basic",
                    Description = "Perfect for beginners getting started on their fitness journey.",
                    MonthlyPrice = 499,
                    AnnualPrice = 4790
                },
                new MembershipPlan
                {
                    PlanId = 2,
                    Name = "Pro",
                    Description = "For dedicated members who want more access and support.",
                    MonthlyPrice = 899,
                    AnnualPrice = 8630
                },
                new MembershipPlan
                {
                    PlanId = 3,
                    Name = "Elite",
                    Description = "The full FitHub experience for serious athletes.",
                    MonthlyPrice = 1499,
                    AnnualPrice = 14390
                }
            );
        }
    }
}
