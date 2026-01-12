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

        decimal vDescCond = infDps.valores?.vDescCondIncond?.vDescCond ?? 0M;
        decimal vDescIncond = infDps.valores?.vDescCondIncond?.vDescIncond ?? 0M;

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

        var cLocIncid = inf.cLocIncid;
        var municpioISSQN = cLocIncid != null ? MunicipiosIbge.GetMunicipio(Int32.Parse(cLocIncid)) : null;
        if (cLocPrest == null)
            warnings.FieldMissing("cLocIncid", "infNFSe.cLocIncid", "-");
        else if (municpioISSQN == null)
            warnings.MunicipioNotFound("infNFSe.cLocIncid");

        var logoBase64 = Helper.GetLogo(Path.Combine(AppContext.BaseDirectory, municipioPrestador?.LogoPath ?? string.Empty));

        // Logo da nfse
        var logoNfse = _options.LogoNFSePath != null ? Helper.GetLogo(_options.LogoNFSePath) : Helper.GetLogo(Path.Combine(AppContext.BaseDirectory, "Assets", "Logos", "nfse.png"));

        // Caminhos/valores auxiliares
        int? tpRetIssqn = infDps.valores?.trib?.tribMun?.tpRetISSQN;
        if (tpRetIssqn == null || tpRetIssqn.Value == 0) //verificação necessária pois alguns xml simplesmente não preenchem o BM
            tpRetIssqn = infDps.valores?.trib?.tribMun?.BM?.tpRetISSQN;
        int? opSimpNac = infDps.prest?.regTrib?.opSimpNac;
        int? tpRetPisCofins = infDps.valores?.trib?.tribFed?.piscofins?.tpRetPisCofins;

        decimal? vAliqAplic = valores?.pAliqAplic;
        decimal? vIssqn = valores?.vISSQN;
        decimal? vLiq = valores?.vLiq;
        decimal? vIRRF = infDps.valores?.trib?.tribFed?.vRetIRRF;
        decimal? vCOFINS = infDps.valores?.trib?.tribFed?.piscofins?.vCofins;
        decimal? vPIS = infDps.valores?.trib?.tribFed?.piscofins?.vPis;
        decimal? vCP = infDps.valores?.trib?.tribFed?.vRetCP;
        decimal? vCSLL = infDps.valores?.trib?.tribFed?.vRetCSLL;

        decimal? vTotTribFed = infDps.valores?.trib?.totTrib?.vTotTrib?.vTotTribFed;
        if (vTotTribFed == null || (vTotTribFed.HasValue && vTotTribFed.Value == 0M)) //nem sempre o objeto totalizador é informado no xml
            vTotTribFed = (vIRRF ?? 0M) + (vPIS ?? 0M) + (vCOFINS ?? 0M) + (vCP ?? 0M) + (vCSLL ?? 0M);

        decimal vTotalRetFed = (vIRRF ?? 0M) + (vCP ?? 0M) + (vCSLL ?? 0M);

        decimal vRetPisCofins = 0M;
        switch (tpRetPisCofins)
        {
            case 1: // 1 - PIS/COFINS Retido
                vRetPisCofins = (vPIS ?? 0M) + (vPIS ?? 0M);
                break;
            case 2: // 2 - PIS / COFINS Não Retido
            default:
                vRetPisCofins = 0M;
                break;
            case 3: // 3 - PIS Retido / COFINS Não Retido
                vRetPisCofins = (vPIS ?? 0M);
                break;
            case 4: // 4 - PIS Não Retido/ COFINS Retido;
                vRetPisCofins = (vCOFINS ?? 0M);
                break;
        }

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
            ["{{PREST_CNPJ}}"] = !string.IsNullOrEmpty(infDps.prest?.CPF) ? DanfeFallback.OrDash(Helper.FormatCpf(infDps.prest?.CPF), warnings, fieldName: "CNPJ Prestador", path: "infNFSe.DPS.InfDPS.prest.CPF")
                : DanfeFallback.OrDash(Helper.FormatCnpj(infDps.prest?.CNPJ), warnings, fieldName: "CNPJ Prestador", path: "infNFSe.DPS.InfDPS.prest.CNPJ"),
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
            ["{{TOMA_CNPJ}}"] = !string.IsNullOrEmpty(infDps.toma?.CPF) ? DanfeFallback.OrDash(Helper.FormatCpf(infDps.toma?.CPF), warnings, fieldName: "CNPJ Tomador", path: "infNFSe.DPS.InfDPS.toma.CPF")
                : DanfeFallback.OrDash(Helper.FormatCnpj(infDps.toma?.CNPJ), warnings, "CNPJ Tomador", "infNFSe.DPS.InfDPS.toma.CNPJ"),
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
            ["{{SERV_PAIS}}"] = DanfeFallback.OrDash(infDps.serv?.locPrest?.cPaisPrestacao, warnings, "País da Prestação", "infNFSe.DPS.InfDPS.serv.locPrest.cPaisPrestacao"),

            // Tributação Municipal
            ["{{ISS_TRIBUTACAO}}"] = GetDescricaoTributacao(infDps.valores?.trib?.tribMun?.tribISSQN),
            //TO DO: verificar se esse pais é o de prestação ou do tomador
            ["{{ISS_PAIS}}"] = DanfeFallback.OrDash(infDps.toma?.end?.endExt?.cPais, warnings, "País Resultado da Prestação do Serviço", "infNFSe.DPS.InfDPS.toma.end.endExt.cPais"),
            ["{{ISS_MUN_INC}}"] = DanfeFallback.OrDash(municpioISSQN?.NomeComUf, warnings, "Município Incidência", "MunicipiosIbge.GetMunicipio(cLocIncid).NomeComUf"),
            ["{{ISS_REGIME}}"] = GetDescricaoRegimeEspecial(infDps.prest?.regTrib?.regEspTrib),
            ["{{ISS_OPERACAO}}"] = GetDescricaoTipoImunidade(infDps.valores?.trib?.tribMun?.tpImunidade),
            ["{{ISS_SUSPENSAO}}"] = GetDescricaoTipoSuspensaoISSQN(infDps.valores?.trib?.tribMun?.exigSusp?.tpSusp),
            ["{{ISS_PROCESSO}}"] = DanfeFallback.OrDash(infDps.valores?.trib?.tribMun?.exigSusp?.nProcesso, warnings, "Número Processo Suspensão", "infNFSe.DPS.InfDPS.valores.trib.tribMun.exigSusp.nProcesso"),
            ["{{ISS_BENEFICIO}}"] = DanfeFallback.OrDash(infDps.valores?.trib?.tribMun?.BM?.nBM.ToString(), warnings, "Benefício Municipal", "infNFSe.DPS.InfDPS.valores.trib.tribMun.BM.nBM"),
            ["{{ISS_DESC_INCOND}}"] = DanfeFallback.OrCurrency(infDps.valores?.vDescCondIncond?.vDescIncond, ptBR, warnings, "vDescIncond", "infNFSe.valores.vDescCondIncond.vDescIncond"),
            ["{{ISS_DEDUCOES}}"] = DanfeFallback.OrCurrency(infDps.valores?.vDedRed?.vDR, ptBR, warnings, "vDR", "infNFSe.valores.vDedRed.vDR"),
            ["{{ISS_CALCULO}}"] = DanfeFallback.OrCurrency(infDps.valores?.trib?.tribMun?.BM?.vRedBCBM, ptBR, warnings, "vRedBCBM", "infNFSe.valores.trib.tribMun.BM.vRedBCBM"), //TO DO: verificar no futuro se Calculo do BM realmente se refere a esse campo
            ["{{ISS_BC}}"] = (tpRetIssqn == 2 || opSimpNac == 1) ? vServico.ToString("C", ptBR) : "-",
            ["{{ISS_ALIQ}}"] = (tpRetIssqn == 2 || opSimpNac == 1) ? DanfeFallback.OrPercent(vAliqAplic, ptBR, warnings, "pAliqAplic", "infNFSe.valores.pAliqAplic") : "-",
            ["{{ISS_RETENCAO}}"] = GetDescricaoRetencao(tpRetIssqn),
            ["{{ISS_APURADO}}"] = (tpRetIssqn == 2 || opSimpNac == 1) ? DanfeFallback.OrCurrency(vIssqn, ptBR, warnings, "vISSQN", "infNFSe.valores.vISSQN") : "-",

            // Tributação Federal
            ["{{FED_IRRF}}"] = DanfeFallback.OrCurrency(vIRRF, ptBR, warnings, "vIRRF", "infDps.valores.trib.tribFed.vRetIRRF"),
            ["{{FED_PIS}}"] = DanfeFallback.OrCurrency(vPIS, ptBR, warnings, "vPIS", "infNFSe.valores.trib.tribFed.piscofins.vPis"),
            ["{{FED_COFINS}}"] = DanfeFallback.OrCurrency(vCOFINS, ptBR, warnings, "vCOFINS", "infDps.valores.trib.tribFed.piscofins.vCofins"),
            ["{{FED_CSLL}}"] = DanfeFallback.OrCurrency(vCSLL, ptBR, warnings, "vCSLL", "infDps.valores.trib.tribFed.vRetCSLL"),
            ["{{FED_CP}}"] = DanfeFallback.OrCurrency(vCP, ptBR, warnings, "vCP", "infDps.valores.trib.tribFed.vRetCP"),
            ["{{FED_RET_PISCOFINS}}"] = GetDescricaoTipoRetencaoPisCofins(infDps.valores?.trib?.tribFed?.piscofins?.tpRetPisCofins),
            ["{{FED_TOTAL}}"] = DanfeFallback.OrCurrency(vTotTribFed, ptBR, warnings, "vTotTribFed", "infDps.valores.trib.totTrib.vTotTrib.vTotTribFed"),

            // Valores
            ["{{VALOR_SERVICO}}"] = vServico.ToString("C", ptBR),
            ["{{VALOR_LIQUIDO}}"] = DanfeFallback.OrCurrency(vLiq, ptBR, warnings, "vLiq", "infNFSe.valores.vLiq"),
            ["{{DESC_COND}}"] = vDescCond != 0 ? vDescCond.ToString("C", ptBR) : "R$",
            ["{{DESC_INCOND}}"] = vDescIncond != 0 ? vDescIncond.ToString("C", ptBR) : "R$",
            ["{{ISS_RETIDO}}"] = (tpRetIssqn == 2) ? DanfeFallback.OrCurrency(vIssqn, ptBR, warnings, "vISSQN", "infNFSe.valores.vISSQN") : "-",
            //["{{FED_RETIDOS}}"] = (tpRetIssqn == 2) ? "R$ 0,00" : "-",
            ["{{FED_RETIDOS}}"] = vTotalRetFed == 0M ? "-" : vTotalRetFed.ToString("C", ptBR),
            ["{{PISCOFINS_RET}}"] = vRetPisCofins != 0 ? vRetPisCofins.ToString("C", ptBR) : "-",

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

    private string GetDescricaoTipoRetencaoPisCofins(int? tpRetPisCofins)
    {
        /*
           Tipo de retenção ao do PIS/COFINS:

            1 - PIS/COFINS Retido;
            2 - PIS/COFINS Não Retido;
            3 - PIS Retido/COFINS Não Retido;
            4 - PIS Não Retido/COFINS Retido;
         */
        switch (tpRetPisCofins)
        {
            case 1:
                return "PIS/COFINS Retido";
            case 2:
                return "PIS/COFINS Não Retido";
            case 3:
                return "PIS Retido/COFINS Não Retido";
            case 4:
                return "PIS Não Retido/COFINS Retido";
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

    private string GetDescricaoTipoImunidade(int? tpImunidade)
    {
        /*
           Tipos de Imunidades municipais:
            0 - Imunidade (tipo não informado na nota de origem);
            1 - Patrimônio, renda ou serviços, uns dos outros (CF88, Art 150, VI, a);
            2 - Entidades religiosas e templos de qualquer culto, inclusive suas organizações assistenciais e beneficentes (CF88, Art 150, VI, b);
            3 - Patrimônio, renda ou serviços dos partidos políticos, inclusive suas fundações, das entidades sindicais dos trabalhadores, das instituições de educação e de assistência social, sem fins lucrativos, atendidos os requisitos da lei (CF88, Art 150, VI, c);
            4 - Livros, jornais, periódicos e o papel destinado a sua impressão (CF88, Art 150, VI, d);
            5 - Fonogramas e videofonogramas musicais produzidos no Brasil contendo obras musicais ou literomusicais de autores brasileiros e/ou obras em geral interpretadas por artistas brasileiros bem como os suportes materiais ou arquivos digitais que os contenham, salvo na etapa de replicação industrial de mídias ópticas de leitura a laser.   (CF88, Art 150, VI, e);
         */
        switch (tpImunidade)
        {
            case 0:
                return "Nenhum";
            case 1:
                return "Patrimônio, renda ou serviços, uns dos outros";
            case 2:
                return "Entidades religiosas e templos de qualquer culto";
            case 3:
                return "Patrimônio, renda ou serviços dos partidos políticos";
            case 4:
                return "Livros, jornais, periódicos e o papel destinado a sua impressão";
            case 5:
                return "Fonogramas e videofonogramas musicais produzidos no Brasil";
            default:
                return "-";
        }
    }

    private string GetDescricaoTipoSuspensaoISSQN(int? tpSusp)
    {
        /*
           Opção para Exigibilidade Suspensa:

            1 - Exigibilidade do ISSQN Suspensa por Decisão Judicial;
            2 - Exigibilidade do ISSQN Suspensa por Processo Administrativo;
         */
        switch (tpSusp)
        {
            case 0:
                return "Não";
            case 1:
                return "Suspensa por Decisão Judicial";
            case 2:
                return "Suspensa por Processo Administrativo";
            default:
                return "-";
        }
    }
}
