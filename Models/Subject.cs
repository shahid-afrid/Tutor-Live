using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TutorLiveMentor.Models
{
    public class Subject
    {
        public int SubjectId { get; set; }
        
        [Required]
        public string Name { get; set; }
        
        [Required]
        public string Department { get; set; }

        // Additional fields for CSEDS semester management
        public int Year { get; set; } = 1;
        
        public string Semester { get; set; } = string.Empty;
        
        public DateTime? SemesterStartDate { get; set; }
        
        public DateTime? SemesterEndDate { get; set; }

        // Navigation property
        public virtual ICollection<AssignedSubject> AssignedSubjects { get; set; }
    }
}
