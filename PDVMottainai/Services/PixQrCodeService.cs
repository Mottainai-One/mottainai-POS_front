using QRCoder;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Media.Imaging;

namespace PDVMottainai.Services
{
    public static class PixQrCodeService
    {
        // Configure a chave DICT real do estabelecimento, o nome cadastrado do recebedor e a cidade.
        public static string PixKey { get; set; } = string.Empty;
        public static string MerchantName { get; set; } = string.Empty;
        public static string MerchantCity { get; set; } = string.Empty;
        public static bool IsConfigured =>
            !string.IsNullOrWhiteSpace(PixKey) &&
            !string.IsNullOrWhiteSpace(MerchantName) &&
            !string.IsNullOrWhiteSpace(MerchantCity);

        public static (BitmapSource Image, string Payload, string Reference) Create(decimal amount, int saleNumber)
        {
            if (!IsConfigured)
                throw new InvalidOperationException("Configure a chave PIX, o nome do recebedor e a cidade.");

            var reference = $"PDV{saleNumber:D4}{DateTime.Now:HHmmssfff}";
            var accountInformation = Field("00", "br.gov.bcb.pix") + Field("01", PixKey.Trim());
            var payload = Field("00", "01") +
                          Field("26", accountInformation) +
                          Field("52", "0000") +
                          Field("53", "986") +
                          Field("54", amount.ToString("0.00", CultureInfo.InvariantCulture)) +
                          Field("58", "BR") +
                          Field("59", Normalize(MerchantName, 25)) +
                          Field("60", Normalize(MerchantCity, 15)) +
                          Field("62", Field("05", reference)) +
                          "6304";
            payload += CalculateCrc16(payload).ToString("X4", CultureInfo.InvariantCulture);

            return (CreateQrImage(payload), payload, reference);
        }

        public static (BitmapSource Image, string Payload, string Reference) CreateDemo(decimal amount, int saleNumber)
        {
            var reference = $"DEMO-{saleNumber:D4}-{DateTime.Now:HHmmssfff}";
            var payload = $"MOTTAINAI-PIX-DEMO|VENDA={saleNumber:D4}|VALOR={amount.ToString("0.00", CultureInfo.InvariantCulture)}|REF={reference}";
            return (CreateQrImage(payload), payload, reference);
        }

        private static BitmapSource CreateQrImage(string payload)
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
            var png = new PngByteQRCode(data).GetGraphic(8);
            using var stream = new MemoryStream(png);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }

        private static string Field(string id, string value) =>
            id + value.Length.ToString("D2", CultureInfo.InvariantCulture) + value;

        private static string Normalize(string value, int maxLength)
        {
            var normalized = new string(value.Normalize(NormalizationForm.FormD)
                .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                .ToArray()).Normalize(NormalizationForm.FormC).ToUpperInvariant().Trim();
            if (normalized.Length > maxLength)
                throw new ArgumentException($"O campo PIX excede {maxLength} caracteres.");
            return normalized;
        }

        private static ushort CalculateCrc16(string text)
        {
            ushort crc = 0xFFFF;
            foreach (var character in Encoding.ASCII.GetBytes(text))
            {
                crc ^= (ushort)(character << 8);
                for (var bit = 0; bit < 8; bit++)
                    crc = (ushort)((crc & 0x8000) != 0 ? (crc << 1) ^ 0x1021 : crc << 1);
            }
            return crc;
        }
    }
}
