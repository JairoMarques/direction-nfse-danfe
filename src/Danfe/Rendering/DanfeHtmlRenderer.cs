using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Direction.NFSe.Danfe;

public sealed class DanfeHtmlRenderer
{
    private const string TransparentPixelBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR4nGNgYAAAAAMAASsJTYQAAAAASUVORK5CYII=";

    private readonly string _templatePath;
    private readonly DanfeOptions _options;

    public DanfeHtmlRenderer(DanfeOptions options)
    {
        _options = options ?? new DanfeOptions();

        var basePath = _options.BasePath ?? AppContext.BaseDirectory;
        _templatePath = _options.TemplatePath ?? Path.Combine(basePath, "Assets", "Templates", "Danfe.html");
    }

    public (string Html, IReadOnlyList<DanfeWarning> Warnings) Render(NFSeSchema nfse, DanfeEnvironment environment, bool isCancelled = false)
    {
        if (nfse == null) throw new ArgumentNullException(nameof(nfse));
        if (nfse.infNFSe == null) throw new ArgumentException("NFSe.infNFSe não pode ser nulo", nameof(nfse));

        var warnings = new DanfeWarningCollector();

        var isProd = environment == DanfeEnvironment.Production;

        var validade = isProd ? "" : "NFS-e SEM VALIDADE JURÍDICA";
        var template = File.ReadAllText(_templatePath, Encoding.UTF8);

        // Root shortcuts (evita repetir cadeia e facilita paths)
        var inf = nfse.infNFSe;
        var dps = inf.DPS;
        var infDps = dps?.InfDPS;
        var valores = inf.valores;

        // Campos básicos (se algum deles for crítico e estiver nulo, melhor lançar)
        if (dps == null) throw new ArgumentException("NFSe.infNFSe.DPS não pode ser nulo", nameof(nfse));
        if (infDps == null) throw new ArgumentException("NFSe.infNFSe.DPS.InfDPS não pode ser nulo", nameof(nfse));
        if (string.IsNullOrWhiteSpace(inf.Id)) throw new ArgumentException("NFSe.infNFSe.Id não pode ser nulo/vazio", nameof(nfse));

        string numeroNfse = inf.nNFSe.ToString();
        string numeroDps = infDps.nDPS.ToString();
        string serieDps = infDps.serie.ToString();

        DateTime? competencia = Helper.TryParseDate(infDps.dCompet);
        if (!competencia.HasValue) warnings.FieldMissing("dCompet", "infNFSe.DPS.InfDPS.dCompet", "-");

        DateTime? dhEmissaoNfs = inf.dhProc;
        if (!dhEmissaoNfs.HasValue) warnings.FieldMissing("dhProc", "infNFSe.dhProc", "-");

        DateTime? dhEmissaoDps = Helper.TryParseDateTime(infDps.dhEmi);
        if (!dhEmissaoDps.HasValue) warnings.FieldMissing("dhEmi", "infNFSe.DPS.InfDPS.dhEmi", "-");

        decimal vServico = infDps.valores?.vServPrest?.vServ ?? 0m;
        if (infDps.valores?.vServPrest?.vServ == null) warnings.FieldMissing("vServPrest.vServ", "infNFSe.DPS.InfDPS.valores.vServPrest.vServ", "0,00");

        string chaveAcesso = inf.Id!.Substring(3);
        if (string.IsNullOrWhiteSpace(chaveAcesso)) warnings.FieldMissing("chaveAcesso", "infNFSe.Id", string.Empty);

        var ptBR = new CultureInfo("pt-BR");

        // Tributação (se ficar vazio, warning)
        var cTribNac = infDps.serv?.cServ?.cTribNac;
        var xTribNac = inf.xTribNac;

        var descricaoTributoNacional = $"{Regex.Replace(cTribNac ?? string.Empty, @"(\d{2})(\d{2})(\d{2})", "$1.$2.$3")} - {xTribNac}";

        if (string.IsNullOrWhiteSpace(cTribNac) && string.IsNullOrWhiteSpace(xTribNac)) warnings.FieldMissing("cTribNac/xTribNac", "infNFSe.DPS.InfDPS.serv.cServ.cTribNac | infNFSe.xTribNac", "-");

        var cTribMun = infDps.serv?.cServ?.cTribMun;
        var xTribMun = inf.xTribMun;

        var descricaoTributoMunicipal = string.IsNullOrWhiteSpace(cTribMun) ? (xTribMun ?? string.Empty) : $"{cTribMun} - {xTribMun}";

        if (string.IsNullOrWhiteSpace(descricaoTributoMunicipal)) warnings.FieldMissing("cTribMun/xTribMun", "infNFSe.DPS.InfDPS.serv.cServ.cTribMun | infNFSe.xTribMun", "-");

        // QRCode
        string url = $"https://www.{(isProd ? "" : "producaorestrita.")}nfse.gov.br/ConsultaPublica/?tpc=1&chave={chaveAcesso}";
        var bytes = Helper.GetQrCode(url);
        var imgQrCodeSrc = $"data:image/png;base64,{Convert.ToBase64String(bytes)}";

        // Municípios (auto init)
        if (_options.AutoInitializeMunicipios)
        {
            var basePath = _options.BasePath ?? AppContext.BaseDirectory;
            var estados = _options.EstadosCsvPath ?? Path.Combine(basePath, "Assets", "estados.csv");
            var municipios = _options.MunicipiosCsvPath ?? Path.Combine(basePath, "Assets", "municipios.csv");
            MunicipiosIbge.Initialize(estados, municipios);
        }

        // Município prestador
        var cLocPrest = infDps.serv?.locPrest?.cLocPrestacao;
        var municipioPrestador = cLocPrest != null ? MunicipiosIbge.GetMunicipio(cLocPrest) : null;
        if (cLocPrest == null)
            warnings.FieldMissing("cLocPrestacao", "infNFSe.DPS.InfDPS.serv.locPrest.cLocPrestacao", "-");
        else if (municipioPrestador == null)
            warnings.MunicipioNotFound("infNFSe.DPS.InfDPS.serv.locPrest.cLocPrestacao");

        var logoBase64 = Helper.GetLogo(Path.Combine(AppContext.BaseDirectory, municipioPrestador?.LogoPath ?? string.Empty));

        // Logo da nfse
        var logoNfse = _options.LogoNFSePath != null ? Helper.GetLogo(_options.LogoNFSePath) : Helper.GetLogo(Path.Combine(AppContext.BaseDirectory, "Assets", "Logos", "nfse.png"));

        // Caminhos/valores auxiliares
        int? tpRetIssqn = infDps.valores?.trib?.tribMun?.tpRetISSQN;
        int? opSimpNac = infDps.prest?.regTrib?.opSimpNac;

        decimal? vAliqAplic = valores?.pAliqAplic;
        decimal? vIssqn = valores?.vISSQN;
        decimal? vLiq = valores?.vLiq;

        // Verifica ses a NFSe está cancelada
        string canceladaDiv = isCancelled
            ? @"<div style=""
              position:absolute;
              top:50%;
              left:50%;
              display:inline-block;                 /* importante */
              -webkit-transform: translate(-50%, -50%) rotate(-30deg);
              transform: translate(-50%, -50%) rotate(-30deg);
              -webkit-transform-origin: 50% 50%;
              transform-origin: 50% 50%;
              font-size:96px;
              font-weight:800;
              color: rgba(200,0,0,0.18);
              border: 8px solid rgba(200,0,0,0.18);
              padding: 20px 40px;
              text-transform:uppercase;
              z-index:9999;
              pointer-events:none;
              white-space:nowrap;"">
                          CANCELADA
              </div>"
            : string.Empty;

        // Monta mapa de placeholders (agora com warnings)
        var map = new Dictionary<string, string>
        {
            // Cancelada
            ["{{NFSE_CANCELADA_DIV}}"] = canceladaDiv,
            // Fonts
            ["{{FONT_FAMILY}}"] = _options.FontFamily ?? "Verdana, Helvetica, sans-serif;",
            ["{{FONT_SIZE}}"] = _options.FontSize ?? "12px;",
            ["{{FONT_SIZE_HEADER}}"] = _options.FontSize ?? "14px;",
            ["{{FONT_SIZE_QRCODE}}"] = _options.FontSize ?? "11px;",
            // Logos
            ["{{NFSE_LOGO}}"] = logoNfse ?? TransparentPixelBase64,
            ["{{PREFEITURA_LOGO}}"] = logoBase64 ?? TransparentPixelBase64,
            ["{{LOGO_NAME}}"] = DanfeFallback.OrDash(municipioPrestador?.LogoName, warnings, fieldName: "LogoName", path: "MunicipiosIbge.GetMunicipio(...).LogoName"),

            // Cabeçalho
            ["{{VALIDADE_JURIDICA}}"] = validade,
            ["{{CHAVE_ACESSO}}"] = DanfeFallback.OrDash(chaveAcesso, warnings, fieldName: "chaveAcesso", path: "infNFSe.Id"),

            // QrCode
            ["{{QRCODE_SRC}}"] = imgQrCodeSrc,

            // Dados NFSe / DPS
            ["{{NUMERO_NFSE}}"] = numeroNfse,
            ["{{NUMERO_DPS}}"] = DanfeFallback.OrDash(numeroDps, warnings, "nDPS", "infNFSe.DPS.InfDPS.nDPS"),
            ["{{SERIE_DPS}}"] = DanfeFallback.OrDash(serieDps, warnings, "serie", "infNFSe.DPS.InfDPS.serie"),
            ["{{COMPETENCIA}}"] = competencia?.ToString("dd/MM/yyyy") ?? DanfeFallback.OrDash(null, warnings, "dCompet", "infNFSe.DPS.InfDPS.dCompet"),
            ["{{DATA_HORA_EMISSAO}}"] = dhEmissaoNfs?.ToString("dd/MM/yyyy HH:mm:ss") ?? DanfeFallback.OrDash(null, warnings, "dhProc", "infNFSe.dhProc"),
            ["{{DATA_HORA_EMISSAO_DPS}}"] = dhEmissaoDps?.ToString("dd/MM/yyyy HH:mm:ss") ?? DanfeFallback.OrDash(null, warnings, "dhEmi", "infNFSe.DPS.InfDPS.dhEmi"),

            // Prestador
            ["{{PREST_SERV}}"] = GetDescricaoEmitente(infDps.tpEmit),
            ["{{PREST_CNPJ}}"] = DanfeFallback.OrDash(Helper.FormatCnpj(infDps.prest?.CNPJ), warnings, fieldName: "CNPJ Prestador", path: "infNFSe.DPS.InfDPS.prest.CNPJ"),
            ["{{PREST_IM}}"] = DanfeFallback.OrDash(infDps.prest?.IM, warnings, "IM Prestador", "infNFSe.DPS.InfDPS.prest.IM"),
            ["{{PREST_RAZAO}}"] = DanfeFallback.OrDash(inf.emit?.xNome, warnings, "xNome Prestador", "infNFSe.emit.xNome"),
            ["{{PREST_ENDERECO}}"] = DanfeFallback.OrDash(Helper.BuildEndereco(inf.emit?.enderNac), warnings, "Endereço Prestador", "infNFSe.emit.enderNac"),
            ["{{PREST_MUNICIPIO}}"] = DanfeFallback.OrDash($"{inf.xLocEmi} - {inf.emit?.enderNac?.UF}", warnings, "Município/UF Prestador", "infNFSe.xLocEmi | infNFSe.emit.enderNac.UF"),
            ["{{PREST_CEP}}"] = DanfeFallback.OrDash(Helper.FormatCep(inf.emit?.enderNac?.CEP), warnings, "CEP Prestador", "infNFSe.emit.enderNac.CEP"),
            ["{{PREST_FONE}}"] = DanfeFallback.OrDash(Helper.FormatTelefone(infDps.prest?.fone), warnings, "Fone Prestador", "infNFSe.DPS.InfDPS.prest.fone"),
            ["{{PREST_EMAIL}}"] = DanfeFallback.OrDash(infDps.prest?.email, warnings, "Email Prestador", "infNFSe.DPS.InfDPS.prest.email"),
            ["{{PREST_SIMPLES}}"] = GetDescricaoPrestadorSimples(infDps.prest?.regTrib?.opSimpNac),
            ["{{PREST_REGIME_SN}}"] = GetDescricaoRegimeSimples(infDps.prest?.regTrib?.regApTribSN),

            // Tomador
            ["{{TOMA_CNPJ}}"] = DanfeFallback.OrDash(Helper.FormatCnpj(infDps.toma?.CNPJ), warnings, "CNPJ Tomador", "infNFSe.DPS.InfDPS.toma.CNPJ"),
            ["{{TOMA_IM}}"] = DanfeFallback.OrDash(infDps.toma?.IM),
            ["{{TOMA_RAZAO}}"] = DanfeFallback.OrDash(infDps.toma?.xNome, warnings, "xNome Tomador", "infNFSe.DPS.InfDPS.toma.xNome"),
            ["{{TOMA_ENDERECO}}"] = DanfeFallback.OrDash(Helper.BuildEndereco(infDps.toma?.end), warnings, "Endereço Tomador", "infNFSe.DPS.InfDPS.toma.end"),
            ["{{TOMA_CEP}}"] = DanfeFallback.OrDash(Helper.FormatCep(infDps.toma?.end?.endNac?.CEP), warnings, "CEP Tomador", "infNFSe.DPS.InfDPS.toma.end.endNac.CEP"),
            ["{{TOMA_CMUN}}"] = ResolveMunicipioNomeComUf(infDps.toma?.end?.endNac?.cMun, warnings, "infNFSe.DPS.InfDPS.toma.end.endNac.cMun"),
            ["{{TOMA_EMAIL}}"] = DanfeFallback.OrDash(infDps.toma?.email, warnings, "Email Tomador", "infNFSe.DPS.InfDPS.toma.email"),
            ["{{TOMA_FONE}}"] = DanfeFallback.OrDash(Helper.FormatTelefone(infDps.toma?.fone), warnings, "Fone Tomador", "infNFSe.DPS.InfDPS.toma.fone"),

            // Serviço
            ["{{SERV_CTRIBNAC}}"] = DanfeFallback.OrDash(descricaoTributoNacional, warnings, "Descrição Tributo Nacional", "infNFSe.DPS.InfDPS.serv.cServ.cTribNac | infNFSe.xTribNac").Limit(80),
            ["{{SERV_CTRIBMUN}}"] = DanfeFallback.OrDash(descricaoTributoMunicipal, warnings, "Descrição Tributo Municipal", "infNFSe.DPS.InfDPS.serv.cServ.cTribMun | infNFSe.xTribMun").Limit(80),
            ["{{SERV_NBS}}"] = DanfeFallback.OrDash(infDps.serv?.cServ?.cNBS.ToString(), warnings, "cNBS", "infNFSe.DPS.InfDPS.serv.cServ.cNBS"),
            ["{{SERV_DESC_HTML}}"] = Helper.BuildDescricaoServicoHtml(infDps.serv?.cServ?.xDescServ),
            ["{{SERV_LOCAL}}"] = DanfeFallback.OrDash(municipioPrestador?.NomeComUf, warnings, "Município Prestação", "MunicipiosIbge.GetMunicipio(cLocPrestacao).NomeComUf"),
            ["{{SERV_PAIS}}"] = "-", // TODO

            // Tributação Municipal
            ["{{ISS_TRIBUTACAO}}"] = GetDescricaoTributacao(infDps.valores?.trib?.tribMun?.tribISSQN),
            ["{{ISS_PAIS}}"] = "-",
            ["{{ISS_MUN_INC}}"] = DanfeFallback.OrDash(municipioPrestador?.NomeComUf, warnings, "Município Incidência", "MunicipiosIbge.GetMunicipio(cLocPrestacao).NomeComUf"),
            ["{{ISS_REGIME}}"] = GetDescricaoRegimeEspecial(infDps.prest?.regTrib?.regEspTrib),
            ["{{ISS_OPERACAO}}"] = "-",
            ["{{ISS_SUSPENSAO}}"] = "Não",
            ["{{ISS_PROCESSO}}"] = "-",
            ["{{ISS_BENEFICIO}}"] = "-",
            ["{{ISS_DESC_INCOND}}"] = "-",
            ["{{ISS_DEDUCOES}}"] = "-",
            ["{{ISS_CALCULO}}"] = "-",
            ["{{ISS_BC}}"] = (tpRetIssqn == 2 || opSimpNac == 1) ? vServico.ToString("C", ptBR) : "-",
            ["{{ISS_ALIQ}}"] = (tpRetIssqn == 2 || opSimpNac == 1) ? DanfeFallback.OrPercent(vAliqAplic, ptBR, warnings, "pAliqAplic", "infNFSe.valores.pAliqAplic") : "-",
            ["{{ISS_RETENCAO}}"] = GetDescricaoRetencao(tpRetIssqn),
            ["{{ISS_APURADO}}"] = (tpRetIssqn == 2 || opSimpNac == 1) ? DanfeFallback.OrCurrency(vIssqn, ptBR, warnings, "vISSQN", "infNFSe.valores.vISSQN") : "-",

            // Tributação Federal (TODO)
            ["{{FED_IRRF}}"] = "-",
            ["{{FED_PIS}}"] = "-",
            ["{{FED_COFINS}}"] = "-",
            ["{{FED_CSLL}}"] = "-",
            ["{{FED_CP}}"] = "-",
            ["{{FED_RET_PISCOFINS}}"] = "-",
            ["{{FED_TOTAL}}"] = "-",

            // Valores
            ["{{VALOR_SERVICO}}"] = vServico.ToString("C", ptBR),
            ["{{VALOR_LIQUIDO}}"] = DanfeFallback.OrCurrency(vLiq, ptBR, warnings, "vLiq", "infNFSe.valores.vLiq"),
            ["{{DESC_COND}}"] = "R$",
            ["{{DESC_INCOND}}"] = "R$",
            ["{{ISS_RETIDO}}"] = (tpRetIssqn == 2) ? DanfeFallback.OrCurrency(vIssqn, ptBR, warnings, "vISSQN", "infNFSe.valores.vISSQN") : "-",
            ["{{FED_RETIDOS}}"] = (tpRetIssqn == 2) ? "R$ 0,00" : "-",
            ["{{PISCOFINS_RET}}"] = "-",

            // Totais tributos
            ["{{TOT_FED}}"] = DanfeFallback.OrDash(infDps.valores?.trib?.totTrib?.pTotTrib?.pTotTribFed.ToString(CultureInfo.InvariantCulture)),
            ["{{TOT_EST}}"] = DanfeFallback.OrDash(infDps.valores?.trib?.totTrib?.pTotTrib?.pTotTribEst.ToString(CultureInfo.InvariantCulture)),
            ["{{TOT_MUN}}"] = DanfeFallback.OrDash(infDps.valores?.trib?.totTrib?.pTotTrib?.pTotTribMun.ToString(CultureInfo.InvariantCulture)),

            // Inf complementares
            ["{{INF_COMPLEMENTARES}}"] = Helper.BuildInfComplementares(infDps.serv)
        };

        // Aplica os replaces
        foreach (var kv in map)
        {
            bool isRawHtml =
                kv.Key == "{{SERV_DESC_HTML}}" ||
                kv.Key == "{{INF_COMPLEMENTARES}}" ||
                kv.Key == "{{LOGO_NAME}}" ||
                kv.Key == "{{NFSE_CANCELADA_DIV}}";

            string value = isRawHtml ? kv.Value : Helper.HtmlEncode(kv.Value);
            template = template.Replace(kv.Key, value ?? string.Empty);
        }

        // Detecta placeholders não resolvidos (opcional, mas recomendado)
        foreach (var placeholder in map.Keys)
        {
            if (template.Contains(placeholder))
            {
                warnings.TemplatePlaceholderEmpty(placeholder);
                template = template.Replace(placeholder, string.Empty);
            }
        }

        return (template, warnings.Warnings);
    }

    // Helper local: resolve município do tomador sem explodir e com warning
    private static string ResolveMunicipioNomeComUf(int? cMun, DanfeWarningCollector warnings, string path)
    {
        if (!cMun.HasValue)
        {
            warnings.FieldMissing("cMun", path, "-");
            return "-";
        }

        var mun = MunicipiosIbge.GetMunicipio(cMun.Value);
        if (mun == null)
        {
            warnings.MunicipioNotFound(path);
            return "-";
        }

        return DanfeFallback.OrDash(mun.NomeComUf, warnings, "NomeComUf", path);
    }


    private string GetDescricaoRetencao(int? tpRetISSQN)
    {
        switch (tpRetISSQN)
        {
            case 1:
                return "Não Retido";
            case 2:
                return "Retido pelo Tomador";
            case 3:
                return "Retido pelo Intermediario";
            default:
                return "-";
        }
    }

    private string GetDescricaoTributacao(int? tribISSQN)
    {
        switch (tribISSQN)
        {
            case 1:
                return "Operação Tributável";
            case 2:
                return "Imunidade";
            case 3:
                return "Exportação de serviço";
            case 4:
                return "Não Incidência";
            default:
                return "";
        }
    }

    private string GetDescricaoEmitente(int tpEmis)
    {
        switch (tpEmis)
        {
            case 1:
                return "Prestador do Serviço";
            case 2:
                return "Tomador do Serviço";
            case 3:
                return "Intermediário";
            default:
                return "-";
        }
    }

    private string GetDescricaoPrestadorSimples(int? opSimpNac)
    {
        switch (opSimpNac)
        {
            case 1:
                return "Não Optante";
            case 2:
                return "Optante - Microempreendedor Individual(MEI)";
            case 3:
                return "Optante - Microempresa ou Empresa de Pequeno Porte (ME/EPP)";
            default:
                return "-";
        }
    }
    private string GetDescricaoRegimeSimples(int? regApTribSN)
    {
        /*
          Opção para que o contribuinte optante pelo Simples Nacional ME/EPP (opSimpNac = 3) possa indicar, ao emitir o documento fiscal, em qual regime de apuração os tributos federais e municipal estão inseridos, caso tenha ultrapassado algum sublimite ou limite definido para o Simples Nacional.
            1 – Regime de apuração dos tributos federais e municipal pelo SN;
            2 – Regime de apuração dos tributos federais pelo SN e ISSQN  por fora do SN conforme respectiva legislação municipal do tributo;
            3 – Regime de apuração dos tributos federais e municipal por fora do SN conforme respectivas legilações federal e municipal de cada tributo;
         */
        switch (regApTribSN)
        {
            case 1:
                return "Regime de apuração dos tributos federais e municipal pelo Simples Nacional";
            case 2:
                return "Regime de apuração dos tributos federais pelo SN e ISSQN  por fora do SN conforme respectiva legislação municipal do tributo";
            case 3:
                return "Regime de apuração dos tributos federais e municipal por fora do SN conforme respectivas legilações federal e municipal de cada tributo";
            default:
                return "-";
        }
    }
    private string GetDescricaoRegimeEspecial(int? regEspTrib)
    {
        /*
           Tipos de Regimes Especiais de Tributação:
            0 - Nenhum;
            1 - Ato Cooperado (Cooperativa);
            2 - Estimativa;
            3 - Microempresa Municipal;
            4 - Notário ou Registrador;
            5 - Profissional Autônomo;
            6 - Sociedade de Profissionais;
         */
        switch (regEspTrib)
        {
            case 0:
                return "Nenhum";
            case 1:
                return "Ato Cooperado (Cooperativa)";
            case 2:
                return "Estimativa";
            case 3:
                return "Microempresa Municipal";
            case 4:
                return "Notário ou Registrador";
            case 5:
                return "Profissional Autônomo";
            case 6:
                return "Sociedade de Profissionais";
            default:
                return "-";
        }
    }
}
