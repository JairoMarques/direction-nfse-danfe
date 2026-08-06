using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using Direction.NFSe.Danfe;
using Xunit;

namespace Danfe.Tests;

public class GoldenHtmlTests
{
    [Fact]
    public void Render_Nt008Xml_RendersDanfeV2WithIbsCbsValues()
    {
        var nfse = LoadFixture();
        var renderer = new DanfeHtmlRenderer(new DanfeOptions());

        var (html, _) = renderer.Render(nfse, DanfeEnvironment.Production, DanfeStatus.Autorizada);
        var normalized = HtmlNormalization.Normalize(html);

        Assert.Contains("DANFSe v2.0", normalized);
        Assert.Contains("NFS-e Gerada", normalized);
        Assert.Contains("NFS-e regular", normalized);
        Assert.Contains("Ambiente Gerador: 1", normalized);
        Assert.Contains("Tipo de Ambiente: 1", normalized);
        Assert.Matches(
            new Regex(@"\.c-qr img\s*\{\s*width:\s*15\.4mm;\s*height:\s*15\.4mm;", RegexOptions.IgnoreCase),
            normalized);
        Assert.Contains("000 / 000001", normalized);
        Assert.Contains("100301 / 99888888 / Localidade / -", normalized);
        Assert.Contains("R$ 85,50", NormalizeCurrencySpacing(normalized));
        Assert.Contains("R$ 1.304,67", NormalizeCurrencySpacing(normalized));
        Assert.Contains("0,10% / 0,00%", normalized);
        Assert.Contains("R$ 1,30", NormalizeCurrencySpacing(normalized));
        Assert.Contains("R$ 11,74", NormalizeCurrencySpacing(normalized));
        Assert.Contains("R$ 13,04", NormalizeCurrencySpacing(normalized));
        Assert.Contains("O DESTINATÁRIO É O PRÓPRIO TOMADOR/ADQUIRENTE DA OPERAÇÃO", normalized);
        Assert.Contains("Totais Aproximados dos Tributos cfe. Lei nº 12.741/2012", normalized);
        Assert.DoesNotContain("Regime Especial de Tributação ISSQN", normalized);
        Assert.DoesNotContain("Benefício Municipal", normalized);
        Assert.Matches(
            new Regex(@"<tr class=""sec-title"">\s*<td class=""sec-title-label"">Prestador / Fornecedor</td>\s*<td", RegexOptions.IgnoreCase),
            html);
        Assert.DoesNotContain("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR4nGNgYAAAAAMAASsJTYQAAAAASUVORK5CYII=", html);
        Assert.DoesNotMatch(new Regex(@"\{\{[^}]+\}\}"), normalized);
    }

    [Fact]
    public void Render_PisCofinsRetained_MovesValuesToSocialContributions()
    {
        var nfse = LoadFixture();
        var pisCofins = nfse.infNFSe!.DPS!.InfDPS!.valores!.trib!.tribFed!.piscofins!;
        pisCofins.tpRetPisCofins = 1;

        var renderer = new DanfeHtmlRenderer(new DanfeOptions());
        var (html, _) = renderer.Render(nfse, DanfeEnvironment.Production, DanfeStatus.Autorizada);
        var normalized = NormalizeCurrencySpacing(NormalizeRenderedHtml(html));

        Assert.Contains("R$ 50,75", normalized);
        Assert.True(Regex.Matches(normalized, "R\\$ 0,00").Count >= 2);
    }

    [Fact]
    public void Render_CompetenceAfter2026_OmitsPisCofinsDebitRow()
    {
        var nfse = LoadFixture();
        nfse.infNFSe!.DPS!.InfDPS!.dCompet = "2027-01-01";

        var renderer = new DanfeHtmlRenderer(new DanfeOptions());
        var (html, _) = renderer.Render(nfse, DanfeEnvironment.Production, DanfeStatus.Autorizada);

        Assert.DoesNotContain("PIS - Débito Apuração Própria", html);
        Assert.DoesNotContain("COFINS - Débito Apuração Própria", html);
    }

    [Fact]
    public void Render_OptionalIssData_RendersItsRows()
    {
        var nfse = LoadFixture();
        nfse.infNFSe!.DPS!.InfDPS!.prest!.regTrib!.regEspTrib = 1;

        var renderer = new DanfeHtmlRenderer(new DanfeOptions());
        var (html, _) = renderer.Render(nfse, DanfeEnvironment.Production, DanfeStatus.Autorizada);

        Assert.Contains("Regime Especial de Tributação ISSQN", html);
    }

    [Fact]
    public void Render_NotSubjectToIss_RendersOnlyTheRequiredMessage()
    {
        var nfse = LoadFixture();
        nfse.infNFSe!.DPS!.InfDPS!.valores!.trib!.tribMun!.tribISSQN = 4;

        var renderer = new DanfeHtmlRenderer(new DanfeOptions());
        var (html, _) = renderer.Render(nfse, DanfeEnvironment.Production, DanfeStatus.Autorizada);

        Assert.Contains("TRIBUTAÇÃO MUNICIPAL (ISSQN) - OPERAÇÃO NÃO SUJEITA AO ISSQN", html);
        Assert.DoesNotContain("Tipo de Tributação do ISSQN", html);
        Assert.DoesNotContain("BC ISSQN", html);
    }

    [Fact]
    public void Render_MunicipalBenefitAndDeductions_UsesAuthorizedNfseValues()
    {
        var nfse = LoadFixture();
        var inf = nfse.infNFSe!;
        inf.valores!.tpBM = "2";
        inf.valores.vCalcBM = 20m;
        inf.IBSCBS!.valores!.vDR = 30m;
        inf.DPS!.InfDPS!.prest!.regTrib!.regEspTrib = 9;

        var renderer = new DanfeHtmlRenderer(new DanfeOptions());
        var (html, _) = renderer.Render(nfse, DanfeEnvironment.Production, DanfeStatus.Autorizada);
        var normalized = NormalizeCurrencySpacing(NormalizeRenderedHtml(html));

        Assert.Contains("Redução da base de cálculo em percentual", normalized);
        Assert.Contains("R$ 20,00", normalized);
        Assert.Contains("R$ 30,00", normalized);
        Assert.Contains("Outros", normalized);
    }

    [Fact]
    public void Render_TaxDescriptions_PrefersMunicipalDescription()
    {
        var nfse = LoadFixture();
        nfse.infNFSe!.xTribNac = "Descrição nacional";
        nfse.infNFSe.xTribMun = "Descrição municipal";

        var renderer = new DanfeHtmlRenderer(new DanfeOptions());
        var (html, _) = renderer.Render(nfse, DanfeEnvironment.Production, DanfeStatus.Autorizada);

        var normalized = NormalizeRenderedHtml(html);
        Assert.Contains("Descrição municipal", normalized);
        Assert.DoesNotContain("Descrição nacional", normalized);
    }

    [Fact]
    public void Render_ForeignIntermediaryAndPurchaseOrder_PreservesNationalModelFields()
    {
        var nfse = LoadFixture();
        var infDps = nfse.infNFSe!.DPS!.InfDPS!;
        infDps.interm = new Intermediario
        {
            NIF = "PT-123456",
            xNome = "Intermediário Exterior",
            end = new EndSimples
            {
                xLgr = "Rua Internacional",
                nro = "10",
                endExt = new EndExt
                {
                    xCidade = "Lisboa",
                    xEstProvReg = "Lisboa",
                    cEndPost = "1000-001"
                }
            }
        };
        infDps.serv!.infoCompl ??= new infoCompl();
        infDps.serv.infoCompl.xPed = "PED-123";
        infDps.serv.infoCompl.gItemPed = new InfoItemPedido { xItemPed = ["10", "20"] };

        var renderer = new DanfeHtmlRenderer(new DanfeOptions());
        var (html, _) = renderer.Render(nfse, DanfeEnvironment.Production, DanfeStatus.Autorizada);
        var normalized = NormalizeRenderedHtml(html);

        Assert.Contains("PT-123456", normalized);
        Assert.Contains("Lisboa / Lisboa", normalized);
        Assert.Contains("1000-001", normalized);
        Assert.DoesNotContain("- / 1000-001", normalized);
        Assert.Contains("Núm. Ped.: PED-123", normalized);
        Assert.Contains("Item Ped.: 10, 20", normalized);
    }

    [Fact]
    public void Render_LongDescriptions_TruncatesWithEllipsisAndKeepsTaxTotals()
    {
        var nfse = LoadFixture();
        var infDps = nfse.infNFSe!.DPS!.InfDPS!;
        infDps.serv!.cServ!.xDescServ = new string('S', 1400);
        infDps.serv.infoCompl ??= new infoCompl();
        infDps.serv.infoCompl.xInfComp = new string('I', 2100);

        var renderer = new DanfeHtmlRenderer(new DanfeOptions());
        var (html, _) = renderer.Render(nfse, DanfeEnvironment.Production, DanfeStatus.Autorizada);

        Assert.Contains("...", html);
        Assert.DoesNotContain(new string('S', 1300), html);
        Assert.DoesNotContain(new string('I', 1997), html);
        Assert.Contains("Totais Aproximados dos Tributos", html);
    }

    [Fact]
    public void Render_PercentageDeduction_UsesCalculatedAuthorizedValue()
    {
        var nfse = LoadFixture();
        var inf = nfse.infNFSe!;
        inf.DPS!.InfDPS!.valores!.vDedRed = new VDedRed { pDR = 10m };
        inf.valores!.vCalcDR = 45m;
        inf.IBSCBS!.valores!.vDR = null;

        var renderer = new DanfeHtmlRenderer(new DanfeOptions());
        var (html, _) = renderer.Render(nfse, DanfeEnvironment.Production, DanfeStatus.Autorizada);
        var normalized = NormalizeCurrencySpacing(NormalizeRenderedHtml(html));

        Assert.Contains("R$ 45,00", normalized);
    }

    [Fact]
    public void Render_Cancelled_UsesNt008WatermarkStyle()
    {
        var nfse = LoadFixture();
        var renderer = new DanfeHtmlRenderer(new DanfeOptions());

        var (html, _) = renderer.Render(nfse, DanfeEnvironment.Production, DanfeStatus.Cancelada);

        Assert.Contains("font-size:50pt", html);
        Assert.Contains("font-weight:normal", html);
        Assert.Contains("color:#a6a6a6", html);
        Assert.DoesNotContain("border: 8px", html);
    }

    private static NFSeSchema LoadFixture()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "nfse-ibscbs.xml");
        using var stream = File.OpenRead(path);
        var serializer = new XmlSerializer(typeof(NFSeSchema));
        return Assert.IsType<NFSeSchema>(serializer.Deserialize(stream));
    }

    private static string NormalizeCurrencySpacing(string value) => value.Replace('\u00a0', ' ');

    private static string NormalizeRenderedHtml(string html) => WebUtility.HtmlDecode(HtmlNormalization.Normalize(html));
}
