using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace StudentManagement.Services
{
    public interface IBlobStorageService
    {
        Task<(string BlobName, string SasUrl)> UploadProfileImageAsync(IFormFile file);
        Task<string> GetSasUrlAsync(string blobName, int expiryHours = 1);
        Task<bool> DeleteBlobAsync(string blobName);
        bool IsValidImageFile(IFormFile file);
    }

    public class BlobStorageService : IBlobStorageService
    {
        private readonly BlobContainerClient _containerClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<BlobStorageService> _logger;

        private readonly string[] _allowedExtensions = { ".jpg", ".jpeg", ".png" };
        private readonly string[] _allowedMimeTypes = { "image/jpeg", "image/png" };
        private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5MB

        public BlobStorageService(IConfiguration configuration, ILogger<BlobStorageService> logger)
        {
            _configuration = configuration;
            _logger = logger;

            var connectionString = configuration["AzureBlobStorage:ConnectionString"]!;
            var containerName = configuration["AzureBlobStorage:ContainerName"]!;

            var serviceClient = new BlobServiceClient(connectionString);
            _containerClient = serviceClient.GetBlobContainerClient(containerName);
            _containerClient.CreateIfNotExistsAsync(PublicAccessType.None).GetAwaiter().GetResult();
        }

        public async Task<(string BlobName, string SasUrl)> UploadProfileImageAsync(IFormFile file)
        {
            if (!IsValidImageFile(file))
                throw new InvalidOperationException("Invalid file type or size.");

            try
            {
                // Process and resize image
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                var blobName = $"profiles/{Guid.NewGuid()}{extension}";

                using var processedStream = new MemoryStream();
                using (var image = await Image.LoadAsync(file.OpenReadStream()))
                {
                    // Resize to max 400x400 while maintaining aspect ratio
                    image.Mutate(x => x.Resize(new ResizeOptions
                    {
                        Size = new Size(400, 400),
                        Mode = ResizeMode.Max
                    }));

                    await image.SaveAsJpegAsync(processedStream);
                }

                processedStream.Position = 0;
                var blobClient = _containerClient.GetBlobClient(blobName);

                await blobClient.UploadAsync(processedStream, new BlobHttpHeaders
                {
                    ContentType = "image/jpeg"
                });

                var sasUrl = await GetSasUrlAsync(blobName);
                return (blobName, sasUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading profile image");
                throw;
            }
        }

        public async Task<string> GetSasUrlAsync(string blobName, int expiryHours = 2)
        {
            try
            {
                var blobClient = _containerClient.GetBlobClient(blobName);

                if (!await blobClient.ExistsAsync())
                    return string.Empty;

                // Generate SAS token
                var sasBuilder = new BlobSasBuilder
                {
                    BlobContainerName = _containerClient.Name,
                    BlobName = blobName,
                    Resource = "b",
                    ExpiresOn = DateTimeOffset.UtcNow.AddHours(expiryHours)
                };
                sasBuilder.SetPermissions(BlobSasPermissions.Read);

                var accountName = _configuration["AzureBlobStorage:AccountName"]!;
                var accountKey = _configuration["AzureBlobStorage:AccountKey"]!;

                var credential = new Azure.Storage.StorageSharedKeyCredential(accountName, accountKey);
                var sasToken = sasBuilder.ToSasQueryParameters(credential).ToString();

                return $"{blobClient.Uri}?{sasToken}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating SAS URL for blob {BlobName}", blobName);
                return string.Empty;
            }
        }

        public async Task<bool> DeleteBlobAsync(string blobName)
        {
            try
            {
                var blobClient = _containerClient.GetBlobClient(blobName);
                return await blobClient.DeleteIfExistsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting blob {BlobName}", blobName);
                return false;
            }
        }

        public bool IsValidImageFile(IFormFile file)
        {
            if (file == null || file.Length == 0) return false;
            if (file.Length > MaxFileSizeBytes) return false;

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!_allowedExtensions.Contains(extension)) return false;

            if (!_allowedMimeTypes.Contains(file.ContentType.ToLowerInvariant())) return false;

            return true;
        }
    }
}
