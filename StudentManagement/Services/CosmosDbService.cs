using Microsoft.Azure.Cosmos;
using StudentManagement.Models;

namespace StudentManagement.Services
{
    public interface ICosmosDbService
    {
        Task<IEnumerable<Student>> GetAllStudentsAsync(bool includeDeleted = false);
        Task<Student?> GetStudentByIdAsync(string id);
        Task<Student> AddStudentAsync(Student student);
        Task<Student> UpdateStudentAsync(Student student);
        Task<bool> DeleteStudentAsync(string id, bool hardDelete = false);
        Task<IEnumerable<Student>> SearchStudentsAsync(string searchTerm);
        Task<(IEnumerable<Student> Students, int TotalCount)> GetPagedStudentsAsync(
            int page, int pageSize, string? searchTerm = null, string? statusFilter = null);
        Task<DashboardStats> GetDashboardStatsAsync();
        Task<bool> EmailExistsAsync(string email, string? excludeId = null);
    }

    public class DashboardStats
    {
        public int TotalStudents { get; set; }
        public int ActiveStudents { get; set; }
        public int InactiveStudents { get; set; }
        public int GraduatedStudents { get; set; }
        public int SuspendedStudents { get; set; }
        public double AverageGPA { get; set; }
        public List<Student> RecentStudents { get; set; } = new();
        public Dictionary<string, int> StudentsByCourse { get; set; } = new();
    }

    public class CosmosDbService : ICosmosDbService
    {
        private readonly Container _container;
        private readonly ILogger<CosmosDbService> _logger;

        public CosmosDbService(IConfiguration configuration, ILogger<CosmosDbService> logger)
        {
            _logger = logger;
            var connectionString = configuration["AzureCosmosDB:ConnectionString"]!;
            var databaseName = configuration["AzureCosmosDB:DatabaseName"]!;
            var containerName = configuration["AzureCosmosDB:ContainerName"]!;

            var client = new CosmosClient(connectionString, new CosmosClientOptions
            {
                SerializerOptions = new CosmosSerializationOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                }
            });

            // Ensure database and container exist
            var database = client.CreateDatabaseIfNotExistsAsync(databaseName).GetAwaiter().GetResult();
            var containerProperties = new ContainerProperties(containerName, "/id");
            _container = database.Database.CreateContainerIfNotExistsAsync(containerProperties, 400)
                .GetAwaiter().GetResult();
        }

        public async Task<IEnumerable<Student>> GetAllStudentsAsync(bool includeDeleted = false)
        {
            try
            {
                var query = includeDeleted
                    ? "SELECT * FROM c ORDER BY c.createdAt DESC"
                    : "SELECT * FROM c WHERE c.isDeleted = false ORDER BY c.createdAt DESC";

                var queryDef = new QueryDefinition(query);
                var results = new List<Student>();

                using var feed = _container.GetItemQueryIterator<Student>(queryDef);
                while (feed.HasMoreResults)
                {
                    var response = await feed.ReadNextAsync();
                    results.AddRange(response);
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all students");
                throw;
            }
        }

        public async Task<Student?> GetStudentByIdAsync(string id)
        {
            try
            {
                var response = await _container.ReadItemAsync<Student>(id, new PartitionKey(id));
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving student {Id}", id);
                throw;
            }
        }

        public async Task<Student> AddStudentAsync(Student student)
        {
            try
            {
                student.Id = Guid.NewGuid().ToString();
                student.StudentNumber = GenerateStudentNumber();
                student.CreatedAt = DateTime.UtcNow;
                student.UpdatedAt = DateTime.UtcNow;

                var response = await _container.CreateItemAsync(student, new PartitionKey(student.Id));
                return response.Resource;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding student");
                throw;
            }
        }

        public async Task<Student> UpdateStudentAsync(Student student)
        {
            try
            {
                student.UpdatedAt = DateTime.UtcNow;
                var response = await _container.ReplaceItemAsync(student, student.Id, new PartitionKey(student.Id));
                return response.Resource;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating student {Id}", student.Id);
                throw;
            }
        }

        public async Task<bool> DeleteStudentAsync(string id, bool hardDelete = false)
        {
            try
            {
                if (hardDelete)
                {
                    await _container.DeleteItemAsync<Student>(id, new PartitionKey(id));
                }
                else
                {
                    var student = await GetStudentByIdAsync(id);
                    if (student == null) return false;
                    student.IsDeleted = true;
                    student.EnrollmentStatus = EnrollmentStatus.Inactive;
                    student.UpdatedAt = DateTime.UtcNow;
                    await _container.ReplaceItemAsync(student, id, new PartitionKey(id));
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting student {Id}", id);
                return false;
            }
        }

        public async Task<IEnumerable<Student>> SearchStudentsAsync(string searchTerm)
        {
            try
            {
                var term = searchTerm.ToLower();
                var query = new QueryDefinition(
                    "SELECT * FROM c WHERE c.isDeleted = false AND " +
                    "(CONTAINS(LOWER(c.firstName), @term) OR " +
                    "CONTAINS(LOWER(c.lastName), @term) OR " +
                    "CONTAINS(LOWER(c.email), @term) OR " +
                    "CONTAINS(c.studentNumber, @term)) " +
                    "ORDER BY c.createdAt DESC")
                    .WithParameter("@term", term);

                var results = new List<Student>();
                using var feed = _container.GetItemQueryIterator<Student>(query);
                while (feed.HasMoreResults)
                {
                    var response = await feed.ReadNextAsync();
                    results.AddRange(response);
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching students");
                throw;
            }
        }

        public async Task<(IEnumerable<Student> Students, int TotalCount)> GetPagedStudentsAsync(
            int page, int pageSize, string? searchTerm = null, string? statusFilter = null)
        {
            try
            {
                var allStudents = (await GetAllStudentsAsync()).ToList();

                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    var term = searchTerm.ToLower();
                    allStudents = allStudents.Where(s =>
                        s.FirstName.ToLower().Contains(term) ||
                        s.LastName.ToLower().Contains(term) ||
                        s.Email.ToLower().Contains(term) ||
                        s.StudentNumber.Contains(term) ||
                        s.Course.ToLower().Contains(term)
                    ).ToList();
                }

                if (!string.IsNullOrWhiteSpace(statusFilter) &&
                    Enum.TryParse<EnrollmentStatus>(statusFilter, out var status))
                {
                    allStudents = allStudents.Where(s => s.EnrollmentStatus == status).ToList();
                }

                var totalCount = allStudents.Count;
                var pagedStudents = allStudents
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                return (pagedStudents, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting paged students");
                throw;
            }
        }

        public async Task<DashboardStats> GetDashboardStatsAsync()
        {
            try
            {
                var allStudents = (await GetAllStudentsAsync(includeDeleted: false)).ToList();

                return new DashboardStats
                {
                    TotalStudents = allStudents.Count,
                    ActiveStudents = allStudents.Count(s => s.EnrollmentStatus == EnrollmentStatus.Active),
                    InactiveStudents = allStudents.Count(s => s.EnrollmentStatus == EnrollmentStatus.Inactive),
                    GraduatedStudents = allStudents.Count(s => s.EnrollmentStatus == EnrollmentStatus.Graduated),
                    SuspendedStudents = allStudents.Count(s => s.EnrollmentStatus == EnrollmentStatus.Suspended),
                    AverageGPA = allStudents.Any() ? Math.Round(allStudents.Average(s => s.GPA), 2) : 0,
                    RecentStudents = allStudents.Take(5).ToList(),
                    StudentsByCourse = allStudents
                        .GroupBy(s => s.Course)
                        .ToDictionary(g => g.Key, g => g.Count())
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting dashboard stats");
                throw;
            }
        }

        public async Task<bool> EmailExistsAsync(string email, string? excludeId = null)
        {
            var students = await GetAllStudentsAsync();
            return students.Any(s => s.Email.ToLower() == email.ToLower() &&
                                     (excludeId == null || s.Id != excludeId));
        }

        private static string GenerateStudentNumber()
        {
            return $"STU{DateTime.UtcNow.Year}{Random.Shared.Next(10000, 99999)}";
        }
    }
}
