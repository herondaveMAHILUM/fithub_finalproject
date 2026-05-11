using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FitHub_FinalProject.Models
{
    public class Membership
    {
        [Key]
        public int MembershipId { get; set; }

        public int UserId { get; set; }
        public int PlanId { get; set; }

        [Required, MaxLength(20)]
        public string BillingCycle { get; set; } = "monthly";

        [Required, MaxLength(20)]
        public string Status { get; set; } = "Active";

        public DateTime StartDate { get; set; }
        public DateTime ExpiryDate { get; set; }
        public DateTime NextBillingDate { get; set; }

        [MaxLength(50)]
        public string? PaymentMethod { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public User User { get; set; } = null!;
        public MembershipPlan Plan { get; set; } = null!;
    }
}
