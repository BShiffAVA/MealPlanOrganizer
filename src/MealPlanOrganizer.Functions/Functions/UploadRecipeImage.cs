using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace MealPlanOrganizer.Functions.Functions
{
    public class UploadRecipeImage
    {
        private readonly ILogger _logger;
        private readonly BlobContainerClient _containerClient;

        public UploadRecipeImage(ILoggerFactory loggerFactory, BlobContainerClient containerClient)
        {
            _logger = loggerFactory.CreateLogger<UploadRecipeImage>();
            _containerClient = containerClient;
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
                if (string.IsNullOrWhiteSpace(recipeId) || !Guid.TryParse(recipeId, out var _))
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
                    _logger.LogWarning("No boundary found in Content-Type");
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteStringAsync(JsonSerializer.Serialize(new { error = "Invalid multipart/form-data format" }));
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

                // Validate file size (max 5 MB)
                const long maxSize = 5 * 1024 * 1024; // 5 MB
                if (fileData.Length > maxSize)
                {
                    _logger.LogWarning("File too large: {Size} bytes", fileData.Length);
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteStringAsync(JsonSerializer.Serialize(new { error = $"File exceeds maximum size of {maxSize / 1024 / 1024}MB" }));
                    return badReq;
                }

                // Generate unique blob name: {recipeId}/{timestamp}-{random}.jpg
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var random = Guid.NewGuid().ToString("N").Substring(0, 8);
                var blobName = $"{recipeId}/{timestamp}-{random}.jpg";

                _logger.LogInformation("Uploading file to blob: {BlobName}, Size: {Size}", blobName, fileData.Length);

                // Upload to blob storage
                var blobClient = _containerClient.GetBlobClient(blobName);
                await blobClient.UploadAsync(new BinaryData(fileData), overwrite: true);

                _logger.LogInformation("File uploaded successfully: {BlobName}", blobName);

                // Generate SAS URL (valid for 365 days)
                var sasUri = GenerateSasUrl(blobClient, BlobSasPermissions.Read, TimeSpan.FromDays(365));

                _logger.LogInformation("Generated SAS URL for blob: {BlobName}", blobName);

                // Return response with image URL
                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/json");
                await response.WriteStringAsync(JsonSerializer.Serialize(new
                {
                    imageUrl = sasUri,
                    blobName = blobName
                }));

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading recipe image for recipe {RecipeId}", recipeId);
                var errResp = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errResp.WriteStringAsync(JsonSerializer.Serialize(new { error = "An error occurred while uploading the image" }));
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

                // Find the first boundary
                int boundaryIndex = IndexOf(body, boundaryBytes);
                if (boundaryIndex < 0)
                    return null;

                // Move past first boundary
                int pos = boundaryIndex + boundaryBytes.Length;

                // Find the double CRLF that separates headers from content
                int headerEndIndex = IndexOf(body, doubleCrLf, pos);
                if (headerEndIndex < 0)
                    return null;

                // File content starts after the double CRLF
                int contentStart = headerEndIndex + doubleCrLf.Length;

                // Find the next boundary (which marks end of file)
                int nextBoundaryIndex = IndexOf(body, boundaryBytes, contentStart);
                if (nextBoundaryIndex < 0)
                    return null;

                // File content ends before the next boundary (minus CRLF)
                int contentEnd = nextBoundaryIndex - crLf.Length;

                // Extract and return file data
                var fileData = new byte[contentEnd - contentStart];
                Array.Copy(body, contentStart, fileData, 0, fileData.Length);

                return fileData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting file from multipart body");
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

        private string GenerateSasUrl(BlobClient blobClient, BlobSasPermissions permissions, TimeSpan expiry)
        {
            // Generate SAS URL using the blob client's credentials
            if (blobClient.CanGenerateSasUri)
            {
                var sasBuilder = new BlobSasBuilder(permissions, DateTimeOffset.UtcNow.Add(expiry))
                {
                    BlobContainerName = blobClient.BlobContainerName,
                    BlobName = blobClient.Name
                };

                var sasUri = blobClient.GenerateSasUri(sasBuilder);
                return sasUri.ToString();
            }
            else
            {
                // Fallback: return blob URI without SAS (for development/testing)
                _logger.LogWarning("Cannot generate SAS URI - returning blob URI directly");
                return blobClient.Uri.ToString();
            }
        }
    }
}

