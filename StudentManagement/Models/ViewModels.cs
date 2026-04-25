using System.ComponentModel.DataAnnotations;

namespace StudentManagement.Models
{
    public class StudentViewModel
    {
        public string? Id { get; set; }

        [Required(ErrorMessage = "First name is required")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "First name must be between 2 and 50 characters")]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Last name must be between 2 and 50 characters")]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        [Display(Name = "Email Address")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mobile number is required")]
        [Phone(ErrorMessage = "Please enter a valid phone number")]
        [RegularExpression(@"^[\+]?[(]?[0-9]{3}[)]?[-\s\.]?[0-9]{3}[-\s\.]?[0-9]{4,6}$",
            ErrorMessage = "Please enter a valid mobile number")]
        [Display(Name = "Mobile Number")]
        public string MobileNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enrollment status is required")]
        [Display(Name = "Enrollment Status")]
        public EnrollmentStatus EnrollmentStatus { get; set; } = EnrollmentStatus.Active;

        [Required(ErrorMessage = "Course is required")]
        [StringLength(100, ErrorMessage = "Course name cannot exceed 100 characters")]
        public string Course { get; set; } = string.Empty;

        [Range(1, 7, ErrorMessage = "Year must be between 1 and 7")]
        public int Year { get; set; } = 1;

        [Range(0.0, 4.0, ErrorMessage = "GPA must be between 0.0 and 4.0")]
        public double GPA { get; set; } = 0.0;

        public IFormFile? ProfileImage { get; set; }
        public string? ExistingImageUrl { get; set; }
        public string? ExistingImageBlobName { get; set; }
    }

    public class StudentListViewModel
    {
        public List<Student> Students { get; set; } = new();
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int TotalCount { get; set; } = 0;
        public string SearchTerm { get; set; } = string.Empty;
        public string StatusFilter { get; set; } = string.Empty;
        public int PageSize { get; set; } = 10;
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;
    }

    public class DashboardViewModel
    {
        public int TotalStudents { get; set; }
        public int ActiveStudents { get; set; }
        public int InactiveStudents { get; set; }
        public int GraduatedStudents { get; set; }
        public int SuspendedStudents { get; set; }
        public List<Student> RecentStudents { get; set; } = new();
        public double AverageGPA { get; set; }
        public Dictionary<string, int> StudentsByCourse { get; set; } = new();
    }
}
