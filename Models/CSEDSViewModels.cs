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
        
        // Faculty and Subject lists for management
        public List<Faculty> DepartmentFaculty { get; set; } = new List<Faculty>();
        public List<Subject> DepartmentSubjects { get; set; } = new List<Subject>();
        public List<SubjectFacultyMappingDto> SubjectFacultyMappings { get; set; } = new List<SubjectFacultyMappingDto>();
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
        
        // For editing purposes
        public bool IsEdit { get; set; } = false;
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

        // Available options for dropdowns with Roman numerals
        public List<int> AvailableYears { get; set; } = new List<int> { 1, 2, 3, 4 };
        public List<SemesterOption> AvailableSemesters { get; set; } = new List<SemesterOption>
        {
            new SemesterOption { Value = "I", Text = "I (1)", NumericValue = 1 },
            new SemesterOption { Value = "II", Text = "II (2)", NumericValue = 2 }
        };
        
        // For editing purposes
        public bool IsEdit { get; set; } = false;
    }

    /// <summary>
    /// Represents semester options with roman numerals
    /// </summary>
    public class SemesterOption
    {
        public string Value { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public int NumericValue { get; set; }
    }

    /// <summary>
    /// View model for faculty-subject assignment management
    /// </summary>
    public class FacultySubjectAssignmentViewModel
    {
        public int SubjectId { get; set; }
        public string SubjectName { get; set; } = string.Empty;
        public int Year { get; set; }
        public string Semester { get; set; } = string.Empty;
        
        public List<int> SelectedFacultyIds { get; set; } = new List<int>();
        public List<Faculty> AvailableFaculty { get; set; } = new List<Faculty>();
        public List<Faculty> CurrentlyAssignedFaculty { get; set; } = new List<Faculty>();
    }

    /// <summary>
    /// DTO for displaying subject-faculty mappings
    /// </summary>
    public class SubjectFacultyMappingDto
    {
        public int SubjectId { get; set; }
        public string SubjectName { get; set; } = string.Empty;
        public int Year { get; set; }
        public string Semester { get; set; } = string.Empty;
        public DateTime? SemesterStartDate { get; set; }
        public DateTime? SemesterEndDate { get; set; }
        public List<FacultyInfo> AssignedFaculty { get; set; } = new List<FacultyInfo>();
        public int EnrollmentCount { get; set; }
    }

    /// <summary>
    /// Faculty information DTO
    /// </summary>
    public class FacultyInfo
    {
        public int FacultyId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int AssignedSubjectId { get; set; }
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
        public List<SemesterOption> AvailableSemesters { get; set; } = new List<SemesterOption>
        {
            new SemesterOption { Value = "I", Text = "I (1)", NumericValue = 1 },
            new SemesterOption { Value = "II", Text = "II (2)", NumericValue = 2 }
        };

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
        public string StudentRegdNumber { get; set; } = string.Empty; // Added registration number
        public string StudentEmail { get; set; } = string.Empty;
        public string StudentYear { get; set; } = string.Empty;
        public string SubjectName { get; set; } = string.Empty;
        public string FacultyName { get; set; } = string.Empty;
        public string FacultyEmail { get; set; } = string.Empty;
        public DateTime EnrollmentDate { get; set; }
        public string Semester { get; set; } = string.Empty;
    }

    /// <summary>
    /// View model for comprehensive faculty management
    /// </summary>
    public class FacultyManagementViewModel
    {
        public List<FacultyDetailDto> DepartmentFaculty { get; set; } = new List<FacultyDetailDto>();
        public List<Subject> AvailableSubjects { get; set; } = new List<Subject>();
        public string Department { get; set; } = "CSEDS";
        public string AdminEmail { get; set; } = string.Empty;
    }

    /// <summary>
    /// Detailed faculty information with assignments
    /// </summary>
    public class FacultyDetailDto
    {
        public int FacultyId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public List<AssignedSubjectInfo> AssignedSubjects { get; set; } = new List<AssignedSubjectInfo>();
        public int TotalEnrollments { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? LastLoginDate { get; set; }
    }

    /// <summary>
    /// Assigned subject information for faculty
    /// </summary>
    public class AssignedSubjectInfo
    {
        public int AssignedSubjectId { get; set; }
        public int SubjectId { get; set; }
        public string SubjectName { get; set; } = string.Empty;
        public int Year { get; set; }
        public string Semester { get; set; } = string.Empty;
        public int EnrollmentCount { get; set; }
        public DateTime? SemesterStartDate { get; set; }
        public DateTime? SemesterEndDate { get; set; }
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// View model for comprehensive subject management
    /// </summary>
    public class SubjectManagementViewModel
    {
        public List<SubjectDetailDto> DepartmentSubjects { get; set; } = new List<SubjectDetailDto>();
        public List<Faculty> AvailableFaculty { get; set; } = new List<Faculty>();
        public string Department { get; set; } = "CSEDS";
        public string AdminEmail { get; set; } = string.Empty;
    }

    /// <summary>
    /// Detailed subject information with faculty assignments
    /// </summary>
    public class SubjectDetailDto
    {
        public int SubjectId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public int Year { get; set; }
        public string Semester { get; set; } = string.Empty;
        public DateTime? SemesterStartDate { get; set; }
        public DateTime? SemesterEndDate { get; set; }
        public List<FacultyInfo> AssignedFaculty { get; set; } = new List<FacultyInfo>();
        public int TotalEnrollments { get; set; }
        public bool IsActive { get; set; }
        public DateTime? CreatedDate { get; set; }
    }

    /// <summary>
    /// Request/Response models for API endpoints
    /// </summary>
    public class FacultySubjectAssignmentRequest
    {
        public int SubjectId { get; set; }
        public List<int> FacultyIds { get; set; } = new List<int>();
    }

    public class RemoveFacultyAssignmentRequest
    {
        public int AssignedSubjectId { get; set; }
    }

    public class BulkFacultyOperationRequest
    {
        public List<int> FacultyIds { get; set; } = new List<int>();
        public string Operation { get; set; } = string.Empty; // "delete", "activate", "deactivate"
    }

    public class BulkSubjectOperationRequest
    {
        public List<int> SubjectIds { get; set; } = new List<int>();
        public string Operation { get; set; } = string.Empty; // "delete", "activate", "deactivate"
    }

    /// <summary>
    /// Dashboard statistics summary
    /// </summary>
    public class DepartmentStatistics
    {
        public int TotalFaculty { get; set; }
        public int TotalSubjects { get; set; }
        public int TotalStudents { get; set; }
        public int TotalEnrollments { get; set; }
        public int ActiveSubjects { get; set; }
        public int AssignedSubjects { get; set; }
        public int UnassignedSubjects { get; set; }
        public double AverageEnrollmentsPerSubject { get; set; }
        public double AverageSubjectsPerFaculty { get; set; }
        public List<YearlyStatistic> YearlyBreakdown { get; set; } = new List<YearlyStatistic>();
    }

    public class YearlyStatistic
    {
        public int Year { get; set; }
        public int SubjectCount { get; set; }
        public int StudentCount { get; set; }
        public int EnrollmentCount { get; set; }
    }

    /// <summary>
    /// Enhanced search and filter options
    /// </summary>
    public class FacultySearchFilter
    {
        public string? SearchText { get; set; }
        public string? Department { get; set; }
        public bool? HasAssignments { get; set; }
        public int? MinEnrollments { get; set; }
        public int? MaxEnrollments { get; set; }
        public DateTime? CreatedAfter { get; set; }
        public DateTime? CreatedBefore { get; set; }
        public string SortBy { get; set; } = "Name"; // Name, Email, TotalEnrollments, CreatedDate
        public string SortOrder { get; set; } = "ASC"; // ASC, DESC
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    public class SubjectSearchFilter
    {
        public string? SearchText { get; set; }
        public string? Department { get; set; }
        public int? Year { get; set; }
        public string? Semester { get; set; }
        public bool? IsActive { get; set; }
        public bool? HasFacultyAssigned { get; set; }
        public int? MinEnrollments { get; set; }
        public int? MaxEnrollments { get; set; }
        public DateTime? SemesterAfter { get; set; }
        public DateTime? SemesterBefore { get; set; }
        public string SortBy { get; set; } = "Name"; // Name, Year, TotalEnrollments, SemesterStartDate
        public string SortOrder { get; set; } = "ASC"; // ASC, DESC
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    /// <summary>
    /// Validation and error handling models
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
    }

    public class OperationResult<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> Errors { get; set; } = new List<string>();
    }

    /// <summary>
    /// Import/Export models
    /// </summary>
    public class FacultyImportModel
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string AssignedSubjects { get; set; } = string.Empty; // Comma-separated subject names or IDs
    }

    public class SubjectImportModel
    {
        public string Name { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public int Year { get; set; }
        public string Semester { get; set; } = string.Empty;
        public DateTime? SemesterStartDate { get; set; }
        public DateTime? SemesterEndDate { get; set; }
        public string AssignedFaculty { get; set; } = string.Empty; // Comma-separated faculty emails
    }

    public class ExportOptions
    {
        public bool IncludeFacultyDetails { get; set; } = true;
        public bool IncludeSubjectDetails { get; set; } = true;
        public bool IncludeEnrollmentData { get; set; } = true;
        public bool IncludeStatistics { get; set; } = false;
        public string Format { get; set; } = "Excel"; // Excel, PDF, CSV
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    /// <summary>
    /// View model for comprehensive student management
    /// </summary>
    public class StudentManagementViewModel
    {
        public List<StudentDetailDto> DepartmentStudents { get; set; } = new List<StudentDetailDto>();
        public List<Subject> AvailableSubjects { get; set; } = new List<Subject>();
        public string Department { get; set; } = "CSEDS";
        public string AdminEmail { get; set; } = string.Empty;
        public List<int> AvailableYears { get; set; } = new List<int> { 1, 2, 3, 4 };
        public List<SemesterOption> AvailableSemesters { get; set; } = new List<SemesterOption>
        {
            new SemesterOption { Value = "I", Text = "I (1)", NumericValue = 1 },
            new SemesterOption { Value = "II", Text = "II (2)", NumericValue = 2 }
        };
    }

    /// <summary>
    /// Detailed student information with enrollments
    /// </summary>
    public class StudentDetailDto
    {
        public string StudentId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string RegdNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Year { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public List<EnrolledSubjectInfo> EnrolledSubjects { get; set; } = new List<EnrolledSubjectInfo>();
        public int TotalEnrollments { get; set; }
        public DateTime? LastLoginDate { get; set; }
        public DateTime? CreatedDate { get; set; }
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// Enrolled subject information for student
    /// </summary>
    public class EnrolledSubjectInfo
    {
        public int EnrollmentId { get; set; }
        public int SubjectId { get; set; }
        public string SubjectName { get; set; } = string.Empty;
        public string FacultyName { get; set; } = string.Empty;
        public string Semester { get; set; } = string.Empty;
        public int Year { get; set; }
        public DateTime? EnrollmentDate { get; set; }
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// View model for adding/editing CSEDS students
    /// </summary>
    public class CSEDSStudentViewModel
    {
        public string StudentId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Student name is required")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Registration number is required")]
        [StringLength(20, ErrorMessage = "Registration number cannot exceed 20 characters")]
        public string RegdNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [StringLength(100, ErrorMessage = "Email cannot exceed 100 characters")]
        public string Email { get; set; } = string.Empty;

        [StringLength(255)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Year is required")]
        public string Year { get; set; } = string.Empty;

        // Department is automatically set to CSEDS
        public string Department { get; set; } = "CSEDS";

        // Available options for dropdowns
        public List<string> AvailableYears { get; set; } = new List<string> { "I Year", "II Year", "III Year", "IV Year" };
        
        // For editing purposes
        public bool IsEdit { get; set; } = false;
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// Enhanced search and filter options for students
    /// </summary>
    public class StudentSearchFilter
    {
        public string? SearchText { get; set; }
        public string? Department { get; set; }
        public string? Year { get; set; }
        public string? Semester { get; set; }
        public bool? IsActive { get; set; }
        public bool? HasEnrollments { get; set; }
        public int? MinEnrollments { get; set; }
        public int? MaxEnrollments { get; set; }
        public DateTime? CreatedAfter { get; set; }
        public DateTime? CreatedBefore { get; set; }
        public string SortBy { get; set; } = "FullName"; // FullName, RegdNumber, Email, Year, TotalEnrollments, CreatedDate
        public string SortOrder { get; set; } = "ASC"; // ASC, DESC
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    /// <summary>
    /// Request/Response models for student operations
    /// </summary>
    public class BulkStudentOperationRequest
    {
        public List<string> StudentIds { get; set; } = new List<string>();
        public string Operation { get; set; } = string.Empty; // "delete", "activate", "deactivate"
    }

    /// <summary>
    /// Student import model for bulk operations
    /// </summary>
    public class StudentImportModel
    {
        public string FullName { get; set; } = string.Empty;
        public string RegdNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Year { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
    }
}