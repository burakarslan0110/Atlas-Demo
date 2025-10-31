namespace ProductService.Services;

public interface IImageStorageService
{







    Task<string> UploadImageAsync(string fileName, string contentType, Stream stream);





    Task DeleteImageAsync(string fileName);






    string GetImageUrl(string fileName);
}
