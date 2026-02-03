using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using MealPlanOrganizer.Functions.Data;

namespace MealPlanOrganizer.Functions.Functions
{
    public class UploadRecipeImage
    {
        private readonly ILogger _logger;
        private readonly BlobContainerClient _containerClient;
        private readonly BlobServiceClient _blobServiceClient;
        private readonly AppDbContext _context;

        public UploadRecipeImage(
            ILoggerFactory loggerFactory,
            BlobContainerClient containerClient,
            BlobServiceClient blobServiceClient,
            AppDbContext context)
        {
            _logger = loggerFactory.CreateLogger<UploadRecipeImage>();
            _containerClient = containerClient;
            _blobServiceClient = blobServiceClient;
            _context = context;
        }

        [Function("UploadRecipeImage")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "recipes/{recipeId}/upload-image")] HttpRequestData req,
            string recipeId)
        {
            _logger.LogInformation("Received UploadRecipeImage request for recipe: {RecipeId}", recipeId);

            try
            {
                // Validate recipeId
                if (string.IsNullOrWhiteSpace(recipeId) || !Guid.TryParse(recipeId, out var recipeGuid))
                {
                    _logger.LogWarning("Invalid recipeId format: {RecipeId}", recipeId);
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteStringAsync(JsonSerializer.Serialize(new { error = "Invalid recipe ID format" }));
                    return badReq;
                }

                // Check if request has multipart/form-data
                if (!req.Headers.TryGetValues("Content-Type", out var contentTypes))
                {
                    _logger.LogWarning("Missing Content-Type header");
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteStringAsync(JsonSerializer.Serialize(new { error = "Missing Content-Type header" }));
                    return badReq;
                }

                var contentType = string.Join(";", contentTypes);
                _logger.LogInformation("Content-Type header: {ContentType}", contentType);

                if (!contentType.Contains("multipart/form-data"))
                {
                    _logger.LogWarning("Invalid Content-Type: {ContentType}", contentType);
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteStringAsync(JsonSerializer.Serialize(new { error = "Content-Type must be multipart/form-data" }));
                    return badReq;
                }

                // Read the request body as byte array
                using var ms = new MemoryStream();
                await req.Body.CopyToAsync(ms);
                var body = ms.ToArray();

                _logger.LogInformation("Request body size: {BodySize} bytes", body.Length);

                if (body.Length == 0)
                {
                    _logger.LogWarning("Empty request body");
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteStringAsync(JsonSerializer.Serialize(new { error = "Request body is empty" }));
                    return badReq;
                }

                // Parse boundary from Content-Type header
                var boundaryStart = contentType.IndexOf("boundary=");
                if (boundaryStart < 0)
                {
                    _logger.LogWarning("No boundary found in Content-Type: {ContentType}", contentType);
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteStringAsync(JsonSerializer.Serialize(new { error = "Invalid multipart/form-data format - no boundary" }));
                    return badReq;
                }

                var boundary = contentType.Substring(boundaryStart + 9).Trim('"');
                _logger.LogInformation("Extracted boundary: {Boundary}", boundary);

                // Extract file data from multipart body
                var fileData = ExtractFileFromMultipart(body, boundary);

                if (fileData == null || fileData.Length == 0)
                {
                    _logger.LogWarning("No file data found in multipart body");
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteStringAsync(JsonSerializer.Serialize(new { error = "No file data found in multipart body" }));
                    return badReq;
                }

                _logger.LogInformation("Extracted file data: {FileSize} bytes", fileData.Length);

                // Validate file size (max 5 MB)
                const long maxSize = 5 * 1024 * 1024; // 5 MB
                if (fileData.Length > maxSize)
                {
                    _logger.LogWarning("File too large: {Size} bytes", fileData.Length);
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteStringAsync(JsonSerializer.Serialize(new { error = $"File exceeds maximum size of {maxSize / 1024 / 1024}MB" }));
                    return badReq;
                }

                // Check blob container client
                if (_containerClient == null)
                {
                    _logger.LogError("BlobContainerClient is null - DI not configured properly");
                    var errResp = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await errResp.WriteStringAsync(JsonSerializer.Serialize(new { error = "Blob storage is not configured properly" }));
                    return errResp;
                }

                _logger.LogInformation("BlobContainerClient configured. Container: {ContainerName}", _containerClient.Name);

                // Ensure container exists
                try
                {
                    _logger.LogInformation("Attempting to create/verify container with Uri: {ContainerUri}", _containerClient.Uri);
                    await _containerClient.CreateIfNotExistsAsync();
                    _logger.LogInformation("Container verified/created: {ContainerName}", _containerClient.Name);
                }
                catch (Azure.RequestFailedException rfe) when (rfe.Status == 403)
                {
                    _logger.LogError("Authorization failure (403) accessing blob storage. This usually means:");
                    _logger.LogError("1. Function App managed identity doesn't have Storage Blob Data Contributor role");
                    _logger.LogError("2. Storage account firewall is blocking the request");
                    _logger.LogError("3. The storage account name is incorrect");
                    _logger.LogError("Status: {Status}, Message: {Message}", rfe.Status, rfe.Message);
                    
                    var errResp = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await errResp.WriteStringAsync(JsonSerializer.Serialize(new 
                    { 
                        error = "Storage authorization failed (403). Verify Function App has Storage Blob Data Contributor role on the storage account.",
                        errorCode = rfe.ErrorCode,
                        details = rfe.Message
                    }));
                    return errResp;
                }
                catch (Exception containerEx)
                {
                    _logger.LogError(containerEx, "Failed to create/verify container. Exception type: {ExceptionType}, Message: {Message}", 
                        containerEx.GetType().Name, containerEx.Message);
                    var errResp = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await errResp.WriteStringAsync(JsonSerializer.Serialize(new 
                    { 
                        error = "Failed to access blob storage container",
                        details = containerEx.Message
                    }));
                    return errResp;
                }

                // Generate unique blob name: {recipeId}/{timestamp}-{random}.jpg
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var random = Guid.NewGuid().ToString("N").Substring(0, 8);
                var blobName = $"{recipeId}/{timestamp}-{random}.jpg";

                _logger.LogInformation("Uploading file to blob: {BlobName}", blobName);

                try
                {
                    // Upload to blob storage
                    var blobClient = _containerClient.GetBlobClient(blobName);
                    _logger.LogInformation("Got blob client for: {BlobUri}", blobClient.Uri);

                    await blobClient.UploadAsync(new BinaryData(fileData), overwrite: true);
                    _logger.LogInformation("File uploaded successfully to blob: {BlobName}", blobName);
                }
                catch (Exception uploadEx)
                {
                    _logger.LogError(uploadEx, "Error uploading to blob storage. Exception type: {ExceptionType}, Message: {Message}", 
                        uploadEx.GetType().Name, uploadEx.Message);
                    
                    var errResp = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await errResp.WriteStringAsync(JsonSerializer.Serialize(new 
                    { 
                        error = "Failed to upload file to blob storage",
                        details = uploadEx.Message
                    }));
                    return errResp;
                }

                // Store plain blob URL; SAS will be generated on GET
                try
                {
                    var blobClient = _containerClient.GetBlobClient(blobName);
                    var blobUrl = blobClient.Uri.ToString();

                    // Update recipe with blob URL
                    var recipe = await _context.Recipes.FirstOrDefaultAsync(r => r.Id == recipeGuid);
                    if (recipe == null)
                    {
                        _logger.LogWarning("Recipe not found for image update: {RecipeId}", recipeGuid);
                        var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                        await notFound.WriteStringAsync(JsonSerializer.Serialize(new { error = "Recipe not found" }));
                        return notFound;
                    }

                    recipe.ImageUrl = blobUrl;
                    recipe.UpdatedUtc = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    // Return response with blob URL
                    var response = req.CreateResponse(HttpStatusCode.OK);
                    response.Headers.Add("Content-Type", "application/json");
                    await response.WriteStringAsync(JsonSerializer.Serialize(new
                    {
                        imageUrl = blobUrl,
                        blobName = blobName
                    }));

                    return response;
                }
                catch (Exception urlEx)
                {
                    _logger.LogError(urlEx, "Error storing blob URL. Exception type: {ExceptionType}, Message: {Message}",
                        urlEx.GetType().Name, urlEx.Message);

                    var errResp = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await errResp.WriteStringAsync(JsonSerializer.Serialize(new
                    {
                        error = "File uploaded but storing blob URL failed",
                        details = urlEx.Message
                    }));
                    return errResp;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in UploadRecipeImage. Exception type: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}", 
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                var errResp = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errResp.WriteStringAsync(JsonSerializer.Serialize(new 
                { 
                    error = "An unexpected error occurred while uploading the image",
                    details = ex.Message
                }));
                return errResp;
            }
        }

        private byte[]? ExtractFileFromMultipart(byte[] body, string boundary)
        {
            try
            {
                var boundaryBytes = System.Text.Encoding.UTF8.GetBytes("--" + boundary);
                var doubleCrLf = System.Text.Encoding.UTF8.GetBytes("\r\n\r\n");
                var crLf = System.Text.Encoding.UTF8.GetBytes("\r\n");

                _logger.LogInformation("Looking for boundary: --{Boundary}", boundary);
                _logger.LogInformation("Boundary bytes length: {BoundaryLength}", boundaryBytes.Length);

                // Find the first boundary
                int boundaryIndex = IndexOf(body, boundaryBytes);
                _logger.LogInformation("First boundary found at index: {BoundaryIndex}", boundaryIndex);

                if (boundaryIndex < 0)
                {
                    _logger.LogError("No boundary found in body. Body starts with: {BodyStart}", 
                        System.Text.Encoding.UTF8.GetString(body.Take(Math.Min(100, body.Length)).ToArray()));
                    return null;
                }

                // Move past first boundary
                int pos = boundaryIndex + boundaryBytes.Length;
                _logger.LogInformation("Position after first boundary: {Pos}", pos);

                // Find the double CRLF that separates headers from content
                int headerEndIndex = IndexOf(body, doubleCrLf, pos);
                _logger.LogInformation("Header end (double CRLF) found at index: {HeaderEndIndex}", headerEndIndex);

                if (headerEndIndex < 0)
                {
                    _logger.LogError("No double CRLF found to mark end of headers. Body section: {BodySection}", 
                        System.Text.Encoding.UTF8.GetString(body.Skip(pos).Take(Math.Min(200, body.Length - pos)).ToArray()));
                    return null;
                }

                // File content starts after the double CRLF
                int contentStart = headerEndIndex + doubleCrLf.Length;
                _logger.LogInformation("Content starts at index: {ContentStart}", contentStart);

                // Find the next boundary (which marks end of file)
                int nextBoundaryIndex = IndexOf(body, boundaryBytes, contentStart);
                _logger.LogInformation("Next boundary found at index: {NextBoundaryIndex}", nextBoundaryIndex);

                if (nextBoundaryIndex < 0)
                {
                    _logger.LogError("No closing boundary found");
                    return null;
                }

                // File content ends before the next boundary (minus CRLF)
                int contentEnd = nextBoundaryIndex - crLf.Length;
                _logger.LogInformation("Content range: {ContentStart} to {ContentEnd}", contentStart, contentEnd);

                if (contentEnd <= contentStart)
                {
                    _logger.LogError("Invalid content range: start={ContentStart}, end={ContentEnd}", contentStart, contentEnd);
                    return null;
                }

                // Extract and return file data
                var fileData = new byte[contentEnd - contentStart];
                Array.Copy(body, contentStart, fileData, 0, fileData.Length);

                _logger.LogInformation("Successfully extracted file data: {FileDataLength} bytes", fileData.Length);
                return fileData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting file from multipart body. Exception: {Message}, StackTrace: {StackTrace}", 
                    ex.Message, ex.StackTrace);
                return null;
            }
        }

        private int IndexOf(byte[] source, byte[] pattern, int startIndex = 0)
        {
            if (pattern.Length == 0)
                return startIndex;

            for (int i = startIndex; i <= source.Length - pattern.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (source[i + j] != pattern[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match)
                    return i;
            }
            return -1;
        }

        // SAS generation is handled on GET to avoid short-lived URLs at upload time.
    }
}

