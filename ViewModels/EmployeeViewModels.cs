using System.ComponentModel.DataAnnotations;

namespace MvcMusic.ViewModels
{
    public class EmployeeCreateViewModel
    {
        [Required(ErrorMessage = "First Name is required.")]
        [Display(Name = "First Name")]
        public required string FirstName { get; set; }

        [Required(ErrorMessage = "Last Name is required.")]
        [Display(Name = "Last Name")]
        public required string LastName { get; set; }

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress]
        [Display(Name = "Email")]
        public required string Email { get; set; }

        [Required(ErrorMessage = "Role is required.")]
        [Display(Name = "Role")]
        public required string Role { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public required string Password { get; set; }
    }

    public class EmployeeEditViewModel
    {
        public required string Id { get; set; }

        [Required]
        [Display(Name = "First Name")]
        public required string FirstName { get; set; }

        [Required]
        [Display(Name = "Last Name")]
        public required string LastName { get; set; }

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public required string Email { get; set; }

        [Required]
        [Display(Name = "Role")]
        public required string Role { get; set; }

        // Read-only display
        public string? UserName { get; set; }
    }

    public class ResetPasswordViewModel
    {
        public required string Id { get; set; }
        public string? UserName { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public required string NewPassword { get; set; }
    }
}
