using Microsoft.AspNetCore.Mvc;
using StudentManagement.Models;
using StudentManagement.Services;

namespace StudentManagement.Controllers
{
    public class StudentsController : Controller
    {
        private readonly ICosmosDbService _cosmosDbService;
        private readonly IBlobStorageService _blobStorageService;
        private readonly ILogger<StudentsController> _logger;

        public StudentsController(
            ICosmosDbService cosmosDbService,
            IBlobStorageService blobStorageService,
            ILogger<StudentsController> logger)
        {
            _cosmosDbService = cosmosDbService;
            _blobStorageService = blobStorageService;
            _logger = logger;
        }

        // ─── LIST ────────────────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Index(int page = 1, int pageSize = 10,
            string search = "", string status = "")
        {
            try
            {
                var (students, totalCount) = await _cosmosDbService
                    .GetPagedStudentsAsync(page, pageSize, search, status);

                // Refresh SAS URLs for profile images
                var studentList = students.ToList();
                foreach (var student in studentList.Where(s => !string.IsNullOrEmpty(s.ProfileImageBlobName)))
                {
                    student.ProfileImageUrl = await _blobStorageService
                        .GetSasUrlAsync(student.ProfileImageBlobName!);
                }

                var model = new StudentListViewModel
                {
                    Students = studentList,
                    CurrentPage = page,
                    TotalPages = (int)Math.Ceiling((double)totalCount / pageSize),
                    TotalCount = totalCount,
                    SearchTerm = search,
                    StatusFilter = status,
                    PageSize = pageSize
                };

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading student list");
                TempData["Error"] = "Error loading students. Please try again.";
                return View(new StudentListViewModel());
            }
        }

        // ─── CREATE ──────────────────────────────────────────────────────────
        [HttpGet]
        public IActionResult Create()
        {
            return View(new StudentViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(StudentViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                // Check email uniqueness
                if (await _cosmosDbService.EmailExistsAsync(model.Email))
                {
                    ModelState.AddModelError("Email", "A student with this email already exists.");
                    return View(model);
                }

                var student = MapToStudent(model);

                // Handle profile image upload
                if (model.ProfileImage != null && model.ProfileImage.Length > 0)
                {
                    if (!_blobStorageService.IsValidImageFile(model.ProfileImage))
                    {
                        ModelState.AddModelError("ProfileImage",
                            "Only JPG/PNG images under 5MB are allowed.");
                        return View(model);
                    }

                    var (blobName, sasUrl) = await _blobStorageService
                        .UploadProfileImageAsync(model.ProfileImage);
                    student.ProfileImageBlobName = blobName;
                    student.ProfileImageUrl = sasUrl;
                }

                await _cosmosDbService.AddStudentAsync(student);
                TempData["Success"] = $"Student {student.FullName} added successfully!";
                _logger.LogInformation("Student created: {Id}", student.Id);

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating student");
                TempData["Error"] = "Error creating student. Please try again.";
                return View(model);
            }
        }

        // ─── DETAILS ─────────────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Details(string id)
        {
            var student = await _cosmosDbService.GetStudentByIdAsync(id);
            if (student == null) return NotFound();

            if (!string.IsNullOrEmpty(student.ProfileImageBlobName))
                student.ProfileImageUrl = await _blobStorageService
                    .GetSasUrlAsync(student.ProfileImageBlobName);

            return View(student);
        }

        // ─── EDIT ────────────────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            var student = await _cosmosDbService.GetStudentByIdAsync(id);
            if (student == null) return NotFound();

            string? imageUrl = null;
            if (!string.IsNullOrEmpty(student.ProfileImageBlobName))
                imageUrl = await _blobStorageService.GetSasUrlAsync(student.ProfileImageBlobName);

            var model = new StudentViewModel
            {
                Id = student.Id,
                FirstName = student.FirstName,
                LastName = student.LastName,
                Email = student.Email,
                MobileNumber = student.MobileNumber,
                EnrollmentStatus = student.EnrollmentStatus,
                Course = student.Course,
                Year = student.Year,
                GPA = student.GPA,
                ExistingImageUrl = imageUrl,
                ExistingImageBlobName = student.ProfileImageBlobName
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, StudentViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                var existingStudent = await _cosmosDbService.GetStudentByIdAsync(id);
                if (existingStudent == null) return NotFound();

                // Check email uniqueness (excluding self)
                if (await _cosmosDbService.EmailExistsAsync(model.Email, id))
                {
                    ModelState.AddModelError("Email", "This email is already used by another student.");
                    return View(model);
                }

                // Update fields
                existingStudent.FirstName = model.FirstName;
                existingStudent.LastName = model.LastName;
                existingStudent.Email = model.Email;
                existingStudent.MobileNumber = model.MobileNumber;
                existingStudent.EnrollmentStatus = model.EnrollmentStatus;
                existingStudent.Course = model.Course;
                existingStudent.Year = model.Year;
                existingStudent.GPA = model.GPA;

                // Handle new image upload
                if (model.ProfileImage != null && model.ProfileImage.Length > 0)
                {
                    if (!_blobStorageService.IsValidImageFile(model.ProfileImage))
                    {
                        ModelState.AddModelError("ProfileImage",
                            "Only JPG/PNG images under 5MB are allowed.");
                        return View(model);
                    }

                    // Delete old image
                    if (!string.IsNullOrEmpty(existingStudent.ProfileImageBlobName))
                        await _blobStorageService.DeleteBlobAsync(existingStudent.ProfileImageBlobName);

                    var (blobName, sasUrl) = await _blobStorageService
                        .UploadProfileImageAsync(model.ProfileImage);
                    existingStudent.ProfileImageBlobName = blobName;
                    existingStudent.ProfileImageUrl = sasUrl;
                }

                await _cosmosDbService.UpdateStudentAsync(existingStudent);
                TempData["Success"] = $"Student {existingStudent.FullName} updated successfully!";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating student {Id}", id);
                TempData["Error"] = "Error updating student. Please try again.";
                return View(model);
            }
        }

        // ─── SOFT DELETE ─────────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SoftDelete(string id)
        {
            try
            {
                var success = await _cosmosDbService.DeleteStudentAsync(id, hardDelete: false);
                TempData[success ? "Success" : "Error"] = success
                    ? "Student marked as inactive successfully."
                    : "Error performing soft delete.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error soft-deleting student {Id}", id);
                TempData["Error"] = "Error performing operation.";
            }

            return RedirectToAction(nameof(Index));
        }

        // ─── HARD DELETE ─────────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            try
            {
                var student = await _cosmosDbService.GetStudentByIdAsync(id);
                if (student != null && !string.IsNullOrEmpty(student.ProfileImageBlobName))
                    await _blobStorageService.DeleteBlobAsync(student.ProfileImageBlobName);

                var success = await _cosmosDbService.DeleteStudentAsync(id, hardDelete: true);
                TempData[success ? "Success" : "Error"] = success
                    ? "Student permanently deleted."
                    : "Error deleting student.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting student {Id}", id);
                TempData["Error"] = "Error deleting student.";
            }

            return RedirectToAction(nameof(Index));
        }

        // ─── SEARCH API ──────────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Search(string term)
        {
            try
            {
                var students = await _cosmosDbService.SearchStudentsAsync(term ?? "");
                return Json(students.Select(s => new
                {
                    s.Id,
                    s.StudentNumber,
                    s.FullName,
                    s.Email,
                    s.Course,
                    Status = s.EnrollmentStatus.ToString()
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching students");
                return Json(new List<object>());
            }
        }

        // ─── PRIVATE HELPERS ─────────────────────────────────────────────────
        private static Student MapToStudent(StudentViewModel model) => new()
        {
            FirstName = model.FirstName,
            LastName = model.LastName,
            Email = model.Email,
            MobileNumber = model.MobileNumber,
            EnrollmentStatus = model.EnrollmentStatus,
            Course = model.Course,
            Year = model.Year,
            GPA = model.GPA
        };
    }
}
