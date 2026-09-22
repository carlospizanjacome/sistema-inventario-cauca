using QRCoder;

namespace Almacen.Services;

/// <summary>
/// Genera códigos QR para los bienes usando QRCoder.
/// Devuelve imágenes PNG en base64 listas para incrustar en HTML.
/// </summary>
public class QrService
{
    /// <summary>
    /// Genera un QR en PNG y lo devuelve como data URI base64.
    /// </summary>
    /// <param name="contenido">Texto a codificar (ej: código del bien).</param>
    /// <param name="tamanoPx">Tamaño en píxeles (default 200).</param>
    public string GenerarDataUri(string contenido, int tamanoPx = 200)
    {
        if (string.IsNullOrWhiteSpace(contenido))
            contenido = "SIN-CODIGO";

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(contenido, QRCodeGenerator.ECCLevel.Q);

        using var qrCode = new PngByteQRCode(data);
        var bytes = qrCode.GetGraphic(tamanoPx / 25); // pixeles por módulo

        return $"data:image/png;base64,{Convert.ToBase64String(bytes)}";
    }

    /// <summary>
    /// Genera un QR en PNG (bytes) para embeber en PDF o descarga.
    /// </summary>
    public byte[] GenerarPng(string contenido, int tamanoPx = 200)
    {
        if (string.IsNullOrWhiteSpace(contenido))
            contenido = "SIN-CODIGO";

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(contenido, QRCodeGenerator.ECCLevel.Q);

        using var qrCode = new PngByteQRCode(data);
        return qrCode.GetGraphic(tamanoPx / 25);
    }
}