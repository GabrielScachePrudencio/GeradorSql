using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace GeradorSql_Liffe
{
    using Microsoft.Win32;
    using System;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Reflection.Emit;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using System.Windows.Threading;

    namespace SQLGenerator
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

                return
    $@"SELECT COUNT(*) 
FROM producao
WHERE idProcedimento = {r.IdProcedimento}
AND idConvenio = {r.IdConvenio}
AND valor = {r.ValorAntigo}{filtroData};

UPDATE producao
SET valor = {r.ValorNovo}
WHERE idProcedimento = {r.IdProcedimento}
AND idConvenio = {r.IdConvenio}
AND valor = {r.ValorAntigo}{filtroData}
and idProducao > 0;;

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
        }

        public partial class MainWindow : Window
        {
            private readonly ObservableCollection<CsvRow> dados = new();

            public MainWindow()
            {
                InitializeComponent();
                tabela.ItemsSource = dados;
            }

            private void BtnAbrirCSV_Click(object sender, RoutedEventArgs e)
            {
                var dlg = new OpenFileDialog { Filter = "CSV files|*.csv|All files|*.*" };
                if (dlg.ShowDialog() != true) return;

                dados.Clear();

                try
                {
                    using var reader = new StreamReader(dlg.FileName);

                    string header = reader.ReadLine();
                    MessageBox.Show($"HEADER:\n{header}");

                    int numeroLinha = 1;

                    while (!reader.EndOfStream)
                    {
                        var linha = reader.ReadLine();
                        numeroLinha++;

                        if (string.IsNullOrWhiteSpace(linha)) continue;

                        // Mostra linha crua
                        //MessageBox.Show($"📄 LINHA {numeroLinha}:\n{linha}");

                        // SPLIT UNIVERSAL (resolve 99% dos CSV zoado)
                        var c = linha.Split(new char[] { ',', ';', '\t' });

                        // Remove espaços extras
                        for (int i = 0; i < c.Length; i++)
                            c[i] = c[i].Trim();

                        //MessageBox.Show($"🔢 COLUNAS: {c.Length}");

                        // Mostra conteúdo de cada coluna
                        string detalhe = "";
                        for (int i = 0; i < c.Length; i++)
                            detalhe += $"c[{i}] = '{c[i]}'\n";

                        //MessageBox.Show($"📊 DETALHES:\n{detalhe}");

                        if (c.Length < 8)
                        {
                           // MessageBox.Show($"❌ LINHA {numeroLinha} IGNORADA (menos de 8 colunas)");
                            continue;
                        }

                        try
                        {
                            var row = new CsvRow
                            {
                                IdProcedimento = c[0],
                                Nome = c[1],
                                Tuss = c[2],
                                IdConvenio = c[3],
                                Convenio = c[4],
                                ValorAntigo = c.Length > 8 ? c[8] : "",
                                ValorNovo = c[5],
                                DataCadastro = c[6],
                                Ativo = c[7]
                            };

                            dados.Add(row);

                            //MessageBox.Show(
                                //$"✅ LINHA {numeroLinha} OK\n\n" +
                                //$"IdProcedimento: {row.IdProcedimento}\n" +
                                //$"ValorAntigo: {row.ValorAntigo}\n" +
                               // $"ValorNovo: {row.ValorNovo}"
                            //);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"💥 ERRO NA LINHA {numeroLinha}:\n{ex.Message}");
                        }
                    }

                    MessageBox.Show("🚀 IMPORTAÇÃO FINALIZADA");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Erro ao abrir CSV", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            private void BtnUpdateProducao_Click(object sender, RoutedEventArgs e) => GerarSQL(1);
            private void BtnInsertValor_Click(object sender, RoutedEventArgs e) => GerarSQL(2);
            private void BtnUpdateValor_Click(object sender, RoutedEventArgs e) => GerarSQL(3);
            private void BtnInsertProc_Click(object sender, RoutedEventArgs e) => GerarSQL(4);

            private void GerarSQL(int tipo)
            {
                DateTime? inicio = dataInicio.SelectedDate;
                DateTime? fim = dataFim.SelectedDate;

                string[] titulos = { "UPDATE PRODUCAO", "INSERT VALOR", "UPDATE VALOR", "INSERT PROCEDIMENTOS" };
                string[] cores = { "#22c55e", "#3b82f6", "#f59e0b", "#a855f7" };

                string titulo = titulos[tipo - 1];
                string cor = cores[tipo - 1];

                painelCards.Children.Clear();
                emptyLabel.Visibility = Visibility.Collapsed;

                int idx = 1;
                foreach (var r in dados)
                {
                    string sql = tipo switch
                    {
                        1 => GeradorSQL.GerarUpdateProducao(r, inicio, fim),
                        2 => GeradorSQL.GerarInsertValor(r),
                        3 => GeradorSQL.GerarUpdateValor(r),
                        4 => GeradorSQL.GerarInsertsProcedimentos(r),
                        _ => ""
                    };

                    if (string.IsNullOrWhiteSpace(sql)) continue;
                    painelCards.Children.Add(CriarCardSQL($"{titulo} #{idx}", sql, cor));
                    idx++;
                }

                if (painelCards.Children.Count == 0)
                {
                    emptyLabel.Text = "Nenhum dado encontrado. Carregue um CSV primeiro.";
                    emptyLabel.Visibility = Visibility.Visible;
                    lblTotalCards.Text = "0 blocos";
                }
                else
                {
                    lblTotalCards.Text = $"{idx - 1} blocos";
                }
            }

            private void BtnSalvarSQL_Click(object sender, RoutedEventArgs e)
            {
                var dlg = new SaveFileDialog { FileName = "script.sql", Filter = "SQL files|*.sql|All files|*.*" };
                if (dlg.ShowDialog() != true) return;

                var sb = new System.Text.StringBuilder();
                foreach (UIElement el in painelCards.Children)
                {
                    if (el is Border cardBorder && cardBorder.Child is DockPanel dp)
                    {
                        foreach (UIElement child in dp.Children)
                        {
                            if (child is ScrollViewer sv && sv.Content is TextBlock tb)
                                sb.AppendLine(tb.Text);
                        }
                    }
                }

                try { File.WriteAllText(dlg.FileName, sb.ToString()); }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Erro ao salvar", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            private Border CriarCardSQL(string titulo, string sql, string corHex)
            {
                var cor = (Color)ColorConverter.ConvertFromString(corHex);

                var lblTitulo = new TextBlock
                {
                    Text = titulo,
                    Foreground = new SolidColorBrush(cor),
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    FontFamily = new FontFamily("Consolas, Courier New"),
                    VerticalAlignment = VerticalAlignment.Center
                };

                var btnCopiar = new Button
                {
                    Content = "Copiar",
                    Foreground = new SolidColorBrush(cor),
                    Background = new SolidColorBrush(Color.FromArgb(34, cor.R, cor.G, cor.B)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(85, cor.R, cor.G, cor.B)),
                    Style = (Style)FindResource("CopyBtn")
                };
                btnCopiar.Click += (_, _) =>
                {
                    Clipboard.SetText(sql);
                    btnCopiar.Content = "Copiado!";
                    var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
                    t.Tick += (_, _) => { btnCopiar.Content = "Copiar"; t.Stop(); };
                    t.Start();
                };

                var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition());
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                Grid.SetColumn(lblTitulo, 0);
                Grid.SetColumn(btnCopiar, 1);
                headerGrid.Children.Add(lblTitulo);
                headerGrid.Children.Add(btnCopiar);

                var lblSql = new TextBlock
                {
                    Text = sql,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5eead4")),
                    FontFamily = new FontFamily("Consolas, Courier New"),
                    FontSize = 12.5,
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0d1120")),
                    Padding = new Thickness(12, 10, 12, 10),
                    TextWrapping = TextWrapping.NoWrap
                };

                var sqlScroll = new ScrollViewer
                {
                    Content = lblSql,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0d1120"))
                };

                var body = new DockPanel { LastChildFill = true };
                DockPanel.SetDock(headerGrid, Dock.Top);
                body.Children.Add(headerGrid);
                body.Children.Add(sqlScroll);

                return new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#12172a")),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(51, cor.R, cor.G, cor.B)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(12),
                    Margin = new Thickness(0, 0, 0, 10),
                    Child = body
                };
            }
        }
    }

}