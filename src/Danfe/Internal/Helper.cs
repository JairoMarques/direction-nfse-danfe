using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
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
            // A NT 008 limita o campo a 1.300 caracteres, com reticências.
            var encoded = HtmlEncode(desc.Limit(1300));
            return encoded.Replace("\r\n", "<br/>").Replace("\n", "<br/>");
        }

        private static void AppendWithSeparator(StringBuilder sb, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                if (sb.Length > 0) sb.Append(" | ");
                sb.Append(value);
            }
        }

        public static string BuildInfComplementares(Servico? serv, NFSeSubstituida? subst)
        {
            if (serv?.cServ == null) return "";

            var sb = new StringBuilder();

            if (subst != null)
            {
                AppendWithSeparator(sb, $"<b>NFSe Subst:</b> {subst.chSubstda}");
            }

            if (serv.infoCompl != null)
            {
                if (!string.IsNullOrEmpty(serv.infoCompl.idDocTec))
                    AppendWithSeparator(sb, $"Identificador de Responsabilidade Técnica: {serv.infoCompl.idDocTec} \n");

                if (!string.IsNullOrEmpty(serv.infoCompl.xInfComp))
                    AppendWithSeparator(sb, $"<b>Inf Cont:</b> {serv.infoCompl.xInfComp}");

                if (!string.IsNullOrWhiteSpace(serv.infoCompl.docRef))
                    AppendWithSeparator(sb, $"<b>Doc Ref:</b> {serv.infoCompl.docRef}");
            }

            if (serv.cServ.cNBS != 0)
            {
                AppendWithSeparator(sb, $"<b>NBS:</b> {serv.cServ.cNBS}");
            }

            return sb.Length == 0 ? "-" : sb.ToString();
        }

        public static string BuildInfComplementares(InfNFSe inf, CultureInfo culture)
        {
            var items = new List<string>();
            var infDps = inf.DPS?.InfDPS;
            var serv = infDps?.serv;

            AppendComplementary(items, "Inf. Cont.:", serv?.infoCompl?.xInfComp);
            AppendComplementary(items, "NFS-e Subst.:", infDps?.subst?.chSubstda);
            AppendComplementary(items, "Doc. Ref.:", serv?.infoCompl?.docRef);
            AppendComplementary(items, "Cod. Obra:", serv?.obra?.cObra);
            AppendComplementary(items, "Insc. Imob.:", infDps?.IBSCBS?.imovel?.inscImobFisc ?? serv?.obra?.inscImobFisc);
            AppendComplementary(items, "Cod. Evt.:", serv?.atvEvento?.idAtvEvt);
            AppendComplementary(items, "Doc. Tec.:", serv?.infoCompl?.idDocTec);
            AppendComplementary(items, "Núm. Ped.:", serv?.infoCompl?.xPed);

            var itensPedido = serv?.infoCompl?.gItemPed?.xItemPed;
            if (itensPedido is { Count: > 0 })
                AppendComplementary(items, "Item Ped.:", string.Join(", ", itensPedido));

            AppendComplementary(items, "Inf. A. T. Mun.:", inf.valores?.xOutInf);

            // Reserva a linha obrigatória dos totais; somente o conteúdo anterior é truncado.
            var complementares = string.Join(" | ", items).Limit(1997);
            var totais = BuildTotaisAproximados(infDps?.valores?.trib?.totTrib, culture);
            return string.IsNullOrEmpty(complementares)
                ? totais
                : $"{HtmlEncode(complementares)}<br/>{totais}";
        }

        private static void AppendComplementary(List<string> items, string label, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                items.Add($"{label} {value!.Trim()}");
        }

        private static string BuildTotaisAproximados(TotTrib? totais, CultureInfo culture)
        {
            string federal = "-";
            string estadual = "-";
            string municipal = "-";

            if (totais?.vTotTrib != null)
            {
                federal = totais.vTotTrib.vTotTribFed.ToString("C", culture);
                estadual = totais.vTotTrib.vTotTribEst.ToString("C", culture);
                municipal = totais.vTotTrib.vTotTribMun.ToString("C", culture);
            }
            else if (totais?.pTotTrib != null)
            {
                federal = $"{totais.pTotTrib.pTotTribFed.ToString("N2", culture)}%";
                estadual = $"{totais.pTotTrib.pTotTribEst.ToString("N2", culture)}%";
                municipal = $"{totais.pTotTrib.pTotTribMun.ToString("N2", culture)}%";
            }

            return $"Totais Aproximados dos Tributos cfe. Lei nº 12.741/2012: Federais: {federal}; Estaduais: {estadual}; Municipais: {municipal};";
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

        public static string FormatTaxIdentifier(string? cnpj, string? cpf, string? nif)
        {
            if (!string.IsNullOrWhiteSpace(cnpj)) return FormatCnpj(cnpj);
            if (!string.IsNullOrWhiteSpace(cpf)) return FormatCpf(cpf);
            return NullToDash(nif);
        }

        public static string FormatNbs(int nbs)
        {
            if (nbs == 0) return "-";

            var value = nbs.ToString("D9", CultureInfo.InvariantCulture);
            return Regex.Replace(value, @"^(\d)(\d{4})(\d{2})(\d{2})$", "$1.$2.$3.$4");
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

            // Evita descartar quase todo o conteúdo quando há uma palavra longa sem espaços.
            if (ultimoEspaco >= corte.Length - 40)
                corte = corte.Substring(0, ultimoEspaco);

            return corte + "...";
        }

        internal static string? GetLogo(string logoPath)
        {
            if (!File.Exists(logoPath)) return null;

            var imageBytes = File.ReadAllBytes(logoPath);
            return Convert.ToBase64String(imageBytes);
        }

        internal static string RemoveBlock(string html, string beginMarker, string endMarker)
        {
            // Remove inclusive markers
            var pattern = $"{Regex.Escape(beginMarker)}.*?{Regex.Escape(endMarker)}";
            return Regex.Replace(html, pattern, "", RegexOptions.Singleline);
        }

        public static string ApplyConditionalSections(
            string html,
            bool hasTomador,
            bool hasIntermediario,
            bool hasDestinatario,
            bool destinatarioIsTomador,
            bool showPisCofins,
            bool issSubject,
            bool showIssOptionalRow1,
            bool showIssOptionalRow2)
        {
            // TOMADOR
            if (hasTomador)
            {
                html = RemoveBlock(html, "<!-- TOMADOR:BEGIN_NOT_IDENTIFIED -->", "<!-- TOMADOR:END_NOT_IDENTIFIED -->");
            }
            else
            {
                html = RemoveBlock(html, "<!-- TOMADOR:BEGIN_IDENTIFIED -->", "<!-- TOMADOR:END_IDENTIFIED -->");
            }

            // INTERMEDIÁRIO
            if (hasIntermediario)
            {
                html = RemoveBlock(html, "<!-- INTERMEDIARIO:BEGIN_NOT_IDENTIFIED -->", "<!-- INTERMEDIARIO:END_NOT_IDENTIFIED -->");
            }
            else
            {
                html = RemoveBlock(html, "<!-- INTERMEDIARIO:BEGIN_IDENTIFIED -->", "<!-- INTERMEDIARIO:END_IDENTIFIED -->");
            }

            // DESTINATARIO
            if (hasDestinatario)
            {
                html = RemoveBlock(html, "<!-- DESTINATARIO:BEGIN_SAME_AS_TOMADOR -->", "<!-- DESTINATARIO:END_SAME_AS_TOMADOR -->");
                html = RemoveBlock(html, "<!-- DESTINATARIO:BEGIN_NOT_IDENTIFIED -->", "<!-- DESTINATARIO:END_NOT_IDENTIFIED -->");
            }
            else if (destinatarioIsTomador)
            {
                html = RemoveBlock(html, "<!-- DESTINATARIO:BEGIN_IDENTIFIED -->", "<!-- DESTINATARIO:END_IDENTIFIED -->");
                html = RemoveBlock(html, "<!-- DESTINATARIO:BEGIN_NOT_IDENTIFIED -->", "<!-- DESTINATARIO:END_NOT_IDENTIFIED -->");
            }
            else
            {
                html = RemoveBlock(html, "<!-- DESTINATARIO:BEGIN_IDENTIFIED -->", "<!-- DESTINATARIO:END_IDENTIFIED -->");
                html = RemoveBlock(html, "<!-- DESTINATARIO:BEGIN_SAME_AS_TOMADOR -->", "<!-- DESTINATARIO:END_SAME_AS_TOMADOR -->");
            }

            if (!showPisCofins)
                html = RemoveBlock(html, "<!-- PISCOFINS:BEGIN -->", "<!-- PISCOFINS:END -->");

            if (issSubject)
                html = RemoveBlock(html, "<!-- ISS:BEGIN_NOT_SUBJECT -->", "<!-- ISS:END_NOT_SUBJECT -->");
            else
                html = RemoveBlock(html, "<!-- ISS:BEGIN_SUBJECT -->", "<!-- ISS:END_SUBJECT -->");

            if (!showIssOptionalRow1)
                html = RemoveBlock(html, "<!-- ISS_OPTIONAL_1:BEGIN -->", "<!-- ISS_OPTIONAL_1:END -->");

            if (!showIssOptionalRow2)
                html = RemoveBlock(html, "<!-- ISS_OPTIONAL_2:BEGIN -->", "<!-- ISS_OPTIONAL_2:END -->");

            return html;
        }
    }
}
