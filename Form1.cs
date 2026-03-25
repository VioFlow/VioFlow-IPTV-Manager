using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VioFlow_IPTV_Manager
{
    public partial class Form1 : Form
    {
        private string versaoPendente = ""; // Memória para guardar a versão nova

        private void VerificarAtualizacao()
        {
            Task.Run(() =>
            {
                try
                {
                    using (WebClient client = new WebClient())
                    {
                        string urlVersao = "https://raw.githubusercontent.com/VioFlow/VioFlow-IPTV-Manager/refs/heads/main/VioFlow_IPTV_Manager_Version.txt";
                        string versaoRemota = client.DownloadString(urlVersao).Trim();
                        string versaoAtual = "1.0.0";

                        if (versaoRemota != versaoAtual)
                        {
                            this.Invoke(new Action(() =>
                            {
                                versaoPendente = versaoRemota;

                                linkAtualizacao.Visible = true;
                                linkAtualizacao.Text = $"⚠️ {ObterTraducao("Nova atualização disponível!")} ({versaoPendente})";
                                // Criamos um formulário invisível temporário para "segurar" o MessageBox na frente
                                Form wrapper = new Form { TopMost = true };

                                DialogResult dr = MessageBox.Show(
                                    wrapper, // <--- Isso obriga a mensagem a ficar na frente da "capa" do app
                                    ObterTraducao("Uma nova versão do VioFlow está disponível! Deseja baixar agora?"),
                                    ObterTraducao("Atualização Disponível"),
                                    MessageBoxButtons.YesNo,
                                    MessageBoxIcon.Information);

                                if (dr == DialogResult.Yes)
                                {
                                    System.Diagnostics.Process.Start("https://github.com/VioFlow/VioFlow-IPTV-Manager/releases");
                                }

                                wrapper.Dispose(); // Limpa o lixo da memória
                            }));
                        }
                    }
                }
                catch { }
            });
        }

        // 🧠 MEMÓRIA DO SENSOR DE PESQUISA (Evita travamentos ao digitar rápido)
        private int idDaBusca = 0;

        // 🧠 A memória que vai guardar o EPG Global da lista atual
        public string epgGlobalAtual = "";

        // VARIÁVEL GLOBAL PARA GUARDAR SEUS EPGs
        string epgsGlobais = "";

        // CORREÇÃO: HttpClient estático para evitar vazamento de sockets
        private static readonly HttpClient _httpClient = new HttpClient();

        // CORREÇÃO: Cache de logos com stream vivo para não corromper a imagem
        private Dictionary<string, (Image Imagem, MemoryStream Stream)> cacheDeLogos =
            new Dictionary<string, (Image, MemoryStream)>();

        // Memória para guardar todos os IDs e Nomes que vierem do EPG
        private Dictionary<string, string> memoriaEpg = new Dictionary<string, string>();

        // 🧠 AS DUAS MEMÓRIAS DA MÁQUINA DO TEMPO (Passado e Futuro)
        private Stack<List<string[]>> historicoDoTempo = new Stack<List<string[]>>();
        private Stack<List<string[]>> futuroDoTempo = new Stack<List<string[]>>();

        // 📸 A MÁQUINA FOTOGRÁFICA (Tira a foto da tela na hora)
        private List<string[]> TirarFotoDaTabela()
        {
            var foto = new List<string[]>();
            foreach (DataGridViewRow linha in tabelaCanais.Rows)
            {
                if (linha.IsNewRow) continue;
                string status = tabelaCanais.Columns.Contains("StatusUrl") ? linha.Cells["StatusUrl"].Value?.ToString() ?? "" : "";
                string logo = linha.Cells["LogoUrl"].Value?.ToString() ?? "";
                string nome = linha.Cells["NomeCanal"].Value?.ToString() ?? "";
                string epg = linha.Cells["EpgId"].Value?.ToString() ?? "";
                string cat = linha.Cells["Categoria"].Value?.ToString() ?? "";
                string url = linha.Cells["Url"].Value?.ToString() ?? "";
                foto.Add(new string[] { status, logo, nome, epg, cat, url });
            }
            return foto;
        }

        // 💾 O NOVO MOTOR DE SALVAR (Que apaga o futuro alternativo)
        private const int LIMITE_HISTORICO = 2; // Salva o PC de explodir a RAM
        private const int LIMITE_CACHE_LOGOS = 500;
        private HashSet<string> downloadsEmAndamento = new HashSet<string>();

        private string nomeArquivoAtual = "Nenhuma lista carregada";

        private void SalvarBackupDoTempo()
        {
            if (tabelaCanais != null) tabelaCanais.EndEdit();
            futuroDoTempo.Clear();
            historicoDoTempo.Push(TirarFotoDaTabela());

            // Impede o programa de guardar mais que 5 backups pesados na memória
            while (historicoDoTempo.Count > LIMITE_HISTORICO)
            {
                var temp = historicoDoTempo.ToArray();
                historicoDoTempo.Clear();
                for (int i = Math.Min(temp.Length - 1, LIMITE_HISTORICO - 1); i >= 0; i--)
                    historicoDoTempo.Push(temp[i]);
                break;
            }
        }

        // Mudei a assinatura para aceitar se é Refazer ou não
        private async void RestaurarFotoDaTabela(System.Collections.Generic.List<string[]> foto, bool isRefazer = false)
        {
            tabelaCanais.EndEdit();

            int totalAtual = 0;
            foreach (DataGridViewRow r in tabelaCanais.Rows) if (!r.IsNewRow) totalAtual++;

            // ✨ REGRA 1: Quantidade de canais igual = EDIÇÃO SIMPLES (Instantâneo)
            if (foto.Count == totalAtual)
            {
                tabelaCanais.SuspendLayout();
                int idxStatus = tabelaCanais.Columns["StatusUrl"].Index;
                int idxLogoUrl = tabelaCanais.Columns["LogoUrl"].Index;
                int idxNome = tabelaCanais.Columns["NomeCanal"].Index;
                int idxEpg = tabelaCanais.Columns["EpgId"].Index;
                int idxCat = tabelaCanais.Columns["Categoria"].Index;
                int idxUrl = tabelaCanais.Columns["Url"].Index;
                int idxFoto = tabelaCanais.Columns["FotoCanal"].Index;

                for (int i = 0; i < foto.Count; i++)
                {
                    DataGridViewRow linhaAtual = tabelaCanais.Rows[i];
                    string[] dadosPassado = foto[i];

                    if (linhaAtual.Cells[idxStatus].Value?.ToString() != dadosPassado[0]) linhaAtual.Cells[idxStatus].Value = dadosPassado[0];

                    if (linhaAtual.Cells[idxLogoUrl].Value?.ToString() != dadosPassado[1])
                    {
                        linhaAtual.Cells[idxLogoUrl].Value = dadosPassado[1];
                        string urlLogo = dadosPassado[1];
                        if (!string.IsNullOrEmpty(urlLogo) && cacheDeLogos.ContainsKey(urlLogo))
                            linhaAtual.Cells[idxFoto].Value = cacheDeLogos[urlLogo].Imagem;
                        else
                            linhaAtual.Cells[idxFoto].Value = null;
                    }

                    if (linhaAtual.Cells[idxNome].Value?.ToString() != dadosPassado[2]) linhaAtual.Cells[idxNome].Value = dadosPassado[2];
                    if (linhaAtual.Cells[idxEpg].Value?.ToString() != dadosPassado[3]) linhaAtual.Cells[idxEpg].Value = dadosPassado[3];
                    if (linhaAtual.Cells[idxCat].Value?.ToString() != dadosPassado[4]) linhaAtual.Cells[idxCat].Value = dadosPassado[4];
                    if (linhaAtual.Cells[idxUrl].Value?.ToString() != dadosPassado[5]) linhaAtual.Cells[idxUrl].Value = dadosPassado[5];
                }

                tabelaCanais.ResumeLayout();
                AtualizarStatus();
                BaixarImagensInvisivelmente();
                tabelaCanais.Invalidate();
            }
            // 🚀 REGRA 2: Quantidade de canais mudou = USA O MOTOR TURBO
            else
            {
                // 1. Define o texto dependendo de qual botão foi clicado
                string titulo = isRefazer ? "⏩ Refazendo... Aguarde!" : "⏪ Desfazendo... Aguarde!";

                // 2. Tela de carregamento maior e mais informativa
                Form telaLoad = new Form() { Width = 380, Height = 100, StartPosition = FormStartPosition.CenterScreen, FormBorderStyle = FormBorderStyle.None, BackColor = Color.FromArgb(35, 35, 38), TopMost = true };
                Label lblAviso = new Label() { Left = 10, Top = 20, Width = 360, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 11, FontStyle.Bold), Text = titulo };

                // O subtexto explicando que a lista é grande
                Label lblSubAviso = new Label() { Left = 10, Top = 55, Width = 360, ForeColor = Color.LightGray, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 9), Text = $"Processando lista gigante ({foto.Count:N0} canais)..." };

                telaLoad.Controls.Add(lblAviso);
                telaLoad.Controls.Add(lblSubAviso);
                telaLoad.Show();
                Application.DoEvents();

                // 3. Chama o Motor V8
                await ReconstruirTabelaTurbo(foto);

                telaLoad.Close();
                telaLoad.Dispose();
                AtualizarStatus();
            }
        }

        public Form1()
        {
            InitializeComponent();


            CriarBotaoIdioma();

            // LIGA O MOTOR EM TODOS OS MOVIMENTOS POSSÍVEIS DA TABELA:
            tabelaCanais.MouseWheel += (s, ev) => BaixarImagensInvisivelmente(); // Bolinha do Mouse
            tabelaCanais.SelectionChanged += (s, ev) => BaixarImagensInvisivelmente(); // Setinhas do Teclado



            // 1. Desativa a cópia teimosa nativa do DataGridView
            tabelaCanais.ClipboardCopyMode = DataGridViewClipboardCopyMode.Disable;

            // 2. Garante que o nosso evento de teclado vai ser chamado
            tabelaCanais.KeyDown += TabelaCanais_CopiarUnico;

            panel2.SendToBack();
            panel1.SendToBack();
            tabelaCanais.BringToFront();
            tabelaCanais.Dock = DockStyle.Fill;

            // 🎨 DESIGN AUTOMÁTICO DOS BOTÕES
            foreach (Control c in this.Controls)
            {
                if (c is Button b) ConfigurarBotaoPro(b);
                if (c is Panel p)
                    foreach (Control btnNoPainel in p.Controls)
                        if (btnNoPainel is Button bp) ConfigurarBotaoPro(bp);
            }

            // 🚀 TELA DE BOAS VINDAS (SPLASH SCREEN)
            this.Shown += async (s, ev) =>
            {
                this.Hide();
                Form splash = new Form()
                {
                    Width = 500,
                    Height = 300,
                    StartPosition = FormStartPosition.CenterScreen,
                    FormBorderStyle = FormBorderStyle.None,
                    BackColor = Color.FromArgb(31, 31, 31),
                    TopMost = true
                };

                string caminhoImagem = Path.Combine(Application.StartupPath, "abertura.png");
                if (File.Exists(caminhoImagem))
                {
                    splash.BackgroundImage = Image.FromFile(caminhoImagem);
                    splash.BackgroundImageLayout = ImageLayout.Stretch;
                }
                else
                {
                    Label lblLogo = new Label() { Left = 0, Top = 80, Width = 500, Height = 60, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.White, Font = new Font("Segoe UI", 28, FontStyle.Bold), Text = "VioFlow" };
                    Label lblSub = new Label() { Left = 0, Top = 140, Width = 500, Height = 40, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.DeepSkyBlue, Font = new Font("Segoe UI", 16, FontStyle.Italic), Text = "IPTV Manager" };
                    Label lblVer = new Label() { Left = 0, Top = 230, Width = 500, Height = 30, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.Gray, Font = new Font("Segoe UI", 9), Text = "Versão 1.0.0 • Carregando motor principal..." };
                    splash.Controls.Add(lblLogo);
                    splash.Controls.Add(lblSub);
                    splash.Controls.Add(lblVer);
                }

                splash.Show();
                await Task.Delay(3000);
                splash.Close();
                splash.Dispose();
                this.Show();
            };

            ConfigurarTabela();
            this.Text = "VioFlow IPTV Manager - Versão 1.0.0";
        }

        // 🛠️ Função auxiliar para botões
        private void ConfigurarBotaoPro(Button btn)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.Cursor = Cursors.Hand;
        }

        private void ConfigurarTabela()
        {
            tabelaCanais.Columns.Clear();

            tabelaCanais.BorderStyle = BorderStyle.None;
            tabelaCanais.Margin = new Padding(0);
            tabelaCanais.Padding = new Padding(0);
            tabelaCanais.AdvancedCellBorderStyle.Left = DataGridViewAdvancedCellBorderStyle.None;
            tabelaCanais.AdvancedColumnHeadersBorderStyle.Left = DataGridViewAdvancedCellBorderStyle.None;

            // Coluna CH
            tabelaCanais.Columns.Add("CH", "CH");
            tabelaCanais.Columns["CH"].Width = 50;
            tabelaCanais.Columns["CH"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            tabelaCanais.Columns["CH"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            tabelaCanais.Columns["CH"].ReadOnly = true;
            tabelaCanais.Columns["CH"].Resizable = DataGridViewTriState.False;
            tabelaCanais.Columns["CH"].DefaultCellStyle.ForeColor = Color.Gray;

            // Coluna Logo (visual)
            tabelaCanais.Columns.Add("FotoCanal", "Logo");
            tabelaCanais.Columns["FotoCanal"].Width = 80;
            tabelaCanais.Columns["FotoCanal"].ReadOnly = true;

            // Outras colunas
            tabelaCanais.Columns.Add("LogoUrl", "Imagem URL");
            tabelaCanais.Columns.Add("NomeCanal", "Nome");
            tabelaCanais.Columns.Add("EpgId", "ID do EPG");
            tabelaCanais.Columns.Add("Categoria", "Categoria");
            tabelaCanais.Columns.Add("Url", "Link (URL)");
            tabelaCanais.Columns.Add("StatusUrl", "Status");

            // Design
            tabelaCanais.RowHeadersVisible = false;
            tabelaCanais.AllowUserToAddRows = true;
            tabelaCanais.BackgroundColor = Color.White;
            tabelaCanais.EnableHeadersVisualStyles = false;
            tabelaCanais.ColumnHeadersHeight = 45;
            tabelaCanais.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(45, 45, 48);
            tabelaCanais.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            tabelaCanais.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 11, FontStyle.Bold);
            tabelaCanais.RowTemplate.Height = 60;
            tabelaCanais.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            // ----------------------------------------------------------------------------------
            // 🧠 SENSOR DE EDIÇÃO MANUAL (Para o Desfazer funcionar em nomes apagados/editados)
            // ----------------------------------------------------------------------------------
            tabelaCanais.CellBeginEdit += (s, e) =>
            {
                // No momento que você clica pra editar ou apagar, ele tira a foto do "Passado"
                historicoDoTempo.Push(TirarFotoDaTabela());
                // Limpa o futuro (refazer) porque você iniciou uma nova ação
                futuroDoTempo.Clear();
            };

            // 🎨 MOTOR DE PINTURA DA LOGO
            tabelaCanais.CellPainting += (s, e) =>
            {
                if (e.ColumnIndex >= 0 && tabelaCanais.Columns[e.ColumnIndex].Name == "FotoCanal" && e.RowIndex >= 0)
                {
                    e.PaintBackground(e.CellBounds, true);

                    if (e.Value is Image img)
                    {
                        int margem = 4;
                        Rectangle rect = e.CellBounds;
                        float ratio = Math.Min((float)(rect.Width - margem) / img.Width, (float)(rect.Height - margem) / img.Height);
                        int novaLargura = (int)(img.Width * ratio);
                        int novaAltura = (int)(img.Height * ratio);
                        int x = rect.X + (rect.Width - novaLargura) / 2;
                        int y = rect.Y + (rect.Height - novaAltura) / 2;

                        e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        e.Graphics.DrawImage(img, new Rectangle(x, y, novaLargura, novaAltura));
                    }
                    e.Handled = true;
                }
            };

            tabelaCanais.RowsAdded += (s, e) => RenumerarCanais();
            tabelaCanais.RowsRemoved += (s, e) => RenumerarCanais();
        }

        private void RenumerarCanais()
        {
            for (int i = 0; i < tabelaCanais.Rows.Count; i++)
                if (!tabelaCanais.Rows[i].IsNewRow)
                    tabelaCanais.Rows[i].Cells["CH"].Value = (i + 1).ToString();
        }

        // =====================================================================
        // MÉTODO CENTRAL DE PARSING M3U — evita código duplicado em 4 lugares
        // =====================================================================
        private static List<string[]> ParsearM3U(string[] linhasArquivo)
        {
            var resultado = new List<string[]>();
            string logo = "", nome = "", epg = "", categoria = "Sem Categoria";

            foreach (string linha in linhasArquivo)
            {
                string l = linha.Trim();
                if (string.IsNullOrEmpty(l)) continue;

                if (l.StartsWith("#EXTINF"))
                {
                    logo = ""; epg = ""; categoria = "Sem Categoria";

                    int idxNome = l.LastIndexOf(',');
                    if (idxNome != -1) nome = l.Substring(idxNome + 1).Trim();

                    var mLogo = System.Text.RegularExpressions.Regex.Match(l, @"tvg-logo=""(.*?)""");
                    if (mLogo.Success) logo = mLogo.Groups[1].Value;

                    var mEpg = System.Text.RegularExpressions.Regex.Match(l, @"tvg-id=""(.*?)""");
                    if (mEpg.Success) epg = mEpg.Groups[1].Value;

                    var mCat = System.Text.RegularExpressions.Regex.Match(l, @"group-title=""(.*?)""");
                    if (mCat.Success) categoria = mCat.Groups[1].Value;
                }
                else if (!l.StartsWith("#"))
                {
                    resultado.Add(new string[] { logo, nome, epg, categoria, l });
                    nome = "";
                }
            }
            return resultado;
        }

        // Adiciona lista de canais parsados à tabela
        private void AdicionarCanaisNaTabela(List<string[]> canais,
            Action<int, int, int> progressCallback = null)
        {
            var linhasParaAdicionar = new List<DataGridViewRow>();
            for (int i = 0; i < canais.Count; i++)
            {
                var c = canais[i];
                DataGridViewRow novaLinha = new DataGridViewRow();
                novaLinha.CreateCells(tabelaCanais);
                novaLinha.Height = 60;
                if (tabelaCanais.Columns.Contains("StatusUrl")) novaLinha.Cells[tabelaCanais.Columns["StatusUrl"].Index].Value = "";
                if (tabelaCanais.Columns.Contains("LogoUrl")) novaLinha.Cells[tabelaCanais.Columns["LogoUrl"].Index].Value = c[0];
                if (tabelaCanais.Columns.Contains("NomeCanal")) novaLinha.Cells[tabelaCanais.Columns["NomeCanal"].Index].Value = c[1];
                if (tabelaCanais.Columns.Contains("EpgId")) novaLinha.Cells[tabelaCanais.Columns["EpgId"].Index].Value = c[2];
                if (tabelaCanais.Columns.Contains("Categoria")) novaLinha.Cells[tabelaCanais.Columns["Categoria"].Index].Value = c[3];
                if (tabelaCanais.Columns.Contains("Url")) novaLinha.Cells[tabelaCanais.Columns["Url"].Index].Value = c[4];
                linhasParaAdicionar.Add(novaLinha);
            }
            tabelaCanais.Rows.AddRange(linhasParaAdicionar.ToArray());
        }

        // Abre lista de arquivo local
        // Abre lista de arquivo local
        private void button1_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog() { Filter = "Listas IPTV|*.m3u;*.m3u8", Title = ObterTraducao("Selecione sua Lista") };
            if (ofd.ShowDialog() != DialogResult.OK) return;

            // ✨ NOVIDADE: Salva o nome do arquivo na nossa variável global
            nomeArquivoAtual = System.IO.Path.GetFileName(ofd.FileName);

            // CHAMA A FAXINA AQUI 👇
            LimparMemoriaParaNovaLista();

            // ✨ NOVIDADE: Atualiza o painel dizendo que está começando o trabalho
            AtualizarStatus("Lendo arquivo...");

            Form telaLoad = new Form() { Width = 550, Height = 170, StartPosition = FormStartPosition.CenterScreen, FormBorderStyle = FormBorderStyle.None, BackColor = Color.WhiteSmoke, TopMost = true };
            Label lblAviso = new Label() { Left = 20, Top = 30, Width = 510, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 12, FontStyle.Bold), Text = ObterTraducao("⏳ Lendo arquivo e processando canais...") };
            Label lblPorcentagem = new Label() { Left = 20, Top = 70, Width = 510, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 10), Text = ObterTraducao("Preparando a leitura...") };
            ProgressBar barra = new ProgressBar() { Left = 30, Top = 110, Width = 490, Height = 20, Style = ProgressBarStyle.Continuous };
            telaLoad.Controls.Add(lblAviso); telaLoad.Controls.Add(lblPorcentagem); telaLoad.Controls.Add(barra);
            TraduzirTelaDinamica(telaLoad);
            telaLoad.Show(); Application.DoEvents();

            tabelaCanais.SuspendLayout();
            tabelaCanais.Rows.Clear();

            foreach (DataGridViewColumn col in tabelaCanais.Columns)
                if (col is DataGridViewImageColumn imgCol) imgCol.DefaultCellStyle.NullValue = new Bitmap(1, 1);

            var modoAntigo = tabelaCanais.AutoSizeColumnsMode;
            tabelaCanais.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

            string[] linhasArquivo = File.ReadAllLines(ofd.FileName);

            epgGlobalAtual = "";
            if (linhasArquivo.Length > 0 && linhasArquivo[0].ToUpper().StartsWith("#EXTM3U"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(linhasArquivo[0], @"(?:url-tvg|x-tvg-url)=""([^""]+)""");
                if (match.Success) epgGlobalAtual = match.Groups[1].Value;
            }

            // Conta canais e grupos para o progresso (passagem rápida antes do parse)
            int canaisEncontrados = 0;
            var gruposUnicos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            barra.Maximum = linhasArquivo.Length;

            for (int i = 0; i < linhasArquivo.Length; i++)
            {
                if (i % 500 == 0)
                {
                    barra.Value = i;
                    lblPorcentagem.Text = ObterTraducao($"📺 Canais: {canaisEncontrados:N0}  |  📁 Grupos: {gruposUnicos.Count:N0}  |  Linha: {i:N0}/{linhasArquivo.Length:N0}");
                    Application.DoEvents();
                }
                string l = linhasArquivo[i].Trim();
                if (l.StartsWith("#EXTINF"))
                {
                    var mCat = System.Text.RegularExpressions.Regex.Match(l, @"group-title=""(.*?)""");
                    if (mCat.Success && !string.IsNullOrWhiteSpace(mCat.Groups[1].Value))
                        gruposUnicos.Add(mCat.Groups[1].Value);
                }
                else if (!string.IsNullOrEmpty(l) && !l.StartsWith("#"))
                    canaisEncontrados++;
            }

            lblAviso.Text = ObterTraducao("🚀 Despejando canais na tela...");
            lblPorcentagem.Text = ObterTraducao($"Finalizando: {canaisEncontrados:N0} canais e {gruposUnicos.Count:N0} grupos lidos.");
            Application.DoEvents();

            var canais = ParsearM3U(linhasArquivo);
            AdicionarCanaisNaTabela(canais);

            tabelaCanais.AutoSizeColumnsMode = modoAntigo;
            tabelaCanais.ResumeLayout();
            barra.Value = linhasArquivo.Length;
            telaLoad.Close();
            BaixarImagensInvisivelmente();

            // Salva o primeiro backup do Desfazer logo que a lista abre!
            historicoDoTempo.Clear();
            futuroDoTempo.Clear();
            historicoDoTempo.Push(TirarFotoDaTabela());

            // ✨ NOVIDADE: Atualiza o painel inferior com o nome da lista e a quantidade final
            AtualizarStatus("Lista carregada com sucesso!");

            // ✨ NOVIDADE: Atualiza o título da MessageBox com a versão nova
            MessageBox.Show(ObterTraducao($"Carregamento Concluído!\n{canais.Count:N0} canais distribuídos em {gruposUnicos.Count:N0} grupos."), ObterTraducao("VioFlow"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ✨ MOTOR INTELIGENTE V3: Anti-Bloqueio e Otimizado
        private async void BaixarImagensInvisivelmente()
        {
            if (tabelaCanais.RowCount == 0) return;

            int primeiraLinha = tabelaCanais.FirstDisplayedScrollingRowIndex;
            if (primeiraLinha < 0) return;

            int linhasVisiveis = tabelaCanais.DisplayedRowCount(true);

            for (int i = primeiraLinha; i < primeiraLinha + linhasVisiveis; i++)
            {
                if (i >= tabelaCanais.RowCount) break;

                var linha = tabelaCanais.Rows[i];
                string urlDaFoto = linha.Cells["LogoUrl"].Value?.ToString() ?? "";

                if (string.IsNullOrEmpty(urlDaFoto) || !urlDaFoto.StartsWith("http")) continue;

                // SE A CÉLULA JÁ TEM A IMAGEM, PULA! (Deixa o programa 10x mais rápido)
                if (linha.Cells["FotoCanal"].Value != null && linha.Cells["FotoCanal"].Value is Image) continue;

                if (cacheDeLogos.ContainsKey(urlDaFoto))
                {
                    linha.Cells["FotoCanal"].Value = cacheDeLogos[urlDaFoto].Imagem;
                    continue;
                }

                if (downloadsEmAndamento.Contains(urlDaFoto)) continue;

                downloadsEmAndamento.Add(urlDaFoto);

                try
                {
                    if (cacheDeLogos.Count >= LIMITE_CACHE_LOGOS)
                    {
                        var chaveAntiga = cacheDeLogos.Keys.First();
                        cacheDeLogos.Remove(chaveAntiga);
                    }

                    using (var clienteFoto = new System.Net.Http.HttpClient())
                    {
                        clienteFoto.Timeout = TimeSpan.FromSeconds(5);
                        // 🕵️‍♂️ DISFARCE ATIVADO: Finge que somos o navegador Google Chrome
                        clienteFoto.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/122.0.0.0");

                        byte[] dados = await clienteFoto.GetByteArrayAsync(urlDaFoto);
                        MemoryStream ms = new MemoryStream(dados);
                        Image img = Image.FromStream(ms);

                        cacheDeLogos[urlDaFoto] = (img, ms);

                        int linhaAtual = tabelaCanais.FirstDisplayedScrollingRowIndex;
                        if (linhaAtual >= 0)
                        {
                            int qtdVisivel = tabelaCanais.DisplayedRowCount(true);
                            for (int j = linhaAtual; j < linhaAtual + qtdVisivel; j++)
                            {
                                if (j < tabelaCanais.RowCount && tabelaCanais.Rows[j].Cells["LogoUrl"].Value?.ToString() == urlDaFoto)
                                {
                                    tabelaCanais.Rows[j].Cells["FotoCanal"].Value = img;
                                }
                            }
                        }
                    }
                }
                catch { /* Servidor realmente fora do ar, ignora */ }
                finally
                {
                    downloadsEmAndamento.Remove(urlDaFoto);
                }
            }
        }

        private void btnSalvarLista_Click(object sender, EventArgs e)
        {
            if (tabelaCanais.Rows.Count <= 1)
            {
                MessageBox.Show(ObterTraducao("A lista está vazia! Abra uma lista primeiro."), ObterTraducao("Aviso"));
                return;
            }

            int escolhaOrdem = 0;

            using (Form fSave = new Form() { Width = 400, Height = 230, Text = "💾 Salvar Lista", StartPosition = FormStartPosition.CenterScreen, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false })
            {
                Label lbl = new Label() { Left = 20, Top = 20, Width = 350, Text = "Deseja salvar os canais:", Font = new Font("Segoe UI", 11, FontStyle.Bold) };
                Button btnTela = new Button() { Left = 20, Top = 60, Width = 340, Height = 45, Text = "🖥️ Na ordem que está na tela", BackColor = Color.LightSkyBlue, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
                btnTela.Click += (s, ev) => { escolhaOrdem = 1; fSave.Close(); };

                // ✨ CORREÇÃO: Tirei o "do arquivo" para bater 100% com a chave do dicionário!
                Button btnOrig = new Button() { Left = 20, Top = 115, Width = 340, Height = 45, Text = "📄 Na ordem original", BackColor = Color.LightGreen, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
                btnOrig.Click += (s, ev) => { escolhaOrdem = 2; fSave.Close(); };

                fSave.Controls.Add(lbl); fSave.Controls.Add(btnTela); fSave.Controls.Add(btnOrig);
                TraduzirTelaDinamica(fSave);
                fSave.ShowDialog();
            }

            if (escolhaOrdem == 0) return;

            var canaisOrganizados = tabelaCanais.Rows
                .Cast<DataGridViewRow>()
                .Where(r => !r.IsNewRow)
                .ToList();

            if (escolhaOrdem == 2)
            {
                canaisOrganizados.Sort((a, b) =>
                {
                    int idA = a.Tag != null ? Convert.ToInt32(a.Tag) : 999999;
                    int idB = b.Tag != null ? Convert.ToInt32(b.Tag) : 999999;
                    return idA.CompareTo(idB);
                });
            }

            // ✨ CORREÇÃO: Título da janela do Windows sendo traduzido
            SaveFileDialog meuSalvador = new SaveFileDialog() { Filter = "Lista IPTV (*.m3u)|*.m3u|Lista IPTV (*.m3u8)|*.m3u8", Title = ObterTraducao("Salvar Lista Exportada"), FileName = "Lista_Exportada.m3u" };
            if (meuSalvador.ShowDialog() != DialogResult.OK) return;

            var linhasParaSalvar = new List<string>();

            // ==========================================
            // 🌟 CORREÇÃO DO BUG DO CABEÇALHO EPG
            // ==========================================
            string cabecalho = "#EXTM3U";

            string epgFinal = !string.IsNullOrWhiteSpace(epgGlobalAtual) ? epgGlobalAtual : epgsGlobais;

            if (!string.IsNullOrWhiteSpace(epgFinal))
            {
                cabecalho += $" x-tvg-url=\"{epgFinal}\"";
            }

            linhasParaSalvar.Add(cabecalho);
            linhasParaSalvar.Add("# ==========================================");
            linhasParaSalvar.Add("# ✨ Lista Editada e Otimizada com VioFlow ✨");
            linhasParaSalvar.Add("# ==========================================");

            foreach (DataGridViewRow linha in canaisOrganizados)
            {
                string logoUrl = linha.Cells["LogoUrl"].Value?.ToString() ?? "";

                // ✨ CORREÇÃO: Traduzindo canais sem nome e sem categoria
                string nome = linha.Cells["NomeCanal"].Value?.ToString() ?? ObterTraducao("Canal Sem Nome");
                string epgId = linha.Cells["EpgId"].Value?.ToString() ?? "";
                string categoria = linha.Cells["Categoria"].Value?.ToString() ?? ObterTraducao("Sem Categoria");
                string url = linha.Cells["Url"].Value?.ToString() ?? "";

                linhasParaSalvar.Add($"#EXTINF:-1 tvg-id=\"{epgId}\" tvg-logo=\"{logoUrl}\" group-title=\"{categoria}\",{nome}");
                linhasParaSalvar.Add(url);
            }

            try
            {
                File.WriteAllLines(meuSalvador.FileName, linhasParaSalvar, Encoding.UTF8);
                MessageBox.Show(ObterTraducao("Lista exportada com sucesso!\nO cabeçalho EPG e as edições foram preservados."), ObterTraducao("VioFlow - Salvar"));
            }
            catch (Exception ex)
            {
                // ✨ CORREÇÃO: Erro isolado da variável
                MessageBox.Show(ObterTraducao("Erro ao salvar: ") + ex.Message);
            }
        }

        private string ultimoTermoBusca = "";

        private async void txtPesquisa_TextChanged(object sender, EventArgs e)
        {
            string termoBusca = txtPesquisa.Text.Trim();
            idDaBusca++;
            int buscaAtual = idDaBusca;

            await Task.Delay(400);
            if (buscaAtual != idDaBusca) return;
            if (string.IsNullOrEmpty(termoBusca)) return;

            if (termoBusca != ultimoTermoBusca)
            {
                ultimoTermoBusca = termoBusca;
                ExecutarBusca(termoBusca, false);
            }
        }

        private void txtPesquisa_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                ExecutarBusca(txtPesquisa.Text.Trim(), true);
            }
        }

        private void ExecutarBusca(string termoBusca, bool buscarProximo)
        {
            if (string.IsNullOrEmpty(termoBusca)) return;

            int inicio = 0;
            if (tabelaCanais.CurrentCell != null)
            {
                inicio = tabelaCanais.CurrentCell.RowIndex;
                if (buscarProximo) inicio++;
            }

            for (int i = inicio; i < tabelaCanais.Rows.Count; i++)
                if (ChecarCanal(i, termoBusca)) return;

            if (buscarProximo)
            {
                for (int i = 0; i < inicio - 1 && i < tabelaCanais.Rows.Count; i++)
                {
                    if (ChecarCanal(i, termoBusca))
                    {
                        lblStatus.Text = ObterTraducao($"🔁 Fim da lista! A busca recomeçou do topo. (Linha {i + 1})");
                        return;
                    }
                }
                lblStatus.Text = ObterTraducao("❌ Não há mais resultados para esta palavra.");
            }
            else
            {
                lblStatus.Text = ObterTraducao("❌ Canal não encontrado na lista.");
            }
        }

        private bool ChecarCanal(int i, string termoBusca)
        {
            DataGridViewRow linha = tabelaCanais.Rows[i];
            if (linha.IsNewRow) return false;

            string nome = linha.Cells["NomeCanal"].Value as string ?? "";
            string categoria = linha.Cells["Categoria"].Value as string ?? "";

            if (nome.IndexOf(termoBusca, StringComparison.OrdinalIgnoreCase) >= 0 ||
                categoria.IndexOf(termoBusca, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                tabelaCanais.ClearSelection();
                linha.Selected = true;
                tabelaCanais.CurrentCell = linha.Cells["NomeCanal"];
                tabelaCanais.FirstDisplayedScrollingRowIndex = linha.Index;
                lblStatus.Text = ObterTraducao($"✅ Canal encontrado na linha {linha.Index + 1}");
                return true;
            }
            return false;
        }

        /// <summary>
        /// Motor V8: Reconstrói a tabela em lotes de 5000 para não travar a tela.
        /// </summary>
        private async Task ReconstruirTabelaTurbo(List<string[]> dados, ProgressBar barra = null, Label lblStatus = null)
        {
            int idxStatus = tabelaCanais.Columns["StatusUrl"].Index;
            int idxLogoUrl = tabelaCanais.Columns["LogoUrl"].Index;
            int idxNome = tabelaCanais.Columns["NomeCanal"].Index;
            int idxEpg = tabelaCanais.Columns["EpgId"].Index;
            int idxCat = tabelaCanais.Columns["Categoria"].Index;
            int idxUrl = tabelaCanais.Columns["Url"].Index;
            int idxFoto = tabelaCanais.Columns["FotoCanal"].Index;

            tabelaCanais.SuspendLayout();
            tabelaCanais.Rows.Clear();

            var modoAntigo = tabelaCanais.AutoSizeColumnsMode;
            tabelaCanais.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

            const int LOTE = 5000;
            int total = dados.Count;
            int processados = 0;

            while (processados < total)
            {
                int fim = Math.Min(processados + LOTE, total);
                int tamanhoLote = fim - processados;
                var lote = new DataGridViewRow[tamanhoLote];

                await Task.Run(() =>
                {
                    for (int i = 0; i < tamanhoLote; i++)
                    {
                        string[] d = dados[processados + i];
                        var row = new DataGridViewRow();
                        row.CreateCells(tabelaCanais);
                        row.Height = 60;

                        row.Cells[idxStatus].Value = d[0];
                        row.Cells[idxLogoUrl].Value = d[1];
                        row.Cells[idxNome].Value = d[2];
                        row.Cells[idxEpg].Value = d[3];
                        row.Cells[idxCat].Value = d[4];
                        row.Cells[idxUrl].Value = d[5];

                        string urlLogo = d[1];
                        if (!string.IsNullOrEmpty(urlLogo) && cacheDeLogos.ContainsKey(urlLogo))
                            row.Cells[idxFoto].Value = cacheDeLogos[urlLogo].Imagem;

                        lote[i] = row;
                    }
                });

                tabelaCanais.Rows.AddRange(lote);
                processados = fim;

                if (barra != null)
                    barra.Value = Math.Min((int)((double)processados / total * barra.Maximum), barra.Maximum);
                if (lblStatus != null)
                    lblStatus.Text = $"Desenhando: {processados:N0} de {total:N0}...";

                await Task.Delay(1);
                Application.DoEvents();
            }

            tabelaCanais.AutoSizeColumnsMode = modoAntigo;
            tabelaCanais.ResumeLayout();
            tabelaCanais.Invalidate();
            BaixarImagensInvisivelmente();
        }
        private async void btnApagar_Click(object sender, EventArgs e)
        {
            var categoriasUnicas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DataGridViewRow linha in tabelaCanais.Rows)
                if (!linha.IsNewRow)
                    // ✨ CORREÇÃO: "Sem Categoria" envelopado
                    categoriasUnicas.Add(linha.Cells["Categoria"].Value?.ToString() ?? ObterTraducao("Sem Categoria"));

            if (categoriasUnicas.Count == 0) return;

            var listaEscolhida = MostrarJanelaDeCategorias(categoriasUnicas);
            if (listaEscolhida.Count == 0) return;

            var categoriasParaApagar = new HashSet<string>(listaEscolhida, StringComparer.OrdinalIgnoreCase);

            SalvarBackupDoTempo();

            var fotoOriginal = TirarFotoDaTabela();
            int totalCanais = fotoOriginal.Count;

            Form telaLoad = new Form() { Width = 450, Height = 140, StartPosition = FormStartPosition.CenterScreen, FormBorderStyle = FormBorderStyle.None, BackColor = Color.FromArgb(35, 35, 38), TopMost = true };

            // ✨ CORREÇÕES: Envelopando os textos do Splash de Carregamento
            Label lblAviso = new Label() { Left = 20, Top = 20, Width = 410, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 11, FontStyle.Bold), Text = ObterTraducao("⚡ VioFlow Turbo: Filtrando Canais...") };
            Label lblContagem = new Label() { Left = 20, Top = 55, Width = 410, ForeColor = Color.LightGray, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 9), Text = ObterTraducao($"Preparando {totalCanais:N0} canais...") };

            ProgressBar barra = new ProgressBar() { Left = 30, Top = 90, Width = 390, Height = 20, Style = ProgressBarStyle.Continuous, Maximum = Math.Max(1, totalCanais), Value = 0 };
            telaLoad.Controls.Add(lblAviso); telaLoad.Controls.Add(lblContagem); telaLoad.Controls.Add(barra);
            telaLoad.Show(); Application.DoEvents();

            var fotoFiltrada = new List<string[]>(totalCanais);
            int apagados = 0;

            await Task.Run(() =>
            {
                for (int i = 0; i < totalCanais; i++)
                {
                    var dados = fotoOriginal[i];

                    if (!categoriasParaApagar.Contains(dados[4]))
                        fotoFiltrada.Add(dados);
                    else
                        apagados++;

                    if (i % 5000 == 0 || i == totalCanais - 1)
                    {
                        int progresso = i + 1;
                        telaLoad.Invoke(new Action(() =>
                        {
                            barra.Value = Math.Min(progresso, barra.Maximum);
                            lblContagem.Text = ObterTraducao($"Filtrando: {progresso:N0} de {totalCanais:N0}  |  🗑️ Para apagar: {apagados:N0}");
                        }));
                    }
                }
            });

            lblAviso.Text = ObterTraducao("🚀 Reconstruindo tabela...");
            lblContagem.Text = ObterTraducao($"Desenhando {fotoFiltrada.Count:N0} canais restantes...");
            Application.DoEvents();

            // CHAMA O MOTOR NOVO AQUI
            await ReconstruirTabelaTurbo(fotoFiltrada, barra, lblContagem);

            telaLoad.Close();
            telaLoad.Dispose();

            AtualizarStatus();

            // ✨ CORREÇÃO: Título "VioFlow 1.0.0" envelopado pro caso de mudar no futuro, e texto principal ajustado.
            MessageBox.Show(ObterTraducao($"Faxina Turbo Concluída!\n\n🗑️ {apagados:N0} canais removidos.\n✅ {fotoFiltrada.Count:N0} canais mantidos."), ObterTraducao("VioFlow 1.3"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }



        private void btnSubir_Click(object sender, EventArgs e)
        {
            SalvarBackupDoTempo();
            if (tabelaCanais.SelectedRows.Count == 0) return;

            int pos = tabelaCanais.SelectedRows[0].Index;
            if (pos > 0)
            {
                DataGridViewRow linha = tabelaCanais.Rows[pos];
                tabelaCanais.Rows.Remove(linha);
                tabelaCanais.Rows.Insert(pos - 1, linha);
                tabelaCanais.ClearSelection();
                tabelaCanais.CurrentCell = tabelaCanais.Rows[pos - 1].Cells[2];
                tabelaCanais.Rows[pos - 1].Selected = true;
            }
        }

        private void btnDescer_Click(object sender, EventArgs e)
        {
            SalvarBackupDoTempo();
            if (tabelaCanais.SelectedRows.Count == 0) return;

            int pos = tabelaCanais.SelectedRows[0].Index;
            if (pos < tabelaCanais.Rows.Count - 2)
            {
                DataGridViewRow linha = tabelaCanais.Rows[pos];
                tabelaCanais.Rows.Remove(linha);
                tabelaCanais.Rows.Insert(pos + 1, linha);
                tabelaCanais.ClearSelection();
                tabelaCanais.CurrentCell = tabelaCanais.Rows[pos + 1].Cells[2];
                tabelaCanais.Rows[pos + 1].Selected = true;
            }
        }

        private void contextMenuStrip1_Opening(object sender, System.ComponentModel.CancelEventArgs e) { }
        private void tabelaCanais_CellContentClick(object sender, DataGridViewCellEventArgs e) { }

        private void mudarCategoriaToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SalvarBackupDoTempo();

            if (tabelaCanais.SelectedRows.Count == 0)
            {
                MessageBox.Show(ObterTraducao("Selecione a linha de pelo menos um canal para renomear!"), ObterTraducao("Aviso"));
                return;
            }

            var categoriasExistentes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DataGridViewRow linha in tabelaCanais.Rows)
                if (!linha.IsNewRow && linha.Cells["Categoria"].Value != null)
                {
                    string cat = linha.Cells["Categoria"].Value.ToString().Trim();
                    if (!string.IsNullOrEmpty(cat)) categoriasExistentes.Add(cat);
                }

            // ✨ CORREÇÃO: Título da janela envelopado
            Form formNome = new Form() { Width = 400, Height = 200, Text = ObterTraducao("Renomear Múltiplas Categorias"), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false };

            // ✨ CORREÇÃO: Texto de instrução envelopado
            Label lblText = new Label() { Left = 20, Top = 20, Width = 350, Text = ObterTraducao($"Escolha ou digite a categoria para os {tabelaCanais.SelectedRows.Count} canais selecionados:") };
            ComboBox comboCategoria = new ComboBox() { Left = 20, Top = 50, Width = 340, DropDownStyle = ComboBoxStyle.DropDown };

            var listaCat = new List<string>(categoriasExistentes);
            listaCat.Sort();
            comboCategoria.Items.AddRange(listaCat.ToArray());

            // ✨ CORREÇÃO: Texto do botão envelopado
            Button btnOk = new Button() { Text = ObterTraducao("Aplicar a Todos"), Left = 130, Top = 100, Width = 120, DialogResult = DialogResult.OK, BackColor = Color.LightGreen };

            formNome.Controls.Add(lblText); formNome.Controls.Add(comboCategoria); formNome.Controls.Add(btnOk);
            formNome.AcceptButton = btnOk;

            // ✨ A MÁGICA: Traduz os móveis da janela antes dela aparecer!
            TraduzirTelaDinamica(formNome);

            if (formNome.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(comboCategoria.Text))
            {
                string novaCategoria = comboCategoria.Text.Trim();
                int cont = 0;
                foreach (DataGridViewRow linha in tabelaCanais.SelectedRows)
                {
                    if (!linha.IsNewRow)
                    {
                        linha.Cells["Categoria"].Value = novaCategoria;
                        linha.DefaultCellStyle.BackColor = Color.LightYellow;
                        if (tabelaCanais.Columns.Contains("StatusUrl")) linha.Cells["StatusUrl"].Value = ObterTraducao("⏳ Editado");
                        cont++;
                    }
                }
                MessageBox.Show(ObterTraducao($"A categoria '{novaCategoria}' foi aplicada a {cont} canais."), ObterTraducao("Sucesso"));
            }
        }

        private string MostrarJanelaDeTexto(string texto, string titulo)
        {
            Form prompt = new Form() { Width = 400, Height = 150, FormBorderStyle = FormBorderStyle.FixedDialog, Text = titulo, StartPosition = FormStartPosition.CenterScreen };
            Label textLabel = new Label() { Left = 20, Top = 20, Text = texto, Width = 350 };
            TextBox textBox = new TextBox() { Left = 20, Top = 50, Width = 340 };
            Button confirmation = new Button() { Text = "Confirmar", Left = 260, Width = 100, Top = 80, DialogResult = DialogResult.OK };
            prompt.Controls.Add(textLabel); prompt.Controls.Add(textBox); prompt.Controls.Add(confirmation);
            prompt.AcceptButton = confirmation;
            return prompt.ShowDialog() == DialogResult.OK ? textBox.Text : "";
        }

        private void excluirCanaisToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SalvarBackupDoTempo();
            if (tabelaCanais.SelectedRows.Count == 0) return;

            if (MessageBox.Show(ObterTraducao("Tem certeza que deseja excluir os canais selecionados?"), ObterTraducao("Confirmação"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                int quantidade = 0;
                foreach (DataGridViewRow linha in tabelaCanais.SelectedRows)
                    if (!linha.IsNewRow) { tabelaCanais.Rows.Remove(linha); quantidade++; }
                MessageBox.Show(ObterTraducao($"{quantidade:N0} canais foram excluídos!"), ObterTraducao("Faxina Concluída"));
            }
        }

        private void limparNomeDoCanalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SalvarBackupDoTempo();
            if (tabelaCanais.SelectedRows.Count == 0) return;

            string textoParaRemover = MostrarJanelaDeTexto(ObterTraducao("Qual texto remover dos nomes? (Ex: [FHD]):"), ObterTraducao("Limpar Nomes"));
            if (string.IsNullOrEmpty(textoParaRemover)) return;

            int quantidade = 0;
            foreach (DataGridViewRow linha in tabelaCanais.SelectedRows)
                if (!linha.IsNewRow)
                {
                    string nomeAtual = linha.Cells["NomeCanal"].Value?.ToString() ?? "";
                    linha.Cells["NomeCanal"].Value = nomeAtual.Replace(textoParaRemover, "").Trim();
                    quantidade++;
                }
            MessageBox.Show(ObterTraducao($"O texto '{textoParaRemover}' foi removido de {quantidade:N0} canais!"), ObterTraducao("Limpeza Concluída"));
        }

        private List<string> MostrarJanelaDeCategorias(HashSet<string> categorias)
        {
            var selecionadas = new List<string>();
            // ✨ CORREÇÃO: Título da janela traduzido
            Form prompt = new Form() { Width = 450, Height = 500, FormBorderStyle = FormBorderStyle.FixedDialog, Text = ObterTraducao("Faxina de Categorias"), StartPosition = FormStartPosition.CenterScreen };

            // ✨ CORREÇÃO: Texto de instrução traduzido
            Label textLabel = new Label() { Left = 20, Top = 20, Text = ObterTraducao("Marque as categorias que deseja EXCLUIR:"), Width = 400 };

            CheckedListBox chkList = new CheckedListBox() { Left = 20, Top = 50, Width = 390, Height = 340, CheckOnClick = true };
            foreach (string c in categorias) chkList.Items.Add(c);

            // ✨ CORREÇÃO: Botões traduzidos
            Button confirmation = new Button() { Text = ObterTraducao("Apagar Marcadas"), Left = 260, Width = 150, Top = 410, DialogResult = DialogResult.OK, BackColor = Color.Red, ForeColor = Color.White };
            Button cancel = new Button() { Text = ObterTraducao("Cancelar"), Left = 150, Width = 100, Top = 410, DialogResult = DialogResult.Cancel };

            prompt.Controls.Add(textLabel); prompt.Controls.Add(chkList); prompt.Controls.Add(confirmation); prompt.Controls.Add(cancel);

            // ✨ A MÁGICA: Traduz os controles da janela antes dela abrir
            TraduzirTelaDinamica(prompt);

            if (prompt.ShowDialog() == DialogResult.OK)
                foreach (var item in chkList.CheckedItems)
                    selecionadas.Add(item.ToString()!);

            return selecionadas;
        }

        private void statusStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e) { }

        private void AtualizarStatus(string acao = "")
        {
            int total = tabelaCanais.Rows.Count > 0 ? tabelaCanais.Rows.Count - 1 : 0;
            int selecionados = tabelaCanais.SelectedRows.Count;

            string[] txtArq = { "Arquivo", "File", "Archivo" };
            string[] txtTot = { "Total", "Total", "Total" };
            string[] txtSel = { "Selecionados", "Selected", "Seleccionados" };
            string[] txtAct = { "Ação", "Action", "Acción" };
            string[] txtLid = { "Pronto", "Ready", "Listo" };


            string actFinal = string.IsNullOrEmpty(acao) ? txtLid[IdiomaAtual] : ObterTraducao(acao);

            if (lblStatus != null)
                lblStatus.Text = $"📄 {txtArq[IdiomaAtual]}: {nomeArquivoAtual}   |   📺 {txtTot[IdiomaAtual]}: {total:N0} {ObterTraducao("canais")}  |   ✅ {txtSel[IdiomaAtual]}: {selecionados:N0}   |   ⚡ {txtAct[IdiomaAtual]}: {actFinal}";
        }

        private void trocarURLDoCanalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SalvarBackupDoTempo();
            if (tabelaCanais.SelectedRows.Count == 0) return;

            DataGridViewRow linha = tabelaCanais.SelectedRows[0];
            if (linha.IsNewRow) return;

            string novaUrl = MostrarJanelaDeTexto(ObterTraducao("Cole a NOVA URL completa para este canal:"), ObterTraducao("Trocar URL do Canal"));
            if (!string.IsNullOrEmpty(novaUrl))
            {
                linha.Cells["Url"].Value = novaUrl;
                MessageBox.Show(ObterTraducao("A URL do canal foi atualizada com sucesso!"), ObterTraducao("Troca Concluída"));
            }
        }

        private void btnMesclar_Click(object sender, EventArgs e)
        {
            SalvarBackupDoTempo();

            // ✨ CORREÇÃO: Título da janela do Windows
            OpenFileDialog meuBuscador = new OpenFileDialog() { Filter = "Listas IPTV|*.m3u;*.m3u8|Todos Arquivos|*.*", Title = ObterTraducao("Selecione a lista NOVA para buscar backups") };
            if (meuBuscador.ShowDialog() != DialogResult.OK) return;

            var qualidadesDesejadas = MostrarJanelaDeQualidades();
            if (qualidadesDesejadas.Count == 0) return;

            var moldeCanais = new Dictionary<string, string[]>();
            var contagemExata = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (DataGridViewRow linha in tabelaCanais.Rows)
            {
                if (linha.IsNewRow) continue;
                string nomeReal = linha.Cells["NomeCanal"].Value?.ToString() ?? "";
                if (!string.IsNullOrWhiteSpace(nomeReal))
                {
                    string nomeSimples = SimplificarNome(nomeReal);
                    if (!moldeCanais.ContainsKey(nomeSimples))
                    {
                        string logo = linha.Cells["LogoUrl"].Value?.ToString() ?? "";
                        string epg = linha.Cells["EpgId"].Value?.ToString() ?? "";

                        // ✨ CORREÇÃO: Categoria vazia
                        string cat = linha.Cells["Categoria"].Value?.ToString() ?? ObterTraducao("Sem Categoria");

                        // Guardamos o molde: [0]NomeOriginal, [1]Logo, [2]EPG, [3]Categoria
                        moldeCanais.Add(nomeSimples, new string[] { nomeReal, logo, epg, cat });
                    }
                    if (!contagemExata.ContainsKey(nomeReal)) contagemExata[nomeReal] = 0;
                }
            }

            if (moldeCanais.Count == 0) { MessageBox.Show(ObterTraducao("Sua lista principal está vazia."), ObterTraducao("Aviso")); return; }

            string[] linhas = File.ReadAllLines(meuBuscador.FileName);
            string nomeNovo = "";
            var canaisEncontrados = new List<string[]>();

            foreach (string linha in linhas)
            {
                string l = linha.Trim();
                if (string.IsNullOrEmpty(l)) continue;
                if (l.StartsWith("#EXTINF")) { int idx = l.LastIndexOf(','); if (idx != -1) nomeNovo = l.Substring(idx + 1).Trim(); }
                else if (!l.StartsWith("#") && !string.IsNullOrEmpty(nomeNovo))
                {
                    string baseNovo = SimplificarNome(nomeNovo);
                    string qualidadeNova = IdentificarQualidade(nomeNovo);
                    if (moldeCanais.ContainsKey(baseNovo) && qualidadesDesejadas.Contains(qualidadeNova))
                    {
                        string[] molde = moldeCanais[baseNovo];
                        // Montamos o pacote: [0]NomeMestre, [1]Qualidade, [2]Logo, [3]EPG, [4]Categoria, [5]LinkNovo, [6]NomeOriginalNovo
                        canaisEncontrados.Add(new string[] { molde[0], qualidadeNova, molde[1], molde[2], molde[3], l, nomeNovo });
                    }
                    nomeNovo = "";
                }
            }

            if (canaisEncontrados.Count == 0) { MessageBox.Show(ObterTraducao("Nenhum canal compatível encontrado."), ObterTraducao("Aviso")); return; }

            // ✨ CORREÇÃO: Título da Janela
            using (Form prompt = new Form() { Width = 700, Height = 550, Text = ObterTraducao("Clonagem Perfeita de Backups"), StartPosition = FormStartPosition.CenterScreen, FormBorderStyle = FormBorderStyle.FixedDialog })
            {
                // ✨ CORREÇÃO: Texto Dinâmico
                Label lbl = new Label() { Left = 20, Top = 20, Width = 650, Text = ObterTraducao($"Achamos {canaisEncontrados.Count} links! Eles herdarão a Logo, EPG e Categoria da lista principal:") };
                CheckedListBox chk = new CheckedListBox() { Left = 20, Top = 50, Width = 640, Height = 400, CheckOnClick = true };
                foreach (var c in canaisEncontrados)
                    chk.Items.Add(ObterTraducao($"[{c[1]}] Transformar '{c[6]}'  -->  em Clone '{c[0]} [Alt]'"));

                // ✨ CORREÇÃO: Botão
                Button btnOk = new Button() { Text = ObterTraducao("Clonar Selecionados"), Left = 490, Top = 465, Width = 170, DialogResult = DialogResult.OK, BackColor = Color.LightGreen };
                prompt.Controls.Add(lbl); prompt.Controls.Add(chk); prompt.Controls.Add(btnOk);

                // ✨ CORREÇÃO: A mágica ativada antes da tela abrir!
                TraduzirTelaDinamica(prompt);
                if (prompt.ShowDialog() == DialogResult.OK)
                {
                    int importados = 0;
                    tabelaCanais.SuspendLayout();
                    for (int i = 0; i < chk.Items.Count; i++)
                    {
                        if (chk.GetItemChecked(i))
                        {
                            var dados = canaisEncontrados[i];
                            string nomeMestre = dados[0];
                            contagemExata[nomeMestre]++;
                            string nomeFinal = $"{nomeMestre} [Alt{contagemExata[nomeMestre]}]";

                            tabelaCanais.Rows.Add(
                                null!,                // 0. CH (Auto)
                                null!,                // 1. FotoCanal (Visual)
                                dados[2],             // 2. LogoUrl (herdada)
                                nomeFinal,            // 3. NomeCanal (com tag Alt)
                                dados[3],             // 4. EpgId (herdado)
                                dados[4],             // 5. Categoria (herdada)
                                dados[5],             // 6. Url (Link novo da lista doadora)
                                ObterTraducao("🔗 Backup Adicionado") // ✨ CORREÇÃO: StatusUrl
                            );
                            importados++;
                        }
                    }
                    tabelaCanais.ResumeLayout();
                    BaixarImagensInvisivelmente();
                    MessageBox.Show(ObterTraducao($"{importados:N0} backups foram adicionados como clones perfeitos!"), ObterTraducao("Clonagem Concluída"));
                }
            }
        }

        private List<string> MostrarJanelaDeQualidades()
        {
            var selecionadas = new List<string>();
            Form prompt = new Form() { Width = 350, Height = 350, Text = "Filtro de Qualidade", StartPosition = FormStartPosition.CenterScreen, FormBorderStyle = FormBorderStyle.FixedDialog };
            Label lbl = new Label() { Left = 20, Top = 20, Width = 300, Text = "Quais qualidades importar como backup?" };
            CheckedListBox chk = new CheckedListBox() { Left = 20, Top = 50, Width = 290, Height = 180, CheckOnClick = true };
            chk.Items.AddRange(new string[] { "FHD", "H265", "4K", "HD", "SD", "Sem Tag" });
            Button btnOk = new Button() { Text = "Buscar Canais", Left = 180, Top = 250, Width = 130, DialogResult = DialogResult.OK };
            prompt.Controls.Add(lbl); prompt.Controls.Add(chk); prompt.Controls.Add(btnOk);
            if (prompt.ShowDialog() == DialogResult.OK)
                foreach (var item in chk.CheckedItems) selecionadas.Add(item.ToString());
            return selecionadas;
        }

        private string IdentificarQualidade(string nome)
        {
            string n = nome.ToLower();
            if (n.Contains("h265") || n.Contains("hevc")) return "H265";
            if (n.Contains("fhd") || n.Contains("full hd")) return "FHD";
            if (n.Contains("4k") || n.Contains("uhd")) return "4K";
            if (n.Contains(" hd") || n.Contains("-hd") || n.Contains("[hd]")) return "HD";
            if (n.Contains("sd")) return "SD";
            return "Sem Tag";
        }

        private string SimplificarNome(string nome)
        {
            string n = nome.ToLower().Trim();
            n = n.Replace(" rj", " rio de janeiro").Replace(" sp", " sao paulo").Replace(" mg", " minas gerais");
            n = n.Replace(" tv", "").Replace(" hd", "").Replace(" fhd", "").Replace(" 4k", "").Replace(" sd", "").Replace(" h265", "").Replace(" hevc", "");
            n = System.Text.RegularExpressions.Regex.Replace(n, "[^a-z0-9]", "");
            return n;
        }

        private string ExtrairDado(string json, string chave)
        {
            if (!json.Contains(chave)) return "";
            int inicio = json.IndexOf(chave) + chave.Length;
            int fim = json.IndexOf("\"", inicio);
            if (inicio >= chave.Length && fim > inicio) return json.Substring(inicio, fim - inicio);
            return "";
        }

        // Botão Configurar EPG
        private void button2_Click_1(object sender, EventArgs e)
        {
            Form telaEpg = new Form() { Width = 450, Height = 220, Text = ObterTraducao("Configurar URLs do EPG"), StartPosition = FormStartPosition.CenterScreen, FormBorderStyle = FormBorderStyle.FixedDialog };
            Label lbl1 = new Label() { Left = 20, Top = 20, Width = 400, Text = ObterTraducao("URL do EPG 1:") };
            TextBox txt1 = new TextBox() { Left = 20, Top = 45, Width = 390 };
            Label lbl2 = new Label() { Left = 20, Top = 80, Width = 400, Text = ObterTraducao("URL do EPG 2 (Opcional):") };
            TextBox txt2 = new TextBox() { Left = 20, Top = 105, Width = 390 };
            Button btnSalvar = new Button() { Left = 160, Top = 140, Width = 120, Text = ObterTraducao("Salvar EPG"), DialogResult = DialogResult.OK, BackColor = Color.LightGreen };

            if (epgsGlobais.Contains(",")) { var p = epgsGlobais.Split(','); txt1.Text = p[0]; txt2.Text = p[1]; }
            else txt1.Text = epgsGlobais;

            telaEpg.Controls.Add(lbl1); telaEpg.Controls.Add(txt1);
            telaEpg.Controls.Add(lbl2); telaEpg.Controls.Add(txt2);
            telaEpg.Controls.Add(btnSalvar);

            // ✨ CORREÇÃO: Aplica a tradução dinâmica na janela antes de abrir!
            TraduzirTelaDinamica(telaEpg);
            if (telaEpg.ShowDialog() == DialogResult.OK)
            {
                string link1 = txt1.Text.Trim();
                string link2 = txt2.Text.Trim();
                epgsGlobais = !string.IsNullOrEmpty(link2) ? $"{link1},{link2}" : link1;
                MessageBox.Show(ObterTraducao("Links salvos! O VioFlow vai baixar os guias e preencher os IDs."), ObterTraducao("VioFlow EPG"));
                CarregarEpgNaMemoria(link1, link2);
            }
        }

        private async void CarregarEpgNaMemoria(string url1, string url2)
        {
            memoriaEpg.Clear();
            if (lblStatus != null) lblStatus.Text = ObterTraducao("⏳ Baixando lista de EPG da internet... Aguarde.");

            try
            {
                using (HttpClient cliente = new HttpClient())
                {
                    cliente.Timeout = TimeSpan.FromMinutes(3);

                    async Task BaixarEExtrair(string url)
                    {
                        if (string.IsNullOrWhiteSpace(url)) return;
                        try
                        {
                            string xml = await cliente.GetStringAsync(url);
                            string[] blocos = xml.Split(new string[] { "<channel id=\"" }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (string bloco in blocos)
                            {
                                int aspaFinal = bloco.IndexOf("\"");
                                if (aspaFinal > 0)
                                {
                                    string id = bloco.Substring(0, aspaFinal);
                                    int inicioNome = bloco.IndexOf("<display-name");
                                    if (inicioNome > 0)
                                    {
                                        int inicioNomeReal = bloco.IndexOf(">", inicioNome) + 1;
                                        int fimNomeReal = bloco.IndexOf("</display-name>", inicioNomeReal);
                                        if (fimNomeReal > inicioNomeReal)
                                        {
                                            string nome = bloco.Substring(inicioNomeReal, fimNomeReal - inicioNomeReal).Trim();
                                            if (!memoriaEpg.ContainsKey(id)) memoriaEpg.Add(id, nome);
                                        }
                                    }
                                }
                            }
                        }
                        catch { }
                    }

                    await BaixarEExtrair(url1);
                    await BaixarEExtrair(url2);
                }

                if (lblStatus != null) lblStatus.Text = ObterTraducao($"✅ EPG Carregado! {memoriaEpg.Count:N0} canais disponíveis.");
                MessageBox.Show(ObterTraducao($"Download concluído! O VioFlow encontrou {memoriaEpg.Count:N0} opções de EPG.\n\nSelecione os canais, clique com o BOTÃO DIREITO e escolha 'Definir EPG Manualmente'!"), ObterTraducao("Memória EPG Pronta"));
            }
            catch (Exception ex) { MessageBox.Show(ObterTraducao("Erro: ") + ex.Message); }
        }

        private void definirEPGManualmenteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SalvarBackupDoTempo();
            if (tabelaCanais.SelectedRows.Count == 0) return;
            if (memoriaEpg.Count == 0) { MessageBox.Show(ObterTraducao("Nenhum EPG carregado! Vá em 'Configurar EPG' primeiro."), ObterTraducao("Aviso")); return; }

            Form janelaMapa = new Form() { Width = 500, Height = 500, Text = ObterTraducao("Mapeamento Manual de EPG"), StartPosition = FormStartPosition.CenterScreen, FormBorderStyle = FormBorderStyle.FixedDialog };
            Label lbl = new Label() { Left = 20, Top = 20, Width = 450, Text = ObterTraducao("Pesquise e clique na opção correta:") };
            TextBox txtBusca = new TextBox() { Left = 20, Top = 50, Width = 440 };
            ListBox listaOpcoes = new ListBox() { Left = 20, Top = 80, Width = 440, Height = 320 };
            Button btnAplicar = new Button() { Text = ObterTraducao("Aplicar Selecionado"), Left = 320, Top = 415, Width = 140, DialogResult = DialogResult.OK, BackColor = Color.LightGreen };

            Action atualizarLista = () =>
            {
                listaOpcoes.Items.Clear();
                string termo = txtBusca.Text.ToLower();
                foreach (var item in memoriaEpg)
                    if (item.Value.ToLower().Contains(termo) || item.Key.ToLower().Contains(termo))
                        listaOpcoes.Items.Add($"[{item.Key}] {item.Value}");
            };

            atualizarLista();
            txtBusca.TextChanged += (s, ev) => atualizarLista();
            janelaMapa.Controls.Add(lbl); janelaMapa.Controls.Add(txtBusca);
            janelaMapa.Controls.Add(listaOpcoes); janelaMapa.Controls.Add(btnAplicar);

            // ✨ CORREÇÃO: Aplica a tradução dinâmica na janela de pesquisa!
            TraduzirTelaDinamica(janelaMapa);
            if (janelaMapa.ShowDialog() == DialogResult.OK && listaOpcoes.SelectedItem != null)
            {
                string selecionado = listaOpcoes.SelectedItem.ToString() ?? "";
                int fimChave = selecionado.IndexOf("]");
                if (fimChave > 0)
                {
                    string idEpg = selecionado.Substring(1, fimChave - 1);
                    int cont = 0;
                    foreach (DataGridViewRow linha in tabelaCanais.SelectedRows)
                        if (!linha.IsNewRow) { linha.Cells["EpgId"].Value = idEpg; cont++; }
                    MessageBox.Show(ObterTraducao($"O EPG '{idEpg}' foi aplicado a {cont:N0} canais!"), ObterTraducao("Mapeamento Concluído"));
                }
            }
        }
        // 🔥 MOTOR DE VALIDAÇÃO PROFISSIONAL (Anti-Falsos Positivos)
        private async System.Threading.Tasks.Task<bool> TestarCanalReal(string url, System.Net.Http.HttpClient cliente, System.Threading.CancellationToken token)
        {
            try
            {
                using (var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, url))
                {
                    // Pede ao servidor apenas os primeiros 1024 bytes (Economiza internet e não trava o PC)
                    request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 1024);

                    using (var response = await cliente.SendAsync(request, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, token))
                    {
                        // Se der erro de cara (404, 500, etc), ou não for conteúdo parcial/sucesso, tá OFF
                        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.PartialContent)
                            return false;

                        // 1. Verifica o tipo de conteúdo real que o servidor enviou
                        var contentType = response.Content.Headers.ContentType?.MediaType?.ToLower() ?? "";

                        // 🌟 MELHORIA MONSTRA: Validação de playlist M3U8
                        if (url.Contains(".m3u8") || contentType.Contains("mpegurl") || contentType.Contains("application/vnd.apple.mpegurl"))
                        {
                            string textoM3u8 = await response.Content.ReadAsStringAsync();
                            return textoM3u8.Contains("#EXTM3U"); // Se não tiver essa tag, é uma m3u8 falsa/quebrada
                        }

                        // 2. Se for link direto (TS, MP4, MKV), verifica se é vídeo/stream
                        if (!contentType.Contains("video") &&
                            !contentType.Contains("mpeg") &&
                            !contentType.Contains("stream") &&
                            !contentType.Contains("octet"))
                        {
                            return false;
                        }

                        // 3. Lê os primeiros bytes para garantir que não é um HTML bloqueado disfarçado de vídeo
                        using (var stream = await response.Content.ReadAsStreamAsync())
                        {
                            byte[] buffer = new byte[512];
                            int lidos = await stream.ReadAsync(buffer, 0, buffer.Length, token);

                            if (lidos < 50) return false;

                            string texto = System.Text.Encoding.UTF8.GetString(buffer).ToLower();

                            // Detecta páginas fakes de provedor de internet bloqueando IPTV
                            if (texto.Contains("<html") || texto.Contains("!doctype html") || texto.Contains("error 404") || texto.Contains("forbidden"))
                                return false;
                        }

                        return true; // Passou em todos os testes, é vídeo de verdade!
                    }
                }
            }
            catch
            {
                return false; // Deu timeout ou erro de conexão
            }
        }

        private async void button3_Click(object sender, EventArgs e)
        {
            if (tabelaCanais.Rows.Count <= 1) return;

            System.Threading.CancellationTokenSource cancelador = new System.Threading.CancellationTokenSource();

            // Interface Premium do VioFlow
            Form formProgresso = new Form()
            {
                Width = 400,
                Height = 300,
                Text = ObterTraducao("Radar VioFlow (Pro)"), // ✨ ENVELOPADO
                StartPosition = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                ControlBox = false,
                BackColor = Color.FromArgb(35, 35, 38)
            };

            // ✨ ENVELOPADOS
            Label lblAviso = new Label() { Left = 20, Top = 15, Width = 350, ForeColor = Color.White, Font = new Font("Segoe UI", 10, FontStyle.Bold), Text = ObterTraducao("📡 Análise Profunda de Stream. Aguarde...") };
            Label lblTotal = new Label() { Left = 20, Top = 50, Width = 350, ForeColor = Color.LightGray, Font = new Font("Segoe UI", 9), Text = ObterTraducao("Total de canais: 0") };
            Label lblFalta = new Label() { Left = 20, Top = 80, Width = 350, ForeColor = Color.LightGray, Font = new Font("Segoe UI", 9), Text = ObterTraducao("Falta verificar: 0") };
            Label lblOn = new Label() { Left = 20, Top = 110, Width = 350, ForeColor = Color.LimeGreen, Font = new Font("Segoe UI", 11, FontStyle.Bold), Text = ObterTraducao("🟢 Canais ON: 0") };
            Label lblOff = new Label() { Left = 20, Top = 140, Width = 350, ForeColor = Color.Tomato, Font = new Font("Segoe UI", 11, FontStyle.Bold), Text = ObterTraducao("🔴 Canais OFF: 0") };
            Button btnCancelar = new Button() { Left = 125, Top = 185, Width = 150, Height = 35, Text = ObterTraducao("⛔ Cancelar Teste"), BackColor = Color.IndianRed, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold) };

            btnCancelar.Click += (s, ev) =>
            {
                cancelador.Cancel();
                btnCancelar.Text = ObterTraducao("Cancelando..."); // ✨ ENVELOPADO
                btnCancelar.Enabled = false;
                btnCancelar.BackColor = Color.Gray;
            };

            formProgresso.Controls.Add(lblAviso); formProgresso.Controls.Add(lblTotal);
            formProgresso.Controls.Add(lblFalta); formProgresso.Controls.Add(lblOn);
            formProgresso.Controls.Add(lblOff); formProgresso.Controls.Add(btnCancelar);

            // ✨ A MÁGICA ANTES DA TELA ABRIR!
            TraduzirTelaDinamica(formProgresso);
            formProgresso.Show();
            Application.DoEvents();

            int total = tabelaCanais.Rows.Count - 1;
            int onCount = 0; int offCount = 0; int processados = 0;

            lblTotal.Text = ObterTraducao($"Total de canais: {total:N0}");

            System.Net.Http.HttpClientHandler manipulador = new System.Net.Http.HttpClientHandler();
            manipulador.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; };
            manipulador.AllowAutoRedirect = true;

            using (System.Net.Http.HttpClient cliente = new System.Net.Http.HttpClient(manipulador))
            {
                cliente.Timeout = TimeSpan.FromSeconds(12);

                // ⚡ CABEÇALHOS REALISTAS (Anti-Bloqueio)
                cliente.DefaultRequestHeaders.Clear();
                cliente.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "VLC/3.0.18 LibVLC/3.0.18");
                cliente.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "*/*");
                cliente.DefaultRequestHeaders.TryAddWithoutValidation("Connection", "keep-alive");

                foreach (DataGridViewRow linha in tabelaCanais.Rows)
                {
                    if (linha.IsNewRow) continue;
                    if (cancelador.IsCancellationRequested) break;

                    string url = linha.Cells["Url"].Value?.ToString() ?? "";

                    lblFalta.Text = ObterTraducao($"Falta verificar: {(total - processados):N0}");
                    linha.Cells["StatusUrl"].Value = ObterTraducao("⏳ Testando...");

                    // Rola a tela junto com o teste
                    if (linha.Index > 2) tabelaCanais.FirstDisplayedScrollingRowIndex = linha.Index - 2;

                    if (string.IsNullOrWhiteSpace(url))
                    {
                        linha.Cells["StatusUrl"].Value = ObterTraducao("🔴 OFF (Sem Link)");
                        linha.DefaultCellStyle.BackColor = Color.LightPink;
                        offCount++;
                    }
                    else
                    {
                        // 🔥 CHAMA O NOSSO MOTOR INVESTIGADOR
                        bool isOnline = await TestarCanalReal(url, cliente, cancelador.Token);

                        if (isOnline)
                        {
                            linha.Cells["StatusUrl"].Value = ObterTraducao("🟢 ON"); // ✨ ENVELOPADO
                            linha.DefaultCellStyle.BackColor = Color.LightGreen;
                            onCount++;
                        }
                        else
                        {
                            linha.Cells["StatusUrl"].Value = ObterTraducao("🔴 OFF"); // ✨ ENVELOPADO
                            linha.DefaultCellStyle.BackColor = Color.LightPink;
                            offCount++;
                        }
                    }

                    processados++;
                    // ✨ ENVELOPADO (Estava faltando este!)
                    lblFalta.Text = ObterTraducao($"Falta verificar: {(total - processados):N0}");
                    lblOn.Text = ObterTraducao($"🟢 Canais ON: {onCount:N0}");
                    lblOff.Text = ObterTraducao($"🔴 Canais OFF: {offCount:N0}");

                    // Pausa rápida para a interface respirar
                    await System.Threading.Tasks.Task.Delay(10);
                }
            }

            formProgresso.Close();
            formProgresso.Dispose();

            if (cancelador.IsCancellationRequested)
                MessageBox.Show(ObterTraducao($"O teste foi interrompido.\n\nResultados:\n🟢 ON: {onCount:N0}\n🔴 OFF: {offCount:N0}"), ObterTraducao("VioFlow - Cancelado"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            else
                MessageBox.Show(ObterTraducao($"Teste concluído!\n\nResultados:\n🟢 ON: {onCount:N0}\n🔴 OFF: {offCount:N0}"), ObterTraducao("VioFlow - Radar Pro"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }




        private void btnTema_Click(object sender, EventArgs e) { /* TODO: implementar tema escuro */ }

        private void btnSobre_Click(object sender, EventArgs e)
        {
            Form telaSobre = new Form() { Width = 500, Height = 500, Text = "Sobre o VioFlow", StartPosition = FormStartPosition.CenterScreen, FormBorderStyle = FormBorderStyle.FixedDialog, BackColor = this.BackColor, ForeColor = this.ForeColor, MaximizeBox = false, MinimizeBox = false };

            Label lblTitulo = new Label() { Left = 20, Top = 20, AutoSize = true, Font = new Font("Segoe UI", 18, FontStyle.Bold), Text = "VioFlow IPTV Manager" };
            Label lblDesc = new Label() { Left = 20, Top = 65, Width = 440, Height = 60, Font = new Font("Segoe UI", 10), Text = "A ferramenta definitiva para limpar, organizar e testar suas listas IPTV.\n\nDesenvolvido por: VioFlow" };
            Label lblAvisoLegal = new Label() { Left = 20, Top = 135, Width = 440, Height = 55, Font = new Font("Segoe UI", 9, FontStyle.Italic), ForeColor = Color.IndianRed, Text = "⚠️ AVISO LEGAL: O VioFlow é exclusivamente um editor de texto local. Não fornece, hospeda, vende ou contém nenhum link ou conteúdo de mídia." };

            LinkLabel linkSite = new LinkLabel() { Left = 20, Top = 200, Width = 200, Font = new Font("Segoe UI", 10), Text = "🌐 Visite nosso Github", LinkColor = Color.DodgerBlue };
            linkSite.LinkClicked += (s, ev) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/VioFlow") { UseShellExecute = true });

            Button btnChangelog = new Button() { Text = "📋 O que há de novo? (Changelog)", Left = 240, Top = 195, Width = 220, Height = 30, BackColor = Color.FromArgb(60, 60, 65), ForeColor = Color.White, Font = new Font("Segoe UI", 9), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btnChangelog.Click += (s, ev) => AbrirTelaChangelog();

            // ✨ CORREÇÃO: Colocamos o ObterTraducao para garantir a quebra de linha em outros idiomas
            Label lblMotivacao = new Label() { Left = 10, Top = 245, Width = 460, Height = 40, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = Color.Chocolate, Text = ObterTraducao("Curtiu o VioFlow?\r\nApoie com um café ☕ e fortaleça o projeto! 💙"), TextAlign = ContentAlignment.MiddleCenter };

            Button btnPix = new Button() { Text = "💠 Doar com PIX", Left = 20, Top = 295, Width = 210, Height = 45, BackColor = Color.MediumSeaGreen, ForeColor = Color.White, Font = new Font("Segoe UI", 10, FontStyle.Bold), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            Button btnPayPal = new Button() { Text = "💙 Doar com PayPal", Left = 250, Top = 295, Width = 210, Height = 45, BackColor = Color.SteelBlue, ForeColor = Color.White, Font = new Font("Segoe UI", 10, FontStyle.Bold), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };

            btnPix.Click += (s, ev) => AbrirTelaPix();
            btnPayPal.Click += (s, ev) => AbrirTelaPayPal();

            Button btnFechar = new Button() { Text = "Fechar", Left = 190, Top = 370, Width = 100, Height = 35, DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };

            telaSobre.Controls.Add(lblTitulo); telaSobre.Controls.Add(lblDesc);
            telaSobre.Controls.Add(lblAvisoLegal); telaSobre.Controls.Add(linkSite);
            telaSobre.Controls.Add(btnChangelog); telaSobre.Controls.Add(lblMotivacao);
            telaSobre.Controls.Add(btnPix); telaSobre.Controls.Add(btnPayPal);
            telaSobre.Controls.Add(btnFechar);

            TraduzirTelaDinamica(telaSobre);
            telaSobre.ShowDialog();
        }

        // 💠 TELA DO PIX
        private void AbrirTelaPix()
        {
            Form telaPix = new Form() { Width = 420, Height = 560, Text = "Apoie o VioFlow com PIX", StartPosition = FormStartPosition.CenterScreen, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, BackColor = Color.FromArgb(35, 35, 38) };

            Label lblTitulo = new Label() { Left = 0, Top = 20, Width = 420, Height = 40, ForeColor = Color.MediumSpringGreen, Font = new Font("Segoe UI", 16, FontStyle.Bold), Text = "Doação via PIX 💠", TextAlign = ContentAlignment.MiddleCenter };
            Label lblDesc = new Label() { Left = 20, Top = 60, Width = 360, ForeColor = Color.LightGray, Font = new Font("Segoe UI", 10), Text = "Escaneie o QR Code abaixo com o aplicativo do seu banco:", TextAlign = ContentAlignment.MiddleCenter, Height = 40 };

            PictureBox picQRCode = new PictureBox() { Left = 85, Top = 110, Width = 230, Height = 230, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            picQRCode.Image = Properties.Resources.qrcode;

            Label lblOu = new Label() { Left = 20, Top = 355, Width = 360, ForeColor = Color.LightGray, Font = new Font("Segoe UI", 10), Text = "Ou use o PIX Copia e Cola:", TextAlign = ContentAlignment.MiddleCenter };
            TextBox txtPix = new TextBox() { Left = 30, Top = 385, Width = 340, Font = new Font("Segoe UI", 10), Text = "07879eef-5082-42ac-b528-0f7b64f850bf", ReadOnly = true, BackColor = Color.FromArgb(50, 50, 55), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, TextAlign = HorizontalAlignment.Center };
            Button btnCopiar = new Button() { Left = 50, Top = 430, Width = 300, Height = 40, Text = "📄 Copiar Código PIX", BackColor = Color.LightGreen, ForeColor = Color.Black, Font = new Font("Segoe UI", 10, FontStyle.Bold), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };

            btnCopiar.Click += async (s, ev) =>
            {
                System.Windows.Forms.Clipboard.SetText(txtPix.Text);
                btnCopiar.Text = ObterTraducao("✅ Copiado com Sucesso!");
                btnCopiar.BackColor = Color.MediumSeaGreen;
                await Task.Delay(2000);
                if (btnCopiar.IsHandleCreated) { btnCopiar.Text = ObterTraducao("📄 Copiar Código PIX"); btnCopiar.BackColor = Color.LightGreen; }
            };

            Button btnFechar = new Button() { Left = 145, Top = 485, Width = 110, Height = 30, Text = "Fechar", BackColor = Color.Gray, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btnFechar.Click += (s, ev) => telaPix.Close();

            telaPix.Controls.Add(lblTitulo); telaPix.Controls.Add(lblDesc); telaPix.Controls.Add(picQRCode);
            telaPix.Controls.Add(lblOu); telaPix.Controls.Add(txtPix); telaPix.Controls.Add(btnCopiar); telaPix.Controls.Add(btnFechar);

            TraduzirTelaDinamica(telaPix);
            telaPix.ShowDialog();
        }

        // 💙 TELA DO PAYPAL
        private void AbrirTelaPayPal()
        {
            Form telaPayPal = new Form() { Width = 420, Height = 560, Text = "Apoie o VioFlow com PayPal", StartPosition = FormStartPosition.CenterScreen, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, BackColor = Color.FromArgb(35, 35, 38) };

            Label lblTitulo = new Label() { Left = 0, Top = 20, Width = 420, Height = 40, ForeColor = Color.LightSkyBlue, Font = new Font("Segoe UI", 16, FontStyle.Bold), Text = "Doação via PayPal 💙", TextAlign = ContentAlignment.MiddleCenter };
            Label lblDesc = new Label() { Left = 20, Top = 60, Width = 360, ForeColor = Color.LightGray, Font = new Font("Segoe UI", 10), Text = "Escaneie o QR Code abaixo com a câmera do seu celular:", TextAlign = ContentAlignment.MiddleCenter, Height = 40 };

            PictureBox picQRCode = new PictureBox() { Left = 85, Top = 110, Width = 230, Height = 230, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle };

            picQRCode.Image = Properties.Resources.qrcodepaypal;

            Label lblOu = new Label() { Left = 20, Top = 355, Width = 360, ForeColor = Color.LightGray, Font = new Font("Segoe UI", 10), Text = "Ou clique no botão abaixo para abrir o site:", TextAlign = ContentAlignment.MiddleCenter };

            Button btnAbrirSite = new Button() { Left = 50, Top = 390, Width = 300, Height = 45, Text = "🌐 Abrir Página do PayPal", BackColor = Color.SteelBlue, ForeColor = Color.White, Font = new Font("Segoe UI", 10, FontStyle.Bold), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };

            btnAbrirSite.Click += (s, ev) =>
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://www.paypal.com/donate/?business=SEG7HUXPAQ5AW&no_recurring=0&currency_code=USD") { UseShellExecute = true });
            };

            Button btnFechar = new Button() { Left = 145, Top = 485, Width = 110, Height = 30, Text = "Fechar", BackColor = Color.Gray, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btnFechar.Click += (s, ev) => telaPayPal.Close();

            telaPayPal.Controls.Add(lblTitulo); telaPayPal.Controls.Add(lblDesc); telaPayPal.Controls.Add(picQRCode);
            telaPayPal.Controls.Add(lblOu); telaPayPal.Controls.Add(btnAbrirSite); telaPayPal.Controls.Add(btnFechar);

            TraduzirTelaDinamica(telaPayPal);
            telaPayPal.ShowDialog();
        }

        // 📋 TELA DO CHANGELOG (Histórico de Atualizações)
        private void AbrirTelaChangelog()
        {
            Form telaChangelog = new Form() { Width = 600, Height = 600, Text = "O que há de novo?", StartPosition = FormStartPosition.CenterScreen, BackColor = Color.FromArgb(35, 35, 38), ForeColor = Color.White, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false };

            Label lblTitulo = new Label() { Text = "Histórico de Atualizações", Font = new Font("Segoe UI", 16, FontStyle.Bold), ForeColor = Color.DodgerBlue, Left = 20, Top = 20, Width = 540, Height = 40, TextAlign = ContentAlignment.MiddleCenter };

            // ✨ CORREÇÃO: O RichTextBox precisava ser envelopado com o ObterTraducao para traduzir todo aquele blocão de texto gigante
            RichTextBox txtLog = new RichTextBox() { Left = 25, Top = 70, Width = 535, Height = 430, BackColor = Color.FromArgb(45, 45, 48), ForeColor = Color.LightGray, Font = new Font("Segoe UI", 10), ReadOnly = true, BorderStyle = BorderStyle.None };

            txtLog.Text = ObterTraducao(
        @"🚀 VERSÃO 1.0.0 - A Atualização de Performance (Lançamento Oficial)
• [NOVO] Motor Inteligente de Imagens: O VioFlow agora conta com 'Lazy Loading'. Ele rastreia a barra de rolagem e baixa apenas as logos visíveis, economizando até 90% de Memória RAM e banda de internet.
• [NOVO] Sistema Anti-Bloqueio (User-Agent): Disfarce de navegador integrado para contornar bloqueios de segurança de servidores IPTV ao baixar imagens.
• [NOVO] Cache Dinâmico: Limite inteligente de 500 logos simultâneas na memória, garantindo estabilidade absoluta mesmo ao rolar listas gigantes de 500k+ linhas.
• [NOVO] Central de Transplantes Turbinada: Novo painel visual de 'Antes e Depois' (Roubar Logo) com Motor Anti-Fantasma assíncrono. Downloads de imagens são cancelados instantaneamente ao trocar de canal para evitar mistura de fotos e garantir navegação sem travamentos.
• [NOVO] Nova central de Apoio ao Desenvolvedor, com integração nativa via PIX (QR Code e Copia/Cola) e PayPal.
• [MELHORIA] Interface visual polida e com ajustes avançados de responsividade no Dark Mode.
• [MELHORIA] Consumo de processamento (CPU) reduzido a quase 0% com o PC em repouso.

⚙️ VERSÃO 0.8 - O Update do Inspetor
• [NOVO] Testador de Canais Integrado: Validação de links em tempo real para identificar e remover canais offline ou quebrados.
• [NOVO] Sistema de Limpeza: Remoção de duplicados e formatação em lote de nomes de canais.
• [MELHORIA] Refatoração do motor de busca e filtros na tabela principal.

🔍 VERSÃO 0.5 - Estruturação de Dados
• [NOVO] Implementação de Expressões Regulares (Regex) avançadas para leitura cirúrgica de parâmetros complexos do M3U (tvg-id, tvg-logo, group-title).
• [NOVO] Tabela (DataGrid) interativa permitindo edição manual ágil das colunas.
• [BUGFIX] Correção de travamentos ao tentar mesclar múltiplas listas pesadas.

🌱 VERSÃO 0.1 - Projeto Alpha
• Nascimento do VioFlow IPTV Manager.
• Criação do chassi principal para leitura, edição básica de texto e salvamento de arquivos .m3u e .m3u8.");

            Button btnFechar = new Button() { Text = "Fechar Histórico", Left = 225, Top = 515, Width = 150, Height = 35, BackColor = Color.Gray, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            btnFechar.Click += (s, ev) => telaChangelog.Close();

            telaChangelog.Controls.Add(lblTitulo);
            telaChangelog.Controls.Add(txtLog);
            telaChangelog.Controls.Add(btnFechar);

            TraduzirTelaDinamica(telaChangelog);
            telaChangelog.ShowDialog();
        }

        // Botão Formatar Nomes (Title Case)
        private void button4_Click_2(object sender, EventArgs e)
        {
            SalvarBackupDoTempo();
            if (tabelaCanais.SelectedRows.Count == 0) return;

            int cont = 0;
            foreach (DataGridViewRow linha in tabelaCanais.SelectedRows)
            {
                if (!linha.IsNewRow)
                {
                    string nomeAtual = linha.Cells["NomeCanal"].Value?.ToString() ?? "";
                    if (!string.IsNullOrWhiteSpace(nomeAtual))
                    {
                        linha.Cells["NomeCanal"].Value = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(nomeAtual.ToLower());
                        linha.DefaultCellStyle.BackColor = Color.LightYellow;

                        // ✨ CORREÇÃO: Status envelopado para traduzir direto na tabela!
                        if (tabelaCanais.Columns.Contains("StatusUrl"))
                            linha.Cells["StatusUrl"].Value = ObterTraducao("⏳ Editado");

                        cont++;
                    }
                }
            }
            MessageBox.Show(ObterTraducao($"O nome de {cont:N0} canais foi formatado!"), ObterTraducao("Mágica Concluída"));
        }

        // Abrir lista da Web (Com Testador de Conta + Escolha de Conteúdo)
        private async void button5_Click(object sender, EventArgs e)
        {
            string urlOriginal = MostrarJanelaDeTexto(ObterTraducao("Cole o link (URL) da sua lista IPTV (.m3u):"), ObterTraducao("Abrir da Web"));
            if (string.IsNullOrWhiteSpace(urlOriginal)) return; // Se cancelar a janela

            if (!urlOriginal.StartsWith("http"))
            {
                MessageBox.Show(ObterTraducao("O link precisa começar com 'http'."), ObterTraducao("Aviso"));
                return;
            }

            bool ehXtreamCodes = urlOriginal.Contains("username=") && urlOriginal.Contains("password=");
            bool extrairSoAoVivo = false;

            // 1. SE FOR XTREAM CODES, MOSTRA A TELA DE ESCOLHA
            if (ehXtreamCodes)
            {
                Form promptFiltro = new Form() { Width = 380, Height = 250, Text = ObterTraducao("🔍 Filtro de Conteúdo"), StartPosition = FormStartPosition.CenterScreen, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false };
                Label lblP = new Label() { Left = 20, Top = 20, Width = 340, Text = ObterTraducao("O que deseja carregar no VioFlow?"), Font = new Font("Segoe UI", 10, FontStyle.Bold) };
                RadioButton rbLive = new RadioButton() { Left = 20, Top = 60, Width = 340, Text = ObterTraducao("📺 Só TV ao Vivo (Muito Rápido)"), Checked = true, Font = new Font("Segoe UI", 10) };
                RadioButton rbTudo = new RadioButton() { Left = 20, Top = 90, Width = 340, Text = ObterTraducao("🌍 Lista Completa (+ Filmes e Séries)"), Font = new Font("Segoe UI", 10) };
                Label lblAv = new Label() { Left = 40, Top = 115, Width = 300, ForeColor = Color.Red, Font = new Font("Segoe UI", 8), Text = ObterTraducao("Atenção: A lista completa pode demorar para baixar.") };
                Button btnExtrair = new Button() { Text = ObterTraducao("⚡ Extrair Agora"), Left = 20, Top = 150, Width = 320, Height = 40, BackColor = Color.LightGreen, DialogResult = DialogResult.OK, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
                promptFiltro.Controls.Add(lblP); promptFiltro.Controls.Add(rbLive); promptFiltro.Controls.Add(rbTudo); promptFiltro.Controls.Add(lblAv); promptFiltro.Controls.Add(btnExtrair);

                TraduzirTelaDinamica(promptFiltro);
                if (promptFiltro.ShowDialog() != DialogResult.OK) return;
                extrairSoAoVivo = rbLive.Checked;
            }

            try
            {
                string hostName = new Uri(urlOriginal).Host;
                nomeArquivoAtual = $"🌐 Link Web ({hostName})";
            }
            catch
            {
                nomeArquivoAtual = "🌐 Link Web (URL)";
            }

            AtualizarStatus("Conectando ao servidor...");

            LimparMemoriaParaNovaLista();

            // 2. PREPARA A TELA DE CARREGAMENTO
            Form telaLoad = new Form() { Width = 450, Height = 170, StartPosition = FormStartPosition.CenterScreen, FormBorderStyle = FormBorderStyle.None, BackColor = Color.WhiteSmoke, TopMost = true };
            Label lblAviso = new Label() { Left = 20, Top = 30, Width = 410, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 12, FontStyle.Bold), Text = ObterTraducao("🕵️‍♂️ Analisando Servidor...") };
            Label lblPorcentagem = new Label() { Left = 20, Top = 70, Width = 410, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 10), Text = ObterTraducao("Verificando status da conta...") };
            ProgressBar barra = new ProgressBar() { Left = 30, Top = 110, Width = 390, Height = 20, Style = ProgressBarStyle.Marquee };
            telaLoad.Controls.Add(lblAviso); telaLoad.Controls.Add(lblPorcentagem); telaLoad.Controls.Add(barra);
            TraduzirTelaDinamica(telaLoad);
            telaLoad.Show(); Application.DoEvents();

            try
            {
                var mani = new HttpClientHandler() { ServerCertificateCustomValidationCallback = (_, __, ___, ____) => true, AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate };
                using (var cliente = new HttpClient(mani))
                {
                    cliente.Timeout = TimeSpan.FromMinutes(15);
                    cliente.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/122.0.0.0");

                    string apiUrl = urlOriginal;

                    // 3. TESTA SE A CONTA ESTÁ ONLINE (Se for Xtream Codes)
                    if (ehXtreamCodes)
                    {
                        apiUrl = urlOriginal.Replace("get.php", "player_api.php");
                        apiUrl = System.Text.RegularExpressions.Regex.Replace(apiUrl, @"&type=[^&]+", "");
                        apiUrl = System.Text.RegularExpressions.Regex.Replace(apiUrl, @"&output=[^&]+", "");

                        try
                        {
                            string jsonStatus = await cliente.GetStringAsync(apiUrl);
                            var matchStatus = System.Text.RegularExpressions.Regex.Match(jsonStatus, "\"status\":\"([^\"]+)\"");
                            if (matchStatus.Success && matchStatus.Groups[1].Value != "Active")
                            {
                                telaLoad.Close();
                                MessageBox.Show(ObterTraducao($"O servidor bloqueou ou a conta está inativa.\nStatus: {matchStatus.Groups[1].Value.ToUpper()}"), ObterTraducao("Conta OFF"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                AtualizarStatus("Pronto");
                                return;
                            }
                            lblAviso.Text = ObterTraducao("✅ Conta ATIVA!");
                            lblPorcentagem.Text = ObterTraducao("Iniciando extração...");
                            Application.DoEvents(); await Task.Delay(1000);
                        }
                        catch { /* Ignora erro de API e tenta baixar direto */ }
                    }

                    // 4. CAMINHO A: EXTRAÇÃO RÁPIDA (SÓ AO VIVO VIA JSON)
                    if (ehXtreamCodes && extrairSoAoVivo)
                    {
                        lblAviso.Text = ObterTraducao("📺 Baixando TV ao Vivo...");
                        lblPorcentagem.Text = ObterTraducao("⏳ Buscando categorias..."); Application.DoEvents();

                        string jsonCats = await cliente.GetStringAsync(apiUrl + "&action=get_live_categories");
                        var mapaCats = new Dictionary<string, string>();
                        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(jsonCats, @"\""category_id\""\s*:\s*\""([^\""]+)\"".*?\""category_name\""\s*:\s*\""([^\""]+)\"""))
                            mapaCats[m.Groups[1].Value] = m.Groups[2].Value.Replace("\\", "");

                        lblPorcentagem.Text = ObterTraducao("⏳ Baixando lista de canais..."); Application.DoEvents();

                        string jsonStreams = "";
                        using (var resp = await cliente.GetAsync(apiUrl + "&action=get_live_streams", HttpCompletionOption.ResponseHeadersRead))
                        {
                            resp.EnsureSuccessStatusCode();
                            using (var stream = await resp.Content.ReadAsStreamAsync())
                            using (var mem = new MemoryStream())
                            {
                                byte[] buf = new byte[8192]; int lidos; long total = 0; int ult = 0;
                                while ((lidos = await stream.ReadAsync(buf, 0, buf.Length)) > 0)
                                {
                                    mem.Write(buf, 0, lidos); total += lidos;
                                    if (Environment.TickCount - ult > 200) { lblPorcentagem.Text = ObterTraducao($"⏳ {(total / 1024.0 / 1024.0):F2} MB baixados..."); Application.DoEvents(); ult = Environment.TickCount; }
                                }
                                jsonStreams = Encoding.UTF8.GetString(mem.ToArray());
                            }
                        }

                        lblAviso.Text = ObterTraducao("🚀 Processando canais..."); Application.DoEvents();
                        string user = System.Text.RegularExpressions.Regex.Match(urlOriginal, @"username=([^&]+)").Groups[1].Value;
                        string pass = System.Text.RegularExpressions.Regex.Match(urlOriginal, @"password=([^&]+)").Groups[1].Value;
                        string baseUrl = new Uri(urlOriginal).GetLeftPart(UriPartial.Authority);

                        Func<string, string, string> Puxar = (j, k) =>
                        {
                            var mt = System.Text.RegularExpressions.Regex.Match(j, $"\"{k}\"\\s*:\\s*(?:\"([^\"]*)\"|([\\d\\.]+))");
                            return mt.Success ? (mt.Groups[1].Success ? mt.Groups[1].Value : mt.Groups[2].Value) : "";
                        };

                        tabelaCanais.SuspendLayout(); tabelaCanais.Rows.Clear();
                        foreach (DataGridViewColumn col in tabelaCanais.Columns) if (col is DataGridViewImageColumn ic) ic.DefaultCellStyle.NullValue = new Bitmap(1, 1);
                        var modoAnt = tabelaCanais.AutoSizeColumnsMode; tabelaCanais.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

                        var linhasAdd = new List<DataGridViewRow>();
                        int achados = 0;

                        foreach (System.Text.RegularExpressions.Match bloco in System.Text.RegularExpressions.Regex.Matches(jsonStreams, @"\{(?:[^{}]|(?<=\\)[{}])*\}"))
                        {
                            string b = bloco.Value;
                            if (!b.Contains("\"stream_type\":\"live\"")) continue;

                            string catId = Puxar(b, "category_id");
                            string cat = mapaCats.ContainsKey(catId) ? mapaCats[catId] : ObterTraducao("Canais de TV");
                            string sid = Puxar(b, "stream_id");

                            var row = new DataGridViewRow(); row.CreateCells(tabelaCanais); row.Height = 60;
                            if (tabelaCanais.Columns.Contains("StatusUrl")) row.Cells[tabelaCanais.Columns["StatusUrl"].Index].Value = "";
                            if (tabelaCanais.Columns.Contains("LogoUrl")) row.Cells[tabelaCanais.Columns["LogoUrl"].Index].Value = Puxar(b, "stream_icon").Replace("\\/", "/");
                            if (tabelaCanais.Columns.Contains("NomeCanal")) row.Cells[tabelaCanais.Columns["NomeCanal"].Index].Value = Puxar(b, "name");
                            if (tabelaCanais.Columns.Contains("EpgId")) row.Cells[tabelaCanais.Columns["EpgId"].Index].Value = Puxar(b, "epg_channel_id");
                            if (tabelaCanais.Columns.Contains("Categoria")) row.Cells[tabelaCanais.Columns["Categoria"].Index].Value = cat;
                            if (tabelaCanais.Columns.Contains("Url")) row.Cells[tabelaCanais.Columns["Url"].Index].Value = $"{baseUrl}/live/{user}/{pass}/{sid}.ts";

                            linhasAdd.Add(row); achados++;
                        }

                        tabelaCanais.Rows.AddRange(linhasAdd.ToArray());
                        tabelaCanais.AutoSizeColumnsMode = modoAnt; tabelaCanais.ResumeLayout();
                        telaLoad.Close();
                        BaixarImagensInvisivelmente();

                        historicoDoTempo.Clear();
                        futuroDoTempo.Clear();
                        historicoDoTempo.Push(TirarFotoDaTabela());
                        AtualizarStatus("Lista Web (Ao Vivo) extraída com sucesso!");

                        MessageBox.Show(ObterTraducao($"Extração concluída!\n{achados:N0} canais ao Vivo carregados no VioFlow."), ObterTraducao("VioFlow IPTV Manager"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    // 5. CAMINHO B: LISTA COMPLETA (BAIXANDO M3U)
                    else
                    {
                        barra.Style = ProgressBarStyle.Blocks;
                        string urlDl = urlOriginal;
                        if (ehXtreamCodes && !urlDl.Contains("type=m3u")) urlDl += "&type=m3u_plus&output=mpegts";

                        lblAviso.Text = ObterTraducao("🌍 Baixando Lista Completa...");
                        lblPorcentagem.Text = ObterTraducao("Conectando ao servidor..."); Application.DoEvents();

                        using (var resp = await cliente.GetAsync(urlDl, HttpCompletionOption.ResponseHeadersRead))
                        {
                            resp.EnsureSuccessStatusCode();
                            long? tam = resp.Content.Headers.ContentLength;

                            using (var stream = await resp.Content.ReadAsStreamAsync())
                            using (var mem = new MemoryStream())
                            {
                                byte[] buf = new byte[8192]; int lidos; long total = 0; int ult = 0;
                                while ((lidos = await stream.ReadAsync(buf, 0, buf.Length)) > 0)
                                {
                                    mem.Write(buf, 0, lidos); total += lidos;
                                    if (Environment.TickCount - ult > 200)
                                    {
                                        if (tam.HasValue)
                                        {
                                            int pct = (int)(((double)total / tam.Value) * 100);
                                            lblPorcentagem.Text = ObterTraducao($"Baixando arquivo: {pct}%");
                                            barra.Value = pct > 100 ? 100 : pct;
                                        }
                                        else lblPorcentagem.Text = ObterTraducao($"Baixados: {(total / 1024.0 / 1024.0):F2} MB");
                                        Application.DoEvents(); ult = Environment.TickCount;
                                    }
                                }

                                lblAviso.Text = ObterTraducao("🚀 Processando conteúdo...");
                                lblPorcentagem.Text = ObterTraducao("Lendo informações da lista..."); Application.DoEvents();

                                string[] linhasArq = Encoding.UTF8.GetString(mem.ToArray()).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                                telaLoad.Close();

                                tabelaCanais.SuspendLayout(); tabelaCanais.Rows.Clear();
                                foreach (DataGridViewColumn col in tabelaCanais.Columns) if (col is DataGridViewImageColumn ic) ic.DefaultCellStyle.NullValue = new Bitmap(1, 1);
                                var modoAnt = tabelaCanais.AutoSizeColumnsMode; tabelaCanais.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

                                var canais = ParsearM3U(linhasArq);
                                var gruposUnicos = new HashSet<string>(canais.Select(c => c[3]), StringComparer.OrdinalIgnoreCase);
                                AdicionarCanaisNaTabela(canais);

                                tabelaCanais.AutoSizeColumnsMode = modoAnt; tabelaCanais.ResumeLayout();
                                BaixarImagensInvisivelmente();

                                historicoDoTempo.Clear();
                                futuroDoTempo.Clear();
                                historicoDoTempo.Push(TirarFotoDaTabela());
                                AtualizarStatus("Lista Web Completa carregada com sucesso!");

                                MessageBox.Show(ObterTraducao($"Lista completa carregada!\n{canais.Count:N0} conteúdos em {gruposUnicos.Count:N0} grupos."), ObterTraducao("VioFlow IPTV Manager"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (telaLoad.Visible) telaLoad.Close();
                AtualizarStatus("Pronto");
                MessageBox.Show(ObterTraducao($"Não foi possível carregar a lista.\n\nDetalhe do erro: {ex.Message}"), ObterTraducao("Erro de Conexão"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Exportação por categorias selecionadas
        private void button5_Click_1(object sender, EventArgs e)
        {
            SalvarBackupDoTempo();

            var categoriasUnicas = new HashSet<string>();
            foreach (DataGridViewRow linha in tabelaCanais.Rows)
                if (!linha.IsNewRow) categoriasUnicas.Add(linha.Cells["Categoria"].Value?.ToString() ?? ObterTraducao("Sem Categoria"));

            if (categoriasUnicas.Count == 0) { MessageBox.Show(ObterTraducao("Não há canais para exportar."), ObterTraducao("Aviso")); return; }

            Form prompt = new Form() { Width = 450, Height = 500, Text = ObterTraducao("Exportação Expressa"), StartPosition = FormStartPosition.CenterScreen, FormBorderStyle = FormBorderStyle.FixedDialog };
            Label textLabel = new Label() { Left = 20, Top = 20, Text = ObterTraducao("Marque as categorias que deseja SALVAR:"), Width = 400 };
            CheckedListBox chkList = new CheckedListBox() { Left = 20, Top = 50, Width = 390, Height = 340, CheckOnClick = true };
            var listaCat = new List<string>(categoriasUnicas);
            listaCat.Sort();
            foreach (string c in listaCat) chkList.Items.Add(c);
            Button btnConfirmar = new Button() { Text = ObterTraducao("Exportar Marcadas"), Left = 240, Width = 170, Top = 410, DialogResult = DialogResult.OK, BackColor = Color.LightGreen };
            Button btnCancelar = new Button() { Text = ObterTraducao("Cancelar"), Left = 130, Width = 100, Top = 410, DialogResult = DialogResult.Cancel };
            prompt.Controls.Add(textLabel); prompt.Controls.Add(chkList); prompt.Controls.Add(btnConfirmar); prompt.Controls.Add(btnCancelar);

            // ✨ CORREÇÃO: Máquina de tradução ativada antes da tela abrir!
            TraduzirTelaDinamica(prompt);

            if (prompt.ShowDialog() == DialogResult.OK && chkList.CheckedItems.Count > 0)
            {
                var selecionadas = chkList.CheckedItems.Cast<object>().Select(o => o.ToString()!).ToList();

                SaveFileDialog meuSalvador = new SaveFileDialog() { Filter = "Lista IPTV (*.m3u)|*.m3u|Lista IPTV (*.m3u8)|*.m3u8", Title = ObterTraducao("Salvar Lista Exportada"), FileName = "Lista_Exportada.m3u" };
                if (meuSalvador.ShowDialog() == DialogResult.OK)
                {
                    var linhasParaSalvar = new List<string>();
                    linhasParaSalvar.Add(string.IsNullOrEmpty(epgsGlobais) ? "#EXTM3U" : $"#EXTM3U x-tvg-url=\"{epgsGlobais}\"");
                    int canaisExportados = 0;

                    foreach (DataGridViewRow linha in tabelaCanais.Rows)
                    {
                        if (linha.IsNewRow) continue;
                        string cat = linha.Cells["Categoria"].Value?.ToString() ?? ObterTraducao("Sem Categoria");
                        if (selecionadas.Contains(cat))
                        {
                            string logoUrl = linha.Cells["LogoUrl"].Value?.ToString() ?? "";
                            string nome = linha.Cells["NomeCanal"].Value?.ToString() ?? ObterTraducao("Canal Sem Nome");
                            string epgId = linha.Cells["EpgId"].Value?.ToString() ?? "";
                            string url = linha.Cells["Url"].Value?.ToString() ?? "";
                            linhasParaSalvar.Add($"#EXTINF:-1 tvg-id=\"{epgId}\" tvg-logo=\"{logoUrl}\" group-title=\"{cat}\",{nome}");
                            linhasParaSalvar.Add(url);
                            canaisExportados++;
                        }
                    }

                    File.WriteAllLines(meuSalvador.FileName, linhasParaSalvar, Encoding.UTF8);
                    MessageBox.Show(ObterTraducao($"Exportação concluída!\n{canaisExportados:N0} canais exportados."), ObterTraducao("Exportação Concluída"));
                }
            }
        }

        private void OrdemdasCategorias_Click(object sender, EventArgs e)
        {
            SalvarBackupDoTempo();

            var categoriasAtuais = new List<string>();
            foreach (DataGridViewRow linha in tabelaCanais.Rows)
            {
                if (!linha.IsNewRow)
                {
                    // ✨ CORREÇÃO: "Sem Categoria" envelopado para garantir a busca correta na língua atual
                    string cat = linha.Cells["Categoria"].Value?.ToString() ?? ObterTraducao("Sem Categoria");
                    if (!categoriasAtuais.Contains(cat)) categoriasAtuais.Add(cat);
                }
            }

            if (categoriasAtuais.Count == 0) { MessageBox.Show(ObterTraducao("Não há canais para organizar."), ObterTraducao("Aviso")); return; }

            // ✨ CORREÇÃO: Título da janela envelopado
            Form telaOrdem = new Form() { Width = 400, Height = 500, Text = ObterTraducao("Organizador de Categorias"), StartPosition = FormStartPosition.CenterScreen, FormBorderStyle = FormBorderStyle.FixedDialog };
            Label lblInfo = new Label() { Left = 20, Top = 20, Width = 350, Text = ObterTraducao("Selecione uma categoria e use as setas:") };
            ListBox listaCat = new ListBox() { Left = 20, Top = 50, Width = 280, Height = 340, Font = new Font("Segoe UI", 10) };
            foreach (string c in categoriasAtuais) listaCat.Items.Add(c);

            Button btnSubir = new Button() { Text = "⬆️", Left = 310, Top = 100, Width = 60, Height = 60, BackColor = Color.LightGray };
            Button btnDescer = new Button() { Text = "⬇️", Left = 310, Top = 180, Width = 60, Height = 60, BackColor = Color.LightGray };

            btnSubir.Click += (s, ev) =>
            {
                int i = listaCat.SelectedIndex;
                if (i > 0) { object item = listaCat.SelectedItem; listaCat.Items.RemoveAt(i); listaCat.Items.Insert(i - 1, item); listaCat.SelectedIndex = i - 1; }
            };
            btnDescer.Click += (s, ev) =>
            {
                int i = listaCat.SelectedIndex;
                if (i >= 0 && i < listaCat.Items.Count - 1) { object item = listaCat.SelectedItem; listaCat.Items.RemoveAt(i); listaCat.Items.Insert(i + 1, item); listaCat.SelectedIndex = i + 1; }
            };

            // ✨ CORREÇÃO: Texto do botão envelopado
            Button btnAplicar = new Button() { Text = ObterTraducao("✨ Aplicar Nova Ordem"), Left = 20, Width = 340, Top = 410, Height = 40, DialogResult = DialogResult.OK, BackColor = Color.LightSkyBlue, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            telaOrdem.Controls.Add(lblInfo); telaOrdem.Controls.Add(listaCat); telaOrdem.Controls.Add(btnSubir); telaOrdem.Controls.Add(btnDescer); telaOrdem.Controls.Add(btnAplicar);

            // ✨ A MÁGICA: Traduz os móveis da janela antes dela aparecer!
            TraduzirTelaDinamica(telaOrdem);

            if (telaOrdem.ShowDialog() == DialogResult.OK)
            {
                tabelaCanais.SuspendLayout();
                var novaOrdem = new List<DataGridViewRow>();

                foreach (var item in listaCat.Items)
                {
                    string catAlvo = item.ToString();
                    foreach (DataGridViewRow linha in tabelaCanais.Rows)
                        // ✨ CORREÇÃO: Tem que traduzir de novo o "Sem Categoria" aqui para bater o filtro de busca com a tela!
                        if (!linha.IsNewRow && (linha.Cells["Categoria"].Value?.ToString() ?? ObterTraducao("Sem Categoria")) == catAlvo)
                            novaOrdem.Add(linha);
                }

                tabelaCanais.Rows.Clear();
                tabelaCanais.Rows.AddRange(novaOrdem.ToArray());
                tabelaCanais.ResumeLayout();

                // ✨ Atualizei para VioFlow 1.3 para ficar igual às outras mensagens
                MessageBox.Show(ObterTraducao("Categorias reorganizadas com sucesso!"), ObterTraducao("VioFlow 1.3 - Ordem Aplicada"));
            }
        }

        private void TestadordeM3U_Click(object sender, EventArgs e)
        {
            Form telaTestador = new Form() { Width = 1050, Height = 650, Text = ObterTraducao("🕵️‍♂️ Testador de M3U (Xtream Codes)"), StartPosition = FormStartPosition.CenterScreen, BackColor = Color.WhiteSmoke };
            Label lblInstrucao = new Label() { Left = 20, Top = 20, Width = 900, Font = new Font("Segoe UI", 10, FontStyle.Bold), Text = ObterTraducao("Cole os links M3U abaixo. O VioFlow vai extrair e testar tudo sozinho:") };
            TextBox txtLinks = new TextBox() { Left = 20, Top = 50, Width = 990, Height = 120, Multiline = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 9) };
            Button btnTestar = new Button() { Text = ObterTraducao("⚡ Iniciar Teste"), Left = 20, Top = 180, Width = 140, Height = 40, BackColor = Color.LightSkyBlue, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            Button btnAbrirTela = new Button() { Text = ObterTraducao("🚀 Abrir na Tela Principal"), Left = 170, Top = 180, Width = 230, Height = 40, BackColor = Color.LightGreen, Enabled = false, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            Button btnSalvarFile = new Button() { Text = ObterTraducao("💾 Salvar .M3U"), Left = 410, Top = 180, Width = 180, Height = 40, BackColor = Color.LightGray, Enabled = false };
            Label lblProgresso = new Label() { Left = 600, Top = 190, Width = 430, Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = Color.DarkBlue, Text = "" };

            DataGridView gridResultados = new DataGridView() { Left = 20, Top = 235, Width = 990, Height = 350, AllowUserToAddRows = false, ReadOnly = true, SelectionMode = DataGridViewSelectionMode.FullRowSelect, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Color.White };
            gridResultados.Columns.Add("Url", ObterTraducao("Link Original")); gridResultados.Columns[0].Width = 350;
            gridResultados.Columns.Add("Status", ObterTraducao("Status da Conta"));
            gridResultados.Columns.Add("Validade", ObterTraducao("Vence em"));
            gridResultados.Columns.Add("Conexoes", ObterTraducao("Telas (Uso/Máx)"));

            telaTestador.Controls.Add(lblInstrucao); telaTestador.Controls.Add(txtLinks);
            telaTestador.Controls.Add(btnTestar); telaTestador.Controls.Add(btnAbrirTela);
            telaTestador.Controls.Add(btnSalvarFile); telaTestador.Controls.Add(lblProgresso);
            telaTestador.Controls.Add(gridResultados);

            btnTestar.Click += async (s, ev) =>
            {
                if (string.IsNullOrWhiteSpace(txtLinks.Text)) return;
                gridResultados.Rows.Clear();
                btnTestar.Enabled = false; btnAbrirTela.Enabled = false; btnSalvarFile.Enabled = false;

                var matches = System.Text.RegularExpressions.Regex.Matches(txtLinks.Text, @"https?://[^\s\""']+");
                var urlsUnicas = new HashSet<string>();
                foreach (System.Text.RegularExpressions.Match m in matches) urlsUnicas.Add(m.Value);
                if (urlsUnicas.Count == 0) { MessageBox.Show(ObterTraducao("Nenhum link encontrado."), ObterTraducao("Aviso")); btnTestar.Enabled = true; return; }

                int testados = 0, ativos = 0;
                var mani = new HttpClientHandler() { ServerCertificateCustomValidationCallback = (_, __, ___, ____) => true };
                using (var cliente = new HttpClient(mani))
                {
                    cliente.Timeout = TimeSpan.FromSeconds(8);
                    cliente.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/122.0.0.0");

                    foreach (string urlRaw in urlsUnicas)
                    {
                        testados++;
                        lblProgresso.Text = ObterTraducao($"📡 Testando: {testados:N0} de {urlsUnicas.Count:N0}...");

                        if (!urlRaw.Contains("username=") || !urlRaw.Contains("password="))
                        { gridResultados.Rows.Add(urlRaw, ObterTraducao("❌ Inválido (Sem Senha)"), "-", "-"); continue; }

                        string urlApi = urlRaw.Replace("get.php", "player_api.php");
                        urlApi = System.Text.RegularExpressions.Regex.Replace(urlApi, @"&type=[a-zA-Z0-9_]+", "");
                        urlApi = System.Text.RegularExpressions.Regex.Replace(urlApi, @"&output=[a-zA-Z0-9_]+", "");

                        try
                        {
                            string json = await cliente.GetStringAsync(urlApi);
                            string status = ExtrairDado(json, "\"status\":\"").Replace("\"", "");
                            string expDate = ExtrairDado(json, "\"exp_date\":\"").Replace("\"", "");
                            string maxConn = ExtrairDado(json, "\"max_connections\":\"").Replace("\"", "");
                            string activeConn = ExtrairDado(json, "\"active_cons\":\"").Replace("\"", "");

                            if (string.IsNullOrEmpty(status)) status = "Falha no Login";

                            string validade = ObterTraducao("Ilimitado");
                            if (!string.IsNullOrEmpty(expDate) && expDate != "null" && long.TryParse(expDate, out long ts))
                                validade = DateTimeOffset.FromUnixTimeSeconds(ts).DateTime.ToLocalTime().ToString("dd/MM/yyyy HH:mm");

                            string conexoes = string.IsNullOrEmpty(maxConn) ? "-" : $"{activeConn} " + ObterTraducao("online / Máx:") + $" {maxConn}";

                            int idx = gridResultados.Rows.Add(urlRaw, status == "Active" ? ObterTraducao("✅ ATIVA") : $"🔴 {status.ToUpper()}", validade, conexoes);
                            gridResultados.Rows[idx].DefaultCellStyle.BackColor = status == "Active" ? Color.LightGreen : Color.LightPink;
                            if (status == "Active") ativos++;
                        }
                        catch
                        {
                            int idx = gridResultados.Rows.Add(urlRaw, ObterTraducao("🔴 OFF (Servidor Caiu)"), "-", "-");
                            gridResultados.Rows[idx].DefaultCellStyle.BackColor = Color.LightPink;
                        }
                    }
                }
                lblProgresso.Text = ObterTraducao($"✅ {ativos:N0} listas ativas encontradas.");
                btnTestar.Enabled = true;
                if (ativos > 0) { btnAbrirTela.Enabled = true; btnSalvarFile.Enabled = true; }
            };

            btnAbrirTela.Click += async (s, ev) =>
            {
                if (gridResultados.CurrentRow == null || !gridResultados.CurrentRow.Cells["Status"].Value.ToString().Contains("ATIVA"))
                { MessageBox.Show(ObterTraducao("Selecione uma lista ATIVA (verde).")); return; }

                string urlOriginal = gridResultados.CurrentRow.Cells["Url"].Value.ToString();

                // ✨ NOVIDADE: Atualiza a memória global
                try
                {
                    string hostName = new Uri(urlOriginal).Host;
                    nomeArquivoAtual = $"🌐 Testador ({hostName})";
                }
                catch
                {
                    nomeArquivoAtual = "🌐 Testador (URL)";
                }
                AtualizarStatus("Extraindo do Testador..."); // Aviso inicial no painel de fundo

                Form promptFiltro = new Form() { Width = 380, Height = 250, Text = ObterTraducao("🔍 Filtro de Conteúdo"), StartPosition = FormStartPosition.CenterScreen, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false };
                Label lblP = new Label() { Left = 20, Top = 20, Width = 340, Text = ObterTraducao("O que deseja extrair?"), Font = new Font("Segoe UI", 10, FontStyle.Bold) };
                RadioButton rbLive = new RadioButton() { Left = 20, Top = 60, Width = 340, Text = ObterTraducao("📺 Só TV ao Vivo (Rápido)"), Checked = true, Font = new Font("Segoe UI", 10) };
                RadioButton rbTudo = new RadioButton() { Left = 20, Top = 90, Width = 340, Text = ObterTraducao("🌍 Lista Completa (+ Filmes e Séries)"), Font = new Font("Segoe UI", 10) };
                Label lblAv = new Label() { Left = 40, Top = 115, Width = 300, ForeColor = Color.Red, Font = new Font("Segoe UI", 8), Text = ObterTraducao("Atenção: A lista completa pode demorar vários minutos.") };
                Button btnExtrair = new Button() { Text = ObterTraducao("⚡ Extrair Agora"), Left = 20, Top = 150, Width = 320, Height = 40, BackColor = Color.LightGreen, DialogResult = DialogResult.OK, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
                promptFiltro.Controls.Add(lblP); promptFiltro.Controls.Add(rbLive); promptFiltro.Controls.Add(rbTudo); promptFiltro.Controls.Add(lblAv); promptFiltro.Controls.Add(btnExtrair);

                TraduzirTelaDinamica(promptFiltro);
                if (promptFiltro.ShowDialog() != DialogResult.OK)
                {
                    AtualizarStatus("Pronto"); // Limpa o status se cancelar
                    return;
                }

                // ✨ A FAXINA ENTRA EXATAMENTE AQUI 👇 ✨
                LimparMemoriaParaNovaLista();

                btnAbrirTela.Enabled = false; btnSalvarFile.Enabled = false; btnTestar.Enabled = false;
                try
                {
                    var mani2 = new HttpClientHandler() { ServerCertificateCustomValidationCallback = (_, __, ___, ____) => true, AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate };
                    using (var cliente = new HttpClient(mani2))
                    {
                        cliente.Timeout = TimeSpan.FromMinutes(15);
                        cliente.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/122.0.0.0");

                        if (rbLive.Checked)
                        {
                            lblProgresso.Text = ObterTraducao("⏳ Buscando categorias..."); Application.DoEvents();
                            string apiUrl = urlOriginal.Replace("get.php", "player_api.php");
                            apiUrl = System.Text.RegularExpressions.Regex.Replace(apiUrl, @"&type=[^&]+", "");
                            apiUrl = System.Text.RegularExpressions.Regex.Replace(apiUrl, @"&output=[^&]+", "");

                            string jsonCats = await cliente.GetStringAsync(apiUrl + "&action=get_live_categories");
                            var mapaCats = new Dictionary<string, string>();
                            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(jsonCats, @"\""category_id\""\s*:\s*\""([^\""]+)\"".*?\""category_name\""\s*:\s*\""([^\""]+)\"""))
                                mapaCats[m.Groups[1].Value] = m.Groups[2].Value.Replace("\\", "");

                            lblProgresso.Text = ObterTraducao("⏳ Baixando canais..."); Application.DoEvents();
                            string jsonStreams = "";
                            using (var resp = await cliente.GetAsync(apiUrl + "&action=get_live_streams", HttpCompletionOption.ResponseHeadersRead))
                            {
                                resp.EnsureSuccessStatusCode();
                                using (var stream = await resp.Content.ReadAsStreamAsync())
                                using (var mem = new MemoryStream())
                                {
                                    byte[] buf = new byte[8192]; int lidos; long total = 0; int ult = 0;
                                    while ((lidos = await stream.ReadAsync(buf, 0, buf.Length)) > 0)
                                    {
                                        mem.Write(buf, 0, lidos); total += lidos;
                                        if (Environment.TickCount - ult > 200) { lblProgresso.Text = ObterTraducao($"⏳ {(total / 1024.0 / 1024.0):F2} MB baixados..."); Application.DoEvents(); ult = Environment.TickCount; }
                                    }
                                    jsonStreams = Encoding.UTF8.GetString(mem.ToArray());
                                }
                            }

                            string user = System.Text.RegularExpressions.Regex.Match(urlOriginal, @"username=([^&]+)").Groups[1].Value;
                            string pass = System.Text.RegularExpressions.Regex.Match(urlOriginal, @"password=([^&]+)").Groups[1].Value;
                            string baseUrl = new Uri(urlOriginal).GetLeftPart(UriPartial.Authority);

                            Func<string, string, string> Puxar = (j, k) =>
                            {
                                var mt = System.Text.RegularExpressions.Regex.Match(j, $"\"{k}\"\\s*:\\s*(?:\"([^\"]*)\"|([\\d\\.]+))");
                                return mt.Success ? (mt.Groups[1].Success ? mt.Groups[1].Value : mt.Groups[2].Value) : "";
                            };

                            tabelaCanais.SuspendLayout(); tabelaCanais.Rows.Clear();
                            foreach (DataGridViewColumn col in tabelaCanais.Columns) if (col is DataGridViewImageColumn ic) ic.DefaultCellStyle.NullValue = new Bitmap(1, 1);
                            var modoAnt = tabelaCanais.AutoSizeColumnsMode; tabelaCanais.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

                            var linhasAdd = new List<DataGridViewRow>();
                            int achados = 0;

                            foreach (System.Text.RegularExpressions.Match bloco in System.Text.RegularExpressions.Regex.Matches(jsonStreams, @"\{(?:[^{}]|(?<=\\)[{}])*\}"))
                            {
                                string b = bloco.Value;
                                if (!b.Contains("\"stream_type\":\"live\"")) continue;
                                string nome = Puxar(b, "name");
                                string sid = Puxar(b, "stream_id");
                                string logo = Puxar(b, "stream_icon").Replace("\\/", "/");
                                string epg = Puxar(b, "epg_channel_id");
                                string catId = Puxar(b, "category_id");
                                string cat = mapaCats.ContainsKey(catId) ? mapaCats[catId] : ObterTraducao("Canais de TV");
                                string urlFinal = $"{baseUrl}/live/{user}/{pass}/{sid}.ts";

                                var row = new DataGridViewRow(); row.CreateCells(tabelaCanais); row.Height = 60;
                                if (tabelaCanais.Columns.Contains("StatusUrl")) row.Cells[tabelaCanais.Columns["StatusUrl"].Index].Value = "";
                                if (tabelaCanais.Columns.Contains("LogoUrl")) row.Cells[tabelaCanais.Columns["LogoUrl"].Index].Value = logo;
                                if (tabelaCanais.Columns.Contains("NomeCanal")) row.Cells[tabelaCanais.Columns["NomeCanal"].Index].Value = nome;
                                if (tabelaCanais.Columns.Contains("EpgId")) row.Cells[tabelaCanais.Columns["EpgId"].Index].Value = epg;
                                if (tabelaCanais.Columns.Contains("Categoria")) row.Cells[tabelaCanais.Columns["Categoria"].Index].Value = cat;
                                if (tabelaCanais.Columns.Contains("Url")) row.Cells[tabelaCanais.Columns["Url"].Index].Value = urlFinal;
                                linhasAdd.Add(row); achados++;
                            }

                            tabelaCanais.Rows.AddRange(linhasAdd.ToArray());
                            tabelaCanais.AutoSizeColumnsMode = modoAnt; tabelaCanais.ResumeLayout();
                            telaTestador.Close();
                            BaixarImagensInvisivelmente();

                            historicoDoTempo.Clear();
                            futuroDoTempo.Clear();
                            historicoDoTempo.Push(TirarFotoDaTabela());
                            AtualizarStatus("Lista do Testador (Ao Vivo) extraída com sucesso!");

                            MessageBox.Show(ObterTraducao($"Extração concluída! {achados:N0} canais ao Vivo."), ObterTraducao("Extração Cirúrgica"));
                        }
                        else
                        {
                            string urlDl = urlOriginal;
                            if (!urlDl.Contains("type=m3u")) urlDl += "&type=m3u_plus&output=mpegts";
                            lblProgresso.Text = ObterTraducao("⏳ Gerando lista no servidor..."); Application.DoEvents();

                            using (var resp = await cliente.GetAsync(urlDl, HttpCompletionOption.ResponseHeadersRead))
                            {
                                resp.EnsureSuccessStatusCode();
                                long? tam = resp.Content.Headers.ContentLength;
                                using (var stream = await resp.Content.ReadAsStreamAsync())
                                using (var mem = new MemoryStream())
                                {
                                    byte[] buf = new byte[8192]; int lidos; long total = 0; int ult = 0;
                                    while ((lidos = await stream.ReadAsync(buf, 0, buf.Length)) > 0)
                                    {
                                        mem.Write(buf, 0, lidos); total += lidos;
                                        if (Environment.TickCount - ult > 200)
                                        {
                                            lblProgresso.Text = tam.HasValue
                                                ? ObterTraducao($"⏳ {(((double)total / tam.Value) * 100):F1}%")
                                                : ObterTraducao($"⏳ {(total / 1024.0 / 1024.0):F2} MB");
                                            Application.DoEvents(); ult = Environment.TickCount;
                                        }
                                    }
                                    string[] linhasArq = Encoding.UTF8.GetString(mem.ToArray()).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                                    telaTestador.Close();

                                    tabelaCanais.SuspendLayout(); tabelaCanais.Rows.Clear();
                                    foreach (DataGridViewColumn col in tabelaCanais.Columns) if (col is DataGridViewImageColumn ic) ic.DefaultCellStyle.NullValue = new Bitmap(1, 1);
                                    var modoAnt = tabelaCanais.AutoSizeColumnsMode; tabelaCanais.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

                                    var canais = ParsearM3U(linhasArq);
                                    AdicionarCanaisNaTabela(canais);
                                    tabelaCanais.AutoSizeColumnsMode = modoAnt; tabelaCanais.ResumeLayout();
                                    BaixarImagensInvisivelmente();

                                    historicoDoTempo.Clear();
                                    futuroDoTempo.Clear();
                                    historicoDoTempo.Push(TirarFotoDaTabela());
                                    AtualizarStatus("Lista Completa do Testador carregada com sucesso!");

                                    MessageBox.Show(ObterTraducao($"Lista Completa carregada! {canais.Count:N0} conteúdos."), ObterTraducao("Download Concluído"));
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ObterTraducao("Erro ao extrair: ") + ex.Message);
                    lblProgresso.Text = ObterTraducao("❌ Erro.");
                    AtualizarStatus("Erro ao extrair lista do Testador.");
                }
                btnAbrirTela.Enabled = true; btnSalvarFile.Enabled = true; btnTestar.Enabled = true;
            };

            btnSalvarFile.Click += async (s, ev) =>
            {
                if (gridResultados.CurrentRow == null || !gridResultados.CurrentRow.Cells["Status"].Value.ToString().Contains("ATIVA"))
                { MessageBox.Show(ObterTraducao("Selecione uma lista ATIVA.")); return; }

                SaveFileDialog sfd = new SaveFileDialog() { Filter = "Lista IPTV (*.m3u)|*.m3u", FileName = "Lista_Premium_Resgatada.m3u" };
                if (sfd.ShowDialog() != DialogResult.OK) return;

                string urlDl = gridResultados.CurrentRow.Cells["Url"].Value.ToString();
                if (!urlDl.Contains("type=m3u")) urlDl += "&type=m3u_plus&output=mpegts";

                btnSalvarFile.Enabled = false; btnAbrirTela.Enabled = false; btnTestar.Enabled = false;
                lblProgresso.Text = ObterTraducao("⏳ Gerando lista no servidor..."); Application.DoEvents();

                try
                {
                    var mani3 = new HttpClientHandler() { ServerCertificateCustomValidationCallback = (_, __, ___, ____) => true, AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate };
                    using (var cliente = new HttpClient(mani3))
                    {
                        cliente.Timeout = TimeSpan.FromMinutes(15);
                        cliente.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0");

                        using (var resp = await cliente.GetAsync(urlDl, HttpCompletionOption.ResponseHeadersRead))
                        {
                            resp.EnsureSuccessStatusCode();
                            long? tam = resp.Content.Headers.ContentLength;
                            using (var stream = await resp.Content.ReadAsStreamAsync())
                            using (var arq = new FileStream(sfd.FileName, FileMode.Create, FileAccess.Write))
                            {
                                byte[] buf = new byte[8192]; int lidos; long total = 0; int ult = 0;
                                while ((lidos = await stream.ReadAsync(buf, 0, buf.Length)) > 0)
                                {
                                    arq.Write(buf, 0, lidos); total += lidos;
                                    if (Environment.TickCount - ult > 200)
                                    {
                                        lblProgresso.Text = tam.HasValue
                                            ? ObterTraducao($"⏳ Guardando: {(((double)total / tam.Value) * 100):F1}%")
                                            : ObterTraducao($"⏳ {(total / 1024.0 / 1024.0):F2} MB");
                                        Application.DoEvents(); ult = Environment.TickCount;
                                    }
                                }
                            }
                        }
                    }
                    lblProgresso.Text = ObterTraducao("✅ Arquivo salvo com sucesso!");
                    MessageBox.Show(ObterTraducao("Lista salva no seu computador!"), ObterTraducao("Concluído"));
                }
                catch (Exception ex) { MessageBox.Show(ObterTraducao("Erro ao salvar: ") + ex.Message); lblProgresso.Text = ObterTraducao("❌ Erro."); }

                btnSalvarFile.Enabled = true; btnAbrirTela.Enabled = true; btnTestar.Enabled = true;
            };

            TraduzirTelaDinamica(telaTestador);
            telaTestador.ShowDialog();
        }

        private void monitorTécnicoPlayInfoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (tabelaCanais.SelectedRows.Count == 0 || tabelaCanais.SelectedRows[0].IsNewRow) return;
            string url = tabelaCanais.SelectedRows[0].Cells["Url"].Value?.ToString() ?? "";

            // ✨ CORREÇÃO: Padrões envelopados
            string nome = tabelaCanais.SelectedRows[0].Cells["NomeCanal"].Value?.ToString() ?? ObterTraducao("Canal");
            string epgId = tabelaCanais.SelectedRows[0].Cells["EpgId"].Value?.ToString() ?? ObterTraducao("Sem EPG");

            if (string.IsNullOrWhiteSpace(url)) { MessageBox.Show(ObterTraducao("Este canal não tem link válido.")); return; }

            LibVLCSharp.Shared.Core.Initialize();

            // ✨ CORREÇÃO: Título envelopado
            Form telaPlay = new Form() { Width = 900, Height = 650, Text = ObterTraducao($"📡 Monitor: {nome}"), StartPosition = FormStartPosition.CenterScreen, BackColor = Color.Black, FormBorderStyle = FormBorderStyle.FixedSingle, MaximizeBox = false };

            Panel painelInfo = new Panel() { Height = 140, Dock = DockStyle.Bottom, BackColor = Color.FromArgb(30, 30, 30) };

            // ✨ CORREÇÃO: Labels iniciais envelopados
            Label lblEpg = new Label() { Left = 20, Top = 15, Width = 850, ForeColor = Color.LightSkyBlue, Font = new Font("Consolas", 11, FontStyle.Bold), Text = ObterTraducao($"📋 EPG ID: {epgId}") };
            Label lblEpgDesc = new Label() { Left = 20, Top = 40, Width = 850, ForeColor = Color.LightGray, Font = new Font("Consolas", 9), Text = "" };
            Label lblVideo = new Label() { Left = 20, Top = 70, Width = 850, ForeColor = Color.LimeGreen, Font = new Font("Consolas", 10), Text = ObterTraducao("🎞️ Vídeo: Analisando stream...") };
            Label lblAudio = new Label() { Left = 20, Top = 100, Width = 850, ForeColor = Color.Orange, Font = new Font("Consolas", 10), Text = ObterTraducao("🔊 Áudio: Analisando stream...") };

            painelInfo.Controls.Add(lblEpg); painelInfo.Controls.Add(lblEpgDesc); painelInfo.Controls.Add(lblVideo); painelInfo.Controls.Add(lblAudio);

            var libVLC = new LibVLCSharp.Shared.LibVLC("--no-mouse-events", "--no-video-title-show");
            var mediaPlayer = new LibVLCSharp.Shared.MediaPlayer(libVLC);
            var videoView = new LibVLCSharp.WinForms.VideoView() { MediaPlayer = mediaPlayer, Dock = DockStyle.Fill };

            telaPlay.Controls.Add(videoView);
            telaPlay.Controls.Add(painelInfo);

            mediaPlayer.Playing += (sender2, args) =>
            {
                if (telaPlay.IsDisposed || !telaPlay.IsHandleCreated) return;
                telaPlay.Invoke(new Action(() =>
                {
                    Task.Delay(1500).ContinueWith(t =>
                    {
                        if (telaPlay.IsDisposed || !telaPlay.IsHandleCreated) return;
                        telaPlay.Invoke(new Action(() =>
                        {
                            var tracks = mediaPlayer.Media?.Tracks;
                            if (tracks == null) return;
                            foreach (var track in tracks)
                            {
                                if (track.TrackType == LibVLCSharp.Shared.TrackType.Video)
                                {
                                    var v = track.Data.Video;
                                    byte[] bc = BitConverter.GetBytes(track.Codec);
                                    string codec = Encoding.ASCII.GetString(bc).ToLower();
                                    string cd = codec.Contains("h264") ? "H.264 (AVC)" : codec.Contains("hevc") ? "H.265 (HEVC)" : codec.ToUpper();
                                    lblVideo.Text = $"🎞️ {v.Width}x{v.Height}  |  {cd}  |  {v.FrameRateNum / (float)v.FrameRateDen:F2} FPS";
                                }
                                else if (track.TrackType == LibVLCSharp.Shared.TrackType.Audio)
                                {
                                    var a = track.Data.Audio;
                                    // ✨ CORREÇÃO: Envelopado os termos de áudio
                                    string canais2 = a.Channels == 2 ? ObterTraducao("Estéreo (2.0)") : a.Channels >= 6 ? ObterTraducao("Surround (5.1+)") : ObterTraducao("Mono (1.0)");
                                    string ca = Encoding.ASCII.GetString(BitConverter.GetBytes(track.Codec)).ToUpper();
                                    lblAudio.Text = $"🔊 {ca}  |  {canais2}  |  {a.Rate} Hz";
                                }
                            }
                        }));
                    });
                }));
            };

            telaPlay.FormClosing += (s2, ev2) => { mediaPlayer.Stop(); mediaPlayer.Dispose(); libVLC.Dispose(); };

            Task.Run(async () =>
            {
                try
                {
                    var mUrl = System.Text.RegularExpressions.Regex.Match(url, @"^(https?://[^/]+(?::\d+)?)/(?:live/)?([^/]+)/([^/]+)/([^.]+)\.");
                    if (!mUrl.Success) return;

                    string api = $"{mUrl.Groups[1].Value}/player_api.php?username={mUrl.Groups[2].Value}&password={mUrl.Groups[3].Value}&action=get_short_epg&stream_id={mUrl.Groups[4].Value}";
                    using (var http = new HttpClient())
                    {
                        http.Timeout = TimeSpan.FromSeconds(5);
                        string json = await http.GetStringAsync(api);
                        var mTitle = System.Text.RegularExpressions.Regex.Match(json, @"\""title\""[^\""]*\""([^\""]+)\""");
                        var mDesc = System.Text.RegularExpressions.Regex.Match(json, @"\""description\""[^\""]*\""([^\""]+)\""");

                        if (mTitle.Success && !string.IsNullOrWhiteSpace(mTitle.Groups[1].Value))
                        {
                            Func<string, string> Dec = (txt) =>
                            {
                                if (string.IsNullOrWhiteSpace(txt)) return "";
                                try { return Encoding.UTF8.GetString(Convert.FromBase64String(txt)); }
                                catch { return System.Text.RegularExpressions.Regex.Unescape(txt); }
                            };

                            string titulo = Dec(mTitle.Groups[1].Value);
                            string desc = Dec(mDesc.Success ? mDesc.Groups[1].Value : "");
                            if (desc.Length > 150) desc = desc.Substring(0, 150) + "...";

                            // ✨ CORREÇÃO: "Agora:" envelopado
                            if (!telaPlay.IsDisposed && telaPlay.IsHandleCreated)
                                telaPlay.Invoke(new Action(() => { lblEpg.Text = ObterTraducao($"📺 Agora: {titulo}"); lblEpgDesc.Text = desc; }));
                        }
                    }
                }
                catch
                {
                    // ✨ CORREÇÃO: Erro de conexão envelopado
                    if (!telaPlay.IsDisposed && telaPlay.IsHandleCreated)
                        telaPlay.Invoke(new Action(() => { lblEpg.Text = ObterTraducao($"📋 EPG ID: {epgId} (Erro ao buscar programação)"); }));
                }
            });

            // ✨ A MÁGICA ANTES DA TELA ABRIR
            TraduzirTelaDinamica(telaPlay);
            telaPlay.Show();
            var media = new LibVLCSharp.Shared.Media(libVLC, new Uri(url));
            media.AddOption(":network-caching=1500");
            mediaPlayer.Play(media);
        }

        private void ApagarLogo_Click(object sender, EventArgs e)
        {
            SalvarBackupDoTempo();

            if (tabelaCanais.SelectedRows.Count == 0) return;

            foreach (DataGridViewRow linha in tabelaCanais.SelectedRows)
            {
                if (!linha.IsNewRow)
                {
                    // 1. Limpa o link de texto (Coluna Imagem URL)
                    linha.Cells["LogoUrl"].Value = "";

                    // 2. ✨ O PULO DO GATO: Limpa a imagem física (Coluna Logo)
                    // Se não limparmos o Value, o motor de pintura acha que ainda tem foto
                    linha.Cells["FotoCanal"].Value = null;

                    // 3. Opcional: Feedback visual de que foi removido
                    if (tabelaCanais.Columns.Contains("StatusUrl"))
                        linha.Cells["StatusUrl"].Value = ObterTraducao("🗑️ Logo Removida");
                }
            }

            // 4. Avisa a tabela para se redesenhar (agora sem as imagens apagadas)
            tabelaCanais.Invalidate();
        }

        private void ApagarIDdoEPG_Click(object sender, EventArgs e)
        {
            SalvarBackupDoTempo();
            foreach (DataGridViewRow linha in tabelaCanais.SelectedRows)
                if (!linha.IsNewRow) linha.Cells["EpgId"].Value = "";
        }

        // CORREÇÃO: Loop de cima para baixo para detectar duplicados corretamente
        private void LimparDuplicados_Click(object sender, EventArgs e)
        {
            SalvarBackupDoTempo();
            if (tabelaCanais.Rows.Count <= 1) return;

            int apagados = 0, renomeados = 0;
            var urlsVistas = new HashSet<string>();
            var nomesVistos = new Dictionary<string, string>();
            var contagemAlt = new Dictionary<string, int>();
            bool aplicarAltParaTodos = false;
            bool apagarTodosConflitos = false;

            tabelaCanais.SuspendLayout();

            // CORREÇÃO: Iterar de cima para baixo — o primeiro encontrado é o "original"
            for (int i = 0; i < tabelaCanais.Rows.Count; i++)
            {
                DataGridViewRow linha = tabelaCanais.Rows[i];
                if (linha.IsNewRow) continue;

                string url = linha.Cells["Url"].Value?.ToString() ?? "";
                string nome = linha.Cells["NomeCanal"].Value?.ToString() ?? "";
                string cat = linha.Cells["Categoria"].Value?.ToString() ?? "";
                string chave = (nome + "|" + cat).ToLower().Trim();

                if (!string.IsNullOrWhiteSpace(url) && urlsVistas.Contains(url))
                {
                    tabelaCanais.Rows.RemoveAt(i);
                    i--; // Ajusta o índice após remoção
                    apagados++;
                }
                else if (!string.IsNullOrWhiteSpace(nome) && nomesVistos.ContainsKey(chave))
                {
                    if (apagarTodosConflitos)
                    { tabelaCanais.Rows.RemoveAt(i); i--; apagados++; }
                    else if (aplicarAltParaTodos)
                    {
                        if (!contagemAlt.ContainsKey(chave)) contagemAlt[chave] = 1; else contagemAlt[chave]++;
                        linha.Cells["NomeCanal"].Value = $"{nome} [Alt{contagemAlt[chave]}]";
                        linha.DefaultCellStyle.BackColor = Color.LightYellow;
                        renomeados++;
                    }
                    else
                    {
                        // ✨ CORREÇÃO: Envelopado título e todos os controles!
                        using (Form prompt = new Form() { Width = 550, Height = 320, Text = ObterTraducao("⚠️ Conflito de Backup!"), StartPosition = FormStartPosition.CenterScreen, FormBorderStyle = FormBorderStyle.FixedDialog, TopMost = true })
                        {
                            Label lblAv = new Label() { Left = 20, Top = 20, Width = 500, Height = 40, Font = new Font("Segoe UI", 9, FontStyle.Bold), Text = ObterTraducao($"O canal '{nome}' já existe, mas com link diferente. O que deseja fazer?") };
                            Button btnAp = new Button() { Left = 20, Top = 70, Width = 150, Height = 40, Text = ObterTraducao("🗑️ Apagar Cópia"), BackColor = Color.LightCoral, DialogResult = DialogResult.Yes };
                            Button btnAlt = new Button() { Left = 180, Top = 70, Width = 160, Height = 40, Text = ObterTraducao("🏷️ Renomear [Alt]"), BackColor = Color.LightSkyBlue, DialogResult = DialogResult.No };
                            Label lblOu = new Label() { Left = 20, Top = 130, Width = 500, Text = ObterTraducao("Ou digite um nome e clique em Salvar:") };
                            TextBox txtMan = new TextBox() { Left = 20, Top = 160, Width = 320, Text = $"{nome} " + ObterTraducao("(Backup)") };
                            Button btnMan = new Button() { Left = 350, Top = 158, Width = 120, Height = 25, Text = ObterTraducao("💾 Salvar Manual"), DialogResult = DialogResult.OK };
                            CheckBox chkAll = new CheckBox() { Left = 20, Top = 220, Width = 500, Text = ObterTraducao("Repetir esta escolha para todos os próximos conflitos"), Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = Color.DarkRed };

                            prompt.Controls.Add(lblAv); prompt.Controls.Add(btnAp); prompt.Controls.Add(btnAlt);
                            prompt.Controls.Add(lblOu); prompt.Controls.Add(txtMan); prompt.Controls.Add(btnMan);
                            prompt.Controls.Add(chkAll);

                            // ✨ CORREÇÃO: Máquina ativada caso algo passe
                            TraduzirTelaDinamica(prompt);

                            DialogResult escolha = prompt.ShowDialog();
                            if (escolha == DialogResult.Yes)
                            {
                                if (chkAll.Checked) apagarTodosConflitos = true;
                                tabelaCanais.Rows.RemoveAt(i); i--; apagados++;
                            }
                            else if (escolha == DialogResult.No)
                            {
                                if (chkAll.Checked) aplicarAltParaTodos = true;
                                if (!contagemAlt.ContainsKey(chave)) contagemAlt[chave] = 1; else contagemAlt[chave]++;
                                linha.Cells["NomeCanal"].Value = $"{nome} [Alt{contagemAlt[chave]}]";
                                linha.DefaultCellStyle.BackColor = Color.LightYellow;
                                renomeados++;
                            }
                            else if (escolha == DialogResult.OK)
                            {
                                linha.Cells["NomeCanal"].Value = txtMan.Text;
                                linha.DefaultCellStyle.BackColor = Color.LightYellow;
                                renomeados++;
                            }
                            // Se fechar sem escolher: mantém o canal e registra a URL
                        }
                    }
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(url)) urlsVistas.Add(url);
                    if (!string.IsNullOrWhiteSpace(nome)) nomesVistos[chave] = url;
                }
            }

            tabelaCanais.ResumeLayout();
            AtualizarStatus();
            MessageBox.Show(ObterTraducao($"Faxina Inteligente Concluída!\n\n🗑️ {apagados:N0} canais deletados\n🏷️ {renomeados:N0} canais renomeados"), ObterTraducao("Caçador de Duplicatas"));
        }

        private void Desfazer_Click(object sender, EventArgs e)
        {
            // 1. FORÇA A SINCRONIZAÇÃO DA MEMÓRIA
            if (tabelaCanais.IsCurrentCellInEditMode)
            {
                tabelaCanais.EndEdit(DataGridViewDataErrorContexts.Commit);
                tabelaCanais.BindingContext[tabelaCanais.DataSource]?.EndCurrentEdit();
            }
            this.ActiveControl = null;

            if (historicoDoTempo.Count == 0) return;

            futuroDoTempo.Push(TirarFotoDaTabela());

            // Passamos 'false' para ele saber que é Desfazer
            RestaurarFotoDaTabela(historicoDoTempo.Pop(), false);
        }

        private void Refazer_Click(object sender, EventArgs e)
        {
            // 1. FORÇA A SINCRONIZAÇÃO DA MEMÓRIA
            if (tabelaCanais.IsCurrentCellInEditMode)
            {
                tabelaCanais.EndEdit(DataGridViewDataErrorContexts.Commit);
                tabelaCanais.BindingContext[tabelaCanais.DataSource]?.EndCurrentEdit();
            }
            this.ActiveControl = null;

            if (futuroDoTempo.Count == 0) return;

            historicoDoTempo.Push(TirarFotoDaTabela());

            // Passamos 'true' para ele saber que é Refazer
            RestaurarFotoDaTabela(futuroDoTempo.Pop(), true);
        }

        private void GeradordeCatálogo_Click(object sender, EventArgs e)
        {
            if (tabelaCanais.Rows.Count <= 1) { MessageBox.Show(ObterTraducao("A lista está vazia!"), ObterTraducao("Aviso")); return; }

            Form formMsg = new Form() { Width = 450, Height = 380, Text = ObterTraducao("📊 Personalizar Catálogo"), StartPosition = FormStartPosition.CenterScreen, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false };
            Label lblAviso = new Label() { Left = 20, Top = 20, Width = 400, Text = ObterTraducao("Preencha seus dados para o catálogo:"), Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            Label lblEmp = new Label() { Left = 20, Top = 70, Width = 400, Text = ObterTraducao("Nome da sua Empresa / Servidor:") };
            TextBox txtEmp = new TextBox() { Left = 20, Top = 90, Width = 390, Text = ObterTraducao("Top TV - Entretenimento") };
            Label lblCon = new Label() { Left = 20, Top = 125, Width = 400, Text = ObterTraducao("Seu Contato / WhatsApp:") };
            TextBox txtCon = new TextBox() { Left = 20, Top = 145, Width = 390, Text = "(11) 99999-9999" }; // Não precisa traduzir número
            Label lblMsg = new Label() { Left = 20, Top = 180, Width = 400, Text = ObterTraducao("Mensagem para o Cliente (Opcional):") };
            TextBox txtMsg = new TextBox() { Left = 20, Top = 200, Width = 390, Height = 60, Multiline = true, Text = ObterTraducao("Solicite seu teste grátis de 4 horas!") };
            Button btnGerar = new Button() { Text = ObterTraducao("✨ Gerar Catálogo"), Left = 20, Top = 280, Width = 390, Height = 40, BackColor = Color.LightGreen, DialogResult = DialogResult.OK, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            formMsg.Controls.Add(lblAviso); formMsg.Controls.Add(lblEmp); formMsg.Controls.Add(txtEmp);
            formMsg.Controls.Add(lblCon); formMsg.Controls.Add(txtCon);
            formMsg.Controls.Add(lblMsg); formMsg.Controls.Add(txtMsg);
            formMsg.Controls.Add(btnGerar);

            // ✨ A MÁGICA QUE TRADUZ A TELA ANTES DELA ABRIR
            TraduzirTelaDinamica(formMsg);
            if (formMsg.ShowDialog() == DialogResult.OK)
            {
                SaveFileDialog sfd = new SaveFileDialog() { Filter = "Arquivo de Texto (*.txt)|*.txt", Title = ObterTraducao("Salvar Catálogo"), FileName = "Catalogo_Para_Clientes.txt" };
                if (sfd.ShowDialog() != DialogResult.OK) return;

                var catalogo = new Dictionary<string, List<string>>();
                int total = 0;
                foreach (DataGridViewRow linha in tabelaCanais.Rows)
                {
                    if (linha.IsNewRow) continue;
                    string cat = linha.Cells["Categoria"].Value?.ToString() ?? ObterTraducao("Outros");
                    string nome = linha.Cells["NomeCanal"].Value?.ToString() ?? "";
                    if (string.IsNullOrWhiteSpace(nome)) continue;
                    if (!catalogo.ContainsKey(cat)) catalogo[cat] = new List<string>();
                    catalogo[cat].Add(nome);
                    total++;
                }

                var sb = new StringBuilder();
                sb.AppendLine("==================================================");
                sb.AppendLine(ObterTraducao("📺 CATÁLOGO: ") + txtEmp.Text.ToUpper());
                sb.AppendLine(ObterTraducao("📱 Contato: ") + txtCon.Text);
                sb.AppendLine("==================================================");
                sb.AppendLine(ObterTraducao("Total de Canais: ") + total.ToString("N0"));
                sb.AppendLine(ObterTraducao("Gerado via VioFlow"));
                sb.AppendLine("==================================================");
                if (!string.IsNullOrWhiteSpace(txtMsg.Text)) { sb.AppendLine(txtMsg.Text); sb.AppendLine("=================================================="); }
                sb.AppendLine("");

                foreach (var cat in catalogo)
                {
                    sb.AppendLine($"📁 {cat.Key.ToUpper()}");
                    sb.AppendLine("--------------------------------------------------");
                    foreach (string canal in cat.Value)
                        sb.AppendLine($" • {canal.Replace("[Alt1]", "").Replace("[Alt2]", "").Trim()}");
                    sb.AppendLine("");
                }

                try
                {
                    File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                    MessageBox.Show(ObterTraducao($"Catálogo gerado!\n{total:N0} canais catalogados."), ObterTraducao("Sucesso"));
                }
                catch (Exception ex) { MessageBox.Show(ObterTraducao("Erro: ") + ex.Message, ObterTraducao("Aviso")); }
            }
        }

        private void button4_Click_1(object sender, EventArgs e)
        {
            SalvarBackupDoTempo();

            var canaisDoadores = new List<string[]>();

            Form tela = new Form() { Width = 800, Height = 600, Text = ObterTraducao("Central de Transplantes"), StartPosition = FormStartPosition.CenterScreen, FormBorderStyle = FormBorderStyle.FixedDialog };

            Panel pnlInicio = new Panel() { Dock = DockStyle.Fill };
            Panel pnlInjetar = new Panel() { Dock = DockStyle.Fill, Visible = false };
            Panel pnlClonar = new Panel() { Dock = DockStyle.Fill, Visible = false };
            Panel pnlRoubarLogo = new Panel() { Dock = DockStyle.Fill, Visible = false };
            tela.Controls.Add(pnlInicio); tela.Controls.Add(pnlInjetar); tela.Controls.Add(pnlClonar); tela.Controls.Add(pnlRoubarLogo);

            Button btnCarregar = new Button() { Left = 40, Top = 40, Width = 700, Height = 50, Text = ObterTraducao("📂 1. Abrir Lista Secundária (Doadora)"), BackColor = Color.LightGray, Font = new Font("Arial", 11, FontStyle.Bold) };
            Label lblInjTit = new Label() { Left = 40, Top = 120, Width = 300, Font = new Font("Arial", 14, FontStyle.Bold), Text = ObterTraducao("💉 Injetar URL"), ForeColor = Color.DarkRed };
            Label lblInjDesc = new Label() { Left = 40, Top = 155, Width = 320, Height = 60, Font = new Font("Arial", 10), Text = ObterTraducao("Substitui o link quebrado pelo link da lista nova.") };
            Button btnIrInjetar = new Button() { Left = 40, Top = 220, Width = 150, Height = 40, Text = ObterTraducao("Entrar ➔"), Enabled = false, BackColor = Color.LightCoral };
            Label lblCloTit = new Label() { Left = 420, Top = 120, Width = 300, Font = new Font("Arial", 14, FontStyle.Bold), Text = ObterTraducao("🧬 Clonar Novo"), ForeColor = Color.DarkBlue };
            Label lblCloDesc = new Label() { Left = 420, Top = 155, Width = 320, Height = 60, Font = new Font("Arial", 10), Text = ObterTraducao("Mostra canais inéditos para adicionar.") };
            Button btnIrClonar = new Button() { Left = 420, Top = 220, Width = 150, Height = 40, Text = ObterTraducao("Entrar ➔"), Enabled = false, BackColor = Color.LightSkyBlue };
            Label lblRouboTit = new Label() { Left = 40, Top = 300, Width = 300, Font = new Font("Arial", 14, FontStyle.Bold), Text = ObterTraducao("🎨 Roubar Logo"), ForeColor = Color.Goldenrod };
            Label lblRouboDesc = new Label() { Left = 40, Top = 335, Width = 320, Height = 60, Font = new Font("Arial", 10), Text = ObterTraducao("Transfere a logo de um canal doador para o seu.") };
            Button btnIrRoubar = new Button() { Left = 40, Top = 400, Width = 150, Height = 40, Text = ObterTraducao("Entrar ➔"), Enabled = false, BackColor = Color.Gold };

            pnlInicio.Controls.Add(btnCarregar);
            pnlInicio.Controls.Add(lblInjTit); pnlInicio.Controls.Add(lblInjDesc); pnlInicio.Controls.Add(btnIrInjetar);
            pnlInicio.Controls.Add(lblCloTit); pnlInicio.Controls.Add(lblCloDesc); pnlInicio.Controls.Add(btnIrClonar);
            pnlInicio.Controls.Add(lblRouboTit); pnlInicio.Controls.Add(lblRouboDesc); pnlInicio.Controls.Add(btnIrRoubar);

            // --- INJETAR ---
            Button btnVoltarInj = new Button() { Left = 10, Top = 10, Width = 80, Text = ObterTraducao("⬅ Voltar") };
            Label lblFiltro = new Label() { Left = 110, Top = 15, Width = 50, Text = ObterTraducao("Exibir:") };
            ComboBox comboFiltro = new ComboBox() { Left = 160, Top = 12, Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
            comboFiltro.Items.AddRange(new object[] { ObterTraducao("Todos os Meus Canais"), ObterTraducao("Apenas Canais OFF (🔴)"), ObterTraducao("Apenas Sem URL") });
            comboFiltro.SelectedIndex = 0;
            DataGridView gridPrin = new DataGridView() { Left = 10, Top = 45, Width = 370, Height = 430, SelectionMode = DataGridViewSelectionMode.FullRowSelect, ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false };
            gridPrin.Columns.Add("IndexReal", ObterTraducao("Index")); gridPrin.Columns[0].Visible = false;
            gridPrin.Columns.Add("Nome", ObterTraducao("Seu Canal Principal")); gridPrin.Columns[1].Width = 220;
            gridPrin.Columns.Add("Status", ObterTraducao("Status")); gridPrin.Columns[2].Width = 120;
            Label lblBscSec = new Label() { Left = 400, Top = 15, Width = 150, Text = ObterTraducao("Buscar Doador:") };
            TextBox txtBscSec = new TextBox() { Left = 400, Top = 35, Width = 370 };
            DataGridView gridSec = new DataGridView() { Left = 400, Top = 65, Width = 370, Height = 410, SelectionMode = DataGridViewSelectionMode.FullRowSelect, ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false };
            gridSec.Columns.Add("Nome", ObterTraducao("Canal Doador")); gridSec.Columns[0].Width = 200;
            gridSec.Columns.Add("Url", ObterTraducao("Link")); gridSec.Columns[1].Width = 150;
            Button btnExecInjetar = new Button() { Left = 10, Top = 485, Width = 760, Height = 40, Text = ObterTraducao("💉 Injetar Link do Doador no Meu Canal"), BackColor = Color.LightGreen, Font = new Font("Arial", 10, FontStyle.Bold) };
            pnlInjetar.Controls.Add(btnVoltarInj); pnlInjetar.Controls.Add(lblFiltro); pnlInjetar.Controls.Add(comboFiltro);
            pnlInjetar.Controls.Add(gridPrin); pnlInjetar.Controls.Add(lblBscSec); pnlInjetar.Controls.Add(txtBscSec);
            pnlInjetar.Controls.Add(gridSec); pnlInjetar.Controls.Add(btnExecInjetar);

            // --- CLONAR ---
            Button btnVoltarClo = new Button() { Left = 10, Top = 10, Width = 80, Text = ObterTraducao("⬅ Voltar") };
            Label lblTituloClo = new Label() { Left = 110, Top = 15, Width = 400, Text = ObterTraducao("Canais INÉDITOS:"), Font = new Font("Arial", 9, FontStyle.Bold) };
            CheckBox chkIgnorar = new CheckBox() { Left = 110, Top = 35, Width = 350, Text = ObterTraducao("Ignorar Qualidades ao comparar"), Checked = true };
            TextBox txtBscClo = new TextBox() { Left = 520, Top = 12, Width = 250 };
            DataGridView gridNovos = new DataGridView() { Left = 10, Top = 65, Width = 760, Height = 410, SelectionMode = DataGridViewSelectionMode.FullRowSelect, ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false };
            gridNovos.Columns.Add("Nome", ObterTraducao("Canal Inédito")); gridNovos.Columns[0].Width = 300;
            gridNovos.Columns.Add("Url", ObterTraducao("URL")); gridNovos.Columns[1].Width = 430;
            gridNovos.Columns.Add("LogoUrl", ObterTraducao("Logo")); gridNovos.Columns[2].Visible = false;
            Button btnExecClonar = new Button() { Left = 10, Top = 485, Width = 760, Height = 40, Text = ObterTraducao("🧬 Puxar Canal para Minha Lista"), BackColor = Color.LightSkyBlue, Font = new Font("Arial", 10, FontStyle.Bold) };
            pnlClonar.Controls.Add(btnVoltarClo); pnlClonar.Controls.Add(lblTituloClo); pnlClonar.Controls.Add(chkIgnorar);
            pnlClonar.Controls.Add(txtBscClo); pnlClonar.Controls.Add(gridNovos); pnlClonar.Controls.Add(btnExecClonar);

            // =====================================================================
            // --- ROUBAR LOGO ---
            // =====================================================================

            Button btnVoltarRoubo = new Button() { Left = 10, Top = 10, Width = 80, Text = ObterTraducao("⬅ Voltar") };

            ComboBox comboFiltroLogo = new ComboBox() { Left = 100, Top = 12, Width = 180, DropDownStyle = ComboBoxStyle.DropDownList };
            comboFiltroLogo.Items.AddRange(new object[] { ObterTraducao("Todos os Meus Canais"), ObterTraducao("Apenas Canais SEM Logo") });
            comboFiltroLogo.SelectedIndex = 0;

            DataGridView gridPrinLogo = new DataGridView()
            {
                Left = 10,
                Top = 45,
                Width = 350,
                Height = 430,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                ReadOnly = true,
                AllowUserToAddRows = false,
                RowHeadersVisible = false
            };
            gridPrinLogo.Columns.Add("IndexReal", ObterTraducao("Index")); gridPrinLogo.Columns[0].Visible = false;
            gridPrinLogo.Columns.Add("LogoAtual", ObterTraducao("Logo Atual")); gridPrinLogo.Columns[1].Visible = false;
            gridPrinLogo.Columns.Add("Nome", ObterTraducao("Seu Canal")); gridPrinLogo.Columns[2].Width = 220;
            gridPrinLogo.Columns.Add("TemLogo", ObterTraducao("Status")); gridPrinLogo.Columns[3].Width = 90;

            Label lblAntes = new Label() { Left = 362, Top = 48, Width = 60, Height = 15, Text = ObterTraducao("Atual:"), Font = new Font("Arial", 7, FontStyle.Bold), ForeColor = Color.DimGray, TextAlign = ContentAlignment.MiddleCenter };
            PictureBox picAtual = new PictureBox()
            {
                Left = 362,
                Top = 63,
                Width = 60,
                Height = 60,
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.DimGray
            };
            Label lblSeta = new Label() { Left = 362, Top = 128, Width = 60, Height = 20, Text = ObterTraducao("⬇ Nova"), Font = new Font("Arial", 7, FontStyle.Bold), ForeColor = Color.Goldenrod, TextAlign = ContentAlignment.MiddleCenter };
            PictureBox picPreview = new PictureBox()
            {
                Left = 362,
                Top = 148,
                Width = 60,
                Height = 60,
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.DimGray
            };

            TextBox txtBscSecLogo = new TextBox() { Left = 432, Top = 10, Width = 338, Height = 22 };
            Label lblBscLogoHint = new Label() { Left = 432, Top = 33, Width = 338, Height = 14, Text = ObterTraducao("🔍 Digite para filtrar canais doadores"), ForeColor = Color.Gray, Font = new Font("Arial", 7, FontStyle.Italic) };
            DataGridView gridSecLogo = new DataGridView()
            {
                Left = 432,
                Top = 48,
                Width = 338,
                Height = 427,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                ReadOnly = true,
                AllowUserToAddRows = false,
                RowHeadersVisible = false
            };
            gridSecLogo.Columns.Add("Nome", ObterTraducao("Canal Doador")); gridSecLogo.Columns[0].Width = 160;
            gridSecLogo.Columns.Add("UrlLogo", ObterTraducao("Link da Imagem")); gridSecLogo.Columns[1].Width = 168;

            Button btnExecRoubar = new Button()
            {
                Left = 10,
                Top = 485,
                Width = 760,
                Height = 40,
                Text = ObterTraducao("🎨 Roubar e Aplicar Logo no Meu Canal"),
                BackColor = Color.Gold,
                Font = new Font("Arial", 10, FontStyle.Bold)
            };

            pnlRoubarLogo.Controls.Add(btnVoltarRoubo);
            pnlRoubarLogo.Controls.Add(comboFiltroLogo);
            pnlRoubarLogo.Controls.Add(gridPrinLogo);
            pnlRoubarLogo.Controls.Add(lblAntes);
            pnlRoubarLogo.Controls.Add(picAtual);
            pnlRoubarLogo.Controls.Add(lblSeta);
            pnlRoubarLogo.Controls.Add(picPreview);
            pnlRoubarLogo.Controls.Add(txtBscSecLogo);
            pnlRoubarLogo.Controls.Add(lblBscLogoHint);
            pnlRoubarLogo.Controls.Add(gridSecLogo);
            pnlRoubarLogo.Controls.Add(btnExecRoubar);

            // =====================================================================
            // MOTOR DE IMAGEM ANTI-FANTASMA
            // =====================================================================
            System.Threading.CancellationTokenSource ctsAtual = null;
            System.Threading.CancellationTokenSource ctsPreview = null;

            async void CarregarImagem(PictureBox pic, string url, bool isAtual)
            {
                var novoToken = new System.Threading.CancellationTokenSource();

                if (isAtual)
                {
                    ctsAtual?.Cancel();
                    ctsAtual?.Dispose();
                    ctsAtual = novoToken;
                }
                else
                {
                    ctsPreview?.Cancel();
                    ctsPreview?.Dispose();
                    ctsPreview = novoToken;
                }

                pic.Image = null;

                if (string.IsNullOrWhiteSpace(url) || !url.StartsWith("http")) return;

                if (cacheDeLogos.ContainsKey(url))
                {
                    pic.Image = cacheDeLogos[url].Imagem;
                    return;
                }

                try
                {
                    byte[] dados = await _httpClient.GetByteArrayAsync(url).ConfigureAwait(false);

                    if (novoToken.IsCancellationRequested) return;

                    var ms = new MemoryStream(dados);
                    var img = Image.FromStream(ms);

                    if (cacheDeLogos.Count < LIMITE_CACHE_LOGOS)
                        cacheDeLogos[url] = (img, ms);

                    if (!novoToken.IsCancellationRequested && !pic.IsDisposed)
                        pic.Invoke(new Action(() => { if (!pic.IsDisposed) pic.Image = img; }));
                }
                catch { /* Ignora silenciosamente */ }
            }

            // =====================================================================
            // EVENTOS
            // =====================================================================

            btnCarregar.Click += (s, ev) =>
            {
                OpenFileDialog ofd = new OpenFileDialog() { Filter = "Listas IPTV|*.m3u;*.m3u8" };
                if (ofd.ShowDialog() != DialogResult.OK) return;

                canaisDoadores.Clear();
                string[] linhas = File.ReadAllLines(ofd.FileName);
                string nomeNovo = "", extinfSalvo = "";
                foreach (string l in linhas)
                {
                    string lin = l.Trim();
                    if (lin.StartsWith("#EXTINF"))
                    {
                        extinfSalvo = lin;
                        int idx = lin.LastIndexOf(',');
                        if (idx != -1) nomeNovo = lin.Substring(idx + 1).Trim();
                    }
                    else if (!lin.StartsWith("#") && !string.IsNullOrEmpty(nomeNovo))
                    {
                        canaisDoadores.Add(new string[] { nomeNovo, lin, extinfSalvo });
                        nomeNovo = ""; extinfSalvo = "";
                    }
                }
                btnCarregar.Text = ObterTraducao($"✅ Lista Carregada! ({canaisDoadores.Count:N0} canais)");
                btnCarregar.BackColor = Color.LightGreen;
                btnIrInjetar.Enabled = true; btnIrClonar.Enabled = true; btnIrRoubar.Enabled = true;
            };

            btnVoltarInj.Click += (s, ev) => { pnlInjetar.Visible = false; pnlInicio.Visible = true; };
            btnVoltarClo.Click += (s, ev) => { pnlClonar.Visible = false; pnlInicio.Visible = true; };
            btnVoltarRoubo.Click += (s, ev) =>
            {
                ctsAtual?.Cancel();
                ctsPreview?.Cancel();
                pnlRoubarLogo.Visible = false;
                pnlInicio.Visible = true;
            };

            Action carregarPrincipal = () =>
            {
                gridPrin.Rows.Clear();
                int filtro = comboFiltro.SelectedIndex;
                foreach (DataGridViewRow linha in tabelaCanais.Rows)
                {
                    if (linha.IsNewRow) continue;
                    string status = tabelaCanais.Columns.Contains("StatusUrl") ? linha.Cells["StatusUrl"].Value?.ToString() ?? "" : "";
                    string urlC = linha.Cells["Url"].Value?.ToString() ?? "";
                    string nomeC = linha.Cells["NomeCanal"].Value?.ToString() ?? "";
                    bool mostrar = filtro == 0 || (filtro == 1 && status.Contains("OFF")) || (filtro == 2 && string.IsNullOrWhiteSpace(urlC));
                    if (mostrar && !string.IsNullOrWhiteSpace(nomeC)) gridPrin.Rows.Add(linha.Index, nomeC, status);
                }
            };

            btnIrInjetar.Click += (s, ev) => { pnlInicio.Visible = false; pnlInjetar.Visible = true; carregarPrincipal(); };
            comboFiltro.SelectedIndexChanged += (s, ev) => carregarPrincipal();

            gridPrin.SelectionChanged += (s, ev) =>
            {
                if (gridPrin.CurrentRow != null)
                    txtBscSec.Text = System.Text.RegularExpressions.Regex.Replace(
                        gridPrin.CurrentRow.Cells["Nome"].Value.ToString(), @"\[.*?\]", "").Trim();
            };

            txtBscSec.TextChanged += (s, ev) =>
            {
                gridSec.Rows.Clear();
                string termo = txtBscSec.Text.ToLower();
                foreach (var c in canaisDoadores)
                    if (c[0].ToLower().Contains(termo)) gridSec.Rows.Add(c[0], c[1]);
            };

            btnExecInjetar.Click += (s, ev) =>
            {
                if (gridPrin.CurrentRow == null) { MessageBox.Show(ObterTraducao("Selecione o SEU canal (na lista da esquerda)."), ObterTraducao("Aviso")); return; }
                if (gridSec.CurrentRow == null) { MessageBox.Show(ObterTraducao("Selecione o CANAL DOADOR (na lista da direita)."), ObterTraducao("Aviso")); return; }
                SalvarBackupDoTempo();
                try
                {
                    int idxReal = Convert.ToInt32(gridPrin.CurrentRow.Cells["IndexReal"].Value);
                    string nUrl = gridSec.CurrentRow.Cells["Url"].Value?.ToString() ?? "";
                    tabelaCanais.Rows[idxReal].Cells["Url"].Value = nUrl;
                    if (tabelaCanais.Columns.Contains("StatusUrl"))
                        tabelaCanais.Rows[idxReal].Cells["StatusUrl"].Value = ObterTraducao("💉 Link Injetado");
                    tabelaCanais.Rows[idxReal].DefaultCellStyle.BackColor = Color.LightYellow;
                    gridPrin.CurrentRow.Cells["Status"].Value = ObterTraducao("✅ Injetado");
                    gridPrin.CurrentRow.DefaultCellStyle.BackColor = Color.LightGreen;
                    MessageBox.Show(ObterTraducao("O link do canal doador foi transplantado com sucesso!"), ObterTraducao("VioFlow - Injeção Concluída"));
                }
                catch (Exception ex) { MessageBox.Show(ObterTraducao("Erro técnico") + ": " + ex.Message, ObterTraducao("Erro técnico")); }
            };

            Action carregarIneditos = () =>
            {
                gridNovos.Rows.Clear();
                var meusBase = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                bool ignorar = chkIgnorar.Checked;

                string ObterBase(string n)
                {
                    string limpo = n.ToLower();
                    if (ignorar)
                    {
                        limpo = System.Text.RegularExpressions.Regex.Replace(limpo, @"\b(sd|hd|fhd|uhd|4k|h265|hevc)\b", "");
                        limpo = System.Text.RegularExpressions.Regex.Replace(limpo, @"\[.*?\]|\(.*?\)", "");
                    }
                    return limpo.Replace(" ", "").Trim();
                }

                foreach (DataGridViewRow linha in tabelaCanais.Rows)
                    if (!linha.IsNewRow && linha.Cells["NomeCanal"].Value != null)
                        meusBase.Add(ObterBase(linha.Cells["NomeCanal"].Value.ToString()));

                string busca = txtBscClo.Text.ToLower();
                foreach (var c in canaisDoadores)
                {
                    if (!meusBase.Contains(ObterBase(c[0])) && c[0].ToLower().Contains(busca))
                    {
                        var mLogo = System.Text.RegularExpressions.Regex.Match(c[2], @"tvg-logo=""(.*?)""");
                        gridNovos.Rows.Add(c[0], c[1], mLogo.Success ? mLogo.Groups[1].Value : "");
                    }
                }
            };

            btnIrClonar.Click += (s, ev) => { pnlInicio.Visible = false; pnlClonar.Visible = true; txtBscClo.Text = ""; carregarIneditos(); };
            txtBscClo.TextChanged += (s, ev) => carregarIneditos();
            chkIgnorar.CheckedChanged += (s, ev) => carregarIneditos();

            btnExecClonar.Click += (s, ev) =>
            {
                if (gridNovos.CurrentRow == null) return;
                SalvarBackupDoTempo();
                string nomeN = gridNovos.CurrentRow.Cells["Nome"].Value?.ToString() ?? "";
                string urlN = gridNovos.CurrentRow.Cells["Url"].Value?.ToString() ?? "";
                string logoC = gridNovos.CurrentRow.Cells["LogoUrl"].Value?.ToString() ?? "";
                tabelaCanais.Rows.Add(null!, null!, logoC, nomeN, "", ObterTraducao("Canais Adicionados"), urlN, ObterTraducao("🧬 Clonado"));
                MessageBox.Show(ObterTraducao($"O canal '{nomeN}' foi clonado com sucesso!"), "VioFlow");
                gridNovos.Rows.Remove(gridNovos.CurrentRow);
                BaixarImagensInvisivelmente();
                if (tabelaCanais.Rows.Count > 1)
                    tabelaCanais.FirstDisplayedScrollingRowIndex = tabelaCanais.Rows.Count - 2;
            };

            // --- LÓGICA DA TELA ROUBAR LOGO ---

            Action carregarPrinLogo = () =>
            {
                gridPrinLogo.Rows.Clear();
                picAtual.Image = null;
                picPreview.Image = null;

                int filtro = comboFiltroLogo.SelectedIndex;
                foreach (DataGridViewRow linha in tabelaCanais.Rows)
                {
                    if (linha.IsNewRow) continue;
                    string nomeL = linha.Cells["NomeCanal"].Value?.ToString() ?? "";
                    string logoL = linha.Cells["LogoUrl"].Value?.ToString() ?? "";
                    bool temLogo = !string.IsNullOrWhiteSpace(logoL) && logoL.StartsWith("http");
                    if (filtro == 1 && temLogo) continue;
                    if (!string.IsNullOrWhiteSpace(nomeL))
                        gridPrinLogo.Rows.Add(linha.Index, logoL, nomeL, temLogo ? ObterTraducao("🖼️ Tem Logo") : ObterTraducao("❌ Sem Logo"));
                }
            };

            btnIrRoubar.Click += (s, ev) =>
            {
                pnlInicio.Visible = false;
                pnlRoubarLogo.Visible = true;
                carregarPrinLogo();
            };
            comboFiltroLogo.SelectedIndexChanged += (s, ev) => carregarPrinLogo();

            // ✨ VARIÁVEL DE TRAVA
            bool montandoGrade = false;

            gridPrinLogo.SelectionChanged += (s, ev) =>
            {
                if (gridPrinLogo.CurrentRow == null) return;

                // 1. Pede a SUA foto primeiro
                string urlLogoAtual = gridPrinLogo.CurrentRow.Cells["LogoAtual"].Value?.ToString() ?? "";
                CarregarImagem(picAtual, urlLogoAtual, true);

                // 2. Digitar na busca dispara a tabela do doador
                txtBscSecLogo.Text = System.Text.RegularExpressions.Regex.Replace(
                    gridPrinLogo.CurrentRow.Cells["Nome"].Value?.ToString() ?? "", @"\[.*?\]", "").Trim();
            };

            txtBscSecLogo.TextChanged += (s, ev) =>
            {
                montandoGrade = true; // 🔒 TRANCA O GATILHO DA TABELA DO DOADOR!

                gridSecLogo.Rows.Clear();
                ctsPreview?.Cancel();
                picPreview.Image = null;

                string termo = txtBscSecLogo.Text.ToLower();
                foreach (var c in canaisDoadores)
                {
                    if (c[0].ToLower().Contains(termo))
                    {
                        var mLogo = System.Text.RegularExpressions.Regex.Match(c[2], @"tvg-logo=""(.*?)""");
                        string logoD = mLogo.Success && !string.IsNullOrWhiteSpace(mLogo.Groups[1].Value) ? mLogo.Groups[1].Value : "";
                        gridSecLogo.Rows.Add(c[0], logoD);
                    }
                }

                montandoGrade = false; // 🔓 DESTRANCA O GATILHO

                if (gridSecLogo.Rows.Count > 0)
                {
                    gridSecLogo.CurrentCell = gridSecLogo.Rows[0].Cells[0];
                    string urlFoto = gridSecLogo.Rows[0].Cells["UrlLogo"].Value?.ToString() ?? "";
                    CarregarImagem(picPreview, urlFoto, false);
                }
            };

            gridSecLogo.SelectionChanged += (s, ev) =>
            {
                // 🛡️ Se o programa estiver montando a grade, IGNORA os falsos cliques!
                if (montandoGrade) return;

                if (gridSecLogo.CurrentRow == null) return;
                string urlFoto = gridSecLogo.CurrentRow.Cells["UrlLogo"].Value?.ToString() ?? "";
                CarregarImagem(picPreview, urlFoto, false);
            };

            btnExecRoubar.Click += (s, ev) =>
            {
                if (gridPrinLogo.CurrentRow == null) { MessageBox.Show(ObterTraducao("Selecione o SEU canal na lista da esquerda."), ObterTraducao("Aviso")); return; }
                if (gridSecLogo.CurrentRow == null) { MessageBox.Show(ObterTraducao("Selecione o CANAL DOADOR na lista da direita."), ObterTraducao("Aviso")); return; }

                string urlImg = gridSecLogo.CurrentRow.Cells["UrlLogo"].Value?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(urlImg)) { MessageBox.Show(ObterTraducao("Este canal doador não tem logo."), ObterTraducao("Sem Logo")); return; }

                SalvarBackupDoTempo();

                int idxReal = Convert.ToInt32(gridPrinLogo.CurrentRow.Cells["IndexReal"].Value);

                // 1. Atualiza a URL
                tabelaCanais.Rows[idxReal].Cells["LogoUrl"].Value = urlImg;

                // ✨ CORREÇÃO AQUI: Apaga a foto velha forçadamente da tabela principal!
                tabelaCanais.Rows[idxReal].Cells["FotoCanal"].Value = null;

                tabelaCanais.Rows[idxReal].DefaultCellStyle.BackColor = Color.LightYellow;

                if (tabelaCanais.Columns.Contains("StatusUrl"))
                    tabelaCanais.Rows[idxReal].Cells["StatusUrl"].Value = ObterTraducao("🎨 Logo Nova");

                gridPrinLogo.CurrentRow.Cells["TemLogo"].Value = ObterTraducao("🖼️ Tem Logo");
                gridPrinLogo.CurrentRow.Cells["LogoAtual"].Value = urlImg;
                gridPrinLogo.CurrentRow.DefaultCellStyle.BackColor = Color.LightYellow;

                picAtual.Image = picPreview.Image;

                BaixarImagensInvisivelmente();

                // ✨ Força a tabela principal a se redesenhar na mesma hora!
                tabelaCanais.InvalidateRow(idxReal);

                MessageBox.Show(ObterTraducao("Logo transplantada com sucesso! O painel 'Atual' foi atualizado."), ObterTraducao("Roubo Concluído"));
            };

            tela.FormClosing += (s, ev) =>
            {
                ctsAtual?.Cancel();
                ctsAtual?.Dispose();
                ctsPreview?.Cancel();
                ctsPreview?.Dispose();
            };

            TraduzirTelaDinamica(tela);
            tela.ShowDialog();
        }

        private void colarNovoCanalCompletoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SalvarBackupDoTempo();
            string texto = Clipboard.GetText().Trim();
            if (!texto.Contains("#EXTINF")) { MessageBox.Show(ObterTraducao("O texto copiado não é um bloco IPTV válido."), ObterTraducao("Aviso")); return; }

            // ✨ CORREÇÃO: Envelopando os nomes padrão caso o bloco não tenha nome ou categoria
            string nome = ObterTraducao("Canal Novo"), url = "", logo = "", cat = ObterTraducao("Sem Categoria"), epg = "";

            foreach (string l in texto.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string lin = l.Trim();
                if (lin.StartsWith("http")) { url = lin; }
                else if (lin.StartsWith("#EXTINF"))
                {
                    int idx = lin.LastIndexOf(','); if (idx != -1) nome = lin.Substring(idx + 1).Trim();
                    var mLogo = System.Text.RegularExpressions.Regex.Match(lin, @"tvg-logo=""(.*?)"""); if (mLogo.Success) logo = mLogo.Groups[1].Value;
                    var mCat = System.Text.RegularExpressions.Regex.Match(lin, @"group-title=""(.*?)"""); if (mCat.Success) cat = mCat.Groups[1].Value;
                    var mEpg = System.Text.RegularExpressions.Regex.Match(lin, @"tvg-id=""(.*?)"""); if (mEpg.Success) epg = mEpg.Groups[1].Value;
                }
            }

            // ✨ A ORDEM CORRETA PARA O SEU DATAGRIDVIEW:
            // 0: CH | 1: Foto (null) | 2: LogoUrl | 3: NomeCanal | 4: EpgId | 5: Categoria | 6: Url | 7: StatusUrl
            tabelaCanais.Rows.Add(
                null!,             // 0. CH (Auto)
                null!,             // 1. FotoCanal (Desenho)
                logo,              // 2. Imagem URL
                nome,              // 3. Nome
                epg,               // 4. ID do EPG
                cat,               // 5. Categoria
                url,               // 6. Link (URL)
                ObterTraducao("✨ Colado Novo")    // ✨ CORREÇÃO: 7. Status Envelopado
            );

            if (tabelaCanais.Rows.Count > 1)
                tabelaCanais.FirstDisplayedScrollingRowIndex = tabelaCanais.Rows.Count - 2;

            BaixarImagensInvisivelmente(); // Tenta baixar a logo do canal que acabou de chegar
        }

        private void copiarCanalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Copiar não modifica dados, não precisa de backup
            if (tabelaCanais.CurrentRow == null || tabelaCanais.CurrentRow.IsNewRow) return;
            string logo = tabelaCanais.CurrentRow.Cells["LogoUrl"].Value?.ToString() ?? "";
            string nome = tabelaCanais.CurrentRow.Cells["NomeCanal"].Value?.ToString() ?? "";
            string epg = tabelaCanais.CurrentRow.Cells["EpgId"].Value?.ToString() ?? "";
            string cat = tabelaCanais.CurrentRow.Cells["Categoria"].Value?.ToString() ?? "";
            string url = tabelaCanais.CurrentRow.Cells["Url"].Value?.ToString() ?? "";
            Clipboard.SetText($"#EXTINF:-1 tvg-id=\"{epg}\" tvg-logo=\"{logo}\" group-title=\"{cat}\",{nome}\r\n{url}");
            MessageBox.Show(ObterTraducao($"Canal '{nome}' copiado para a memória!"), ObterTraducao("Copiado"));
        }

        private void colarCanalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SalvarBackupDoTempo();
            if (tabelaCanais.CurrentRow == null || tabelaCanais.CurrentRow.IsNewRow) return;
            string logo = tabelaCanais.CurrentRow.Cells["LogoUrl"].Value?.ToString() ?? "";
            string nome = tabelaCanais.CurrentRow.Cells["NomeCanal"].Value?.ToString() ?? "";
            string epg = tabelaCanais.CurrentRow.Cells["EpgId"].Value?.ToString() ?? "";
            string cat = tabelaCanais.CurrentRow.Cells["Categoria"].Value?.ToString() ?? "";
            string url = tabelaCanais.CurrentRow.Cells["Url"].Value?.ToString() ?? "";
            int novaLinha = tabelaCanais.Rows.Add(null!, logo, nome + " [Cópia]", epg, cat, url);
            tabelaCanais.ClearSelection();
            tabelaCanais.Rows[novaLinha].Selected = true;
            tabelaCanais.FirstDisplayedScrollingRowIndex = novaLinha;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (tabelaCanais.Rows.Count > 1)
            {
                DialogResult resultado = MessageBox.Show(ObterTraducao("Atenção: Você tem canais carregados no VioFlow!\n\nSe sair agora, edições não salvas serão perdidas.\nDeseja realmente fechar?"), ObterTraducao("VioFlow - Sair sem Salvar?"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
                if (resultado == DialogResult.No) e.Cancel = true;
            }
        }

        private void panel1_Paint(object sender, PaintEventArgs e) { }
        private void panel2_Paint(object sender, PaintEventArgs e) { }
        private void button2_Click(object sender, EventArgs e) { }
        private void button4_Click(object sender, EventArgs e) { }

        private void TabelaCanais_CopiarUnico(object sender, KeyEventArgs e)
        {
            // Verifica se apertou Ctrl + C
            if (e.Control && e.KeyCode == Keys.C)
            {
                // Se estiver editando o texto com o cursor piscando, deixa o Windows trabalhar
                if (tabelaCanais.IsCurrentCellInEditMode) return;

                // Pega exatamente a célula que está com a bordinha de foco
                if (tabelaCanais.CurrentCell != null && tabelaCanais.CurrentCell.Value != null)
                {
                    string textoParaCopiar = tabelaCanais.CurrentCell.Value.ToString();

                    if (!string.IsNullOrWhiteSpace(textoParaCopiar))
                    {
                        Clipboard.Clear();
                        Clipboard.SetText(textoParaCopiar);
                    }
                }

                // Avisa o sistema que o comando já foi 100% resolvido por nós
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private async void verInfoDaContaToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (tabelaCanais.CurrentRow == null || tabelaCanais.CurrentRow.IsNewRow) return;

            string urlOriginal = tabelaCanais.CurrentRow.Cells["Url"].Value?.ToString() ?? "";

            // ✨ CORREÇÃO: Envelopando o nome de fallback
            string nomeCanal = tabelaCanais.CurrentRow.Cells["NomeCanal"].Value?.ToString() ?? ObterTraducao("este canal");

            string user = "", pass = "", host = "";

            // 1. TENTA EXTRAIR USUÁRIO E SENHA
            var matchDireto = System.Text.RegularExpressions.Regex.Match(urlOriginal, @"^(https?://[^/]+)/(?:(?:live|movie|series)/)?([^/]+)/([^/]+)/");
            var matchTags = System.Text.RegularExpressions.Regex.Match(urlOriginal, @"username=([^&]+).*?password=([^&]+)");

            if (matchDireto.Success)
            {
                host = matchDireto.Groups[1].Value;
                user = matchDireto.Groups[2].Value; // Captura o usuário independentemente de ter a pasta /live/ ou não
                pass = matchDireto.Groups[3].Value;
            }
            else if (matchTags.Success)
            {
                host = new Uri(urlOriginal).GetLeftPart(UriPartial.Authority);
                user = matchTags.Groups[1].Value;
                pass = matchTags.Groups[2].Value;
            }
            else
            {
                MessageBox.Show(ObterTraducao("Este link não possui o padrão de usuário/senha de servidores IPTV compatíveis."), ObterTraducao("Aviso"));
                return;
            }

            // 2. MONTA A URL DA API
            string urlApi = $"{host}/player_api.php?username={user}&password={pass}";

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(10);
                    client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0");

                    string json = await client.GetStringAsync(urlApi);

                    // Extrai os dados (usando sua função ExtrairDado)
                    string status = ExtrairDado(json, "\"status\":\"").Replace("\"", "");
                    string expDate = ExtrairDado(json, "\"exp_date\":\"").Replace("\"", "");
                    string maxConn = ExtrairDado(json, "\"max_connections\":\"").Replace("\"", "");
                    string activeConn = ExtrairDado(json, "\"active_cons\":\"").Replace("\"", "");

                    // ✨ CORREÇÃO: "Ilimitado" envelopado
                    string validadeStr = ObterTraducao("Ilimitado");
                    if (!string.IsNullOrEmpty(expDate) && expDate != "null" && long.TryParse(expDate, out long ts))
                    {
                        DateTime data = DateTimeOffset.FromUnixTimeSeconds(ts).DateTime.ToLocalTime();
                        validadeStr = data.ToString("dd/MM/yyyy HH:mm");

                        // ✨ CORREÇÃO: Alerta de vencimento envelopado
                        if (data < DateTime.Now) validadeStr += ObterTraducao(" ⚠️ (EXPIRADA)");
                    }

                    // ✨ CORREÇÃO: Extraí a palavra ATIVA para traduzir certinho
                    string statusFormatado = status.ToLower() == "active" ? ObterTraducao("ATIVA") : status.ToUpper();

                    MessageBox.Show(ObterTraducao(
                     $"📊 INFO DA CONTA - {nomeCanal.ToUpper()}\n\n" +
                     $"👤 Usuário: {user}\n" +
                     $"🟢 Status: {statusFormatado}\n" +
                     $"📅 Vencimento: {validadeStr}\n" +
                     $"📱 Telas: {activeConn} " + ObterTraducao("em uso / Máximo:") + $" {maxConn}\n\n" +
                     $"🏠 Host: {new Uri(host).Host}"),
                     ObterTraducao("VioFlow - Gestão de Contas"),
                     MessageBoxButtons.OK,
                     MessageBoxIcon.Information
                    );
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ObterTraducao($"O servidor não respondeu à consulta.\nDetalhe: {ex.Message}"), ObterTraducao("Erro de Conexão"));
            }
        }
        // 🧹 MOTOR DE FAXINA PROFUNDA DA RAM
        private void LimparMemoriaParaNovaLista()
        {
            // 1. Limpa a tela
            tabelaCanais.SuspendLayout();
            tabelaCanais.Rows.Clear();
            tabelaCanais.ResumeLayout();

            // 2. Limpa a Máquina do Tempo
            historicoDoTempo.Clear();
            futuroDoTempo.Clear();

            // 3. Destrói fisicamente as fotos antigas da memória
            foreach (var entrada in cacheDeLogos.Values)
            {
                entrada.Imagem?.Dispose();
                entrada.Stream?.Dispose();
            }
            cacheDeLogos.Clear();

            // 4. Força o Caminhão de Lixo do Windows a passar AGORA MESMO
            GC.Collect(2, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, true); // Dupla chamada garante limpeza de listas gigantes (LOH)
        }

        private void tabelaCanais_Scroll(object sender, ScrollEventArgs e)
        {
            // A cada "girada" da bolinha do mouse, ele puxa as fotos novas!
            BaixarImagensInvisivelmente();
        }
        // ==========================================
        // 🌍 SISTEMA DE IDIOMAS (NÃO APAGUE)
        // ==========================================
        public int IdiomaAtual = 0; // 0 = PT, 1 = EN, 2 = ES

        private Dictionary<string, string[]> dicionarioSimples = new Dictionary<string, string[]>()
{
    // MENUS DO TOPO E LATERAIS
    { "Abrir Lista", new[] { "Abrir Lista", "Open List", "Abrir Lista" } },
    { "Salvar Lista", new[] { "Salvar Lista", "Save List", "Guardar Lista" } },
    { "Mesclar Lista", new[] { "Mesclar Lista", "Merge List", "Mezclar Lista" } },
    { "Lista Doadora", new[] { "Lista Doadora", "Donor List", "Lista Donante" } },
    { "Configurar EPG", new[] { "Configurar EPG", "Setup EPG", "Configurar EPG" } },
    { "Testar Canais", new[] { "Testar Canais", "Test Channels", "Probar Canales" } },
    { "Testador de M3U", new[] { "Testador de M3U", "M3U Tester", "Probador M3U" } },
    { "Gerador de Catálogo", new[] { "Gerador de Catálogo", "Catalog Gen.", "Generar Catálogo" } },
    { "Apagar Categorias", new[] { "Apagar Categorias", "Del. Categories", "Borrar Categorías" } },
    { "Sobre / Doar", new[] { "Sobre / Doar", "About / Donate", "Info / Donar" } },
    { "Pesquisar", new[] { "Pesquisar", "Search", "Buscar" } },
    { "Desfazer", new[] { "Desfazer", "Undo", "Deshacer" } },
    { "Refazer", new[] { "Refazer", "Redo", "Rehacer" } },
    { "Abrir Link M3U", new[] { "Abrir Link M3U", "Open M3U Link", "Abrir Enlace M3U" } },
    { "Subir Canal", new[] { "Subir Canal", "Move Up", "Subir Canal" } },
    { "Descer Canal", new[] { "Descer Canal", "Move Down", "Bajar Canal" } },
    { "Formatar Nome", new[] { "Formatar Nome", "Format Name", "Formatear Nombre" } },
    { "Apagar Logo", new[] { "Apagar Logo", "Delete Logo", "Borrar Logo" } },
    { "Limpar Duplicados", new[] { "Limpar Duplicados", "Clear Duplicates", "Limpiar Dupl." } },
    { "Exportar Categorias", new[] { "Exportar Categorias", "Export Categories", "Exportar Cat." } },
    { "Apagar ID do EPG", new[] { "Apagar ID do EPG", "Clear EPG ID", "Borrar ID EPG" } },
    { "Ordem das Categorias", new[] { "Ordem das Categorias", "Category Order", "Orden Categorías" } },
    { "Ver Info da Conta", new[] { "Ver Info da Conta", "View Account Info", "Ver Info de Cuenta" } },
    { "Monitor Técnico", new[] { "Monitor Técnico", "Technical Monitor", "Monitor Técnico" } },
    { "Copiar Canal", new[] { "Copiar Canal", "Copy Channel", "Copiar Canal" } },
    { "Colar Canal", new[] { "Colar Canal", "Paste Channel", "Pegar Canal" } },
    { "Colar Novo Canal Completo", new[] { "Colar Novo Canal Completo", "Paste New Channel", "Pegar Nuevo Canal" } },
    { "Trocar URL do Canal", new[] { "Trocar URL do Canal", "Change Channel URL", "Cambiar URL de Canal" } },
    { "Limpar Nome do Canal", new[] { "Limpar Nome do Canal", "Clean Channel Name", "Limpiar Nombre" } },
    { "Mudar Categoria", new[] { "Mudar Categoria", "Change Category", "Cambiar Categoría" } },
    { "Excluir Canais", new[] { "Excluir Canais", "Delete Channels", "Eliminar Canales" } },
    { "Definir EPG Manualmente", new[] { "Definir EPG Manualmente", "Set EPG Manually", "Definir EPG Manual" } },

    // CABEÇALHOS DA TABELA PRINCIPAL
    { "Logo", new[] { "Logo", "Logo", "Logo" } },
    { "Imagem URL", new[] { "Imagem URL", "Image URL", "URL Imagen" } },
    { "Nome", new[] { "Nome", "Name", "Nombre" } },
    { "ID do EPG", new[] { "ID do EPG", "EPG ID", "ID EPG" } },
    { "Categoria", new[] { "Categoria", "Category", "Categoría" } },
    { "Link (URL)", new[] { "Link (URL)", "Link (URL)", "Enlace (URL)" } },
    { "Status da Conta", new[] { "Status da Conta", "Account Status", "Estado Cuenta" } },
    { "Status", new[] { "Status", "Status", "Estado" } },

    // TEXTOS GERAIS DOS POP-UPS E TELAS
    { "Central de Transplantes", new[] { "Central de Transplantes", "Transplant Center", "Centro de Trasplantes" } },
    { "Abrir Lista Secundária", new[] { "Abrir Lista Secundária", "Open Secondary List", "Abrir Lista Secundaria" } },
    { "Injetar URL", new[] { "Injetar URL", "Inject URL", "Inyectar URL" } },
    { "Substitui o link quebrado", new[] { "Substitui o link quebrado", "Replaces the broken link", "Reemplaza enlace roto" } },
    { "Clonar Novo", new[] { "Clonar Novo", "Clone New", "Clonar Nuevo" } },
    { "Mostra canais inéditos", new[] { "Mostra canais inéditos", "Shows new channels", "Muestra canales nuevos" } },
    { "Roubar Logo", new[] { "Roubar Logo", "Steal Logo", "Robar Logo" } },
    { "Transfere a logo", new[] { "Transfere a logo", "Transfers the logo", "Transfiere el logo" } },
    { "Entrar", new[] { "Entrar", "Enter", "Entrar" } },
    { "Voltar", new[] { "Voltar", "Back", "Volver" } },
    { "Exibir:", new[] { "Exibir:", "Show:", "Mostrar:" } },
    { "Todos os Meus Canais", new[] { "Todos os Meus Canais", "All My Channels", "Todos Mis Canales" } },
    { "Apenas Canais OFF", new[] { "Apenas Canais OFF", "Only OFF Channels", "Solo Canales OFF" } },
    { "Apenas Sem URL", new[] { "Apenas Sem URL", "Only Without URL", "Solo Sin URL" } },
    { "Apenas Canais SEM Logo", new[] { "Apenas Canais SEM Logo", "Only Channels WITHOUT Logo", "Solo Canales SIN Logo" } },
    { "Buscar Doador", new[] { "Buscar Doador", "Search Donor", "Buscar Donante" } },
    { "Canal Doador", new[] { "Canal Doador", "Donor Channel", "Canal Donante" } },
    { "Seu Canal Principal", new[] { "Seu Canal Principal", "Your Main Channel", "Tu Canal Principal" } },
    { "Seu Canal", new[] { "Seu Canal", "Your Channel", "Tu Canal" } },
    { "Link da Imagem", new[] { "Link da Imagem", "Image Link", "Enlace Imagen" } },
    { "Injetar Link", new[] { "Injetar Link", "Inject Link", "Inyectar Enlace" } },
    { "Canais INÉDITOS", new[] { "Canais INÉDITOS", "NEW Channels", "Canales NUEVOS" } },
    { "Ignorar Qualidades", new[] { "Ignorar Qualidades", "Ignore Qualities", "Ignorar Calidades" } },
    { "Puxar Canal", new[] { "Puxar Canal", "Pull Channel", "Tirar Canal" } },
    { "Atual:", new[] { "Atual:", "Current:", "Actual:" } },
    { "Nova Logo", new[] { "Nova Logo", "New Logo", "Nuevo Logo" } },
    { "Nova", new[] { "Nova", "New", "Nueva" } },
    { "Roubar e Aplicar Logo", new[] { "Roubar e Aplicar Logo", "Steal and Apply Logo", "Robar y Aplicar Logo" } },
    { "Iniciar Teste", new[] { "Iniciar Teste", "Start Test", "Iniciar Prueba" } },
    { "Abrir na Tela Principal", new[] { "Abrir na Tela Principal", "Open in Main Screen", "Abrir en Pantalla Principal" } },
    { "Salvar .M3U", new[] { "Salvar .M3U", "Save .M3U", "Guardar .M3U" } },
    { "Link Original", new[] { "Link Original", "Original Link", "Enlace Original" } },
    { "Vence em", new[] { "Vence em", "Expires on", "Vence en" } },
    { "Telas (Uso/Máx)", new[] { "Telas (Uso/Máx)", "Screens (Use/Max)", "Pantallas (Uso/Máx)" } },
    { "Personalizar Catálogo", new[] { "Personalizar Catálogo", "Customize Catalog", "Personalizar Catálogo" } },
    { "Organizador de Categorias", new[] { "Organizador de Categorias", "Category Organizer", "Organizador Categorías" } },
    { "Faxina de Categorias", new[] { "Faxina de Categorias", "Category Cleanup", "Limpieza Categorías" } },
    { "Apagar Marcadas", new[] { "Apagar Marcadas", "Delete Checked", "Borrar Marcadas" } },
    { "Cancelar", new[] { "Cancelar", "Cancel", "Cancelar" } },
    { "Confirmar", new[] { "Confirmar", "Confirm", "Confirmar" } },
    { "Fechar", new[] { "Fechar", "Close", "Cerrar" } },
    { "Aplicar Nova Ordem", new[] { "Aplicar Nova Ordem", "Apply New Order", "Aplicar Nuevo Orden" } },
    { "Exportação Expressa", new[] { "Exportação Expressa", "Express Export", "Exportación Exprés" } },
    { "Exportar Marcadas", new[] { "Exportar Marcadas", "Export Checked", "Exportar Marcadas" } },
    { "O que deseja extrair?", new[] { "O que deseja extrair?", "What to extract?", "Qué desea extraer?" } },
    { "O que deseja carregar no VioFlow?", new[] { "O que deseja carregar no VioFlow?", "What to load in VioFlow?", "Qué cargar en VioFlow?" } },
    { "Só TV ao Vivo", new[] { "Só TV ao Vivo", "Live TV Only", "Solo TV en Vivo" } },
    { "Lista Completa", new[] { "Lista Completa", "Full List", "Lista Completa" } },
    { "Extrair Agora", new[] { "Extrair Agora", "Extract Now", "Extraer Ahora" } },
    { "Cole o link (URL) da sua lista IPTV (.m3u):", new[] { "Cole o link (URL) da sua lista IPTV (.m3u):", "Paste the link (URL) of your IPTV list (.m3u):", "Pega el enlace (URL) de tu lista IPTV (.m3u):" } },
    { "Abrir da Web", new[] { "Abrir da Web", "Open from Web", "Abrir desde la Web" } },
    { "O link precisa começar com 'http'.", new[] { "O link precisa começar com 'http'.", "The link must start with 'http'.", "El enlace debe comenzar con 'http'." } },

    // AVISOS E MENSAGENS
    { "A lista está vazia!", new[] { "A lista está vazia!", "The list is empty!", "¡La lista está vacía!" } },
    { "Nenhuma lista carregada", new[] { "Nenhuma lista carregada", "No list loaded", "Ninguna lista cargada" } },
    { "Lendo arquivo...", new[] { "Lendo arquivo...", "Reading file...", "Leyendo archivo..." } },
    { "Lendo arquivo e processando canais...", new[] { "Lendo arquivo e processando canais...", "Reading file and processing channels...", "Leyendo archivo y procesando canales..." } },
    { "Processando canais...", new[] { "Processando canais...", "Processing channels...", "Procesando canales..." } },
    { "Conectando ao servidor...", new[] { "Conectando ao servidor...", "Connecting to server...", "Conectando al servidor..." } },
    { "Analisando Servidor...", new[] { "Analisando Servidor...", "Analyzing Server...", "Analizando Servidor..." } },
    { "Iniciando extração...", new[] { "Iniciando extração...", "Starting extraction...", "Iniciando extracción..." } },
    { "Buscando categorias...", new[] { "Buscando categorias...", "Fetching categories...", "Buscando categorías..." } },
    { "Lista carregada com sucesso!", new[] { "Lista carregada com sucesso!", "List loaded successfully!", "¡Lista cargada con éxito!" } },
    { "Lista exportada com sucesso!", new[] { "Lista exportada com sucesso!", "List exported successfully!", "¡Lista exportada con éxito!" } },
    { "Não há mais resultados", new[] { "Não há mais resultados", "No more results", "No hay más resultados" } },
    { "Canal não encontrado", new[] { "Canal não encontrado", "Channel not found", "Canal no encontrado" } },
    { "Fim da lista!", new[] { "Fim da lista!", "End of list!", "¡Fin de la lista!" } },
    { "Na ordem que está na tela", new[] { "Na ordem que está na tela", "Order shown on screen", "Orden en la pantalla" } },
    { "Na ordem original", new[] { "Na ordem original", "Original file order", "Orden original" } },
    { "Tem certeza que deseja excluir", new[] { "Tem certeza que deseja excluir", "Are you sure you want to delete", "¿Seguro que quieres eliminar" } },
    { "O texto copiado não é um bloco", new[] { "O texto copiado não é um bloco válido", "Copied text is not valid", "El texto copiado no es válido" } },
    { "Renomear Múltiplas Categorias", new[] { "Renomear Múltiplas Categorias", "Rename Multiple Categories", "Renombrar Varias Categorías" } },
    { "Aplicar a Todos", new[] { "Aplicar a Todos", "Apply to All", "Aplicar a Todos" } },
    { "Atenção: Você tem canais carregados", new[] { "Atenção: Você tem canais carregados", "Warning: You have loaded channels", "Atención: Tienes canales cargados" } },
    { "Se sair agora, edições não salvas", new[] { "Se sair agora, edições não salvas serão perdidas", "Unsaved edits will be lost if you exit", "Los cambios no guardados se perderán" } },
    { "Carregamento Concluído!", new[] { "Carregamento Concluído!", "Loading Complete!", "¡Carga Completada!" } },
    { "canais distribuídos em", new[] { "canais distribuídos em", "channels distributed in", "canales distribuidos en" } },
    { "grupos.", new[] { "grupos.", "groups.", "grupos." } },
    { "Nenhum canal compatível", new[] { "Nenhum canal compatível encontrado", "No compatible channel found", "No se encontró ningún canal compatible" } },
    { "Nenhum EPG carregado!", new[] { "Nenhum EPG carregado!", "No EPG loaded!", "¡Ningún EPG cargado!" } },
    { "Não há canais para exportar", new[] { "Não há canais para exportar.", "No channels to export.", "No hay canales para exportar." } },
    { "Não há canais para organizar", new[] { "Não há canais para organizar.", "No channels to organize.", "No hay canales para organizar." } },
    { "Nenhum link encontrado", new[] { "Nenhum link encontrado.", "No link found.", "Ningún enlace encontrado." } },
    { "Este canal não tem link válido", new[] { "Este canal não tem link válido.", "This channel has no valid link.", "Este canal no tiene enlace válido." } },
    { "Selecione uma lista ATIVA", new[] { "Selecione uma lista ATIVA", "Select an ACTIVE list", "Seleccione una lista ACTIVA" } },
    { "Selecione a linha", new[] { "Selecione a linha de pelo menos um canal", "Select at least one channel row", "Seleccione la fila de al menos un canal" } },
    { "padrão de usuário/senha", new[] { "não possui o padrão de usuário/senha", "lacks the user/password pattern", "no tiene el patrón de usuario/contraseña" } },
    { "Erro ao extrair:", new[] { "Erro ao extrair:", "Extraction error:", "Error de extracción:" } },
    { "Erro ao salvar:", new[] { "Erro ao salvar:", "Save error:", "Error al guardar:" } },
    { "🚀 Despejando canais na tela...", new[] { "🚀 Despejando canais na tela...", "🚀 Pouring channels on screen...", "🚀 Vertiendo canales en pantalla..." } },
    { "🚀 Reconstruindo tabela...", new[] { "🚀 Reconstruindo tabela...", "🚀 Rebuilding table...", "🚀 Reconstruyendo tabla..." } },
    { "✅ Conta ATIVA!", new[] { "✅ Conta ATIVA!", "✅ Account ACTIVE!", "✅ ¡Cuenta ACTIVA!" } },
    { "📺 Baixando TV ao Vivo...", new[] { "📺 Baixando TV ao Vivo...", "📺 Downloading Live TV...", "📺 Descargando TV en Vivo..." } },
    { "🚀 Processando canais...", new[] { "🚀 Processando canais...", "🚀 Processing channels...", "🚀 Procesando canales..." } },
    { "🌍 Baixando Lista Completa...", new[] { "🌍 Baixando Lista Completa...", "🌍 Downloading Full List...", "🌍 Descargando Lista Completa..." } },
    { "⏳ Buscando categorias...", new[] { "⏳ Buscando categorias...", "⏳ Fetching categories...", "⏳ Buscando categorías..." } },
    { "⏳ Baixando canais...", new[] { "⏳ Baixando canais...", "⏳ Downloading channels...", "⏳ Descargando canales..." } },
    { "⏳ Gerando lista no servidor...", new[] { "⏳ Gerando lista no servidor...", "⏳ Generating list on server...", "⏳ Generando lista en el servidor..." } },
    { "✅ Copiado com Sucesso!", new[] { "✅ Copiado com Sucesso!", "✅ Copied Successfully!", "✅ ¡Copiado con Éxito!" } },
    { "Aviso", new[] { "Aviso", "Warning", "Aviso" } },
    { "Sucesso", new[] { "Sucesso", "Success", "Éxito" } },
    { "Faxina Concluída", new[] { "Faxina Concluída", "Cleanup Complete", "Limpieza Completada" } },
    { "Limpeza Concluída", new[] { "Limpeza Concluída", "Cleanup Complete", "Limpieza Completada" } },
    { "Troca Concluída", new[] { "Troca Concluída", "Change Complete", "Cambio Completado" } },
    { "Clonagem Concluída", new[] { "Clonagem Concluída", "Cloning Complete", "Clonación Completada" } },
    { "VioFlow EPG", new[] { "VioFlow EPG", "VioFlow EPG", "VioFlow EPG" } },
    { "Memória EPG Pronta", new[] { "Memória EPG Pronta", "EPG Memory Ready", "Memoria EPG Lista" } },
    { "Mapeamento Concluído", new[] { "Mapeamento Concluído", "Mapping Complete", "Mapeo Completado" } },
    { "VioFlow - Cancelado", new[] { "VioFlow - Cancelado", "VioFlow - Cancelled", "VioFlow - Cancelado" } },
    { "VioFlow - Radar Pro", new[] { "VioFlow - Radar Pro", "VioFlow - Radar Pro", "VioFlow - Radar Pro" } },
    { "Mágica Concluída", new[] { "Mágica Concluída", "Magic Complete", "Magia Completada" } },
    { "Exportação Concluída", new[] { "Exportação Concluída", "Export Complete", "Exportación Completada" } },
    { "VioFlow 1.3 - Ordem Aplicada", new[] { "VioFlow 1.3 - Ordem Aplicada", "VioFlow 1.3 - Order Applied", "VioFlow 1.3 - Orden Aplicado" } },
    { "Extração Cirúrgica", new[] { "Extração Cirúrgica", "Surgical Extraction", "Extracción Quirúrgica" } },
    { "Download Concluído", new[] { "Download Concluído", "Download Complete", "Descarga Completada" } },
    { "Concluído", new[] { "Concluído", "Complete", "Completado" } },
    { "Copiado", new[] { "Copiado", "Copied", "Copiado" } },

    // DICIONÁRIO ESTENDIDO (ESTRUTURAS DINÂMICAS)
    { "Filtrando: ", new[] { "Filtrando: ", "Filtering: ", "Filtrando: " } },
    { " Para apagar: ", new[] { " Para apagar: ", " To delete: ", " Para borrar: " } },
    { "Desenhando ", new[] { "Desenhando ", "Drawing ", "Dibujando " } },
    { " canais restantes...", new[] { " canais restantes...", " remaining channels...", " canales restantes..." } },
    { " já existe, mas com link diferente. O que deseja fazer?", new[] { " já existe, mas com link diferente. O que deseja fazer?", " already exists with a different link. What to do?", " ya existe con otro enlace. ¿Qué desea hacer?" } },
    { "(Backup)", new[] { "(Backup)", "(Backup)", "(Copia)" } },
    { "Faxina Inteligente Concluída!", new[] { "Faxina Inteligente Concluída!", "Smart Cleanup Complete!", "¡Limpieza Inteligente Completada!" } },
    { " canais deletados", new[] { " canais deletados", " deleted channels", " canales eliminados" } },
    { " canais renomeados", new[] { " canais renomeados", " renamed channels", " canales renombrados" } },
    { "Caçador de Duplicatas", new[] { "Caçador de Duplicatas", "Duplicate Hunter", "Cazador de Duplicados" } },
    { "Qual texto remover dos nomes? (Ex: [FHD]):", new[] { "Qual texto remover dos nomes? (Ex: [FHD]):", "Text to remove from names? (Ex: [FHD]):", "¿Texto a eliminar de los nombres? (Ej: [FHD]):" } },
    { "Limpar Nomes", new[] { "Limpar Nomes", "Clean Names", "Limpiar Nombres" } },
    { "Cole a NOVA URL completa para este canal:", new[] { "Cole a NOVA URL completa para este canal:", "Paste the NEW full URL for this channel:", "Pega la NUEVA URL completa para este canal:" } },
    { "Tem certeza que deseja excluir os canais selecionados?", new[] { "Tem certeza que deseja excluir os canais selecionados?", "Are you sure you want to delete selected channels?", "¿Seguro que desea eliminar los canales seleccionados?" } },
    { "Confirmação", new[] { "Confirmação", "Confirmation", "Confirmación" } },
    { "Total de canais: ", new[] { "Total de canais: ", "Total channels: ", "Total de canales: " } },
    { "Falta verificar: ", new[] { "Falta verificar: ", "Remaining to check: ", "Falta verificar: " } },
    { "Canais ON: ", new[] { "Canais ON: ", "ON Channels: ", "Canales ON: " } },
    { "Canais OFF: ", new[] { "Canais OFF: ", "OFF Channels: ", "Canales OFF: " } },
    { "Testando: ", new[] { "Testando: ", "Testing: ", "Probando: " } },
    { " listas ativas encontradas.", new[] { " listas ativas encontradas.", " active lists found.", " listas activas encontradas." } },
    { "Guardando: ", new[] { "Guardando: ", "Saving: ", "Guardando: " } },
    { "Baixando lista de EPG da internet... Aguarde.", new[] { "Baixando lista de EPG da internet... Aguarde.", "Downloading EPG list from internet... Wait.", "Descargando lista EPG de internet... Espere." } },
    { "EPG Carregado! ", new[] { "EPG Carregado! ", "EPG Loaded! ", "¡EPG Cargado! " } },
    { " canais disponíveis.", new[] { " canais disponíveis.", " available channels.", " canales disponibles." } },
    { "Fim da lista! A busca recomeçou do topo. (Linha ", new[] { "Fim da lista! A busca recomeçou do topo. (Linha ", "End of list! Search restarted from top. (Row ", "¡Fin de la lista! Búsqueda reiniciada desde arriba. (Fila " } },
    { "Não há mais resultados para esta palavra.", new[] { "Não há mais resultados para esta palavra.", "No more results for this word.", "No hay más resultados para esta palavra." } },
    { "Canal não encontrado na lista.", new[] { "Canal não encontrado na lista.", "Channel not found in list.", "Canal no encontrado en la lista." } },
    { "Canal encontrado na linha ", new[] { "Canal encontrado na linha ", "Channel found at row ", "Canal encontrado en la fila " } },
    { "Editado", new[] { "Editado", "Edited", "Editado" } },
    { "Logo Removida", new[] { "Logo Removida", "Logo Removed", "Logo Eliminado" } },
    { "Testando...", new[] { "Testando...", "Testing...", "Probando..." } },
    { "OFF (Sem Link)", new[] { "OFF (Sem Link)", "OFF (No Link)", "OFF (Sin Enlace)" } },
    { "Inválido (Sem Senha)", new[] { "Inválido (Sem Senha)", "Invalid (No Password)", "Inválido (Sin Contraseña)" } },
    { "OFF (Servidor Caiu)", new[] { "OFF (Servidor Caiu)", "OFF (Server Down)", "OFF (Servidor Caído)" } },
    { "Lista Carregada! (", new[] { "Lista Carregada! (", "List Loaded! (", "¡Lista Cargada! (" } },
    { " canais)", new[] { " canais)", " channels)", " canales)" } },
    { "Selecione o SEU canal (na lista da esquerda).", new[] { "Selecione o SEU canal (na lista da esquerda).", "Select YOUR channel (left list).", "Seleccione SU canal (lista izquierda)." } },
    { "Selecione o CANAL DOADOR (na lista da direita).", new[] { "Selecione o CANAL DOADOR (na lista da direita).", "Select DONOR CHANNEL (right list).", "Seleccione CANAL DONANTE (lista derecha)." } },
    { "Link Injetado", new[] { "Link Injetado", "Link Injected", "Enlace Inyectado" } },
    { "Injetado", new[] { "Injetado", "Injected", "Inyectado" } },
    { "O link do canal doador foi transplantado com sucesso!", new[] { "O link do canal doador foi transplantado com sucesso!", "Donor channel link transplanted successfully!", "¡Enlace del canal donante trasplantado con éxito!" } },
    { "VioFlow - Injeção Concluída", new[] { "VioFlow - Injeção Concluída", "VioFlow - Injection Complete", "VioFlow - Inyección Completada" } },
    { "foi clonado com sucesso!", new[] { " foi clonado com sucesso!", " was cloned successfully!", " fue clonado con éxito!" } },
    { "Tem Logo", new[] { "Tem Logo", "Has Logo", "Tiene Logo" } },
    { "Sem Logo", new[] { "Sem Logo", "No Logo", "Sin Logo" } },
    { "Catálogo gerado!\n", new[] { "Catálogo gerado!\n", "Catalog generated!\n", "¡Catálogo generado!\n" } },
    { " canais catalogados.", new[] { " canais catalogados.", " channels cataloged.", " canales catalogados." } },
    { "Selecione sua Lista", new[] { "Selecione sua Lista", "Select your List", "Seleccione su Lista" } },
    { "⏳ Lendo arquivo e processando canais...", new[] { "⏳ Lendo arquivo e processando canais...", "⏳ Reading file and processing channels...", "⏳ Leyendo archivo y procesando canales..." } },
    { "Preparando a leitura...", new[] { "Preparando a leitura...", "Preparing to read...", "Preparando la lectura..." } },
    { "📺 Canais: ", new[] { "📺 Canais: ", "📺 Channels: ", "📺 Canales: " } },
    { "  |  📁 Grupos: ", new[] { "  |  📁 Grupos: ", "  |  📁 Groups: ", "  |  📁 Grupos: " } },
    { "  |  Linha: ", new[] { "  |  Linha: ", "  |  Line: ", "  |  Línea: " } },
    { "Finalizando: ", new[] { "Finalizando: ", "Finishing: ", "Finalizando: " } },
    { " canais e ", new[] { " canais e ", " channels and ", " canales y " } },
    { " grupos lidos.", new[] { " grupos lidos.", " groups read.", " grupos leídos." } },
    { "Deseja salvar os canais:", new[] { "Deseja salvar os canais:", "Do you want to save the channels:", "¿Desea guardar los canales:" } },
    { "Salvar Lista Exportada", new[] { "Salvar Lista Exportada", "Save Exported List", "Guardar Lista Exportada" } },
    { "Canal Sem Nome", new[] { "Canal Sem Nome", "Unnamed Channel", "Canal Sin Nombre" } },
    { "Selecione a lista NOVA para buscar backups", new[] { "Selecione a lista NOVA para buscar backups", "Select NEW list to search for backups", "Seleccione la NUEVA lista para buscar copias" } },
    { "Sem Categoria", new[] { "Sem Categoria", "Uncategorized", "Sin Categoría" } },
    { "Clonagem Perfeita de Backups", new[] { "Clonagem Perfeita de Backups", "Perfect Backup Cloning", "Clonación Perfecta de Copias" } },
    { "Achamos ", new[] { "Achamos ", "Found ", "Encontramos " } },
    { " links! Eles herdarão a Logo, EPG e Categoria da lista principal:", new[] { " links! Eles herdarão a Logo, EPG e Categoria da lista principal:", " links! They will inherit Logo, EPG, and Category from the main list:", " enlaces! Heredarán el Logo, EPG y Categoría de la lista principal:" } },
    { "] Transformar '", new[] { "] Transformar '", "] Transform '", "] Transformar '" } },
    { "'  -->  em Clone '", new[] { "'  -->  em Clone '", "'  -->  into Clone '", "'  -->  en Clon '" } },
    { "Clonar Selecionados", new[] { "Clonar Selecionados", "Clone Selected", "Clonar Seleccionados" } },
    { "🔗 Backup Adicionado", new[] { "🔗 Backup Adicionado", "🔗 Backup Added", "🔗 Copia Añadida" } },
    { "📂 1. Abrir Lista Secundária (Doadora)", new[] { "📂 1. Abrir Lista Secundária (Doadora)", "📂 1. Open Secondary List (Donor)", "📂 1. Abrir Lista Secundaria (Donante)" } },
    { "💉 Injetar Link do Doador no Meu Canal", new[] { "💉 Injetar Link do Doador no Meu Canal", "💉 Inject Donor Link to My Channel", "💉 Inyectar Enlace Donante en Mi Canal" } },
    { "Canais INÉDITOS:", new[] { "Canais INÉDITOS:", "NEW Channels:", "Canales NUEVOS:" } },
    { "Ignorar Qualidades ao comparar", new[] { "Ignorar Qualidades ao comparar", "Ignore Qualities when comparing", "Ignorar Calidades al comparar" } },
    { "Canal Inédito", new[] { "Canal Inédito", "New Channel", "Canal Nuevo" } },
    { "URL", new[] { "URL", "URL", "URL" } },
    { "🧬 Puxar Canal para Minha Lista", new[] { "🧬 Puxar Canal para Minha Lista", "🧬 Pull Channel to My List", "🧬 Añadir Canal a Mi Lista" } },
    { "Logo Atual", new[] { "Logo Atual", "Current Logo", "Logo Actual" } },
    { "⬇ Nova", new[] { "⬇ Nova", "⬇ New", "⬇ Nueva" } },
    { "🔍 Digite para filtrar canais doadores", new[] { "🔍 Digite para filtrar canais doadores", "🔍 Type to filter donor channels", "🔍 Escribe para filtrar canales donantes" } },
    { "🎨 Roubar e Aplicar Logo no Meu Canal", new[] { "🎨 Roubar e Aplicar Logo no Meu Canal", "🎨 Steal and Apply Logo to My Channel", "🎨 Robar y Aplicar Logo en Mi Canal" } },
    { "Erro técnico", new[] { "Erro técnico", "Technical error", "Error técnico" } },
    { "Canais Adicionados", new[] { "Canais Adicionados", "Added Channels", "Canales Añadidos" } },
    { "🧬 Clonado", new[] { "🧬 Clonado", "🧬 Cloned", "🧬 Clonado" } },
    { "🖼️ Tem Logo", new[] { "🖼️ Tem Logo", "🖼️ Has Logo", "🖼️ Tiene Logo" } },
    { "❌ Sem Logo", new[] { "❌ Sem Logo", "❌ No Logo", "❌ Sin Logo" } },
    { "Selecione o SEU canal na lista da esquerda.", new[] { "Selecione o SEU canal na lista da esquerda.", "Select YOUR channel in the left list.", "Seleccione SU canal en la lista izquierda." } },
    { "Selecione o CANAL DOADOR na lista da direita.", new[] { "Selecione o CANAL DOADOR na lista da direita.", "Select the DONOR CHANNEL in the right list.", "Seleccione el CANAL DONANTE en la lista derecha." } },
    { "Este canal doador não tem logo.", new[] { "Este canal doador não tem logo.", "This donor channel has no logo.", "Este canal donante no tiene logo." } },
    { "🎨 Logo Nova", new[] { "🎨 Logo Nova", "🎨 New Logo", "🎨 Logo Nueva" } },
    { "Logo transplantada com sucesso! O painel 'Atual' foi atualizado.", new[] { "Logo transplantada com sucesso! O painel 'Atual' foi atualizado.", "Logo transplanted successfully! 'Current' panel updated.", "¡Logo trasplantado con éxito! El panel 'Actual' ha sido actualizado." } },
    { "Roubo Concluído", new[] { "Roubo Concluído", "Steal Complete", "Robo Completado" } },
    { "Configurar URLs do EPG", new[] { "Configurar URLs do EPG", "Configure EPG URLs", "Configurar URLs de EPG" } },
    { "URL do EPG 1:", new[] { "URL do EPG 1:", "EPG URL 1:", "URL de EPG 1:" } },
    { "URL do EPG 2 (Opcional):", new[] { "URL do EPG 2 (Opcional):", "EPG URL 2 (Optional):", "URL de EPG 2 (Opcional):" } },
    { "Salvar EPG", new[] { "Salvar EPG", "Save EPG", "Guardar EPG" } },
    { "Mapeamento Manual de EPG", new[] { "Mapeamento Manual de EPG", "Manual EPG Mapping", "Mapeo Manual de EPG" } },
    { "Pesquise e clique na opção correta:", new[] { "Pesquise e clique na opção correta:", "Search and click on the correct option:", "Busque y haga clic en la opción correcta:" } },
    { "Aplicar Selecionado", new[] { "Aplicar Selecionado", "Apply Selected", "Aplicar Seleccionado" } },
    { "Links salvos! O VioFlow vai baixar os guias e preencher os IDs.", new[] { "Links salvos! O VioFlow vai baixar os guias e preencher os IDs.", "Links saved! VioFlow will download guides and fill IDs.", "¡Enlaces guardados! VioFlow descargará las guías y llenará los IDs." } },
    { "Radar VioFlow (Pro)", new[] { "Radar VioFlow (Pro)", "VioFlow Radar (Pro)", "Radar VioFlow (Pro)" } },
    { "📡 Análise Profunda de Stream. Aguarde...", new[] { "📡 Análise Profunda de Stream. Aguarde...", "📡 Deep Stream Analysis. Please wait...", "📡 Análisis Profundo de Stream. Espere..." } },
    { "🟢 Canais ON: 0", new[] { "🟢 Canais ON: 0", "🟢 ON Channels: 0", "🟢 Canales ON: 0" } },
    { "🔴 Canais OFF: 0", new[] { "🔴 Canais OFF: 0", "🔴 OFF Channels: 0", "🔴 Canales OFF: 0" } },
    { "⛔ Cancelar Teste", new[] { "⛔ Cancelar Teste", "⛔ Cancel Test", "⛔ Cancelar Prueba" } },
    { "Cancelando...", new[] { "Cancelando...", "Canceling...", "Cancelando..." } },
    { "🟢 ON", new[] { "🟢 ON", "🟢 ON", "🟢 ON" } },
    { "🔴 OFF", new[] { "🔴 OFF", "🔴 OFF", "🔴 OFF" } },
    { "🕵️‍♂️ Testador de M3U (Xtream Codes)", new[] { "🕵️‍♂️ Testador de M3U (Xtream Codes)", "🕵️‍♂️ M3U Tester (Xtream Codes)", "🕵️‍♂️ Probador M3U (Xtream Codes)" } },
    { "Cole os links M3U abaixo. O VioFlow vai extrair e testar tudo sozinho:", new[] { "Cole os links M3U abaixo. O VioFlow vai extrair e testar tudo sozinho:", "Paste M3U links below. VioFlow will extract and test everything:", "Pegue los enlaces M3U. VioFlow extraerá y probará todo:" } },
    { "Ilimitado", new[] { "Ilimitado", "Unlimited", "Ilimitado" } },
    { "online / Máx:", new[] { "online / Máx:", "online / Max:", "en línea / Máx:" } },
    { "✅ ATIVA", new[] { "✅ ATIVA", "✅ ACTIVE", "✅ ACTIVA" } },
    { "🔍 Filtro de Conteúdo", new[] { "🔍 Filtro de Conteúdo", "🔍 Content Filter", "🔍 Filtro de Contenido" } },
    { "Canais de TV", new[] { "Canais de TV", "TV Channels", "Canales de TV" } },
    { " MB baixados...", new[] { " MB baixados...", " MB downloaded...", " MB descargados..." } },
    { "✅ Arquivo salvo com sucesso!", new[] { "✅ Arquivo salvo com sucesso!", "✅ File saved successfully!", "✅ ¡Archivo guardado con éxito!" } },
    { "❌ Erro.", new[] { "❌ Erro.", "❌ Error.", "❌ Error." } },
    { "📊 Personalizar Catálogo", new[] { "📊 Personalizar Catálogo", "📊 Customize Catalog", "📊 Personalizar Catálogo" } },
    { "Preencha seus dados para o catálogo:", new[] { "Preencha seus dados para o catálogo:", "Fill in your details for the catalog:", "Rellena tus datos para el catálogo:" } },
    { "Nome da sua Empresa / Servidor:", new[] { "Nome da sua Empresa / Servidor:", "Your Company / Server Name:", "Nombre de tu Empresa / Servidor:" } },
    { "Top TV - Entretenimento", new[] { "Top TV - Entretenimento", "Top TV - Entertainment", "Top TV - Entretenimiento" } },
    { "Seu Contato / WhatsApp:", new[] { "Seu Contato / WhatsApp:", "Your Contact / WhatsApp:", "Tu Contacto / WhatsApp:" } },
    { "Mensagem para o Cliente (Opcional):", new[] { "Mensagem para o Cliente (Opcional):", "Message for the Client (Optional):", "Mensaje para el Cliente (Opcional):" } },
    { "Solicite seu teste grátis de 4 horas!", new[] { "Solicite seu teste grátis de 4 horas!", "Request your 4-hour free trial!", "¡Solicite su prueba gratis de 4 horas!" } },
    { "✨ Gerar Catálogo", new[] { "✨ Gerar Catálogo", "✨ Generate Catalog", "✨ Generar Catálogo" } },
    { "Salvar Catálogo", new[] { "Salvar Catálogo", "Save Catalog", "Guardar Catálogo" } },
    { "Outros", new[] { "Outros", "Others", "Otros" } },
    { "📺 CATÁLOGO: ", new[] { "📺 CATÁLOGO: ", "📺 CATALOG: ", "📺 CATÁLOGO: " } },
    { "📱 Contato: ", new[] { "📱 Contato: ", "📱 Contact: ", "📱 Contacto: " } },
    { "Total de Canais: ", new[] { "Total de Canais: ", "Total Channels: ", "Total de Canales: " } },
    { "Gerado via VioFlow", new[] { "Gerado via VioFlow", "Generated via VioFlow", "Generado vía VioFlow" } },
    { "Erro: ", new[] { "Erro: ", "Error: ", "Error: " } },
    { "⚡ VioFlow Turbo: Filtrando Canais...", new[] { "⚡ VioFlow Turbo: Filtrando Canais...", "⚡ VioFlow Turbo: Filtering Channels...", "⚡ VioFlow Turbo: Filtrando Canales..." } },
    { "Preparando ", new[] { "Preparando ", "Preparing ", "Preparando " } },
    { " canais...", new[] { " canais...", " channels...", " canales..." } },
    { "Faxina Turbo Concluída!", new[] { "Faxina Turbo Concluída!", "Turbo Cleanup Complete!", "¡Limpieza Turbo Completada!" } },
    { " canais removidos.", new[] { " canais removidos.", " removed channels.", " canales eliminados." } },
    { " canais mantidos.", new[] { " canais mantidos.", " kept channels.", " canales mantenidos." } },
    { "VioFlow 1.0.0", new[] { "VioFlow 1.0.0", "VioFlow 1.0.0", "VioFlow 1.0.0" } },
    { "Sobre o VioFlow", new[] { "Sobre o VioFlow", "About VioFlow", "Acerca de VioFlow" } },
    { "A ferramenta definitiva para limpar, organizar e testar suas listas IPTV.\n\nDesenvolvido por: VioFlow", new[] { "A ferramenta definitiva para limpar, organizar e testar suas listas IPTV.\n\nDesenvolvido por: VioFlow", "The ultimate tool to clean, organize, and test your IPTV lists.\n\nDeveloped by: VioFlow", "La herramienta definitiva para limpiar, organizar y probar tus listas IPTV.\n\nDesarrollado por: VioFlow" } },
    { "⚠️ AVISO LEGAL: O VioFlow é exclusivamente um editor de texto local. Não fornece, hospeda, vende ou contém nenhum link ou conteúdo de mídia.", new[] { "⚠️ AVISO LEGAL: O VioFlow é exclusivamente um editor de texto local. Não fornece, hospeda, vende ou contém nenhum link ou conteúdo de mídia.", "⚠️ LEGAL NOTICE: VioFlow is exclusively a local text editor. It does not provide, host, sell, or contain any links or media content.", "⚠️ AVISO LEGAL: VioFlow es exclusivamente un editor de texto local. No proporciona, aloja, vende ni contiene ningún enlace o contenido multimedia." } },
    { "🌐 Visite nosso Github", new[] { "🌐 Visite nosso Github", "🌐 Visit our Github", "🌐 Visite nuestro Github" } },
    { "📋 O que há de novo? (Changelog)", new[] { "📋 O que há de novo? (Changelog)", "📋 What's new? (Changelog)", "📋 ¿Qué hay de nuevo? (Changelog)" } },
    { "Curtiu o VioFlow?\r\nApoie com um café ☕ e fortaleça o projeto! 💙", new[] { "Curtiu o VioFlow?\r\nApoie com um café ☕ e fortaleça o projeto! 💙", "Like VioFlow?\r\nSupport with a coffee ☕ and strengthen the project! 💙", "¿Te gusta VioFlow?\r\n¡Apoya con un café ☕ e fortalece el proyecto! 💙" } },
    { "💠 Doar com PIX", new[] { "💠 Doar com PIX", "💠 Donate with PIX", "💠 Donar con PIX" } },
    { "💙 Doar com PayPal", new[] { "💙 Doar com PayPal", "💙 Donate with PayPal", "💙 Donar con PayPal" } },
    { "Apoie o VioFlow com PIX", new[] { "Apoie o VioFlow com PIX", "Support VioFlow with PIX", "Apoya VioFlow con PIX" } },
    { "Doação via PIX 💠", new[] { "Doação via PIX 💠", "Donation via PIX 💠", "Donación vía PIX 💠" } },
    { "Escaneie o QR Code abaixo com o aplicativo do seu banco:", new[] { "Escaneie o QR Code abaixo com o aplicativo do seu banco:", "Scan the QR Code below with your bank app:", "Escanea el Código QR abajo con tu app bancaria:" } },
    { "Ou use o PIX Copia e Cola:", new[] { "Ou use o PIX Copia e Cola:", "Or use PIX Copy and Paste:", "O usa PIX Copia y Pega:" } },
    { "📄 Copiar Código PIX", new[] { "📄 Copiar Código PIX", "📄 Copy PIX Code", "📄 Copiar Código PIX" } },
    { "Apoie o VioFlow com PayPal", new[] { "Apoie o VioFlow com PayPal", "Support VioFlow with PayPal", "Apoya VioFlow con PayPal" } },
    { "Doação via PayPal 💙", new[] { "Doação via PayPal 💙", "Donation via PayPal 💙", "Donación vía PayPal 💙" } },
    { "Escaneie o QR Code abaixo com a câmera do seu celular:", new[] { "Escaneie o QR Code abaixo com a câmera do seu celular:", "Scan the QR Code below with your phone camera:", "Escanea el Código QR abajo con la cámara de tu celular:" } },
    { "Ou clique no botão abaixo para abrir o site:", new[] { "Ou clique no botão abaixo para abrir o site:", "Or click the button below to open the website:", "O haz clic en el botón abajo para abrir el sitio:" } },
    { "🌐 Abrir Página do PayPal", new[] { "🌐 Abrir Página do PayPal", "🌐 Open PayPal Page", "🌐 Abrir Página de PayPal" } },
    { "O que há de novo?", new[] { "O que há de novo?", "What's new?", "¿Qué hay de nuevo?" } },
    { "Histórico de Atualizações", new[] { "Histórico de Atualizações", "Update History", "Historial de Actualizaciones" } },
    { "Fechar Histórico", new[] { "Fechar Histórico", "Close History", "Cerrar Historial" } },
    { "A Atualização de Performance", new[] { "A Atualização de Performance", "The Performance Update", "La Actualización de Rendimiento" } },
    { "Lançamento Oficial", new[] { "Lançamento Oficial", "Official Release", "Lanzamiento Oficial" } },
    { "Motor Inteligente de Imagens: O VioFlow agora conta com 'Lazy Loading'. Ele rastreia a barra de rolagem e baixa apenas as logos visíveis, economizando até 90% de Memória RAM e banda de internet.", new[] { "Motor Inteligente de Imagens: O VioFlow agora conta com 'Lazy Loading'. Ele rastreia a barra de rolagem e baixa apenas as logos visíveis, economizando até 90% de Memória RAM e banda de internet.", "Smart Image Engine: VioFlow now has 'Lazy Loading'. It tracks the scrollbar and downloads only visible logos, saving up to 90% RAM and bandwidth.", "Motor Inteligente de Imágenes: VioFlow ahora cuenta con 'Lazy Loading'. Rastrea la barra de desplazamiento y descarga solo logos visibles, ahorrando hasta 90% de RAM y ancho de banda." } },
    { "Sistema Anti-Bloqueio (User-Agent): Disfarce de navegador integrado para contornar bloqueios de segurança de servidores IPTV ao baixar imagens.", new[] { "Sistema Anti-Bloqueio (User-Agent): Disfarce de navegador integrado para contornar bloqueios de segurança de servidores IPTV ao baixar imagens.", "Anti-Block System (User-Agent): Integrated browser disguise to bypass IPTV server security blocks when downloading images.", "Sistema Anti-Bloqueo (User-Agent): Disfraz de navegador integrado para eludir bloqueos de seguridad de servidores IPTV al descargar imágenes." } },
    { "Cache Dinâmico: Limite inteligente de 500 logos simultâneas na memória, garantindo estabilidade absoluta mesmo ao rolar listas gigantes de 500k+ linhas.", new[] { "Cache Dinâmico: Limite inteligente de 500 logos simultâneas na memória, garantindo estabilidade absoluta mesmo ao rolar listas gigantes de 500k+ linhas.", "Dynamic Cache: Smart limit of 500 simultaneous logos in memory, ensuring absolute stability even when scrolling giant lists of 500k+ rows.", "Caché Dinámico: Límite inteligente de 500 logos simultáneos en memoria, asegurando estabilidad absoluta al desplazar listas gigantes de 500k+ filas." } },
    { "Central de Transplantes Turbinada: Novo painel visual de 'Antes e Depois' (Roubar Logo) com Motor Anti-Fantasma assíncrono. Downloads de imagens são cancelados instantaneamente ao trocar de canal para evitar mistura de fotos e garantir navegação sem travamentos.", new[] { "Central de Transplantes Turbinada: Novo painel visual de 'Antes e Depois' (Roubar Logo) com Motor Anti-Fantasma assíncrono. Downloads de imagens são cancelados instantaneamente ao trocar de canal para evitar mistura de fotos e garantir navegação sem travamentos.", "Turbo Transplant Center: New 'Before and After' visual panel (Steal Logo) with async Anti-Ghost Engine. Image downloads cancel instantly when switching channels to prevent mixed photos and ensure smooth navigation.", "Centro de Trasplantes Turbo: Nuevo panel visual 'Antes y Después' (Robar Logo) con Motor Anti-Fantasma asíncrono. Descargas de imágenes se cancelan al cambiar de canal para evitar fotos mezcladas y asegurar navegación fluida." } },
    { "Nova central de Apoio ao Desenvolvedor, com integração nativa via PIX (QR Code e Copia/Cola) e PayPal.", new[] { "Nova central de Apoio ao Desenvolvedor, com integração nativa via PIX (QR Code e Copia/Cola) e PayPal.", "New Developer Support center, with native integration via PIX (QR Code and Copy/Paste) and PayPal.", "Nuevo centro de Apoyo al Desarrollador, con integración nativa vía PIX (QR Code y Copia/Pega) y PayPal." } },
    { "Interface visual polida e com ajustes avançados de responsividade no Dark Mode.", new[] { "Interface visual polida e com ajustes avançados de responsividade no Dark Mode.", "Polished visual interface with advanced responsiveness tweaks in Dark Mode.", "Interfaz visual pulida con ajustes avanzados de respuesta en Modo Oscuro." } },
    { "Consumo de processamento (CPU) reduzido a quase 0% com o PC em repouso.", new[] { "Consumo de processamento (CPU) reduzido a quase 0% com o PC em repouso.", "Processing (CPU) consumption reduced to almost 0% when the PC is idle.", "Consumo de procesamiento (CPU) reducido a casi 0% con el PC en reposo." } },
    { "O Update do Inspetor", new[] { "O Update do Inspetor", "The Inspector Update", "La Actualización del Inspector" } },
    { "Testador de Canais Integrado: Validação de links em tempo real para identificar e remover canais offline ou quebrados.", new[] { "Testador de Canais Integrado: Validação de links em tempo real para identificar e remover canais offline ou quebrados.", "Integrated Channel Tester: Real-time link validation to identify and remove offline or broken channels.", "Probador de Canales Integrado: Validación de enlaces en tiempo real para identificar y eliminar canales offline o rotos." } },
    { "Sistema de Limpeza: Remoção de duplicados e formatação em lote de nomes de canais.", new[] { "Sistema de Limpeza: Remoção de duplicados e formatação em lote de nomes de canais.", "Cleanup System: Duplicate removal and batch formatting of channel names.", "Sistema de Limpieza: Eliminación de duplicados y formato por lotes de nombres de canales." } },
    { "Refatoração do motor de busca e filtros na tabela principal.", new[] { "Refatoração do motor de busca e filtros na tabela principal.", "Refactoring of search engine and filters in the main table.", "Refactorización del motor de búsqueda y filtros en la tabla principal." } },
    { "Estruturação de Dados", new[] { "Estruturação de Dados", "Data Structuring", "Estructuración de Datos" } },
    { "Implementação de Expressões Regulares (Regex) avançadas para leitura cirúrgica de parâmetros complexos do M3U", new[] { "Implementação de Expressões Regulares (Regex) avançadas para leitura cirúrgica de parâmetros complexos do M3U", "Implementation of advanced Regular Expressions (Regex) for surgical reading of complex M3U parameters", "Implementación de Expresiones Regulares (Regex) avanzadas para lectura quirúrgica de parámetros M3U" } },
    { "Tabela (DataGrid) interativa permitindo edição manual ágil das colunas.", new[] { "Tabela (DataGrid) interativa permitindo edição manual ágil das colunas.", "Interactive Table (DataGrid) allowing agile manual editing of columns.", "Tabla (DataGrid) interactiva que permite edición manual ágil de columnas." } },
    { "Correção de travamentos ao tentar mesclar múltiplas listas pesadas.", new[] { "Correção de travamentos ao tentar mesclar múltiplas listas pesadas.", "Fix crashes when trying to merge multiple heavy lists.", "Corrección de bloqueos al intentar mezclar múltiples listas pesadas." } },
    { "Projeto Alpha", new[] { "Projeto Alpha", "Alpha Project", "Proyecto Alpha" } },
    { "Nascimento do VioFlow IPTV Manager.", new[] { "Nascimento do VioFlow IPTV Manager.", "Birth of VioFlow IPTV Manager.", "Nacimiento de VioFlow IPTV Manager." } },
    { "Criação do chassi principal para leitura, edição básica de texto e salvamento de arquivos .m3u e .m3u8.", new[] { "Criação do chassi principal para leitura, edição básica de texto e salvamento de arquivos .m3u e .m3u8.", "Creation of the main chassis for reading, basic text editing, and saving .m3u and .m3u8 files.", "Creación del chasis principal para lectura, edición básica de texto y guardado de archivos .m3u y .m3u8." } },
    { "este canal", new[] { "este canal", "this channel", "este canal" } },
    { " ⚠️ (EXPIRADA)", new[] { " ⚠️ (EXPIRADA)", " ⚠️ (EXPIRED)", " ⚠️ (EXPIRADA)" } },
    { "ATIVA", new[] { "ATIVA", "ACTIVE", "ACTIVA" } },
    { "📊 INFO DA CONTA - ", new[] { "📊 INFO DA CONTA - ", "📊 ACCOUNT INFO - ", "📊 INFO DE LA CUENTA - " } },
    { "👤 Usuário: ", new[] { "👤 Usuário: ", "👤 User: ", "👤 Usuario: " } },
    { "🟢 Status: ", new[] { "🟢 Status: ", "🟢 Status: ", "🟢 Estado: " } },
    { "📅 Vencimento: ", new[] { "📅 Vencimento: ", "📅 Expiration: ", "📅 Vencimiento: " } },
    { "📱 Telas: ", new[] { "📱 Telas: ", "📱 Screens: ", "📱 Pantallas: " } },
    { "em uso / Máximo:", new[] { "em uso / Máximo:", "in use / Max:", "en uso / Máximo:" } },
    { "🏠 Host: ", new[] { "🏠 Host: ", "🏠 Host: ", "🏠 Host: " } },
    { "VioFlow - Gestão de Contas", new[] { "VioFlow - Gestão de Contas", "VioFlow - Account Management", "VioFlow - Gestión de Cuentas" } },
    { "O servidor não respondeu à consulta.\nDetalhe: ", new[] { "O servidor não respondeu à consulta.\nDetalhe: ", "The server did not respond to the query.\nDetail: ", "El servidor no respondió a la consulta.\nDetalle: " } },

    { "Marque as categorias que deseja EXCLUIR:", new[] { "Marque as categorias que deseja EXCLUIR:", "Check the categories you want to DELETE:", "Marque las categorías que desea ELIMINAR:" } },
    { " de ", new[] { " de ", " of ", " de " } },
    { "  |  🗑️ Para apagar: ", new[] { "  |  🗑️ Para apagar: ", "  |  🗑️ To delete: ", "  |  🗑️ Para borrar: " } },
    { "Faxina Turbo Concluída!\n\n🗑️ ", new[] { "Faxina Turbo Concluída!\n\n🗑️ ", "Turbo Cleanup Complete!\n\n🗑️ ", "¡Limpieza Turbo Completada!\n\n🗑️ " } },
    { " canais removidos.\n✅ ", new[] { " canais removidos.\n✅ ", " channels removed.\n✅ ", " canales eliminados.\n✅ " } },
    { "⚠️ Conflito de Backup!", new[] { "⚠️ Conflito de Backup!", "⚠️ Backup Conflict!", "⚠️ ¡Conflicto de Copia!" } },
    { "🗑️ Apagar Cópia", new[] { "🗑️ Apagar Cópia", "🗑️ Delete Copy", "🗑️ Borrar Copia" } },
    { "🏷️ Renomear [Alt]", new[] { "🏷️ Renomear [Alt]", "🏷️ Rename [Alt]", "🏷️ Renombrar [Alt]" } },
    { "Ou digite um nome e clique em Salvar:", new[] { "Ou digite um nome e clique em Salvar:", "Or type a name and click Save:", "O escriba un nombre y haga clic en Guardar:" } },

    { "💾 Salvar Manual", new[] { "💾 Salvar Manual", "💾 Save Manual", "💾 Guardar Manual" } },
    { "Repetir esta escolha para todos os próximos conflitos", new[] { "Repetir esta escolha para todos os próximos conflitos", "Repeat this choice for all future conflicts", "Repetir esta elección para todos los próximos conflictos" } },
    { "O canal", new[] { "O canal", "The channel", "El canal" } },
    { "O teste foi interrompido", new[] { "O teste foi interrompido", "The test was interrupted", "La prueba fue interrumpida" } },
    { "Resultados", new[] { "Resultados", "Results", "Resultados" } },
    { "Marque as categorias que deseja SALVAR:", new[] { "Marque as categorias que deseja SALVAR:", "Check the categories you want to SAVE:", "Marque las categorías que desea GUARDAR:" } },
    { "Uma nova versão do VioFlow está disponível! Deseja baixar agora?", new[] { "Uma nova versão do VioFlow está disponível! Deseja baixar agora?", "A new version of VioFlow is available! Download now?", "¡Una nueva versión de VioFlow está disponible! ¿Descargar ahora?" } },
    { "Atualização Disponível", new[] { "Atualização Disponível", "Update Available", "Actualización Disponible" } },
    { "Transfere a logo de um canal doador para o seu.", new[] { "Transfere a logo de um canal doador para o seu.", "Transfers the logo from a donor channel to yours.", "Transfiere el logo de un canal donante al tuyo." } },
    { "Mostra canais inéditos para adicionar.", new[] { "Mostra canais inéditos para adicionar.", "Shows new channels to add.", "Muestra canales nuevos para añadir." } },
    { "Substitui o link quebrado pelo link da lista nova.", new[] { "Substitui o link quebrado pelo link da lista nova.", "Replaces the broken link with the link from the new list.", "Reemplaza el enlace roto por el enlace de la lista nueva." } },
    { "Total", new[] { "Total", "Total", "Total" } },
    { "canais", new[] { "canais", "channels", "canales" } },
    { "Atenção: Você tem canais carregados no VioFlow!", new[] { "Atenção: Você tem canais carregados no VioFlow!", "Attention: You have channels loaded in VioFlow!", "¡Atención: Tienes canales cargados en VioFlow!" } },
    { "Se sair agora, edições não salvas serão perdidas.", new[] { "Se sair agora, edições não salvas serão perdidas.", "If you exit now, unsaved edits will be lost.", "Si sales ahora, los cambios no guardados se perderán." } },
    { "Deseja realmente fechar?", new[] { "Deseja realmente fechar?", "Do you really want to close?", "¿Realmente deseas cerrar?" } },
    { "Sair sem Salvar?", new[] { "Sair sem Salvar?", "Exit without Saving?", "¿Salir sin Guardar?" } },
    { "🎞️ Vídeo: Analisando stream...", new[] { "🎞️ Vídeo: Analisando stream...", "🎞️ Video: Analyzing stream...", "🎞️ Video: Analizando stream..." } },
    { "🔊 Áudio: Analisando stream...", new[] { "🔊 Áudio: Analisando stream...", "🔊 Audio: Analyzing stream...", "🔊 Audio: Analizando stream..." } },
    { "VERSÃO", new[] { "VERSÃO", "VERSION", "VERSIÓN" } },
    { "[NOVO]", new[] { "[NOVO]", "[NEW]", "[NUEVO]" } },
    { "[MELHORIA]", new[] { "[MELHORIA]", "[IMPROVEMENT]", "[MEJORA]" } },
    { "[BUGFIX]", new[] { "[BUGFIX]", "[BUGFIX]", "[CORRECCIÓN]" } },
    { "Nova atualização disponível!", new[] { "Nova atualização disponível!", "New update available!", "¡Nueva actualización disponible!" } },

};

        private void CriarBotaoIdioma()
        {
            Button btnIdioma = new Button()
            {
                Text = "🌐 Idioma",
                Width = 110,
                Height = 35,
                Left = this.ClientSize.Width - 130,
                Top = 15,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("🇧🇷 Português", null, (s, e) => AplicarIdioma(0));
            menu.Items.Add("🇺🇸 English", null, (s, e) => AplicarIdioma(1));
            menu.Items.Add("🇪🇸 Español", null, (s, e) => AplicarIdioma(2));

            btnIdioma.Click += (s, e) => menu.Show(btnIdioma, 0, btnIdioma.Height);

            this.Controls.Add(btnIdioma);
            btnIdioma.BringToFront();
            SalvarTags(this);
        }

        private void SalvarTags(Control pai)
        {
            foreach (Control c in pai.Controls)
            {
                if ((c is Button || c is Label || c is CheckBox || c is RadioButton) && !string.IsNullOrWhiteSpace(c.Text) && c.Tag == null)
                    c.Tag = c.Text;
                if (c.HasChildren) SalvarTags(c);
            }
        }

        public string ObterTraducao(string textoOriginal)
        {
            if (string.IsNullOrWhiteSpace(textoOriginal)) return textoOriginal;

            string traduzido = textoOriginal;
            var chavesOrdenadas = dicionarioSimples.Keys.OrderByDescending(k => k.Length);

            foreach (var key in chavesOrdenadas)
            {
                if (traduzido.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // Regex resolve problemas de formatação invisível e letras maiúsculas
                    traduzido = System.Text.RegularExpressions.Regex.Replace(
                        traduzido,
                        System.Text.RegularExpressions.Regex.Escape(key),
                        dicionarioSimples[key][IdiomaAtual],
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                }
            }
            return traduzido;
        }

        private void AplicarIdioma(int id)
        {
            IdiomaAtual = id;
            TraduzirControles(this);

            if (tabelaCanais.ContextMenuStrip != null)
                TraduzirMenuDireito(tabelaCanais.ContextMenuStrip.Items);

            if (tabelaCanais.Columns.Contains("FotoCanal")) tabelaCanais.Columns["FotoCanal"].HeaderText = ObterTraducao("Logo");
            if (tabelaCanais.Columns.Contains("LogoUrl")) tabelaCanais.Columns["LogoUrl"].HeaderText = ObterTraducao("Imagem URL");
            if (tabelaCanais.Columns.Contains("NomeCanal")) tabelaCanais.Columns["NomeCanal"].HeaderText = ObterTraducao("Nome");
            if (tabelaCanais.Columns.Contains("EpgId")) tabelaCanais.Columns["EpgId"].HeaderText = ObterTraducao("ID do EPG");
            if (tabelaCanais.Columns.Contains("Categoria")) tabelaCanais.Columns["Categoria"].HeaderText = ObterTraducao("Categoria");
            if (tabelaCanais.Columns.Contains("Url")) tabelaCanais.Columns["Url"].HeaderText = ObterTraducao("Link (URL)");
            if (tabelaCanais.Columns.Contains("StatusUrl")) tabelaCanais.Columns["StatusUrl"].HeaderText = ObterTraducao("Status");

            string[] titulos = { "VioFlow IPTV Manager - Versão 1.0.0", "VioFlow IPTV Manager - Version 1.0.0", "VioFlow IPTV Manager - Versión 1.0.0" };
            this.Text = titulos[id];

            AtualizarStatus();
            if (linkAtualizacao.Visible == true)
            {
                linkAtualizacao.Text = $"⚠️ {ObterTraducao("Nova atualização disponível!")} ({versaoPendente})";
            }
        }

        private void TraduzirControles(Control pai)
        {
            foreach (Control c in pai.Controls)
            {
                if (c.Tag != null) c.Text = ObterTraducao(c.Tag.ToString());
                if (c.HasChildren) TraduzirControles(c);
            }
        }

        public void TraduzirTelaDinamica(Form tela)
        {
            if (IdiomaAtual == 0) return;
            tela.Text = ObterTraducao(tela.Text);
            TraduzirControlesDinamicos(tela);
        }

        private void TraduzirControlesDinamicos(Control pai)
        {
            foreach (Control c in pai.Controls)
            {
                if (!string.IsNullOrWhiteSpace(c.Text) && !(c is TextBox)) c.Text = ObterTraducao(c.Text);
                if (c is ComboBox combo)
                {
                    for (int i = 0; i < combo.Items.Count; i++)
                        if (combo.Items[i] is string strItem) combo.Items[i] = ObterTraducao(strItem);
                }
                else if (c is DataGridView grid)
                {
                    foreach (DataGridViewColumn col in grid.Columns) col.HeaderText = ObterTraducao(col.HeaderText);
                }
                if (c.HasChildren) TraduzirControlesDinamicos(c);
            }
        }
        private void TraduzirMenuDireito(ToolStripItemCollection items)
        {
            foreach (ToolStripItem item in items)
            {
                if (item.Tag == null) item.Tag = item.Text;
                item.Text = ObterTraducao(item.Tag.ToString());

                if (item is ToolStripMenuItem menuItem && menuItem.HasDropDownItems)
                {
                    TraduzirMenuDireito(menuItem.DropDownItems);
                }
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            VerificarAtualizacao();
        }

        private void linkAtualizacao_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            // Abre o navegador direto na página de download
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://github.com/VioFlow/VioFlow-IPTV-Manager/releases",
                UseShellExecute = true
            });
        }
    }
}