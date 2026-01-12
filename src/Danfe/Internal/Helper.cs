using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using QRCoder;

namespace Direction.NFSe.Danfe
{
    internal static class Helper
    {
        public static string NullToDash(string? s) => string.IsNullOrWhiteSpace(s) ? "-" : s!;

        public static string NullToDash(int? i) => i == null || i == 0 ? "-" : i.Value.ToString();
        public static string HtmlEncode(string s) =>
            WebUtility.HtmlEncode(s ?? string.Empty);

        public static string BuildEndereco(EndSimples? end)
        {
            if (end == null) return "-";
            var sb = new StringBuilder();

            sb.Append(end.xLgr);
            if (!string.IsNullOrWhiteSpace(end.nro))
                sb.Append(", ").Append(end.nro);
            if (!string.IsNullOrWhiteSpace(end.xCpl))
                sb.Append(", ").Append(end.xCpl);
            if (!string.IsNullOrWhiteSpace(end.xBairro))
                sb.Append(" - ").Append(end.xBairro);

            return sb.ToString();
        }
        public static string BuildEndereco(EnderNac? end)
        {
            if (end == null) return "-";
            var sb = new StringBuilder();

            sb.Append(end.xLgr);
            if (!string.IsNullOrWhiteSpace(end.nro))
                sb.Append(", ").Append(end.nro);
            if (!string.IsNullOrWhiteSpace(end.xCpl))
                sb.Append(", ").Append(end.xCpl);
            if (!string.IsNullOrWhiteSpace(end.xBairro))
                sb.Append(" - ").Append(end.xBairro);

            return sb.ToString();
        }

        public static string BuildDescricaoServicoHtml(string? desc)
        {
            if (string.IsNullOrWhiteSpace(desc)) return "-";
            // encode e depois quebra linha em <br/>
            var encoded = HtmlEncode(desc!);
            return encoded.Replace("\r\n", "<br/>").Replace("\n", "<br/>");
        }

        public static string BuildInfComplementares(Servico? serv)
        {
            if (serv?.cServ == null) return "";

            var sb = new StringBuilder();

            if (serv.infoCompl != null)
            {
                if (!string.IsNullOrEmpty(serv.infoCompl.idDocTec))
                    sb.Append($"Identificador de Responsabilidade Técnica: {serv.infoCompl.idDocTec} \n");

                if (!string.IsNullOrEmpty(serv.infoCompl.xInfComp))
                    sb.Append($"<b>Inf Cont:</b> {serv.infoCompl.xInfComp} | ");
            }

            if (serv.cServ.cNBS != 0)
            {
                sb.Append($"<b>NBS:</b> {serv.cServ.cNBS}");
            }

            return sb.Length == 0 ? "-" : sb.ToString();
        }

        public static DateTime? TryParseDate(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return dt;
            return null;
        }

        public static DateTime? TryParseDateTime(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return dt;
            return null;
        }

        public static string FormatCnpj(string? cnpj)
        {
            if (string.IsNullOrWhiteSpace(cnpj)) return "-";
            var digits = OnlyDigits(cnpj!);
            if (digits.Length == 14)
                return Convert.ToUInt64(digits).ToString(@"00\.000\.000\/0000\-00");
            return cnpj!;
        }
        public static string FormatCpf(string? cpf)
        {
            if (string.IsNullOrWhiteSpace(cpf)) return "-";
            var digits = OnlyDigits(cpf!);
            if (digits.Length == 11)
                return Convert.ToUInt64(digits).ToString(@"000\.000\.000\-00");
            return cpf!;
        }

        public static string FormatCep(string? cep)
        {
            if (string.IsNullOrEmpty(cep)) return "-";
            var digits = OnlyDigits(cep!);
            if (digits.Length == 8)
                return Convert.ToUInt64(digits).ToString(@"00000\-000");
            return cep!;
        }

        public static string FormatTelefone(string? fone)
        {
            if (string.IsNullOrWhiteSpace(fone)) return "-";
            var digits = OnlyDigits(fone!);
            if (digits.Length == 10)
                return Convert.ToUInt64(digits).ToString(@"\(00\) 0000\-0000");
            if (digits.Length == 11)
                return Convert.ToUInt64(digits).ToString(@"\(00\) 00000\-0000");
            return fone!;
        }

        public static string OnlyDigits(string s)
        {
            var sb = new StringBuilder(s?.Length ?? 0);
            if (s != null)
            {
                foreach (var c in s)
                    if (char.IsDigit(c))
                        sb.Append(c);
            }
            return sb.ToString();
        }
        public static byte[] GetQrCode(string texto, int tamanhoPixels = 20)
        {
            // Gera PNG diretamente (evita System.Drawing, melhora compatibilidade em Linux/containers)
            using var qrGenerator = new QRCodeGenerator();
            using var qrData = qrGenerator.CreateQrCode(texto, QRCodeGenerator.ECCLevel.Q);
            var pngQr = new PngByteQRCode(qrData);
            return pngQr.GetGraphic(tamanhoPixels);
        }
        public static string Limit(this string? texto, int maxChars = 80)
        {
            if (string.IsNullOrEmpty(texto)) return string.Empty;
            if (texto?.Length <= maxChars)
                return texto;

            var corte = texto!.Substring(0, maxChars - 3); // reserva espaço para "..."
            var ultimoEspaco = corte.LastIndexOf(' ');

            if (ultimoEspaco > 0)
                corte = corte.Substring(0, ultimoEspaco);

            return corte + "...";
        }

        internal static string? GetLogo(string logoPath)
        {
            if (!File.Exists(logoPath)) return null;

            var imageBytes = File.ReadAllBytes(logoPath);
            return Convert.ToBase64String(imageBytes);
        }
    }
}
