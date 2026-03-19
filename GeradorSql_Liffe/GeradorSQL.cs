using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeradorSql_Liffe.SQLGenerator
{
    public static class GeradorSQL
    {
        public static string GerarUpdateProducao(CsvRow r, DateTime? inicio, DateTime? fim)
        {
            string filtroData = "";
            if (inicio.HasValue && fim.HasValue)
                filtroData = $" AND date(dataProcedimento) BETWEEN \"{inicio:yyyy-MM-dd}\" AND \"{fim:yyyy-MM-dd}\"";
            else if (inicio.HasValue)
                filtroData = $" AND date(dataProcedimento) >= \"{inicio:yyyy-MM-dd}\"";
            else if (fim.HasValue)
                filtroData = $" AND date(dataProcedimento) <= \"{fim:yyyy-MM-dd}\"";

            // Verifica se o valor antigo existe para decidir se inclui o filtro no WHERE
            string filtroValor = string.IsNullOrWhiteSpace(r.ValorAntigo)
                ? ""
                : $"AND valor = {r.ValorAntigo}";

            return
    $@"SELECT COUNT(*) 
FROM producao
WHERE idProcedimento = {r.IdProcedimento}
AND idConvenio = {r.IdConvenio}
{filtroValor}{filtroData};

UPDATE producao
SET valor = {r.ValorNovo}
WHERE idProcedimento = {r.IdProcedimento}
AND idConvenio = {r.IdConvenio}
{filtroValor}{filtroData}
AND idProducao > 0;

";
        }

        public static string GerarInsertValor(CsvRow r) =>
$@"INSERT INTO procedimentovalorconvenio
(idProcedimento, idConvenio, valor, dataCadastro, ativo)
VALUES
({r.IdProcedimento}, {r.IdConvenio}, {r.ValorNovo}, NOW(), 1);

";

        public static string GerarUpdateValor(CsvRow r) =>
$@"UPDATE procedimentovalorconvenio
SET valor = {r.ValorNovo}
WHERE idProcedimento = {r.IdProcedimento}
AND idConvenio = {r.IdConvenio}
AND valor = {r.ValorAntigo};

";

        public static string GerarInsertsProcedimentos(CsvRow r) =>
$@"INSERT INTO procedimento 
(nome, tuss) 
VALUES
('{r.Nome}', '{r.Tuss}');

";



        public static string GerarSelectValidacao(ObservableCollection<CsvRow> lista, DateTime? inicio, DateTime? fim)
        {
            if (lista.Count == 0) return "-- CSV vazio.";

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("-- SQL PARA CONFERIR O QUE REALMENTE EXISTE NO BANCO");
            sb.AppendLine("SELECT idProcedimento, idConvenio, valor, dataProcedimento");
            sb.AppendLine("FROM producao");
            sb.Append("WHERE (idProcedimento, idConvenio) IN (");

            for (int i = 0; i < lista.Count; i++)
            {
                sb.Append($"({lista[i].IdProcedimento}, {lista[i].IdConvenio})");
                if (i < lista.Count - 1) sb.Append(",");
                if (i > 0 && i % 8 == 0) sb.AppendLine(); // Quebra linha para não travar o editor
            }
            sb.AppendLine(")");

            // FILTRO DE DATA OBRIGATÓRIO (Baseado no seu calendário)
            if (inicio.HasValue && fim.HasValue)
                sb.AppendLine($"AND date(dataProcedimento) BETWEEN '{inicio:yyyy-MM-dd}' AND '{fim:yyyy-MM-dd}'");
            else if (inicio.HasValue)
                sb.AppendLine($"AND date(dataProcedimento) >= '{inicio:yyyy-MM-dd}'");
            else if (fim.HasValue)
                sb.AppendLine($"AND date(dataProcedimento) <= '{fim:yyyy-MM-dd}'");

            sb.AppendLine("AND idProducao > 0;");

            return sb.ToString();
        }


        public static string GerarUpdatesFinaisDeCSV(
    List<Dictionary<string, string>> linhasBanco,
    System.Collections.ObjectModel.ObservableCollection<CsvRow> dadosCsv,
    DateTime? inicio,
    DateTime? fim)
        {
            if (linhasBanco.Count == 0) return "-- Nenhum dado importado do banco.";

            var sb = new StringBuilder();
            sb.AppendLine($"-- ### UPDATES FINAIS GERADOS EM {DateTime.Now:dd/MM/yyyy HH:mm:ss} ###");
            sb.AppendLine();

            // Monta filtro de data
            string filtroData = "";
            if (inicio.HasValue && fim.HasValue)
                filtroData = $"\n  AND date(dataProcedimento) BETWEEN '{inicio:yyyy-MM-dd}' AND '{fim:yyyy-MM-dd}'";
            else if (inicio.HasValue)
                filtroData = $"\n  AND date(dataProcedimento) >= '{inicio:yyyy-MM-dd}'";
            else if (fim.HasValue)
                filtroData = $"\n  AND date(dataProcedimento) <= '{fim:yyyy-MM-dd}'";

            int encontrados = 0;
            int naoEncontrados = 0;

            foreach (var linha in linhasBanco)
            {
                string idProc = ObterValor(linha, "idProcedimento", "id_procedimento", "procedimento_id");
                string idConv = ObterValor(linha, "idConvenio", "id_convenio", "convenio_id");

                if (string.IsNullOrWhiteSpace(idProc) || string.IsNullOrWhiteSpace(idConv))
                {
                    naoEncontrados++;
                    continue;
                }

                var dadosSemDuplicados = dadosCsv
                .GroupBy(x => new { x.IdProcedimento, x.IdConvenio })
                .Select(g => g.First())
                .ToList();

                var match = dadosSemDuplicados.FirstOrDefault(x =>
    x.IdProcedimento.Trim() == idProc.Trim() &&
    x.IdConvenio.Trim() == idConv.Trim());

                if (match == null) { naoEncontrados++; continue; }

                string valorSql = match.ValorNovo.Replace(",", ".");

                sb.AppendLine($"-- Procedimento: {match.Nome} | Convênio: {match.Convenio}");
                //sb.AppendLine($"SELECT COUNT(*) FROM producao");
                // sb.AppendLine($"WHERE idProcedimento = {idProc}");
                //sb.AppendLine($"  AND idConvenio = {idConv}{filtroData}");
                //sb.AppendLine($"  AND idProducao > 0;");
                sb.AppendLine();
                sb.AppendLine($"UPDATE producao");
                sb.AppendLine($"SET valor = {valorSql}");
                sb.AppendLine($"WHERE idProcedimento = {idProc}");
                sb.AppendLine($"  AND idConvenio = {idConv}{filtroData}");
                sb.AppendLine($"  AND idProducao > 0;");
                sb.AppendLine();
                encontrados++;
            }

            sb.AppendLine($"-- Resumo: {encontrados} UPDATE(s) gerado(s), {naoEncontrados} sem correspondência.");
            return sb.ToString();
        }

        // Helper: busca o primeiro campo que existir no dicionário (case-insensitive)
        private static string ObterValor(Dictionary<string, string> dict, params string[] chaves)
        {
            foreach (var chave in chaves)
                if (dict.TryGetValue(chave, out var val))
                    return val;
            return "";
        }



    }
}
