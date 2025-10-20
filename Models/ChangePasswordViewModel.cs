using System.ComponentModel.DataAnnotations;

namespace TutorLiveMentor.Models
{
    public class ChangePasswordViewModel
    {
        public string StudentId { get; set; } // Changed from int to string
        public string StudentName { get; set; }

        [Required(ErrorMessage = "Current password is required")]
        [Display(Name = "Current Password")]
        public string CurrentPassword { get; set; }

        [Required(ErrorMessage = "New password is required")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters long")]
        [Display(Name = "New Password")]
        public string NewPassword { get; set; }

        [Required(ErrorMessage = "Please confirm your new password")]
        [Compare("NewPassword", ErrorMessage = "New password and confirmation password do not match")]
        [Display(Name = "Confirm New Password")]
        public string ConfirmPassword { get; set; }
    }
}