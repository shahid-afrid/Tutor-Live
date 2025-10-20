using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace TutorLiveMentor.Models
{
    /// <summary>
    /// Faculty model representing teaching staff members in the system
    /// </summary>
    public class Faculty
    {
        [Key]
        public int FacultyId { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string Password { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Department { get; set; } = string.Empty;

        // Navigation property for assigned subjects
        public virtual ICollection<AssignedSubject>? AssignedSubjects { get; set; }
    }
}