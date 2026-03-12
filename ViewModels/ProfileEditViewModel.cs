using System.ComponentModel.DataAnnotations;

namespace MvcMusic.ViewModels
{
    public class ProfileEditViewModel
    {
        public string? UserName { get; set; }       // read-only
        public string? ProfilePicture { get; set; }

        [Required(ErrorMessage = "First Name is required.")]
        [Display(Name = "First Name")]
        public required string FirstName { get; set; }

        [Required(ErrorMessage = "Last Name is required.")]
        [Display(Name = "Last Name")]
        public required string LastName { get; set; }

        [Display(Name = "Profile Picture")]
        public IFormFile? NewProfilePicture { get; set; }
    }
}
