using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;

namespace GeradorSql_Liffe.SQLGenerator
{
    public partial class ResultadoBancoWindow : Window
    {
        private List<Dictionary<string, string>> _linhasBanco = new();

        private readonly ObservableCollection<CsvRow> _dadosCsv;
        private readonly DateTime? _inicio;
        private readonly DateTime? _fim;

        public event Action<string>? OnSQLGerado;

        public ResultadoBancoWindow(
            ObservableCollection<CsvRow> dadosCsv,
            DateTime? inicio,
            DateTime? fim)
        {
            InitializeComponent();
            _dadosCsv = dadosCsv;
            _inicio = inicio;
            _fim = fim;
        }

        // ──────────────────────────────────────────────
        //  IMPORTAR CSV DO BANCO
        // ──────────────────────────────────────────────
        private void BtnImportarCSV_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Selecione o CSV exportado do banco",
                Filter = "CSV files|*.csv|All files|*.*"
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                _linhasBanco = LerCSV(dlg.FileName);

                if (_linhasBanco.Count == 0)
                {
                    lblStatus.Text = "⚠️  Arquivo vazio ou sem dados válidos.";
                    lblLinhas.Text = "0 linhas";
                    btnGerarUpdates.IsEnabled = false;
                    return;
                }

                // Monta DataTable para exibir no grid
                var dt = new DataTable();
                foreach (var col in _linhasBanco[0].Keys)
                    dt.Columns.Add(col);

                foreach (var linha in _linhasBanco)
                {
                    var row = dt.NewRow();
                    foreach (var kvp in linha)
                        row[kvp.Key] = kvp.Value;
                    dt.Rows.Add(row);
                }

                gridPreview.ItemsSource = dt.DefaultView;
                lblLinhas.Text = $"{_linhasBanco.Count} linhas";
                lblStatus.Text = $"✅  {_linhasBanco.Count} registros importados — pronto para gerar UPDATEs.";
                btnGerarUpdates.IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao ler CSV:\n{ex.Message}", "Erro",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ──────────────────────────────────────────────
        //  GERAR UPDATES → dispara evento para MainWindow
        // ──────────────────────────────────────────────
        private void BtnGerarUpdates_Click(object sender, RoutedEventArgs e)
        {
            if (_linhasBanco.Count == 0) return;

            try
            {
                string sql = GeradorSQL.GerarUpdatesFinaisDeCSV(_linhasBanco, _dadosCsv, _inicio, _fim);

                OnSQLGerado?.Invoke(sql);

                lblStatus.Text = "✅  SQL enviado para o painel principal!";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao gerar UPDATEs:\n{ex.Message}", "Erro",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ──────────────────────────────────────────────
        //  LEITOR DE CSV GENÉRICO
        // ──────────────────────────────────────────────
        private static List<Dictionary<string, string>> LerCSV(string path)
        {
            var result = new List<Dictionary<string, string>>();

            using var reader = new StreamReader(path, detectEncodingFromByteOrderMarks: true);

            string? headerLine = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(headerLine)) return result;

            char sep = DetectarSeparador(headerLine);
            string[] headers = SplitLinha(headerLine, sep);

            while (!reader.EndOfStream)
            {
                string? linha = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(linha)) continue;

                string[] cols = SplitLinha(linha, sep);

                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < headers.Length; i++)
                    dict[headers[i].Trim()] = i < cols.Length ? cols[i].Trim() : "";

                result.Add(dict);
            }

            return result;
        }

        private static char DetectarSeparador(string linha)
        {
            var candidatos = new[] { ';', ',', '\t' };
            return candidatos.OrderByDescending(c => linha.Count(x => x == c)).First();
        }

        private static string[] SplitLinha(string linha, char sep)
        {
            var campos = new List<string>();
            bool dentroAspas = false;
            var atual = new System.Text.StringBuilder();

            for (int i = 0; i < linha.Length; i++)
            {
                char c = linha[i];

                if (c == '"')
                {
                    if (dentroAspas && i + 1 < linha.Length && linha[i + 1] == '"')
                    {
                        atual.Append('"');
                        i++;
                    }
                    else
                    {
                        dentroAspas = !dentroAspas;
                    }
                }
                else if (c == sep && !dentroAspas)
                {
                    campos.Add(atual.ToString());
                    atual.Clear();
                }
                else
                {
                    atual.Append(c);
                }
            }

            campos.Add(atual.ToString());
            return campos.ToArray();
        }
    }
}