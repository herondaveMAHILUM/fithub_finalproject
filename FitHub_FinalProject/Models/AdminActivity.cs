using System.ComponentModel.DataAnnotations;

namespace FitHub_FinalProject.Models
{
    public class AdminActivity
    {
        [Key]
        public int AdminActivityId { get; set; }

        public int AdminUserId { get; set; }

        [Required, MaxLength(150)]
        public string Action { get; set; } = string.Empty;

        [MaxLength(250)]
        public string? Target { get; set; }

        [MaxLength(45)]
        public string? IpAddress { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public User Admin { get; set; } = null!;
    }
}
