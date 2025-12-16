namespace Direction.NFSe.Danfe;

/// <summary>
/// Opções para localizar template/arquivos auxiliares e ajustar comportamento.
/// </summary>
public sealed class DanfeOptions
{
    /// <summary>Font-Family usada na Danfe</summary>
    public string? FontFamily { get; init; }
    /// <summary>Tamanho da fonte padrão (pixels)</summary>
    public string? FontSize { get; init; }
    /// <summary>Tamanho da fonte dos headers (pixels)</summary>
    public string? FontSizeHeader { get; init; }
    /// <summary>Tamanho da fonte do QrCode (pixels)</summary>
    public string? FontSizeQrCode { get; init; }
    /// <summary>Path da logo da NFSe</summary>
    public string? LogoNFSePath { get; init; }
    /// <summary>Diretório base para resolução de paths relativos (default: AppDomain.CurrentDomain.BaseDirectory).</summary>
    public string? BasePath { get; init; }

    /// <summary>Path completo do template HTML. Se nulo, usa {BasePath}/Templates/Danfe.html</summary>
    public string? TemplatePath { get; init; }

    /// <summary>Path do CSV de estados. Se nulo, usa {BasePath}/Assets/estados.csv</summary>
    public string? EstadosCsvPath { get; init; }

    /// <summary>Path do CSV de municípios. Se nulo, usa {BasePath}/Assets/municipios.csv</summary>
    public string? MunicipiosCsvPath { get; init; }

    /// <summary>Se true, inicializa automaticamente o cache de municípios ao gerar o PDF.</summary>
    public bool AutoInitializeMunicipios { get; init; } = true;
}
