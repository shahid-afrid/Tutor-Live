using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace TutorLiveMentor.Models
{
    public class Student
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)] // Remove auto-increment
        public string Id { get; set; } // Changed from int to string

        [Required]
        public string FullName { get; set; }

        [Required]
        [StringLength(10)]
        public string RegdNumber { get; set; }

        [Required]
        public string Year { get; set; }

        [Required]
        public string Department { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string Password { get; set; }

        public string SelectedSubject { get; set; } = string.Empty;

        // Add the new collection for multiple enrollments
        public virtual ICollection<StudentEnrollment> Enrollments { get; set; } = new List<StudentEnrollment>();

        // Helper method to get enrolled subjects as a formatted string
        public string GetEnrolledSubjectsDisplay()
        {
            if (Enrollments == null || !Enrollments.Any())
                return "No subjects enrolled";

            return string.Join(", ", Enrollments.Select(e => $"{e.AssignedSubject.Subject.Name} ({e.AssignedSubject.Faculty.Name})"));
        }
    }
}
