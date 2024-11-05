using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;
using Minio.DataModel.Result;

namespace VideoConverter
{
    internal class MinioService(IMinioClient minioClient, ILogger<MinioService> logger)
    {
        public async Task UploadFileAsync(string bucketName, string objectName, string filePath)
        {
            try
            {
                var bucketExistsArgs = new BucketExistsArgs();
                bucketExistsArgs.WithBucket(bucketName);

                bool found = await minioClient.BucketExistsAsync(bucketExistsArgs);
                if (!found)
                {
                    var makeBucketArgs = new MakeBucketArgs();

                    makeBucketArgs.WithBucket(bucketName);
                    makeBucketArgs.WithLocation(filePath);

                    await minioClient.MakeBucketAsync(makeBucketArgs);
                }

                var putObjectArgs = new PutObjectArgs();

                putObjectArgs.WithBucket(bucketName);
                putObjectArgs.WithObject(objectName);
                putObjectArgs.WithFileName(filePath);

                await minioClient.PutObjectAsync(putObjectArgs);

                logger.Log(LogLevel.Information,
                    $"Successfully uploaded [{objectName}] to [{bucketName}], WithFileName [{filePath}]");
            }
            catch (Exception ex)
            {
                //Console.WriteLine($"Error occurred: {ex.Message}");
                logger.Log(LogLevel.Error, $"UploadFileAsync >> {ex.Message}");
            }
        }

        public async Task UploadDirectoryAsync(string bucketName, string directoryPath, string prefix = "")
        {
            try
            {
                // Check if the directory exists
                if (!Directory.Exists(directoryPath))
                {
                    logger.Log(LogLevel.Error, "Directory not found: " + directoryPath);
                    return;
                }

                // Recursively iterate through the directory and upload files
                foreach (var file in Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories))
                {
                    // Get the relative path of the file from the root directory
                    var relativePath = Path.GetRelativePath(directoryPath, file);

                    // Adjust object name to maintain the directory structure
                    var objectName = Path.Combine(prefix, relativePath).Replace("\\", "/");
                    logger.Log(LogLevel.Information, $"UploadDirectoryAsync >> Object name is: {objectName}");
                    objectName = $"{objectName}";
                    FileInfo f = new FileInfo(relativePath);
                    var fName = f.Name;
                    // Upload the file
                    await UploadFileAsync(bucketName, objectName, file);
                }

                logger.Log(LogLevel.Information, "Directory upload complete!");
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Error,
                    $"UploadDirectoryAsync >> Error occurred while Read directory: {ex.Message}");
            }
        }

        public async Task ListBucketsAsync()
        {
            ListAllMyBucketsResult list = await minioClient
                .ListBucketsAsync()
                .ConfigureAwait(false);
            foreach (var item in list.Buckets)
            {
                Console.WriteLine(item.Name);
            }

        }
    }
}
