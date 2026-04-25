using Newtonsoft.Json;

namespace StudentManagement.Models
{
    public class Student
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonProperty("studentNumber")]
        public string StudentNumber { get; set; } = string.Empty;

        [JsonProperty("firstName")]
        public string FirstName { get; set; } = string.Empty;

        [JsonProperty("lastName")]
        public string LastName { get; set; } = string.Empty;

        [JsonProperty("email")]
        public string Email { get; set; } = string.Empty;

        [JsonProperty("mobileNumber")]
        public string MobileNumber { get; set; } = string.Empty;

        [JsonProperty("enrollmentStatus")]
        public EnrollmentStatus EnrollmentStatus { get; set; } = EnrollmentStatus.Active;

        [JsonProperty("profileImageBlobName")]
        public string? ProfileImageBlobName { get; set; }

        [JsonProperty("profileImageUrl")]
        public string? ProfileImageUrl { get; set; }

        [JsonProperty("course")]
        public string Course { get; set; } = string.Empty;

        [JsonProperty("year")]
        public int Year { get; set; } = 1;

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [JsonProperty("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [JsonProperty("isDeleted")]
        public bool IsDeleted { get; set; } = false;

        [JsonProperty("gpa")]
        public double GPA { get; set; } = 0.0;

        // Computed property - not stored in Cosmos
        [JsonIgnore]
        public string FullName => $"{FirstName} {LastName}";

        [JsonIgnore]
        public string StatusBadgeClass => EnrollmentStatus switch
        {
            EnrollmentStatus.Active => "badge-active",
            EnrollmentStatus.Inactive => "badge-inactive",
            EnrollmentStatus.Suspended => "badge-suspended",
            EnrollmentStatus.Graduated => "badge-graduated",
            _ => "badge-inactive"
        };
    }

    public enum EnrollmentStatus
    {
        Active,
        Inactive,
        Suspended,
        Graduated
    }
}
