using System.ComponentModel.DataAnnotations;

namespace TutorLiveMentor.Models
{
    public class AdminLoginViewModel
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        public string Password { get; set; }
    }

    public class AdminDashboardViewModel
    {
        public int TotalStudents { get; set; }
        public int TotalFaculties { get; set; }
        public int TotalSubjects { get; set; }
        public int TotalEnrollments { get; set; }
        public int TotalAdmins { get; set; }
    }
}