using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace Direction.NFSe.Danfe
{
    [XmlRoot("NFSe", Namespace = "http://www.sped.fazenda.gov.br/nfse")]
    public class NFSeSchema
    {
        [XmlAttribute("versao")]
        public string? Versao { get; set; }

        public InfNFSe? infNFSe { get; set; }
    }

    public class InfNFSe
    {
        [XmlAttribute("Id")]
        public string? Id { get; set; }
        public string? xLocEmi { get; set; }
        public string? xLocPrestacao { get; set; }
        public int nNFSe { get; set; }
        public string? cLocIncid { get; set; }
        public string? xLocIncid { get; set; }
        public string? xTribNac { get; set; }
        public string? xTribMun { get; set; }
        public string? xNBS { get; set; }
        public string? verAplic { get; set; }
        public int ambGer { get; set; }
        public int tpEmis { get; set; }
        public int procEmi { get; set; }
        public int cStat { get; set; }
        public DateTime dhProc { get; set; }
        public long nDFSe { get; set; }
        public Emit? emit { get; set; }
        public ValoresNfse? valores { get; set; }
        public IBSCBS? IBSCBS { get; set; }
        public DPS? DPS { get; set; }
    }

    public class Emit
    {
        public string? CNPJ { get; set; }
        public string? CPF { get; set; }
        public string? IM { get; set; }
        public string? xNome { get; set; }
        public string? xFant { get; set; }
        public EnderNac? enderNac { get; set; }
        public string? fone { get; set; }
        public string? email { get; set; }
    }

    public class EnderNac
    {
        public string? xLgr { get; set; }
        public string? nro { get; set; }
        public string? xCpl { get; set; }
        public string? xBairro { get; set; }
        public string? cMun { get; set; }
        public string? UF { get; set; }
        public string? CEP { get; set; }
    }

    public class ValoresNfse
    {
        public decimal? vCalcDR { get; set; }
        public string? tpBM { get; set; }
        public decimal? vCalcBM { get; set; }
        public decimal? vBC { get; set; }
        public decimal? pAliqAplic { get; set; }
        public decimal? vISSQN { get; set; }
        public decimal? vTotalRet { get; set; }
        public decimal vLiq { get; set; }
        public string? xOutInf { get; set; }
    }

    [XmlRoot("DPS", Namespace = "http://www.sped.fazenda.gov.br/nfse")]
    public class DPS
    {
        [XmlAttribute("versao")]
        public string? Versao { get; set; } = "1.01";

        [XmlElement("infDPS")]
        public InfDPS? InfDPS { get; set; }
    }

    public class InfDPS
    {
        [XmlAttribute("Id")]
        public string? Id { get; set; }

        public int tpAmb { get; set; }
        public string? dhEmi { get; set; }
        public string? verAplic { get; set; }
        public int serie { get; set; }
        public int nDPS { get; set; }
        public string? dCompet { get; set; }
        public int tpEmit { get; set; }
        public int? cMotivoEmisTI { get; set; }
        public string? chNFSeRej { get; set; }
        public int cLocEmi { get; set; }

        public NFSeSubstituida? subst { get; set; }
        public PrestadorNFS? prest { get; set; }
        public Tomador? toma { get; set; }
        public Intermediario? interm { get; set; }
        public Servico? serv { get; set; }
        public Valores? valores { get; set; }
        public IBSCBS? IBSCBS { get; set; }
    }

    public class NFSeSubstituida
    {
        public string? chSubstda { get; set; }
        public int cMotivo { get; set; }
        public string? xMotivo { get; set; }
    }

    public class PrestadorNFS
    {
        public string? CNPJ { get; set; }
        public string? CPF { get; set; }
        public string? NIF { get; set; }
        public int cNaoNIF { get; set; }
        public string? CAEPF { get; set; }
        public string? IM { get; set; }
        public string? xNome { get; set; }

        public EndSimples? end { get; set; }
        public string? fone { get; set; }
        public string? email { get; set; }

        public RegTrib? regTrib { get; set; }
    }

    public class EndSimples
    {
        public EndNac? endNac { get; set; }
        public EndExt? endExt { get; set; }
        public string? xLgr { get; set; }
        public string? nro { get; set; }
        public string? xCpl { get; set; }
        public string? xBairro { get; set; }
    }

    public class EndExt
    {
        public string? cPais { get; set; }
        public string? cEndPost { get; set; }
        public string? xCidade { get; set; }
        public string? xEstProvReg { get; set; }
    }

    public class EndNac
    {
        public int cMun { get; set; }
        public string? CEP { get; set; }
    }

    public class RegTrib
    {
        public int opSimpNac { get; set; }
        public int? regApTribSN { get; set; }
        public int regEspTrib { get; set; }
    }

    public class Tomador
    {
        public string? CNPJ { get; set; }
        public string? CPF { get; set; }
        public string? NIF { get; set; }
        public int cNaoNIF { get; set; }
        public string? CAEPF { get; set; }
        public string? IM { get; set; }
        public string? xNome { get; set; }
        public EndSimples? end { get; set; }
        public string? fone { get; set; }
        public string? email { get; set; }
    }

    public class Intermediario
    {
        public string? CNPJ { get; set; }
        public string? CPF { get; set; }
        public string? NIF { get; set; }
        public int cNaoNIF { get; set; }
        public string? CAEPF { get; set; }
        public string? IM { get; set; }
        public string? xNome { get; set; }
        public EndSimples? end { get; set; }
        public string? fone { get; set; }
        public string? email { get; set; }
    }

    public class Servico
    {
        public LocPrest? locPrest { get; set; }
        public CServ? cServ { get; set; }
        public comExterior? comExt { get; set; }
        public obra? obra { get; set; }
        public atvEvento? atvEvento { get; set; }
        public infoCompl? infoCompl { get; set; }
    }

    public class LocPrest
    {
        public int cLocPrestacao { get; set; }
        public string? cPaisPrestacao { get; set; }
    }

    public class comExterior
    {
        public int mdPrestacao { get; set; }
        public int vincPrest { get; set; }
        public int tpMoeda { get; set; }
        public decimal vServMoeda { get; set; }
        public string? mecAFComexP { get; set; }
        public string? mecAFComexT { get; set; }
        public int movTempBens { get; set; }
        public string? nDI { get; set; }
        public string? nRE { get; set; }
        public int mdic { get; set; }
    }

    public class obra
    {
        public string? inscImobFisc { get; set; }
        public string? cObra { get; set; }
        public end? end { get; set; }
    }

    public class atvEvento
    {
        public string? xNome { get; set; }
        public string? dtIni { get; set; }
        public string? dtFim { get; set; }
        public string? idAtvEvt { get; set; }
        public end? end { get; set; }
    }

    public class infoCompl
    {
        public string? idDocTec { get; set; }
        public string? docRef { get; set; }
        public string? xInfComp { get; set; }
    }

    public class CServ
    {
        public string? cTribNac { get; set; }
        public string? cTribMun { get; set; }
        public string? xDescServ { get; set; }
        public int cNBS { get; set; }
        public string? cIntContrib { get; set; }
    }

    public class VServPrest
    {
        public decimal vReceb { get; set; }
        public decimal? vServ { get; set; }
    }

    public class VDescCondIncond
    {
        public decimal? vDescIncond { get; set; }
        public decimal? vDescCond { get; set; }
    }
    public class NFSeMun
    {
        public string? cMunNFSeMun { get; set; }
        public string? nNFSeMun { get; set; }
        public string? cVerifNFSeMun { get; set; }
    }
    public class NFNFS
    {
        public string? nNFS { get; set; }
        public string? modNFS { get; set; }
        public string? serieNFS { get; set; }
    }
    public class Fornecedor : Tomador
    {
        //Adicionar campos aqui, caso as informações de tomador mude em relação ao fornecedor
    }
    public class DocDedRed
    {
        public string? chNFSe { get; set; }
        public string? chNFe { get; set; }
        public NFSeMun? NFSeMun { get; set; }
        public NFNFS? NFNFS { get; set; }
        public string? nDocFisc { get; set; }
        public string? nDoc { get; set; }
        public string? tpDedRec { get; set; }
        public string? xDescOutDed { get; set; }
        public string? dtEmiDoc { get; set; }
        public decimal vDedutivelRedutivel { get; set; }
        public decimal vDeducaoReducao { get; set; }
        public Fornecedor? fornec { get; set; }
    }

    public class Documento
    {
        public DocDedRed? docDedRed { get; set; }
    }

    public class VDedRed
    {
        public decimal pDR { get; set; }
        public decimal vDR { get; set; }

        [XmlArray("documentos")]
        public List<Documento>? documentos { get; set; }

        public bool ShouldSerializedocumentos()
        {
            return documentos != null && documentos.Count > 0;
        }
    }

    public class Valores
    {
        public VServPrest? vServPrest { get; set; }
        public VDescCondIncond? vDescCondIncond { get; set; }
        public VDedRed? vDedRed { get; set; }
        public Trib? trib { get; set; }
    }



    public class Trib
    {
        public TribMun? tribMun { get; set; }
        public TribFed? tribFed { get; set; }
        public TotTrib? totTrib { get; set; }
    }

    public class ExigSusp
    {
        public int tpSusp { get; set; }
        public string? nProcesso { get; set; }
    }

    public class BM
    {
        public int nBM { get; set; }
        public decimal? vRedBCBM { get; set; }
        public decimal? pRedBCBM { get; set; }
        public int tpRetISSQN { get; set; }
        public decimal? pAliq { get; set; }
    }

    public class TribMun
    {
        public int tribISSQN { get; set; }
        public int tpRetISSQN { get; set; }
        public string? cPaisResult { get; set; }
        public int? tpImunidade { get; set; }
        public ExigSusp? exigSusp { get; set; }
        public BM? BM { get; set; }
    }

    public class PisCofins
    {
        public string? CST { get; set; }
        public decimal? vBCPisCofins { get; set; }
        public decimal? pAliqPis { get; set; }
        public decimal? pAliqCofins { get; set; }
        public decimal? vPis { get; set; }
        public decimal? vCofins { get; set; }
        public int? tpRetPisCofins { get; set; }
    }
    public class TribFed
    {
        public PisCofins? piscofins { get; set; }
        public decimal? vRetCP { get; set; }
        public decimal? vRetIRRF { get; set; }
        public decimal? vRetCSLL { get; set; }

    }

    public class PTotTrib
    {
        public decimal pTotTribFed { get; set; }
        public decimal pTotTribEst { get; set; }
        public decimal pTotTribMun { get; set; }
    }
    public class VTotTrib
    {
        public decimal vTotTribFed { get; set; }
        public decimal vTotTribEst { get; set; }
        public decimal vTotTribMun { get; set; }
    }

    public class TotTrib
    {
        public VTotTrib? vTotTrib { get; set; }
        public PTotTrib? pTotTrib { get; set; }
        public int? indTotTrib { get; set; }
        public decimal pTotTribSN { get; set; }
    }
    public class IBSCBS
    {
        public int finNFSe { get; set; }
        public int indFinal { get; set; }
        public string? cIndOp { get; set; }
        public string? tpOper { get; set; }
        public gRefNFSe? gRefNFSe { get; set; }
        public string? tpEnteGov { get; set; }
        public int indDest { get; set; }
        public dest? dest { get; set; }
        public imovel? imovel { get; set; }
        public valores? valores { get; set; }
    }

    public class gRefNFSe
    {
        // OCOR.: 1-99 => lista
        public List<string> refNFSe { get; set; } = [];
    }

    public class dest
    {
        public string? CNPJ { get; set; }
        public string? CPF { get; set; }
        public string? NIF { get; set; }
        public int cNaoNIF { get; set; }
        public string? xNome { get; set; }
        public end? end { get; set; }
        public string? fone { get; set; }
        public string? email { get; set; }

    }
    public class imovel
    {
        public string? inscImobFisc { get; set; }
        public string? cCIB { get; set; }

        public end? end { get; set; }
    }

    public class end : EndSimples
    {
        public string? CEP { get; set; }
    }

    public class endNac
    {
        public int cMun { get; set; }
        public string? CEP { get; set; }
    }

    public class endExt
    {
        public string? cPais { get; set; }
        public string? cEndPost { get; set; }
        public string? xCidade { get; set; }
        public string? xEstProvReg { get; set; }
    }

    public class valores
    {
        public gReeRepRes? gReeRepRes { get; set; }
        public trib? trib { get; set; }
    }

    public class gReeRepRes
    {
        // OCOR.: 1-1000 => lista
        public List<documentos> documentos { get; set; } = [];
    }

    public class documentos
    {
        public dFeNacional? dFeNacional { get; set; }
        public docFiscalOutro? docFiscalOutro { get; set; }
        public docOutro? docOutro { get; set; }
        public fornec? fornec { get; set; }
        public string? dtEmiDoc { get; set; }
        public string? dtCompDoc { get; set; }
        public int tpReeRepRes { get; set; }
        public string? xTpReeRepRes { get; set; }
        public decimal vlrReeRepRes { get; set; }

    }

    public class dFeNacional
    {
        public int tipoChaveDFe { get; set; }
        public string? xTipoChaveDFe { get; set; }
        public string? chaveDFe { get; set; }
    }

    public class docFiscalOutro
    {
        public int cMunDocFiscal { get; set; }
        public string? nDocFiscal { get; set; }
        public string? xDocFiscal { get; set; }
    }

    public class docOutro
    {
        public string? nDoc { get; set; }
        public string? xDoc { get; set; }
    }

    public class fornec
    {
        public string? CNPJ { get; set; }
        public string? CPF { get; set; }
        public string? NIF { get; set; }
        public int cNaoNIF { get; set; }
        public string? xNome { get; set; }
    }

    public class trib
    {
        public gIBSCBS? gIBSCBS { get; set; }
    }

    public class gIBSCBS
    {
        public string? CST { get; set; }
        public string? cClassTrib { get; set; }
        public string? cCredPres { get; set; }

        public gTribRegular? gTribRegular { get; set; }
        public gDif? gDif { get; set; }
    }

    public class gTribRegular
    {
        public string? CSTReg { get; set; }
        public string? cClassTribReg { get; set; }
    }

    public class gDif
    {
        public int pDifUF { get; set; }
        public int pDifMun { get; set; }
        public int pDifCBS { get; set; }
    }
}
