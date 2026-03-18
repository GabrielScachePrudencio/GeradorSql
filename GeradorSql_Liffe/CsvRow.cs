using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeradorSql_Liffe.SQLGenerator
{
    public class CsvRow
    {
        public string IdProcedimento { get; set; } = "";
        public string Nome { get; set; } = "";
        public string Tuss { get; set; } = "";
        public string IdConvenio { get; set; } = "";
        public string Convenio { get; set; } = "";
        public string ValorAntigo { get; set; } = "";
        public string ValorNovo { get; set; } = "";
        public string DataCadastro { get; set; } = "";
        public string Ativo { get; set; } = "";
    }
}
