using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FitHub_FinalProject.Models
{
    public class MembershipPlan
    {
        [Key]
        public int PlanId { get; set; }

        [Required, MaxLength(50)]
        public string Name { get; set; } = string.Empty; // Basic, Pro, Elite

        [MaxLength(500)]
        public string? Description { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal MonthlyPrice { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal AnnualPrice { get; set; }

        public bool IsActive { get; set; } = true;

        // Navigation
        public ICollection<Membership> Memberships { get; set; } = new List<Membership>();
    }
}
