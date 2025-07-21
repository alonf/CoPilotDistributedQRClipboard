using QRCoder;

namespace DistributedQRClipboard.Api.Endpoints;

public static class QRCodeEndpoints
{
    public static void MapQRCodeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/qrcode", GenerateQRCode)
            .WithName("GenerateQRCode")
            .WithTags("QRCode")
            .Produces(200, typeof(byte[]), "image/png")
            .ProducesValidationProblem(400);
    }

    private static IResult GenerateQRCode(string text, int size = 200)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Results.BadRequest("Text parameter is required");
            }

            // Ensure size is within reasonable bounds
            if (size < 50 || size > 1000)
            {
                size = 200;
            }

            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrCodeData);
            
            var qrCodeImage = qrCode.GetGraphic(size / 25); // Adjust pixel per module based on size
            
            return Results.File(qrCodeImage, "image/png", $"qrcode_{DateTime.Now:yyyyMMddHHmmss}.png");
        }
        catch (Exception ex)
        {
            return Results.Problem($"Failed to generate QR code: {ex.Message}");
        }
    }
}
