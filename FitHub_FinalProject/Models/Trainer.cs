using System.ComponentModel.DataAnnotations;

namespace FitHub_FinalProject.Models
{
    public class Trainer
    {
        [Key]
        public int TrainerId { get; set; }

        [Required, MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [MaxLength(150)]
        public string? Email { get; set; }

        [MaxLength(20)]
        public string? PhoneNumber { get; set; }

        public string? PhotoPath { get; set; }

        public bool IsActive { get; set; } = true;

        // Navigation
        public ICollection<User> AssignedMembers { get; set; } = new List<User>();
        public ICollection<WorkoutPlan> WorkoutPlans { get; set; } = new List<WorkoutPlan>();
    }
}
