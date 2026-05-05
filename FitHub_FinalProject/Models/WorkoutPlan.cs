using System.ComponentModel.DataAnnotations;

namespace FitHub_FinalProject.Models
{
    public class WorkoutPlan
    {
        [Key]
        public int WorkoutPlanId { get; set; }

        public int UserId { get; set; }
        public int? TrainerId { get; set; }

        [Required, MaxLength(150)]
        public string Title { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public User User { get; set; } = null!;
        public Trainer? Trainer { get; set; }
        public ICollection<WorkoutDay> WorkoutDays { get; set; } = new List<WorkoutDay>();
    }

    public class WorkoutDay
    {
        [Key]
        public int WorkoutDayId { get; set; }

        public int WorkoutPlanId { get; set; }

        // 0=Sunday, 1=Monday … 6=Saturday
        public int DayOfWeek { get; set; }

        [MaxLength(100)]
        public string Focus { get; set; } = string.Empty; // e.g. "Chest & Triceps"

        // Navigation
        public WorkoutPlan WorkoutPlan { get; set; } = null!;
        public ICollection<Exercise> Exercises { get; set; } = new List<Exercise>();
    }

    public class Exercise
    {
        [Key]
        public int ExerciseId { get; set; }

        public int WorkoutDayId { get; set; }

        [Required, MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        public int Sets { get; set; }
        public int Reps { get; set; }

        // Navigation
        public WorkoutDay WorkoutDay { get; set; } = null!;
    }
}
