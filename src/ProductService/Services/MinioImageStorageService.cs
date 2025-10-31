using Minio;
using Minio.DataModel.Args;

namespace ProductService.Services;

public class MinioImageStorageService : IImageStorageService
{
    private readonly IMinioClient _minioClient;
    private readonly string _bucketName;
    private readonly string _publicUrl;
    private readonly ILogger<MinioImageStorageService> _logger;

    public MinioImageStorageService(
        IConfiguration configuration,
        ILogger<MinioImageStorageService> logger)
    {
        var endpoint = configuration["MinIO:Endpoint"] ?? "localhost:9000";
        var accessKey = configuration["MinIO:AccessKey"] ?? "atlasadmin";
        var secretKey = configuration["MinIO:SecretKey"]
            ?? throw new InvalidOperationException("MinIO SecretKey is required. Set MinIO:SecretKey in configuration.");

        _minioClient = new MinioClient()
            .WithEndpoint(endpoint)
            .WithCredentials(accessKey, secretKey)
            .Build();





        _bucketName = configuration["MinIO:BucketName"] ?? "product-images";
        _publicUrl = configuration["MinIO:PublicUrl"] ?? "http://localhost:9000";
        _logger = logger;
    }

    public async Task<string> UploadImageAsync(string fileName, string contentType, Stream stream)
    {
        try
        {

            var bucketExistsArgs = new BucketExistsArgs()
                .WithBucket(_bucketName);

            bool found = await _minioClient.BucketExistsAsync(bucketExistsArgs);

            if (!found)
            {
                var makeBucketArgs = new MakeBucketArgs()
                    .WithBucket(_bucketName);
                await _minioClient.MakeBucketAsync(makeBucketArgs);

                _logger.LogInformation("Created bucket: {BucketName}", _bucketName);
            }


            var uniqueFileName = $"{Guid.NewGuid()}_{fileName}";


            var putObjectArgs = new PutObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(uniqueFileName)
                .WithStreamData(stream)
                .WithObjectSize(stream.Length)
                .WithContentType(contentType);

            await _minioClient.PutObjectAsync(putObjectArgs);

            _logger.LogInformation("Uploaded image: {FileName} to bucket: {BucketName}", uniqueFileName, _bucketName);

            return GetImageUrl(uniqueFileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload image: {FileName}", fileName);
            throw;
        }
    }

    public async Task DeleteImageAsync(string fileName)
    {
        try
        {
            var removeObjectArgs = new RemoveObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(fileName);

            await _minioClient.RemoveObjectAsync(removeObjectArgs);

            _logger.LogInformation("Deleted image: {FileName} from bucket: {BucketName}", fileName, _bucketName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete image: {FileName}", fileName);
            throw;
        }
    }

    public string GetImageUrl(string fileName)
    {
        return $"{_publicUrl}/{_bucketName}/{fileName}";
    }
}
