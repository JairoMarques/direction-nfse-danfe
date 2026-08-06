using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Direction.NFSe.Danfe;

public sealed class DanfeHtmlRenderer
{
    private const string TransparentPixelBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR4nGNgYAAAAAMAASsJTYQAAAAASUVORK5CYII=";

    private readonly string _templatePath;
    private readonly string _basePath;
    private readonly DanfeOptions _options;

    public DanfeHtmlRenderer(DanfeOptions options)
    {
        _options = options ?? new DanfeOptions();

        _basePath = _options.BasePath ?? AppContext.BaseDirectory;
        _templatePath = _options.TemplatePath ?? Path.Combine(_basePath, "Assets", "Templates", "Danfe.html");
    }

    public (string Html, IReadOnlyList<DanfeWarning> Warnings) RenderInternal(NFSeSchema nfse, DanfeEnvironment environment, DanfeStatus status)
    {
        return Render(nfse, environment, status);
    }
    public (string Html, IReadOnlyList<DanfeWarning> Warnings) Render(NFSeSchema nfse, DanfeEnvironment environment, DanfeStatus status)
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

        string numeroNfse = inf.nNFSe ?? "";
        string numeroDps = infDps.nDPS;
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
        int? cLocPrest = infDps.serv?.locPrest?.cLocPrestacao;
        var municipioPrestador = cLocPrest is > 0 ? MunicipiosIbge.GetMunicipio(cLocPrest.Value) : null;
        if (cLocPrest is null or 0)
            warnings.FieldMissing("cLocPrestacao", "infNFSe.DPS.InfDPS.serv.locPrest.cLocPrestacao", "-");
        else if (municipioPrestador == null)
            warnings.MunicipioNotFound("infNFSe.DPS.InfDPS.serv.locPrest.cLocPrestacao");

        var cLocIncid = inf.cLocIncid;
        var municpioISSQN = int.TryParse(cLocIncid, out var codigoIncidencia)
            ? MunicipiosIbge.GetMunicipio(codigoIncidencia)
            : null;
        if (string.IsNullOrWhiteSpace(cLocIncid))
            warnings.FieldMissing("cLocIncid", "infNFSe.cLocIncid", "-");
        else if (municpioISSQN == null)
            warnings.MunicipioNotFound("infNFSe.cLocIncid");

        var logoBase64 = GetLogoMunicipio(municipioPrestador);

        // Logo da nfse
        var logoNfse = _options.LogoNFSePath != null ? Helper.GetLogo(_options.LogoNFSePath) : Helper.GetLogo(Path.Combine(_basePath, "Assets", "Logos", "nfse.png"));

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
        decimal? vCOFINS = infDps.valores?.trib?.tribFed?.piscofins?.vCofins;  //passou a armazenar somente valores não retidos de COFINS, a partir de março de 2026
        decimal? vPIS = infDps.valores?.trib?.tribFed?.piscofins?.vPis; //passou a armazenar somente valores não retidos de PIS, a partir de março de 2026
        decimal? vCP = infDps.valores?.trib?.tribFed?.vRetCP;
        decimal? vCSLL = infDps.valores?.trib?.tribFed?.vRetCSLL;

        var ibsCbsDps = infDps.IBSCBS;
        var ibsCbs = inf.IBSCBS;
        var valoresIbsCbs = ibsCbs?.valores;
        var totaisIbsCbs = ibsCbs?.totCIBS;
        var tribMun = infDps.valores?.trib?.tribMun;
        var tpRetPisCofinsValue = infDps.valores?.trib?.tribFed?.piscofins?.tpRetPisCofins;

        decimal? contribuicoesSociaisRetidas = vCSLL;
        decimal? pisDebitoProprio = vPIS;
        decimal? cofinsDebitoProprio = vCOFINS;
        if (tpRetPisCofinsValue == 1)
        {
            contribuicoesSociaisRetidas = (vCSLL ?? 0m) + (vPIS ?? 0m) + (vCOFINS ?? 0m);
            pisDebitoProprio = 0m;
            cofinsDebitoProprio = 0m;
        }

        var exclusoesReducaoBase = CalculateIbsCbsExclusions(vDescIncond, valoresIbsCbs?.vCalcReeRepRes, vIssqn, vPIS, vCOFINS);
        var totalIbsCbs = SumNullable(totaisIbsCbs?.gIBS?.vIBSTot, totaisIbsCbs?.gCBS?.vCBS);
        var calculoBeneficioMunicipal = inf.valores?.vCalcBM ?? tribMun?.BM?.vRedBCBM;
        var totalDeducoesReducoes = ResolveIssDeductions(infDps, inf, valoresIbsCbs);

        decimal? vTotTribFed = infDps.valores?.trib?.totTrib?.vTotTrib?.vTotTribFed;
        if (vTotTribFed == null || (vTotTribFed.HasValue && vTotTribFed.Value == 0M)) //nem sempre o objeto totalizador é informado no xml
            vTotTribFed = (vIRRF ?? 0M) + (vPIS ?? 0M) + (vCOFINS ?? 0M) + (vCP ?? 0M) + (vCSLL ?? 0M);

        decimal vTotalRetFed = (vIRRF ?? 0M) + (vCP ?? 0M) + (vCSLL ?? 0M);
        decimal vDebPisCofins = (vPIS ?? 0M) + (vCOFINS ?? 0M);

        var canceladaDiv = status == DanfeStatus.Cancelada ? BuildWatermark("CANCELADA") : string.Empty;
        var substituidaDiv = status == DanfeStatus.Substituida ? BuildWatermark("SUBSTITUÍDA") : string.Empty;

        bool hasTomador = infDps.toma != null;
        bool hasIntermediario = infDps.interm != null;
        bool hasDestinatario = ibsCbsDps?.dest != null;
        bool destinatarioIsTomador = !hasDestinatario && ibsCbsDps?.indDest == 0 && hasTomador;
        bool showPisCofins = !competencia.HasValue || competencia.Value.Year <= 2026;
        bool issSubject = tribMun?.tribISSQN != 4;
        bool showIssOptionalRow1 = infDps.prest?.regTrib?.regEspTrib > 0 ||
                                   tribMun?.tpImunidade.HasValue == true ||
                                   tribMun?.exigSusp != null;
        bool showIssOptionalRow2 = !string.IsNullOrWhiteSpace(inf.valores?.tpBM) ||
                                   tribMun?.BM?.vRedBCBM.HasValue == true ||
                                   inf.valores?.vCalcBM.HasValue == true ||
                                   inf.valores?.vCalcDR.HasValue == true ||
                                   infDps.valores?.vDedRed != null ||
                                   valoresIbsCbs?.vDR.HasValue == true ||
                                   valoresIbsCbs?.vCalcReeRepRes.HasValue == true ||
                                   infDps.valores?.vDescCondIncond?.vDescIncond.HasValue == true;

        // Monta mapa de placeholders (agora com warnings)
        var map = new Dictionary<string, string>
        {
            // Cancelada
            ["{{NFSE_CANCELADA_DIV}}"] = canceladaDiv,
            // Substituída
            ["{{NFSE_SUBSTITUIDA_DIV}}"] = substituidaDiv,
            // Fonts
            ["{{FONT_FAMILY}}"] = _options.FontFamily ?? "Verdana, Helvetica, sans-serif;",
            ["{{FONT_SIZE}}"] = _options.FontSize ?? "12px;",
            ["{{FONT_SIZE_HEADER}}"] = _options.FontSize ?? "13px;",
            ["{{FONT_SIZE_QRCODE}}"] = _options.FontSize ?? "11px;",
            // Logos
            ["{{NFSE_LOGO}}"] = logoNfse ?? TransparentPixelBase64,
            ["{{PREFEITURA_LOGO}}"] = logoBase64 ?? TransparentPixelBase64,
            ["{{LOGO_NAME}}"] = DanfeFallback.OrDash(municipioPrestador?.LogoName, warnings, fieldName: "LogoName", path: "MunicipiosIbge.GetMunicipio(...).LogoName"),

            // Cabeçalho
            ["{{VALIDADE_JURIDICA}}"] = validade,
            ["{{CHAVE_ACESSO}}"] = DanfeFallback.OrDash(chaveAcesso, warnings, fieldName: "chaveAcesso", path: "infNFSe.Id"),
            ["{{MUNICIPIO_EMITENTE}}"] = FormatMunicipioUf(inf.xLocEmi, inf.emit?.enderNac?.UF),
            ["{{AMBIENTE_GERADOR}}"] = inf.ambGer > 0 ? inf.ambGer.ToString(CultureInfo.InvariantCulture) : "-",
            ["{{TIPO_AMBIENTE}}"] = infDps.tpAmb > 0 ? infDps.tpAmb.ToString(CultureInfo.InvariantCulture) : "-",

            // QrCode
            ["{{QRCODE_SRC}}"] = imgQrCodeSrc,

            // Dados NFSe / DPS
            ["{{NUMERO_NFSE}}"] = numeroNfse,
            ["{{NUMERO_DPS}}"] = DanfeFallback.OrDash(numeroDps, warnings, "nDPS", "infNFSe.DPS.InfDPS.nDPS"),
            ["{{SERIE_DPS}}"] = DanfeFallback.OrDash(serieDps, warnings, "serie", "infNFSe.DPS.InfDPS.serie"),
            ["{{COMPETENCIA}}"] = competencia?.ToString("dd/MM/yyyy") ?? DanfeFallback.OrDash(null, warnings, "dCompet", "infNFSe.DPS.InfDPS.dCompet"),
            ["{{DATA_HORA_EMISSAO}}"] = dhEmissaoNfs?.ToString("dd/MM/yyyy HH:mm:ss") ?? DanfeFallback.OrDash(null, warnings, "dhProc", "infNFSe.dhProc"),
            ["{{DATA_HORA_EMISSAO_DPS}}"] = dhEmissaoDps?.ToString("dd/MM/yyyy HH:mm:ss") ?? DanfeFallback.OrDash(null, warnings, "dhEmi", "infNFSe.DPS.InfDPS.dhEmi"),
            ["{{EMITENTE_NFSE}}"] = GetDescricaoEmitenteV2(infDps.tpEmit),
            ["{{SITUACAO_NFSE}}"] = GetDescricaoSituacao(inf.cStat),
            ["{{FINALIDADE}}"] = GetDescricaoFinalidade(ibsCbsDps?.finNFSe),

            // Prestador
            ["{{PREST_SERV}}"] = GetDescricaoEmitente(infDps.tpEmit),
            ["{{PREST_CNPJ}}"] = DanfeFallback.OrDash(Helper.FormatTaxIdentifier(infDps.prest?.CNPJ, infDps.prest?.CPF, infDps.prest?.NIF), warnings, fieldName: "CNPJ/CPF/NIF Prestador", path: "infNFSe.DPS.InfDPS.prest"),
            ["{{PREST_IM}}"] = DanfeFallback.OrDash(infDps.prest?.IM, warnings, "IM Prestador", "infNFSe.DPS.InfDPS.prest.IM"),
            ["{{PREST_RAZAO}}"] = DanfeFallback.OrDash(inf.emit?.xNome, warnings, "xNome Prestador", "infNFSe.emit.xNome"),
            ["{{PREST_ENDERECO}}"] = DanfeFallback.OrDash(Helper.BuildEndereco(inf.emit?.enderNac), warnings, "Endereço Prestador", "infNFSe.emit.enderNac"),
            ["{{PREST_MUNICIPIO}}"] = DanfeFallback.OrDash($"{inf.xLocEmi} - {inf.emit?.enderNac?.UF}", warnings, "Município/UF Prestador", "infNFSe.xLocEmi | infNFSe.emit.enderNac.UF"),
            ["{{PREST_CEP}}"] = DanfeFallback.OrDash(Helper.FormatCep(inf.emit?.enderNac?.CEP), warnings, "CEP Prestador", "infNFSe.emit.enderNac.CEP"),
            ["{{PREST_CODIGO_CEP}}"] = FormatCodigoCep(inf.emit?.enderNac?.cMun, inf.emit?.enderNac?.CEP),
            ["{{PREST_FONE}}"] = DanfeFallback.OrDash(Helper.FormatTelefone(infDps.prest?.fone), warnings, "Fone Prestador", "infNFSe.DPS.InfDPS.prest.fone"),
            ["{{PREST_EMAIL}}"] = DanfeFallback.OrDash(infDps.prest?.email, warnings, "Email Prestador", "infNFSe.DPS.InfDPS.prest.email"),
            ["{{PREST_SIMPLES}}"] = GetDescricaoPrestadorSimples(infDps.prest?.regTrib?.opSimpNac),
            ["{{PREST_REGIME_SN}}"] = GetDescricaoRegimeSimples(infDps.prest?.regTrib?.regApTribSN),

            // Intermediário é incluso condicionalmente mais abaixo

            // Tomador é incluso condicionalmente mais abaixo

            // Serviço
            ["{{SERV_CTRIBNAC}}"] = DanfeFallback.OrDash(descricaoTributoNacional, warnings, "Descrição Tributo Nacional", "infNFSe.DPS.InfDPS.serv.cServ.cTribNac | infNFSe.xTribNac").Limit(80),
            ["{{SERV_CTRIBMUN}}"] = DanfeFallback.OrDash(descricaoTributoMunicipal, warnings, "Descrição Tributo Municipal", "infNFSe.DPS.InfDPS.serv.cServ.cTribMun | infNFSe.xTribMun").Limit(80),
            ["{{SERV_NBS}}"] = DanfeFallback.OrDash(infDps.serv?.cServ?.cNBS.ToString(), warnings, "cNBS", "infNFSe.DPS.InfDPS.serv.cServ.cNBS"),
            ["{{SERV_DESC_HTML}}"] = Helper.BuildDescricaoServicoHtml(infDps.serv?.cServ?.xDescServ),
            ["{{SERV_CODIGOS}}"] = $"{FormatTributacaoCode(cTribNac)} / {DanfeFallback.OrDash(cTribMun)}",
            ["{{SERV_NBS_FORMATADO}}"] = Helper.FormatNbs(infDps.serv?.cServ?.cNBS ?? 0),
            ["{{SERV_LOCAL_COMPLETO}}"] = JoinSlash(inf.xLocPrestacao ?? municipioPrestador?.Nome, municipioPrestador?.Uf, infDps.serv?.locPrest?.cPaisPrestacao),
            ["{{SERV_DESCRICAO_TRIBUTACAO}}"] = DanfeFallback.OrDash(FirstNonEmpty(xTribMun, xTribNac).Limit(170), warnings, "Descricao da tributacao", "infNFSe.xTribMun | infNFSe.xTribNac"),
            ["{{SERV_LOCAL}}"] = DanfeFallback.OrDash(municipioPrestador?.NomeComUf, warnings, "Município Prestação", "MunicipiosIbge.GetMunicipio(cLocPrestacao).NomeComUf"),
            ["{{SERV_PAIS}}"] = DanfeFallback.OrDash(infDps.serv?.locPrest?.cPaisPrestacao, warnings, "País da Prestação", "infNFSe.DPS.InfDPS.serv.locPrest.cPaisPrestacao"),

            // Tributação Municipal
            ["{{ISS_TRIBUTACAO}}"] = GetDescricaoTributacao(infDps.valores?.trib?.tribMun?.tribISSQN),
            //TO DO: verificar se esse pais é o de prestação ou do tomador
            ["{{ISS_PAIS}}"] = DanfeFallback.OrDash(infDps.toma?.end?.endExt?.cPais, warnings, "País Resultado da Prestação do Serviço", "infNFSe.DPS.InfDPS.toma.end.endExt.cPais"),
            ["{{ISS_MUN_INC}}"] = DanfeFallback.OrDash(municpioISSQN?.NomeComUf, warnings, "Município Incidência", "MunicipiosIbge.GetMunicipio(cLocIncid).NomeComUf"),
            ["{{ISS_REGIME}}"] = GetDescricaoRegimeEspecial(infDps.prest?.regTrib?.regEspTrib),
            ["{{ISS_MUN_INC_COMPLETO}}"] = JoinSlash(inf.xLocIncid ?? municpioISSQN?.Nome, municpioISSQN?.Uf, infDps.valores?.trib?.tribMun?.cPaisResult),
            ["{{ISS_OPERACAO}}"] = GetDescricaoTipoImunidade(infDps.valores?.trib?.tribMun?.tpImunidade),
            ["{{ISS_SUSPENSAO}}"] = GetDescricaoTipoSuspensaoISSQN(infDps.valores?.trib?.tribMun?.exigSusp?.tpSusp),
            ["{{ISS_PROCESSO}}"] = DanfeFallback.OrDash(infDps.valores?.trib?.tribMun?.exigSusp?.nProcesso, warnings, "Número Processo Suspensão", "infNFSe.DPS.InfDPS.valores.trib.tribMun.exigSusp.nProcesso"),
            ["{{ISS_BENEFICIO}}"] = GetDescricaoBeneficioMunicipal(inf.valores?.tpBM),
            ["{{ISS_DESC_INCOND}}"] = DanfeFallback.OrCurrency(infDps.valores?.vDescCondIncond?.vDescIncond, ptBR, warnings, "vDescIncond", "infNFSe.valores.vDescCondIncond.vDescIncond"),
            ["{{ISS_DEDUCOES}}"] = FormatCurrency(totalDeducoesReducoes, ptBR),
            ["{{ISS_CALCULO}}"] = FormatCurrency(calculoBeneficioMunicipal, ptBR),
            ["{{ISS_BC}}"] = DanfeFallback.OrCurrency(inf.valores?.vBC, ptBR, warnings, "vBC", "nfse.infNFSe.valores.vBC"),
            ["{{ISS_ALIQ}}"] = DanfeFallback.OrPercent(vAliqAplic, ptBR, warnings, "pAliqAplic", "infNFSe.valores.pAliqAplic"),
            ["{{ISS_RETENCAO}}"] = GetDescricaoRetencao(tpRetIssqn),
            ["{{ISS_APURADO}}"] = DanfeFallback.OrCurrency(vIssqn, ptBR, warnings, "vISSQN", "infNFSe.valores.vISSQN"),

            // Tributação Federal
            ["{{FED_IRRF}}"] = DanfeFallback.OrCurrency(vIRRF, ptBR, warnings, "vIRRF", "infDps.valores.trib.tribFed.vRetIRRF"),
            ["{{FED_PIS}}"] = DanfeFallback.OrCurrency(pisDebitoProprio, ptBR, warnings, "vPIS", "infNFSe.valores.trib.tribFed.piscofins.vPis"),
            ["{{FED_COFINS}}"] = DanfeFallback.OrCurrency(cofinsDebitoProprio, ptBR, warnings, "vCOFINS", "infDps.valores.trib.tribFed.piscofins.vCofins"),
            ["{{FED_CSLL}}"] = DanfeFallback.OrCurrency(contribuicoesSociaisRetidas, ptBR, warnings, "Contribuicoes Sociais Retidas", "infDps.valores.trib.tribFed"),
            ["{{FED_CP}}"] = DanfeFallback.OrCurrency(vCP, ptBR, warnings, "vCP", "infDps.valores.trib.tribFed.vRetCP"),
            ["{{FED_RET_PISCOFINSCSLL}}"] = GetDescricaoTipoRetencaoPisCofins(infDps.valores?.trib?.tribFed?.piscofins?.tpRetPisCofins),
            //["{{FED_TOTAL}}"] = DanfeFallback.OrCurrency(vTotTribFed, ptBR, warnings, "vTotTribFed", "infDps.valores.trib.totTrib.vTotTrib.vTotTribFed"), //não existe mais no layout novo

            // Valores
            ["{{VALOR_SERVICO}}"] = vServico.ToString("C", ptBR),
            ["{{VALOR_LIQUIDO}}"] = DanfeFallback.OrCurrency(vLiq, ptBR, warnings, "vLiq", "infNFSe.valores.vLiq"),
            ["{{DESC_COND}}"] = vDescCond != 0 ? vDescCond.ToString("C", ptBR) : "-",
            ["{{DESC_INCOND}}"] = vDescIncond != 0 ? vDescIncond.ToString("C", ptBR) : "-",
            ["{{ISS_RETIDO}}"] = (tpRetIssqn == 2) ? DanfeFallback.OrCurrency(vIssqn, ptBR, warnings, "vISSQN", "infNFSe.valores.vISSQN") : "-",
            ["{{FED_RETIDOS}}"] = vTotalRetFed == 0M ? "-" : vTotalRetFed.ToString("C", ptBR),
            ["{{PISCOFINS_DEB}}"] = vDebPisCofins != 0 ? vDebPisCofins.ToString("C", ptBR) : "-",

            ["{{IBSCBS_CST_CLASS}}"] = JoinSlash(ibsCbsDps?.valores?.trib?.gIBSCBS?.CST, ibsCbsDps?.valores?.trib?.gIBSCBS?.cClassTrib),
            ["{{IBSCBS_INCIDENCIA}}"] = JoinSlash(ibsCbsDps?.cIndOp, ibsCbs?.cLocalidadeIncid, ibsCbs?.xLocalidadeIncid, ResolveUf(ibsCbs?.cLocalidadeIncid)),
            ["{{IBSCBS_EXCLUSOES}}"] = DanfeFallback.OrCurrency(exclusoesReducaoBase, ptBR, warnings, "Exclusoes e reducoes da base de calculo", "infNFSe.IBSCBS"),
            ["{{IBSCBS_BC}}"] = DanfeFallback.OrCurrency(valoresIbsCbs?.vBC, ptBR, warnings, "vBC IBS/CBS", "infNFSe.IBSCBS.valores.vBC"),
            ["{{IBSCBS_REDUCOES}}"] = JoinSlash(FormatPercent(valoresIbsCbs?.uf?.pRedAliqUF, ptBR), FormatPercent(valoresIbsCbs?.mun?.pRedAliqMun, ptBR), FormatPercent(valoresIbsCbs?.fed?.pRedAliqCBS, ptBR)),
            ["{{IBSCBS_ALIQUOTAS_IBS}}"] = JoinSlash(FormatPercent(valoresIbsCbs?.uf?.pIBSUF, ptBR), FormatPercent(valoresIbsCbs?.mun?.pIBSMun, ptBR)),
            ["{{IBSCBS_ALIQ_EFET_MUN}}"] = FormatPercent(valoresIbsCbs?.mun?.pAliqEfetMun, ptBR),
            ["{{IBSCBS_VALOR_MUN}}"] = FormatCurrency(totaisIbsCbs?.gIBS?.gIBSMunTot?.vIBSMun, ptBR),
            ["{{IBSCBS_ALIQ_EFET_UF}}"] = FormatPercent(valoresIbsCbs?.uf?.pAliqEfetUF, ptBR),
            ["{{IBSCBS_VALOR_UF}}"] = FormatCurrency(totaisIbsCbs?.gIBS?.gIBSUFTot?.vIBSUF, ptBR),
            ["{{IBSCBS_TOTAL_IBS}}"] = FormatCurrency(totaisIbsCbs?.gIBS?.vIBSTot, ptBR),
            ["{{IBSCBS_ALIQ_CBS}}"] = FormatPercent(valoresIbsCbs?.fed?.pCBS, ptBR),
            ["{{IBSCBS_ALIQ_EFET_CBS}}"] = FormatPercent(valoresIbsCbs?.fed?.pAliqEfetCBS, ptBR),
            ["{{IBSCBS_TOTAL_CBS}}"] = FormatCurrency(totaisIbsCbs?.gCBS?.vCBS, ptBR),
            ["{{TOTAL_RETENCOES}}"] = FormatCurrency(inf.valores?.vTotalRet, ptBR),
            ["{{IBSCBS_TOTAL}}"] = FormatCurrency(totalIbsCbs, ptBR),
            ["{{IBSCBS_VALOR_TOTAL}}"] = FormatCurrency(totaisIbsCbs?.vTotNF, ptBR),

            // Totais tributos é inserido condicionalmente mais abaixo

            // Inf complementares
            ["{{INF_COMPLEMENTARES}}"] = Helper.BuildInfComplementares(inf, ptBR)
        };
        // Tomador (condicional)
        foreach (var kv in BuildTomadorMap(infDps, warnings))
            map[kv.Key] = kv.Value;

        // Intermediário (condicional)
        foreach (var kv in BuildIntermediarioMap(infDps, warnings))
            map[kv.Key] = kv.Value;

        foreach (var kv in BuildDestinatarioMap(infDps, warnings))
            map[kv.Key] = kv.Value;

        // Totais tributos (condicional)
        foreach (var kv in BuildTotaisTributosMap(infDps, ptBR, warnings))
            map[kv.Key] = kv.Value;

        template = Helper.ApplyConditionalSections(
            template,
            hasTomador,
            hasIntermediario,
            hasDestinatario,
            destinatarioIsTomador,
            showPisCofins,
            issSubject,
            showIssOptionalRow1,
            showIssOptionalRow2);

        // Aplica os replaces
        foreach (var kv in map)
        {
            bool isRawHtml =
                kv.Key == "{{SERV_DESC_HTML}}" ||
                kv.Key == "{{INF_COMPLEMENTARES}}" ||
                kv.Key == "{{LOGO_NAME}}" ||
                kv.Key == "{{NFSE_CANCELADA_DIV}}" ||
                kv.Key == "{{NFSE_SUBSTITUIDA_DIV}}";

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

    private string? GetLogoMunicipio(MunicipiosIbge.Municipio? municipio)
    {
        var imageCodigoIbge = Path.Combine(_basePath, "Assets", "Logos", $"{municipio?.CodigoIbge}.png");
        if (File.Exists(imageCodigoIbge))
            return Helper.GetLogo(imageCodigoIbge);

        return Helper.GetLogo(Path.Combine(_basePath, municipio?.LogoPath ?? string.Empty));
    }
    private Dictionary<string, string> BuildTomadorMap(InfDPS infDps, DanfeWarningCollector warnings)
    {
        if (infDps.toma == null)
            return new Dictionary<string, string>();

        return new Dictionary<string, string>
        {
            ["{{TOMA_CNPJ}}"] = DanfeFallback.OrDash(
                Helper.FormatTaxIdentifier(infDps.toma.CNPJ, infDps.toma.CPF, infDps.toma.NIF),
                warnings,
                "CNPJ/CPF/NIF Tomador",
                "infNFSe.DPS.InfDPS.toma"),

            ["{{TOMA_IM}}"] = DanfeFallback.OrDash(infDps.toma.IM),
            ["{{TOMA_RAZAO}}"] = DanfeFallback.OrDash(
                infDps.toma.xNome,
                warnings,
                "xNome Tomador",
                "infNFSe.DPS.InfDPS.toma.xNome"),

            ["{{TOMA_ENDERECO}}"] = DanfeFallback.OrDash(
                Helper.BuildEndereco(infDps.toma.end),
                warnings,
                "Endereço Tomador",
                "infNFSe.DPS.InfDPS.toma.end"),

            ["{{TOMA_CEP}}"] = DanfeFallback.OrDash(
                FormatPostalCode(infDps.toma.end),
                warnings,
                "CEP Tomador",
                "infNFSe.DPS.InfDPS.toma.end"),

            ["{{TOMA_CODIGO_CEP}}"] = FormatLocationCodeAndPostalCode(infDps.toma.end),

            ["{{TOMA_CMUN}}"] = ResolveMunicipioOrExternal(
                infDps.toma.end,
                warnings,
                "infNFSe.DPS.InfDPS.toma.end"),

            ["{{TOMA_EMAIL}}"] = DanfeFallback.OrDash(
                infDps.toma.email,
                warnings,
                "Email Tomador",
                "infNFSe.DPS.InfDPS.toma.email"),

            ["{{TOMA_FONE}}"] = DanfeFallback.OrDash(
                Helper.FormatTelefone(infDps.toma.fone),
                warnings,
                "Fone Tomador",
                "infNFSe.DPS.InfDPS.toma.fone")
        };
    }
    private Dictionary<string, string> BuildIntermediarioMap(InfDPS infDps, DanfeWarningCollector warnings)
    {
        if (infDps.interm == null)
            return new Dictionary<string, string>();

        return new Dictionary<string, string>
        {
            ["{{INTER_CNPJ}}"] = DanfeFallback.OrDash(
                Helper.FormatTaxIdentifier(infDps.interm.CNPJ, infDps.interm.CPF, infDps.interm.NIF),
                warnings,
                "CNPJ/CPF/NIF Intermediário",
                "infNFSe.DPS.InfDPS.interm"),

            ["{{INTER_IM}}"] = DanfeFallback.OrDash(infDps.interm.IM),

            ["{{INTER_RAZAO}}"] = DanfeFallback.OrDash(
                infDps.interm.xNome,
                warnings,
                "xNome Intermediário",
                "infNFSe.DPS.InfDPS.interm.xNome"),

            ["{{INTER_ENDERECO}}"] = DanfeFallback.OrDash(
                Helper.BuildEndereco(infDps.interm.end),
                warnings,
                "Endereço Intermediário",
                "infNFSe.DPS.InfDPS.interm.end"),

            ["{{INTER_CEP}}"] = DanfeFallback.OrDash(
                FormatPostalCode(infDps.interm.end),
                warnings,
                "CEP Intermediário",
                "infNFSe.DPS.InfDPS.interm.end"),

            ["{{INTER_CODIGO_CEP}}"] = FormatLocationCodeAndPostalCode(infDps.interm.end),

            ["{{INTER_CMUN}}"] = ResolveMunicipioOrExternal(
                infDps.interm.end,
                warnings,
                "infNFSe.DPS.InfDPS.interm.end"),

            ["{{INTER_EMAIL}}"] = DanfeFallback.OrDash(
                infDps.interm.email,
                warnings,
                "Email Intermediário",
                "infNFSe.DPS.InfDPS.interm.email"),

            ["{{INTER_FONE}}"] = DanfeFallback.OrDash(
                Helper.FormatTelefone(infDps.interm.fone),
                warnings,
                "Fone Intermediário",
                "infNFSe.DPS.InfDPS.interm.fone")
        };
    }

    private Dictionary<string, string> BuildDestinatarioMap(InfDPS infDps, DanfeWarningCollector warnings)
    {
        var destinatario = infDps.IBSCBS?.dest;
        if (destinatario == null)
            return new Dictionary<string, string>();

        int? codigoMunicipio = destinatario.end?.endNac?.cMun;
        var municipio = codigoMunicipio is > 0 ? MunicipiosIbge.GetMunicipio(codigoMunicipio.Value) : null;
        var municipioUf = municipio?.NomeComUf;
        if (string.IsNullOrWhiteSpace(municipioUf) && destinatario.end?.endExt != null)
            municipioUf = JoinSlash(destinatario.end.endExt.xCidade, destinatario.end.endExt.xEstProvReg);

        return new Dictionary<string, string>
        {
            ["{{DEST_CNPJ}}"] = DanfeFallback.OrDash(
                Helper.FormatTaxIdentifier(destinatario.CNPJ, destinatario.CPF, destinatario.NIF),
                warnings,
                "CNPJ/CPF/NIF Destinatario",
                "infNFSe.DPS.InfDPS.IBSCBS.dest"),
            ["{{DEST_RAZAO}}"] = DanfeFallback.OrDash(destinatario.xNome),
            ["{{DEST_ENDERECO}}"] = DanfeFallback.OrDash(Helper.BuildEndereco(destinatario.end)),
            ["{{DEST_CMUN}}"] = DanfeFallback.OrDash(municipioUf),
            ["{{DEST_CODIGO_CEP}}"] = FormatLocationCodeAndPostalCode(destinatario.end),
            ["{{DEST_EMAIL}}"] = DanfeFallback.OrDash(destinatario.email),
            ["{{DEST_FONE}}"] = DanfeFallback.OrDash(Helper.FormatTelefone(destinatario.fone))
        };
    }

    private Dictionary<string, string> BuildTotaisTributosMap(InfDPS infDps, CultureInfo ptBR, DanfeWarningCollector warnings)
    {
        if (infDps.valores?.trib?.totTrib?.pTotTribSN != null && infDps.valores?.trib?.totTrib?.pTotTribSN != 0)
            return new Dictionary<string, string>
            {
                // Totais tributos (Simples Nacional)
                ["{{TOT_FED}}"] = "-",
                ["{{TOT_EST}}"] = "-",
                ["{{TOT_MUN}}"] = "-"
            };

        if (infDps.valores?.trib?.totTrib?.pTotTrib?.pTotTribFed != null)
            return new Dictionary<string, string>
            {
                // Totais tributos (percentual)
                ["{{TOT_FED}}"] = DanfeFallback.OrPercent(infDps.valores?.trib?.totTrib?.pTotTrib?.pTotTribFed, ptBR, warnings, "pTotTribFed", "infDps.valores.trib.totTrib.pTotTrib.pTotTribFed"),
                ["{{TOT_EST}}"] = DanfeFallback.OrPercent(infDps.valores?.trib?.totTrib?.pTotTrib?.pTotTribEst, ptBR, warnings, "pTotTribEst", "infDps.valores.trib.totTrib.pTotTrib.pTotTribEst"),
                ["{{TOT_MUN}}"] = DanfeFallback.OrPercent(infDps.valores?.trib?.totTrib?.pTotTrib?.pTotTribMun, ptBR, warnings, "pTotTribMun", "infDps.valores.trib.totTrib.pTotTrib.pTotTribMun"),
            };

        return new Dictionary<string, string>
        {
            // Totais tributos (valores)
            ["{{TOT_FED}}"] = DanfeFallback.OrCurrency(infDps.valores?.trib?.totTrib?.vTotTrib?.vTotTribFed, ptBR, warnings, "vTotTribFed", "infDps.valores.trib.totTrib.vTotTrib.vTotTribFed"),
            ["{{TOT_EST}}"] = DanfeFallback.OrCurrency(infDps.valores?.trib?.totTrib?.vTotTrib?.vTotTribEst, ptBR, warnings, "vTotTribEst", "infDps.valores.trib.totTrib.vTotTrib.vTotTribEst"),
            ["{{TOT_MUN}}"] = DanfeFallback.OrCurrency(infDps.valores?.trib?.totTrib?.vTotTrib?.vTotTribMun, ptBR, warnings, "vTotTribMun", "infDps.valores.trib.totTrib.vTotTrib.vTotTribMun"),
        };
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

    private static string ResolveMunicipioOrExternal(EndSimples? endereco, DanfeWarningCollector warnings, string path)
    {
        if (endereco?.endNac?.cMun is > 0)
            return ResolveMunicipioNomeComUf(endereco.endNac.cMun, warnings, $"{path}.endNac.cMun");

        if (endereco?.endExt != null)
            return JoinSlash(endereco.endExt.xCidade, endereco.endExt.xEstProvReg);

        warnings.FieldMissing("cMun/xCidade", path, "-");
        return "-";
    }

    private static string FormatPostalCode(EndSimples? endereco) =>
        endereco?.endNac != null
            ? Helper.FormatCep(endereco.endNac.CEP)
            : Helper.NullToDash(endereco?.endExt?.cEndPost);

    private static string FormatLocationCodeAndPostalCode(EndSimples? endereco) =>
        endereco?.endNac != null
            ? FormatCodigoCep(endereco.endNac.cMun, endereco.endNac.CEP)
            : Helper.NullToDash(endereco?.endExt?.cEndPost);

    private static decimal? ResolveIssDeductions(InfDPS infDps, InfNFSe inf, valores? valoresIbsCbs)
    {
        if (infDps.valores?.vDedRed?.vDR.HasValue == true)
            return infDps.valores.vDedRed.vDR;

        if (valoresIbsCbs?.vDR.HasValue == true)
            return valoresIbsCbs.vDR;

        return SumNullable(inf.valores?.vCalcDR, valoresIbsCbs?.vCalcReeRepRes);
    }

    private static decimal? CalculateIbsCbsExclusions(params decimal?[] values)
    {
        if (!values.Any(value => value.HasValue))
            return null;

        return values.Sum(value => value ?? 0m);
    }

    private static decimal? SumNullable(decimal? first, decimal? second)
    {
        if (!first.HasValue && !second.HasValue)
            return null;

        return (first ?? 0m) + (second ?? 0m);
    }

    private static string FormatCurrency(decimal? value, CultureInfo culture) =>
        value.HasValue ? value.Value.ToString("C", culture) : "-";

    private static string FormatPercent(decimal? value, CultureInfo culture) =>
        value.HasValue ? $"{value.Value.ToString("N2", culture)}%" : "-";

    private static string FormatTributacaoCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "-";
        return Regex.Replace(value, @"^(\d{2})(\d{2})(\d{2})$", "$1.$2.$3");
    }

    private static string JoinSlash(params string?[] values) =>
        string.Join(" / ", values.Select(value => string.IsNullOrWhiteSpace(value) ? "-" : value));

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string FormatMunicipioUf(string? municipio, string? uf) =>
        JoinSlash(municipio, uf);

    private static string FormatCodigoCep(string? codigo, string? cep) =>
        JoinSlash(codigo, Helper.FormatCep(cep));

    private static string FormatCodigoCep(int? codigo, string? cep) =>
        FormatCodigoCep(codigo is > 0 ? codigo.Value.ToString(CultureInfo.InvariantCulture) : null, cep);

    private static string? ResolveUf(string? codigoMunicipio)
    {
        if (!int.TryParse(codigoMunicipio, out var codigo)) return null;
        return MunicipiosIbge.GetMunicipio(codigo)?.Uf;
    }

    private static string BuildWatermark(string text) => $@"<div style=""
              position:absolute;
              top:50%;
              left:50%;
              display:inline-block;
              -webkit-transform: translate(-50%, -50%) rotate(-30deg);
              transform: translate(-50%, -50%) rotate(-30deg);
              -webkit-transform-origin: 50% 50%;
              transform-origin: 50% 50%;
              font-family: Arial, 'Liberation Sans', Helvetica, sans-serif;
              font-size:50pt;
              font-weight:normal;
              color:#a6a6a6;
              z-index:9999;
              pointer-events:none;
              white-space:nowrap;"">{Helper.HtmlEncode(text)}</div>";

    private static string GetDescricaoAmbienteGerador(int ambiente) => ambiente switch
    {
        1 => "Prefeitura",
        2 => "Sistema Nacional da NFS-e",
        _ => "-"
    };

    private static string GetDescricaoTipoAmbiente(int ambiente) => ambiente switch
    {
        1 => "Produção",
        2 => "Produção Restrita",
        _ => "-"
    };

    private static string GetDescricaoEmitenteV2(int emitente) => emitente switch
    {
        1 => "Prestador",
        2 => "Tomador",
        3 => "Intermediário",
        _ => "-"
    };

    private static string GetDescricaoSituacao(int situacao) => situacao switch
    {
        100 => "NFS-e Gerada",
        102 => "NFS-e de Decisão Judicial",
        103 => "NFS-e Avulsa",
        107 => "NFS-e MEI",
        _ => situacao > 0 ? situacao.ToString(CultureInfo.InvariantCulture) : "-"
    };

    private static string GetDescricaoFinalidade(int? finalidade) => finalidade switch
    {
        0 => "NFS-e regular",
        _ => "-"
    };

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

            0 - PIS/COFINS/CSLL Não Retidos;
            1 - PIS/COFINS Retidos;
            2 - PIS/COFINS Não Retidos;
            3 - PIS/COFINS/CSLL Retidos;
            4 - PIS/COFINS Retidos, CSLL Não Retido;
            5 - PIS Retido, COFINS/CSLL Não Retidos;
            6 - COFINS Retido, PIS/CSLL Não Retidos;
            7 - PIS Não Retido, COFINS/CSLL Retidos;
            8 - PIS/COFINS Não Retidos, CSLL Retido;
            9 - COFINS Não Retido, PIS/CSLL Retidos;
         */
        switch (tpRetPisCofins)
        {
            case 0:
                return "0 - PIS/COFINS/CSLL Não Retidos";
            case 1:
                return "1 - PIS/COFINS Retidos";
            case 2:
                return "2 - PIS/COFINS Não Retidos";
            case 3:
                return "3 - PIS/COFINS/CSLL Retidos";
            case 4:
                return "4 - PIS/COFINS Retidos, CSLL Não Retido";
            case 5:
                return "5 - PIS Retido, COFINS/CSLL Não Retidos";
            case 6:
                return "6 - COFINS Retido, PIS/CSLL Não Retidos";
            case 7:
                return "7 - PIS Não Retido, COFINS/CSLL Retidos";
            case 8:
                return "8 - PIS/COFINS Não Retidos, CSLL Retido";
            case 9:
                return "9 - COFINS Não Retido, PIS/CSLL Retidos";
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
            case 9:
                return "Outros";
            default:
                return "-";
        }
    }

    private static string GetDescricaoBeneficioMunicipal(string? tipo) => tipo switch
    {
        "1" => "Isenção",
        "2" => "Redução da base de cálculo em percentual",
        "3" => "Redução da base de cálculo em valor",
        "4" => "Alíquota diferenciada",
        _ => "-"
    };

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
