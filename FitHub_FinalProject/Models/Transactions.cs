using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FitHub_FinalProject.Models
{
    public class Transactions
    {
        [Key]
        public int TransactionId { get; set; }

        public int UserId { get; set; }

        [Required, MaxLength(200)]
        public string Description { get; set; } = string.Empty;

        [Required, MaxLength(30)]
        public string Type { get; set; } = string.Empty;

        [Column(TypeName = "decimal(10,2)")]
        public decimal Amount { get; set; }

        [Required, MaxLength(50)]
        public string PaymentMethod { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string Status { get; set; } = "Paid";

        public DateTime Date { get; set; } = DateTime.UtcNow;

        public User User { get; set; } = null!;
    }
}
