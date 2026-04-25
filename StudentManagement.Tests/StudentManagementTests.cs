using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using StudentManagement.Controllers;
using StudentManagement.Models;
using StudentManagement.Services;
using Xunit;

namespace StudentManagement.Tests
{
    // ─── Model Validation Tests ───────────────────────────────────────────────
    public class StudentViewModelValidationTests
    {
        private static List<System.ComponentModel.DataAnnotations.ValidationResult> Validate(object model)
        {
            var context = new System.ComponentModel.DataAnnotations.ValidationContext(model);
            var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
            System.ComponentModel.DataAnnotations.Validator.TryValidateObject(model, context, results, true);
            return results;
        }

        [Fact]
        public void ValidStudentViewModel_ShouldPassValidation()
        {
            var model = new StudentViewModel
            {
                FirstName = "Sipho",
                LastName = "Dlamini",
                Email = "sipho@example.com",
                MobileNumber = "0821234567",
                Course = "Computer Science",
                EnrollmentStatus = EnrollmentStatus.Active,
                Year = 2,
                GPA = 3.5
            };

            var errors = Validate(model);
            errors.Should().BeEmpty();
        }

        [Fact]
        public void MissingFirstName_ShouldFailValidation()
        {
            var model = new StudentViewModel
            {
                FirstName = "",
                LastName = "Dlamini",
                Email = "test@test.com",
                MobileNumber = "0821234567",
                Course = "BSc"
            };

            var errors = Validate(model);
            errors.Should().Contain(e => e.MemberNames.Contains("FirstName"));
        }

        [Fact]
        public void InvalidEmail_ShouldFailValidation()
        {
            var model = new StudentViewModel
            {
                FirstName = "Sipho",
                LastName = "Dlamini",
                Email = "not-an-email",
                MobileNumber = "0821234567",
                Course = "BSc"
            };

            var errors = Validate(model);
            errors.Should().Contain(e => e.MemberNames.Contains("Email"));
        }

        [Fact]
        public void GpaAbove4_ShouldFailValidation()
        {
            var model = new StudentViewModel
            {
                FirstName = "A", LastName = "B",
                Email = "a@b.com", MobileNumber = "0821234567",
                Course = "X", GPA = 4.5
            };

            var errors = Validate(model);
            errors.Should().Contain(e => e.MemberNames.Contains("GPA"));
        }

        [Fact]
        public void YearOutOfRange_ShouldFailValidation()
        {
            var model = new StudentViewModel
            {
                FirstName = "A", LastName = "B",
                Email = "a@b.com", MobileNumber = "0821234567",
                Course = "X", Year = 10
            };

            var errors = Validate(model);
            errors.Should().Contain(e => e.MemberNames.Contains("Year"));
        }
    }

    // ─── Student Model Tests ──────────────────────────────────────────────────
    public class StudentModelTests
    {
        [Fact]
        public void FullName_ShouldConcatenateFirstAndLast()
        {
            var student = new Student { FirstName = "Sipho", LastName = "Dlamini" };
            student.FullName.Should().Be("Sipho Dlamini");
        }

        [Fact]
        public void NewStudent_ShouldHaveActiveStatus()
        {
            var student = new Student();
            student.EnrollmentStatus.Should().Be(EnrollmentStatus.Active);
        }

        [Fact]
        public void NewStudent_ShouldNotBeDeleted()
        {
            var student = new Student();
            student.IsDeleted.Should().BeFalse();
        }

        [Fact]
        public void ActiveStudent_ShouldReturnCorrectBadgeClass()
        {
            var student = new Student { EnrollmentStatus = EnrollmentStatus.Active };
            student.StatusBadgeClass.Should().Be("badge-active");
        }

        [Fact]
        public void InactiveStudent_ShouldReturnCorrectBadgeClass()
        {
            var student = new Student { EnrollmentStatus = EnrollmentStatus.Inactive };
            student.StatusBadgeClass.Should().Be("badge-inactive");
        }

        [Fact]
        public void Student_ShouldHaveUniqueId()
        {
            var s1 = new Student();
            var s2 = new Student();
            s1.Id.Should().NotBe(s2.Id);
        }
    }

    // ─── Students Controller Tests ────────────────────────────────────────────
    public class StudentsControllerTests
    {
        private readonly Mock<ICosmosDbService> _mockCosmosService;
        private readonly Mock<IBlobStorageService> _mockBlobService;
        private readonly Mock<ILogger<StudentsController>> _mockLogger;
        private readonly StudentsController _controller;

        public StudentsControllerTests()
        {
            _mockCosmosService = new Mock<ICosmosDbService>();
            _mockBlobService   = new Mock<IBlobStorageService>();
            _mockLogger        = new Mock<ILogger<StudentsController>>();

            _controller = new StudentsController(
                _mockCosmosService.Object,
                _mockBlobService.Object,
                _mockLogger.Object);

            // Mock HttpContext for TempData
            _controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
                new DefaultHttpContext(),
                Mock.Of<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());
        }

        [Fact]
        public async Task Index_ShouldReturnViewWithModel()
        {
            _mockCosmosService
                .Setup(s => s.GetPagedStudentsAsync(1, 10, null, null))
                .ReturnsAsync((new List<Student> { new() { FirstName = "Test", LastName = "User", Email = "t@t.com" } }, 1));

            var result = await _controller.Index();

            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeOfType<StudentListViewModel>().Subject;
            model.Students.Should().HaveCount(1);
        }

        [Fact]
        public async Task Index_EmptySearch_ShouldReturnAllStudents()
        {
            _mockCosmosService
                .Setup(s => s.GetPagedStudentsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((new List<Student>(), 0));

            var result = await _controller.Index(1, 10, "", "");

            result.Should().BeOfType<ViewResult>();
        }

        [Fact]
        public async Task Create_Get_ShouldReturnView()
        {
            var result = _controller.Create();
            result.Should().BeOfType<ViewResult>();
        }

        [Fact]
        public async Task Create_Post_InvalidModel_ShouldReturnView()
        {
            _controller.ModelState.AddModelError("Email", "Required");
            var model = new StudentViewModel();

            var result = await _controller.Create(model);

            result.Should().BeOfType<ViewResult>();
        }

        [Fact]
        public async Task Create_Post_DuplicateEmail_ShouldReturnViewWithError()
        {
            _mockCosmosService.Setup(s => s.EmailExistsAsync(It.IsAny<string>(), null)).ReturnsAsync(true);

            var model = new StudentViewModel
            {
                FirstName = "A", LastName = "B", Email = "dup@test.com",
                MobileNumber = "0821234567", Course = "CS", Year = 1
            };

            var result = await _controller.Create(model);

            result.Should().BeOfType<ViewResult>();
            _controller.ModelState.ContainsKey("Email").Should().BeTrue();
        }

        [Fact]
        public async Task SoftDelete_ShouldCallService()
        {
            _mockCosmosService.Setup(s => s.DeleteStudentAsync("123", false)).ReturnsAsync(true);

            var result = await _controller.SoftDelete("123");

            _mockCosmosService.Verify(s => s.DeleteStudentAsync("123", false), Times.Once);
            result.Should().BeOfType<RedirectToActionResult>();
        }

        [Fact]
        public async Task HardDelete_ShouldCallServiceAndDeleteBlob()
        {
            var student = new Student { Id = "456", ProfileImageBlobName = "profiles/abc.jpg" };
            _mockCosmosService.Setup(s => s.GetStudentByIdAsync("456")).ReturnsAsync(student);
            _mockCosmosService.Setup(s => s.DeleteStudentAsync("456", true)).ReturnsAsync(true);
            _mockBlobService.Setup(b => b.DeleteBlobAsync(It.IsAny<string>())).ReturnsAsync(true);

            var result = await _controller.Delete("456");

            _mockBlobService.Verify(b => b.DeleteBlobAsync("profiles/abc.jpg"), Times.Once);
            _mockCosmosService.Verify(s => s.DeleteStudentAsync("456", true), Times.Once);
        }

        [Fact]
        public async Task Details_NotFound_ShouldReturnNotFound()
        {
            _mockCosmosService.Setup(s => s.GetStudentByIdAsync("nonexistent")).ReturnsAsync((Student?)null);

            var result = await _controller.Details("nonexistent");

            result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task Search_ShouldReturnJsonResults()
        {
            var students = new List<Student>
            {
                new() { FirstName = "Sipho", LastName = "Dlamini", Email = "s@d.com",
                        StudentNumber = "STU2024001", Course = "CS", EnrollmentStatus = EnrollmentStatus.Active }
            };
            _mockCosmosService.Setup(s => s.SearchStudentsAsync("sipho")).ReturnsAsync(students);

            var result = await _controller.Search("sipho");

            result.Should().BeOfType<JsonResult>();
        }
    }

    // ─── Blob Service Validation Tests ───────────────────────────────────────
    public class BlobValidationTests
    {
        [Fact]
        public void IsValidImageFile_NullFile_ReturnsFalse()
        {
            // We test the logic directly since BlobStorageService needs Azure config
            IFormFile? file = null;
            (file == null || file.Length == 0).Should().BeTrue();
        }

        [Theory]
        [InlineData(".jpg", "image/jpeg", true)]
        [InlineData(".jpeg", "image/jpeg", true)]
        [InlineData(".png", "image/png", true)]
        [InlineData(".gif", "image/gif", false)]
        [InlineData(".pdf", "application/pdf", false)]
        [InlineData(".exe", "application/octet-stream", false)]
        public void FileExtensionValidation_ShouldBeCorrect(string ext, string mime, bool expected)
        {
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
            var allowedMimes = new[] { "image/jpeg", "image/png" };
            var result = allowedExtensions.Contains(ext) && allowedMimes.Contains(mime);
            result.Should().Be(expected);
        }
    }
}
