using System.Xml.Serialization;
using Direction.NFSe.Danfe;


Console.Write("Informe o caminho do XML (ou arraste o arquivo aqui e pressione ENTER): ");
var filePathXmlRaw = Console.ReadLine();

ArgumentException.ThrowIfNullOrWhiteSpace(filePathXmlRaw);

// Remove aspas caso o usuário cole/arraste com "..."
var filePathXml = filePathXmlRaw.Trim().Trim('"');

ArgumentException.ThrowIfNullOrWhiteSpace(filePathXml);

if (!File.Exists(filePathXml))
    throw new FileNotFoundException("Arquivo XML não encontrado.", filePathXml);

await using var fs = File.OpenRead(filePathXml);
using var reader = new StreamReader(fs, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

var serializer = new XmlSerializer(typeof(NFSeSchema));

var nfse = serializer.Deserialize(reader) as NFSeSchema
    ?? throw new InvalidDataException("Falha ao desserializar o XML para NFSeSchema.");

var numeroNFSe = nfse.infNFSe?.nNFSe;

var outputDir = Path.GetDirectoryName(filePathXml)
    ?? throw new InvalidOperationException("Não foi possível obter o diretório do XML.");

var outputPdfPath = Path.Combine(outputDir, $"Danfe_{numeroNFSe}.pdf");

var danfeService = new DanfeService();

var result = danfeService.Generate(nfse, DanfeEnvironment.Production);

if (result.PdfBytes == null || result.PdfBytes.Length == 0)
    throw new InvalidOperationException("GeraDanfe retornou null ou bytes vazios (PDF).");

if (result.Warnings.Count > 0)
{
    Console.WriteLine("Warnings encontrados durante a geração do DANFE:");
    foreach (var warning in result.Warnings)
    {
        Console.WriteLine($"- [{warning.Code}] {warning.Message} (Path: {warning.Path})");
    }
}

await File.WriteAllBytesAsync(outputPdfPath, result.PdfBytes);

Console.WriteLine($"DANFE gerado com sucesso: {outputPdfPath}");
