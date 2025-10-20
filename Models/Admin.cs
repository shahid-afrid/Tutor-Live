using System.ComponentModel.DataAnnotations;

namespace TutorLiveMentor.Models
{
    public class Admin
    {
        [Key]
        public int AdminId { get; set; }

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
    }
}