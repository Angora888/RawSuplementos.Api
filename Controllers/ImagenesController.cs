using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace RawSuplementos.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ImagenesController : ControllerBase
    {
        private const long MaximoBytes = 5 * 1024 * 1024;

        private static readonly HashSet<string> ExtensionesPermitidas =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ".jpg",
                ".jpeg",
                ".png",
                ".webp"
            };

        private static readonly HashSet<string> TiposPermitidos =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "image/jpeg",
                "image/png",
                "image/webp"
            };

        private readonly IConfiguration _configuration;

        public ImagenesController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpPost("productos")]
        [RequestSizeLimit(MaximoBytes)]
        public async Task<IActionResult> SubirImagenProducto(
            [FromForm] IFormFile archivo)
        {
            if (archivo == null || archivo.Length == 0)
            {
                return BadRequest("Debe seleccionar una imagen.");
            }

            if (archivo.Length > MaximoBytes)
            {
                return BadRequest("La imagen no puede superar los 5 MB.");
            }

            var extension = Path.GetExtension(archivo.FileName);

            if (string.IsNullOrWhiteSpace(extension) ||
                !ExtensionesPermitidas.Contains(extension) ||
                !TiposPermitidos.Contains(archivo.ContentType))
            {
                return BadRequest(
                    "Formato no permitido. Use JPG, PNG o WEBP."
                );
            }

            var connectionString =
                _configuration["AzureStorage:ConnectionString"];

            var containerName =
                _configuration["AzureStorage:ContainerName"];

            if (string.IsNullOrWhiteSpace(connectionString) ||
                string.IsNullOrWhiteSpace(containerName))
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    "Azure Blob Storage todavía no está configurado."
                );
            }

            var containerClient =
                new BlobContainerClient(
                    connectionString,
                    containerName
                );

            var nombreArchivo =
                $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";

            var blobClient =
                containerClient.GetBlobClient(nombreArchivo);

            await using var stream = archivo.OpenReadStream();

            await blobClient.UploadAsync(
                stream,
                new BlobHttpHeaders
                {
                    ContentType = archivo.ContentType
                }
            );

            return Ok(new
            {
                url = blobClient.Uri.ToString(),
                nombre = nombreArchivo
            });
        }
    }
}
