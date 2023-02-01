using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

[ApiController]
[Route("")]
public class IndexController : ControllerBase
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<IndexController> _logger;

    public IndexController(IWebHostEnvironment environment, ILogger<IndexController> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<IList<UploadResult>>> Upload([FromForm] IEnumerable<IFormFile> files)
    {
        var maxAllowedFiles = 3;
        long maxFileSize = 1024 * 15;
        var filesProcessed = 0;
        var resourcePath = new Uri($"{Request.Scheme}://{Request.Host}/");
        List<UploadResult> uploadResults = new();

        foreach (var file in files)
        {
            var uploadResult = new UploadResult();
            var untrustedFileName = file.FileName;
            uploadResult.FileName = untrustedFileName;
            var trustedFileNameForDisplay = WebUtility.HtmlEncode(untrustedFileName);

            if (filesProcessed < maxAllowedFiles)
            {
                if (file.Length == 0)
                {
                    _logger.LogInformation("{FileName} length is 0 (Err: 1)", trustedFileNameForDisplay);
                    uploadResult.ErrorCode = 1;
                }
                else if (file.Length > maxFileSize)
                {
                    _logger.LogInformation(
                        "{FileName} of {Length} bytes is " + "larger than the limit of {Limit} bytes (Err: 2)",
                        trustedFileNameForDisplay, file.Length, maxFileSize);
                    uploadResult.ErrorCode = 2;
                }
                else
                {
                    try
                    {
                        var path = Path.Combine(_environment.ContentRootPath,
                            trustedFileNameForDisplay);
                        await using FileStream fs = new(path, FileMode.Create);
                        await file.CopyToAsync(fs);

                        _logger.LogInformation("{FileName} saved at {Path}", trustedFileNameForDisplay, path);
                        uploadResult.Uploaded = true;
                    }
                    catch (IOException ex)
                    {
                        _logger.LogError("{FileName} error on upload (Err: 3): {Message}", trustedFileNameForDisplay, ex.Message);
                        uploadResult.ErrorCode = 3;
                    }
                }

                filesProcessed++;
            }
            else
            {
                _logger.LogInformation(
                    "{FileName} not uploaded because the request exceeded the allowed {Count} of files (Err: 4)",
                    trustedFileNameForDisplay, maxAllowedFiles);
                uploadResult.ErrorCode = 4;
            }

            uploadResults.Add(uploadResult);
        }

        return new CreatedResult(resourcePath, uploadResults);

    }
    
    [HttpGet]
    [Route("/{fileName}")]
    public async Task<FileResult> Download(string fileName)
    {
        new FileExtensionContentTypeProvider().TryGetContentType(WebUtility.HtmlDecode(fileName), out var contentType);
        string filePath = Path.Combine(_environment.ContentRootPath, fileName);
        return new PhysicalFileResult(filePath, contentType);
    }
}