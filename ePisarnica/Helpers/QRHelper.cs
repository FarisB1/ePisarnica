using ePisarnica.Models;
using QRCoder;

namespace ePisarnica.Helpers
{
    public class QRHelper
    {
        public readonly AppDbContext _context;
        public readonly IWebHostEnvironment _env;

        public QRHelper(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }
        public string GenerateQrCode(string text, int brojProtokola)
        {
            using var qrGen = new QRCodeGenerator();
            var qrData = qrGen.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
            var qrCode = new PngByteQRCode(qrData);
            var qrBytes = qrCode.GetGraphic(20);

            string folderPath = Path.Combine(_env.WebRootPath, "qrcodes");
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            string fileName = $"qr_{brojProtokola}.png";
            string filePath = Path.Combine(folderPath, fileName);
            System.IO.File.WriteAllBytes(filePath, qrBytes);

            return $"/qrcodes/{fileName}";
        }
    }
}
