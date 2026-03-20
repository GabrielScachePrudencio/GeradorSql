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
    using GeradorSql_Liffe.Utils;
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

        public partial class MainWindow : Window
        {
            private readonly ObservableCollection<CsvRow> dados = new();

            public MainWindow()
            {
                InitializeComponent();
                tabela.ItemsSource = dados;
            }

            // ============================================================
            //  SUBSTITUIÇÃO DO MÉTODO BtnAbrirCSV_Click em MainWindow.cs
            //  Detecta separador automaticamente (vírgula, ponto e vírgula,
            //  tab, pipe) e mapeia colunas pelo NOME do cabeçalho,
            //  sem depender da ordem das colunas no arquivo.
            // ============================================================

            private void BtnAbrirCSV_Click(object sender, RoutedEventArgs e)
            {
                var dlg = new OpenFileDialog { Filter = "CSV files|*.csv|All files|*.*" };
                if (dlg.ShowDialog() != true) return;

                dados.Clear();

                try
                {
                    using var reader = new StreamReader(dlg.FileName, detectEncodingFromByteOrderMarks: true);

                    // ── 1. Lê a linha de cabeçalho ──────────────────────────────────────
                    string? headerLine = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(headerLine))
                    {
                        MessageBox.Show("O arquivo está vazio ou não tem cabeçalho.", "Aviso",
                                        MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // ── 2. Detecta o separador automaticamente ──────────────────────────
                    char sep = DetectarSeparador(headerLine);

                    // ── 3. Mapeia o nome da coluna → índice (case-insensitive + trim) ───
                    var cabecalhos = SplitLinha(headerLine, sep);
                    var idx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < cabecalhos.Length; i++)
                        idx[cabecalhos[i].Trim().Trim('"')] = i;

                    // Nomes aceitos por coluna (adicione aliases conforme o seu CSV)
                    int Col(params string[] aliases)
                    {
                        foreach (var a in aliases)
                            if (idx.TryGetValue(a, out int i)) return i;
                        return -1; // coluna não encontrada
                    }

                    int iIdProc = Col("idProcedimento", "id_procedimento", "idProc", "id");
                    int iNome = Col("Nome", "NomeProcedimento", "Procedimento", "descricao");
                    int iTuss = Col("Tuss", "CodigoTuss", "cod_tuss", "tuss");
                    int iIdConv = Col("IdConvenio", "id_convenio", "idConv");
                    int iConv = Col("Convenio", "NomeConvenio", "convenio");
                    int iValorAntigo = Col("ValorAntigo", "valor_antigo", "v_antigo", "ValorAnterior");
                    int iValorNovo = Col("ValorNovo", "valor_novo", "v_novo", "NovoValor");
                    int iData = Col("DataCadastro", "data_cadastro", "Data", "DataVigencia");
                    int iAtivo = Col("Ativo", "ativo", "Status", "status");

                    // ── 4. Lê as linhas de dados ────────────────────────────────────────
                    string? linha;
                    int numeroLinha = 1;
                    int erros = 0;

                    while ((linha = reader.ReadLine()) != null)
                    {
                        numeroLinha++;
                        if (string.IsNullOrWhiteSpace(linha)) continue;

                        var c = SplitLinha(linha, sep);

                        // Limpa aspas e espaços de cada célula
                        for (int i = 0; i < c.Length; i++)
                            c[i] = c[i].Trim().Trim('"').Trim();

                        string Get(int colIdx) =>
                            colIdx >= 0 && colIdx < c.Length ? c[colIdx] : "";

                        try
                        {
                            dados.Add(new CsvRow
                            {
                                IdProcedimento = Get(iIdProc),
                                Nome = Get(iNome),
                                Tuss = Get(iTuss),
                                IdConvenio = Get(iIdConv),
                                Convenio = Get(iConv),
                                ValorAntigo = Get(iValorAntigo),
                                ValorNovo = Get(iValorNovo),
                                DataCadastro = Get(iData),
                                Ativo = Get(iAtivo)
                            });
                        }
                        catch
                        {
                            erros++;
                        }
                    }

                    string msg = $"🚀 IMPORTAÇÃO FINALIZADA COM SUCESSO!\n\n" +
                                 $"• Separador detectado : '{sep}'\n" +
                                 $"• Linhas importadas   : {dados.Count}\n" +
                                 (erros > 0 ? $"• Linhas com erro     : {erros}" : "");

                    MessageBox.Show(msg, "Importação concluída",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Erro ao abrir CSV",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            // ================================================================
            //  MÉTODOS AUXILIARES — adicione-os também dentro de MainWindow
            // ================================================================

            /// <summary>
            /// Detecta o separador mais provável pela contagem na linha de cabeçalho.
            /// Suporta: ';'  ','  '\t'  '|'
            /// </summary>
            private static char DetectarSeparador(string linha)
            {
                char[] candidatos = { ';', ',', '\t', '|' };
                return candidatos.OrderByDescending(s => linha.Count(c => c == s)).First();
            }

            /// <summary>
            /// Divide uma linha CSV respeitando campos entre aspas duplas.
            /// Funciona com qualquer separador.
            /// </summary>
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
                        // Aspas duplas escapadas ("") dentro de um campo
                        if (dentroAspas && i + 1 < linha.Length && linha[i + 1] == '"')
                        {
                            atual.Append('"');
                            i++; // pula o segundo "
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

                campos.Add(atual.ToString()); // último campo
                return campos.ToArray();
            }

            private void BtnUpdateProducao_Click(object sender, RoutedEventArgs e) => GerarSQL(1);
            private void BtnInsertValor_Click(object sender, RoutedEventArgs e) => GerarSQL(2);
            private void BtnUpdateValor_Click(object sender, RoutedEventArgs e) => GerarSQL(3);
            private void BtnInsertProc_Click(object sender, RoutedEventArgs e) => GerarSQL(4);

            private void BtnGerarCheck_Click(object sender, RoutedEventArgs e)
            {
                try
                {
                    if (dados.Count == 0)
                    {
                        MessageBox.Show("Carregue um CSV primeiro.", "Aviso",
                                        MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Gera e abre o SELECT de conferência
                    string sql = GeradorSQL.GerarSelectValidacao(dados, dataInicio.SelectedDate, dataFim.SelectedDate);
                    string caminhoArquivo = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "check_banco.sql");
                    System.IO.File.WriteAllText(caminhoArquivo, sql);
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(caminhoArquivo) { UseShellExecute = true });

                    // Abre popup passando as datas
                    var janela = new ResultadoBancoWindow(dados, dataInicio.SelectedDate, dataFim.SelectedDate)
                    {
                        Owner = this
                    };

                    // Quando o SQL for gerado, exibe os cards no painel principal
                    janela.OnSQLGerado += (sqlGerado) =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            painelCards.Children.Clear();
                            emptyLabel.Visibility = Visibility.Collapsed;

                            // Divide o SQL em blocos por procedimento (separados por linha em branco dupla)
                            var blocos = sqlGerado
                                .Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries)
                                .Where(b => b.Trim().StartsWith("--") || b.Contains("UPDATE") || b.Contains("SELECT"))
                                .ToList();

                            int idx = 1;
                            foreach (var bloco in blocos)
                            {
                                if (string.IsNullOrWhiteSpace(bloco)) continue;
                                painelCards.Children.Add(CriarCardSQL($"UPDATE FINAL #{idx}", bloco.Trim(), "#22c55e"));
                                idx++;
                            }

                            lblTotalCards.Text = $"{idx - 1} blocos";
                            janela.Close();
                        });
                    };

                    janela.Show();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Erro: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }


            private void GerarSQL(int tipo)
            {
                DateTime? inicio = dataInicio.SelectedDate;
                DateTime? fim = dataFim.SelectedDate;

                string[] titulos = { "UPDATE PRODUCAO", "INSERT CONV VALOR PROC", "UPDATE CONV VALOR PROC", "INSERT PROCEDIMENTOS" };
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

            /*
            private void BtnProcessarResultado_Click(object sender, RoutedEventArgs e)
            {
                // Como o Clipboard está instável, você pode usar um TextBox chamado 'txtResultadoBanco' 
                // ou pegar do Clipboard com o Helper que criamos
                string textoDoBanco = txtResultadoBanco.Text;

                if (string.IsNullOrWhiteSpace(textoDoBanco))
                {
                    MessageBox.Show("Cole o resultado do banco no campo de texto primeiro!");
                    return;
                }

                string sqlFinal = GeradorSQL.GerarUpdatesFinais(textoDoBanco, dados);

                // Salva direto no arquivo para evitar erro de cópia
                string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "updates_finais.sql");
                System.IO.File.WriteAllText(path, sqlFinal);

                // Abre o arquivo pronto para você rodar no banco
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });

                MessageBox.Show($"Sucesso! {path} gerado com os comandos de update.");
            }
            */

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
            private void CopiarParaClipboard(string texto)
            {
                // Tenta 5 vezes antes de desistir
                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        Clipboard.SetText(texto);
                        return; // Se funcionou, sai do método
                    }
                    catch (System.Runtime.InteropServices.COMException)
                    {
                        // Se falhou, espera 50ms e tenta de novo
                        System.Threading.Thread.Sleep(50);
                    }
                }

                // Se chegar aqui, realmente deu erro após 5 tentativas
                MessageBox.Show("Não foi possível acessar a Área de Transferência. Tente novamente.");
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
                    CopiarParaClipboard(sql); // Chama o método robusto aqui

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