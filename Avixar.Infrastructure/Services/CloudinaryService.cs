using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Avixar.Infrastructure.Services
{
    public class CloudinaryService
    {
        private readonly Cloudinary _cloudinary;

        public CloudinaryService(IConfiguration config)
        {
            var section = config.GetSection("CloudinarySettings");
            var account = new Account(
                section["CloudName"],
                section["ApiKey"],
                section["ApiSecret"]
            );
            _cloudinary = new Cloudinary(account);
        }

        // RETURNS: The full URL string to save in database
        public async Task<string?> UploadImageAsync(IFormFile file, string folderName)
        {
            try
            {
                if (file.Length == 0) return null;

                using var stream = file.OpenReadStream();

                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, stream),

                    // Files go into folders like "myapp/users" or "myapp/products"
                    Folder = folderName,

                    // Optional: Resize generic uploads to save bandwidth
                    Transformation = new Transformation().Width(800).Crop("limit")
                };

                var uploadResult = await _cloudinary.UploadAsync(uploadParams);

                // Return the secure URL (https)
                return uploadResult.SecureUrl.ToString();
            }
            catch(Exception ex)
            {
                throw ex;
            }
        }
    }
}
