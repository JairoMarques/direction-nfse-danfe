using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Direction.NFSe.Danfe
{
    public static class MunicipiosIbge
    {
        private static readonly object _lock = new object();
        private static bool _initialized;
        private static Dictionary<int, Municipio> _municipiosPorCodigo = [];

        /// <summary>
        /// Inicializa o dicionário de municípios a partir dos CSVs:
        /// - estados.csv (codigo_uf,uf,...)
        /// - municipios.csv (codigo_ibge,nome,...,codigo_uf,...)
        /// </summary>
        /// <param name="estadosCsvPath">Caminho completo do estados.csv</param>
        /// <param name="municipiosCsvPath">Caminho completo do municipios.csv</param>
        public static void Initialize(string estadosCsvPath, string municipiosCsvPath)
        {
            if (_initialized)
                return;

            lock (_lock)
            {
                if (_initialized)
                    return;

                if (!File.Exists(estadosCsvPath))
                    throw new FileNotFoundException("Arquivo estados.csv não encontrado.", estadosCsvPath);

                if (!File.Exists(municipiosCsvPath))
                    throw new FileNotFoundException("Arquivo municipios.csv não encontrado.", municipiosCsvPath);

                // codigo_uf → "UF"
                var mapaUf = CarregarEstados(estadosCsvPath);

                // codigo_ibge → Municipio
                _municipiosPorCodigo = CarregarMunicipios(municipiosCsvPath, mapaUf);

                _initialized = true;
            }
        }

        /// <summary>
        /// Retorna o objeto Municipio para o código IBGE informado (string).
        /// É necessário chamar Initialize antes.
        /// Retorna null se o código for inválido ou não existir no dicionário.
        /// </summary>
        public static Municipio GetMunicipio(int? codigoIbge)
        {
            if (codigoIbge == null)
                throw new InvalidOperationException("Código do ibge não pode ser nulo!");
            if (!_initialized)
                throw new InvalidOperationException("MunicipiosIbge.Initialize(...) deve ser chamado antes de GetMunicipio.");

            _municipiosPorCodigo.TryGetValue((int)codigoIbge, out var municipio);
            return municipio;
        }

        /// <summary>
        /// Lê o estados.csv e monta um dicionário: codigo_uf → "UF".
        /// Formato esperado (primeiras colunas):
        /// codigo_uf,uf,nome,latitude,longitude,regiao
        /// </summary>
        private static Dictionary<int, string> CarregarEstados(string estadosCsvPath)
        {
            var dict = new Dictionary<int, string>();

            foreach (var line in File.ReadLines(estadosCsvPath))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                // pula cabeçalho
                if (line.StartsWith("codigo_uf", StringComparison.OrdinalIgnoreCase))
                    continue;

                var parts = line.Split(',');

                if (parts.Length < 2)
                    continue;

                if (!int.TryParse(parts[0], out var codigoUf))
                    continue;

                var uf = parts[1].Trim();

                if (!dict.ContainsKey(codigoUf))
                {
                    dict[codigoUf] = uf;
                }
            }

            return dict;
        }

        /// <summary>
        /// Lê o municipios.csv e monta um dicionário: codigo_ibge → Municipio.
        /// Formato esperado (colunas):
        /// codigo_ibge,nome,latitude,longitude,capital,codigo_uf,
        /// siafi_id,ddd,fuso_horario,logo,logo_name
        /// </summary>
        private static Dictionary<int, Municipio> CarregarMunicipios(
            string municipiosCsvPath,
            Dictionary<int, string> mapaCodigoUfParaUf)
        {
            var dict = new Dictionary<int, Municipio>();

            foreach (var line in File.ReadLines(municipiosCsvPath))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                // pula cabeçalho
                if (line.StartsWith("codigo_ibge", StringComparison.OrdinalIgnoreCase))
                    continue;

                var parts = line.Split(',');

                if (parts.Length < 6)
                    continue;

                // codigo_ibge
                if (!int.TryParse(parts[0], out var codigoIbge))
                    continue;

                // nome
                var nome = parts[1].Trim();

                // latitude
                double latitude = 0;
                if (parts.Length > 2)
                    double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out latitude);

                // longitude
                double longitude = 0;
                if (parts.Length > 3)
                    double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out longitude);

                // capital
                bool capital = false;
                if (parts.Length > 4)
                {
                    var capitalStr = parts[4].Trim();
                    capital = capitalStr == "1" ||
                              capitalStr.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                              capitalStr.Equals("sim", StringComparison.OrdinalIgnoreCase);
                }

                // codigo_uf
                if (!int.TryParse(parts[5], out var codigoUf))
                    continue;

                if (!mapaCodigoUfParaUf.TryGetValue(codigoUf, out var uf))
                    continue; // se não achou UF, ignora esse município

                // siafi_id
                int siafiId = 0;
                if (parts.Length > 6)
                    int.TryParse(parts[6], out siafiId);

                // ddd
                string ddd = parts.Length > 7 && !string.IsNullOrWhiteSpace(parts[7]) ? parts[7].Trim() : string.Empty;

                // fuso_horario
                var fusoHorario = parts.Length > 8 ? parts[8].Trim() : string.Empty;

                // logo
                var logoPath = parts.Length > 9 ? parts[9].Trim() : string.Empty;

                // logo_name
                var logoName = parts.Length > 10 ? parts[10].Trim() : string.Empty;

                var municipio = new Municipio
                {
                    CodigoIbge = codigoIbge,
                    Nome = nome,
                    Latitude = latitude,
                    Longitude = longitude,
                    Capital = capital,
                    CodigoUf = codigoUf,
                    Uf = uf,
                    SiafiId = siafiId,
                    Ddd = ddd,
                    FusoHorario = fusoHorario,
                    LogoPath = logoPath,
                    LogoName = logoName
                };

                dict[codigoIbge] = municipio;
            }

            return dict;
        }

        public class Municipio
        {
            // codigo_ibge,nome,latitude,longitude,capital,codigo_uf,siafi_id,ddd,fuso_horario,logo,logo_name
            public int CodigoIbge { get; set; }
            public string Nome { get; set; } = string.Empty;
            public double Latitude { get; set; }
            public double Longitude { get; set; }
            public bool Capital { get; set; }
            public int CodigoUf { get; set; }

            /// <summary>
            /// Sigla da UF, preenchida a partir do estados.csv.
            /// </summary>
            public string Uf { get; set; } = string.Empty;
            public int SiafiId { get; set; }
            public string Ddd { get; set; } = string.Empty;
            public string FusoHorario { get; set; } = string.Empty;
            public string LogoPath { get; set; } = string.Empty;
            public string LogoName { get; set; } = string.Empty;

            /// <summary>
            /// Conveniência para manter o formato antigo: "Nome - UF".
            /// </summary>
            public string NomeComUf => string.IsNullOrEmpty(Uf) ? Nome : $"{Nome} - {Uf}";
        }
    }
}
