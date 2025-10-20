using System.ComponentModel.DataAnnotations;

namespace TutorLiveMentor.Models
{
    /// <summary>
    /// View model for CSEDS department dashboard statistics and data
    /// </summary>
    public class CSEDSDashboardViewModel
    {
        // Department-specific statistics
        public int CSEDSStudentsCount { get; set; }
        public int CSEDSFacultyCount { get; set; }
        public int CSEDSSubjectsCount { get; set; }
        public int CSEDSEnrollmentsCount { get; set; }

        // Admin information
        public string AdminEmail { get; set; } = string.Empty;
        public string AdminDepartment { get; set; } = string.Empty;

        // Recent activity data
        public List<StudentActivityDto> RecentStudents { get; set; } = new List<StudentActivityDto>();
        public List<EnrollmentActivityDto> RecentEnrollments { get; set; } = new List<EnrollmentActivityDto>();
    }

    /// <summary>
    /// DTO for student activity information
    /// </summary>
    public class StudentActivityDto
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Year { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO for enrollment activity information
    /// </summary>
    public class EnrollmentActivityDto
    {
        public string StudentName { get; set; } = string.Empty;
        public string SubjectName { get; set; } = string.Empty;
        public string FacultyName { get; set; } = string.Empty;
        public DateTime EnrollmentDate { get; set; }
    }

    /// <summary>
    /// View model for adding/editing CSEDS faculty members
    /// </summary>
    public class CSEDSFacultyViewModel
    {
        public int FacultyId { get; set; }

        [Required(ErrorMessage = "Faculty name is required")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [StringLength(100, ErrorMessage = "Email cannot exceed 100 characters")]
        public string Email { get; set; } = string.Empty;

        [StringLength(255)]
        public string Password { get; set; } = string.Empty;

        // Department is automatically set to CSEDS
        public string Department { get; set; } = "CSEDS";

        // List of subjects this faculty can be assigned to
        public List<int> SelectedSubjectIds { get; set; } = new List<int>();
        public List<Subject> AvailableSubjects { get; set; } = new List<Subject>();
    }

    /// <summary>
    /// View model for adding/editing CSEDS subjects
    /// </summary>
    public class CSEDSSubjectViewModel
    {
        public int SubjectId { get; set; }

        [Required(ErrorMessage = "Subject name is required")]
        [StringLength(100, ErrorMessage = "Subject name cannot exceed 100 characters")]
        public string Name { get; set; } = string.Empty;

        // Department is automatically set to CSEDS
        public string Department { get; set; } = "CSEDS";

        [Required(ErrorMessage = "Year is required")]
        public int Year { get; set; }

        [Required(ErrorMessage = "Semester is required")]
        public string Semester { get; set; } = string.Empty;

        [Required(ErrorMessage = "Semester start date is required")]
        [DataType(DataType.Date)]
        public DateTime SemesterStartDate { get; set; }

        [Required(ErrorMessage = "Semester end date is required")]
        [DataType(DataType.Date)]
        public DateTime SemesterEndDate { get; set; }

        // Available options for dropdowns
        public List<int> AvailableYears { get; set; } = new List<int> { 1, 2, 3, 4 };
        public List<string> AvailableSemesters { get; set; } = new List<string> { "Fall", "Spring", "Summer" };
    }

    /// <summary>
    /// View model for CSEDS reports and analytics
    /// </summary>
    public class CSEDSReportsViewModel
    {
        // Filter criteria
        public int? SelectedSubjectId { get; set; }
        public int? SelectedFacultyId { get; set; }
        public int? SelectedYear { get; set; }
        public string? SelectedSemester { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        // Available filter options
        public List<Subject> AvailableSubjects { get; set; } = new List<Subject>();
        public List<Faculty> AvailableFaculty { get; set; } = new List<Faculty>();
        public List<int> AvailableYears { get; set; } = new List<int> { 1, 2, 3, 4 };
        public List<string> AvailableSemesters { get; set; } = new List<string> { "Fall", "Spring", "Summer" };

        // Report results
        public List<EnrollmentReportDto> ReportResults { get; set; } = new List<EnrollmentReportDto>();
        public int TotalRecords { get; set; }
    }

    /// <summary>
    /// DTO for enrollment report data
    /// </summary>
    public class EnrollmentReportDto
    {
        public string StudentName { get; set; } = string.Empty;
        public string StudentEmail { get; set; } = string.Empty;
        public string StudentYear { get; set; } = string.Empty;
        public string SubjectName { get; set; } = string.Empty;
        public string FacultyName { get; set; } = string.Empty;
        public string FacultyEmail { get; set; } = string.Empty;
        public DateTime EnrollmentDate { get; set; }
        public string Semester { get; set; } = string.Empty;
    }
}