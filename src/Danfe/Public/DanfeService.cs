using System;
using System.IO;
using System.Xml.Serialization;

namespace Direction.NFSe.Danfe;

public sealed class DanfeService : IDanfeService
{
    private readonly DanfeHtmlRenderer _renderer;
    private readonly DanfePdfGenerator _pdf;

    public DanfeService(DanfeOptions? options = null)
    {
        options ??= new DanfeOptions();
        _renderer = new DanfeHtmlRenderer(options);
        _pdf = new DanfePdfGenerator();
    }

    public DanfeResult Generate(NFSeSchema nfse, DanfeEnvironment environment, DanfeStatus status = DanfeStatus.Autorizada)
    {
        var (html, warnings) = _renderer.RenderInternal(nfse, environment, status);
        var pdfBytes = _pdf.Generate(html);

        return new DanfeResult
        {
            Environment = environment,
            Html = html,
            PdfBytes = pdfBytes,
            Warnings = warnings
        };
    }
    [Obsolete("Use Generate(NFSeSchema, DanfeEnvironment, DanfeStatus)")]
    public DanfeResult Generate(NFSeSchema nfse, DanfeEnvironment environment, bool isCancelled)
    {
        var status = isCancelled ? DanfeStatus.Cancelada : DanfeStatus.Autorizada;

        var (html, warnings) = _renderer.Render(nfse, environment, status);
        var pdfBytes = _pdf.Generate(html);

        return new DanfeResult
        {
            Environment = environment,
            Html = html,
            PdfBytes = pdfBytes,
            Warnings = warnings
        };
    }

    public DanfeResult Generate(string xml, DanfeEnvironment environment, DanfeStatus status = DanfeStatus.Autorizada)
    {
        using var sr = new StringReader(xml);
        var nfse = Deserialize(sr);
        return Generate(nfse, environment, status);
    }
    [Obsolete("Use Generate(string, DanfeEnvironment, DanfeStatus).")]
    public DanfeResult Generate(string xml, DanfeEnvironment environment, bool isCancelled)
    {
        using var sr = new StringReader(xml);
        var nfse = Deserialize(sr);
        return Generate(nfse, environment, isCancelled);
    }

    public DanfeResult Generate(Stream xmlStream, DanfeEnvironment environment, DanfeStatus status = DanfeStatus.Autorizada)
    {
        using var sr = new StreamReader(xmlStream);
        var nfse = Deserialize(sr);
        return Generate(nfse, environment, status);
    }
    [Obsolete("Use Generate(Stream, DanfeEnvironment, DanfeStatus).")]
    public DanfeResult Generate(Stream xmlStream, DanfeEnvironment environment, bool isCancelled)
    {
        using var sr = new StreamReader(xmlStream);
        var nfse = Deserialize(sr);
        return Generate(nfse, environment, isCancelled);
    }

    private static NFSeSchema Deserialize(TextReader reader)
    {
        var serializer = new XmlSerializer(typeof(NFSeSchema));
        var obj = serializer.Deserialize(reader);
        if (obj is not NFSeSchema nfse)
            throw new InvalidOperationException("Falha ao desserializar NFSeSchema.");

        return nfse;
    }
}
