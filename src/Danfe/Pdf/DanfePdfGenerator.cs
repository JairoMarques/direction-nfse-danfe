using DinkToPdf;
using DinkToPdf.Contracts;

namespace Direction.NFSe.Danfe;

public sealed class DanfePdfGenerator
{
    private static readonly IConverter _converter = new SynchronizedConverter(new PdfTools());

    public byte[] Generate(string html)
    {
        return GenerateInternal(html);
    }

    private static byte[] GenerateInternal(string html)
    {
        var document = new HtmlToPdfDocument
        {
            GlobalSettings =
            {
                PaperSize = PaperKind.A4,
                Orientation = Orientation.Portrait,
                Margins = new MarginSettings
                {
                    Top = 0,
                    Bottom = 0,
                    Left = 0,
                    Right = 0
                }
            },
            Objects =
            {
                new ObjectSettings
                {
                    HtmlContent = html,
                    WebSettings =
                    {
                        DefaultEncoding = "utf-8",
                        LoadImages = true,
                        Background = true,
                        EnableIntelligentShrinking = false,
                        PrintMediaType = true
                    }
                }
            }
        };

        return _converter.Convert(document);
    }
}
