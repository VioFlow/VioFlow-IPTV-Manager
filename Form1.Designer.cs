namespace VioFlow_IPTV_Manager
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            button1 = new Button();
            tabelaCanais = new DataGridView();
            contextMenuStrip1 = new ContextMenuStrip(components);
            excluirCanaisToolStripMenuItem = new ToolStripMenuItem();
            mudarCategoriaToolStripMenuItem = new ToolStripMenuItem();
            limparNomeDoCanalToolStripMenuItem = new ToolStripMenuItem();
            trocarURLDoCanalToolStripMenuItem = new ToolStripMenuItem();
            definirEPGManualmenteToolStripMenuItem = new ToolStripMenuItem();
            colarNovoCanalCompletoToolStripMenuItem = new ToolStripMenuItem();
            copiarCanalToolStripMenuItem = new ToolStripMenuItem();
            colarCanalToolStripMenuItem = new ToolStripMenuItem();
            monitorTécnicoPlayInfoToolStripMenuItem = new ToolStripMenuItem();
            verInfoDaContaToolStripMenuItem = new ToolStripMenuItem();
            btnSalvarLista = new Button();
            txtPesquisa = new TextBox();
            label1 = new Label();
            btnApagar = new Button();
            btnSubir = new Button();
            btnDescer = new Button();
            statusStrip1 = new StatusStrip();
            lblStatus = new ToolStripStatusLabel();
            btnMesclar = new Button();
            button2 = new Button();
            button3 = new Button();
            btnSobre = new Button();
            btnListaDoadora = new Button();
            button4 = new Button();
            AbrirdaWeb = new Button();
            ExportarCategorias = new Button();
            OrdemdasCategorias = new Button();
            TestadordeM3U = new Button();
            ApagarLogo = new Button();
            ApagarIDdoEPG = new Button();
            LimparDuplicados = new Button();
            Desfazer = new Button();
            Refazer = new Button();
            GeradordeCatálogo = new Button();
            panel1 = new Panel();
            label2 = new Label();
            pictureBox1 = new PictureBox();
            linkAtualizacao = new LinkLabel();
            panel2 = new Panel();
            ((System.ComponentModel.ISupportInitialize)tabelaCanais).BeginInit();
            contextMenuStrip1.SuspendLayout();
            statusStrip1.SuspendLayout();
            panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
            panel2.SuspendLayout();
            SuspendLayout();
            // 
            // button1
            // 
            button1.BackColor = Color.SteelBlue;
            button1.Cursor = Cursors.Hand;
            button1.FlatAppearance.BorderSize = 0;
            button1.FlatStyle = FlatStyle.Flat;
            button1.ForeColor = Color.White;
            button1.Location = new Point(11, 20);
            button1.Name = "button1";
            button1.Size = new Size(75, 30);
            button1.TabIndex = 0;
            button1.Text = "Abrir Lista";
            button1.UseVisualStyleBackColor = false;
            button1.Click += button1_Click;
            // 
            // tabelaCanais
            // 
            tabelaCanais.BorderStyle = BorderStyle.None;
            tabelaCanais.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            tabelaCanais.ContextMenuStrip = contextMenuStrip1;
            tabelaCanais.Dock = DockStyle.Fill;
            tabelaCanais.GridColor = Color.Silver;
            tabelaCanais.Location = new Point(8, 8);
            tabelaCanais.Margin = new Padding(0);
            tabelaCanais.Name = "tabelaCanais";
            tabelaCanais.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            tabelaCanais.Size = new Size(1669, 685);
            tabelaCanais.TabIndex = 1;
            tabelaCanais.CellContentClick += tabelaCanais_CellContentClick;
            tabelaCanais.Scroll += tabelaCanais_Scroll;
            // 
            // contextMenuStrip1
            // 
            contextMenuStrip1.Items.AddRange(new ToolStripItem[] { excluirCanaisToolStripMenuItem, mudarCategoriaToolStripMenuItem, limparNomeDoCanalToolStripMenuItem, trocarURLDoCanalToolStripMenuItem, definirEPGManualmenteToolStripMenuItem, colarNovoCanalCompletoToolStripMenuItem, copiarCanalToolStripMenuItem, colarCanalToolStripMenuItem, monitorTécnicoPlayInfoToolStripMenuItem, verInfoDaContaToolStripMenuItem });
            contextMenuStrip1.Name = "contextMenuStrip1";
            contextMenuStrip1.Size = new Size(284, 224);
            contextMenuStrip1.Opening += contextMenuStrip1_Opening;
            // 
            // excluirCanaisToolStripMenuItem
            // 
            excluirCanaisToolStripMenuItem.Name = "excluirCanaisToolStripMenuItem";
            excluirCanaisToolStripMenuItem.Size = new Size(283, 22);
            excluirCanaisToolStripMenuItem.Text = "Excluir Canais";
            excluirCanaisToolStripMenuItem.Click += excluirCanaisToolStripMenuItem_Click;
            // 
            // mudarCategoriaToolStripMenuItem
            // 
            mudarCategoriaToolStripMenuItem.Name = "mudarCategoriaToolStripMenuItem";
            mudarCategoriaToolStripMenuItem.Size = new Size(283, 22);
            mudarCategoriaToolStripMenuItem.Text = "Mudar Categoria";
            mudarCategoriaToolStripMenuItem.Click += mudarCategoriaToolStripMenuItem_Click;
            // 
            // limparNomeDoCanalToolStripMenuItem
            // 
            limparNomeDoCanalToolStripMenuItem.Name = "limparNomeDoCanalToolStripMenuItem";
            limparNomeDoCanalToolStripMenuItem.Size = new Size(283, 22);
            limparNomeDoCanalToolStripMenuItem.Text = "Limpar Nome do Canal";
            limparNomeDoCanalToolStripMenuItem.Click += limparNomeDoCanalToolStripMenuItem_Click;
            // 
            // trocarURLDoCanalToolStripMenuItem
            // 
            trocarURLDoCanalToolStripMenuItem.Name = "trocarURLDoCanalToolStripMenuItem";
            trocarURLDoCanalToolStripMenuItem.Size = new Size(283, 22);
            trocarURLDoCanalToolStripMenuItem.Text = "Trocar URL do Canal";
            trocarURLDoCanalToolStripMenuItem.Click += trocarURLDoCanalToolStripMenuItem_Click;
            // 
            // definirEPGManualmenteToolStripMenuItem
            // 
            definirEPGManualmenteToolStripMenuItem.Name = "definirEPGManualmenteToolStripMenuItem";
            definirEPGManualmenteToolStripMenuItem.Size = new Size(283, 22);
            definirEPGManualmenteToolStripMenuItem.Text = "Definir EPG Manualmente";
            definirEPGManualmenteToolStripMenuItem.Click += definirEPGManualmenteToolStripMenuItem_Click;
            // 
            // colarNovoCanalCompletoToolStripMenuItem
            // 
            colarNovoCanalCompletoToolStripMenuItem.Name = "colarNovoCanalCompletoToolStripMenuItem";
            colarNovoCanalCompletoToolStripMenuItem.Size = new Size(283, 22);
            colarNovoCanalCompletoToolStripMenuItem.Text = "Colar Novo Canal Completo (#EXTINF)\"";
            colarNovoCanalCompletoToolStripMenuItem.Click += colarNovoCanalCompletoToolStripMenuItem_Click;
            // 
            // copiarCanalToolStripMenuItem
            // 
            copiarCanalToolStripMenuItem.Name = "copiarCanalToolStripMenuItem";
            copiarCanalToolStripMenuItem.Size = new Size(283, 22);
            copiarCanalToolStripMenuItem.Text = "Copiar Canal";
            copiarCanalToolStripMenuItem.Click += copiarCanalToolStripMenuItem_Click;
            // 
            // colarCanalToolStripMenuItem
            // 
            colarCanalToolStripMenuItem.Name = "colarCanalToolStripMenuItem";
            colarCanalToolStripMenuItem.Size = new Size(283, 22);
            colarCanalToolStripMenuItem.Text = "Colar Canal";
            colarCanalToolStripMenuItem.Click += colarCanalToolStripMenuItem_Click;
            // 
            // monitorTécnicoPlayInfoToolStripMenuItem
            // 
            monitorTécnicoPlayInfoToolStripMenuItem.Name = "monitorTécnicoPlayInfoToolStripMenuItem";
            monitorTécnicoPlayInfoToolStripMenuItem.Size = new Size(283, 22);
            monitorTécnicoPlayInfoToolStripMenuItem.Text = "Monitor Técnico (Play E Info)";
            monitorTécnicoPlayInfoToolStripMenuItem.Click += monitorTécnicoPlayInfoToolStripMenuItem_Click;
            // 
            // verInfoDaContaToolStripMenuItem
            // 
            verInfoDaContaToolStripMenuItem.Name = "verInfoDaContaToolStripMenuItem";
            verInfoDaContaToolStripMenuItem.Size = new Size(283, 22);
            verInfoDaContaToolStripMenuItem.Text = "Ver Info da Conta";
            verInfoDaContaToolStripMenuItem.Click += verInfoDaContaToolStripMenuItem_Click;
            // 
            // btnSalvarLista
            // 
            btnSalvarLista.BackColor = Color.SteelBlue;
            btnSalvarLista.Cursor = Cursors.Hand;
            btnSalvarLista.FlatAppearance.BorderSize = 0;
            btnSalvarLista.FlatStyle = FlatStyle.Flat;
            btnSalvarLista.ForeColor = Color.White;
            btnSalvarLista.Location = new Point(92, 20);
            btnSalvarLista.Name = "btnSalvarLista";
            btnSalvarLista.Size = new Size(75, 30);
            btnSalvarLista.TabIndex = 2;
            btnSalvarLista.Text = "Salvar Lista";
            btnSalvarLista.UseVisualStyleBackColor = false;
            btnSalvarLista.Click += btnSalvarLista_Click;
            // 
            // txtPesquisa
            // 
            txtPesquisa.BackColor = Color.Silver;
            txtPesquisa.Location = new Point(660, 25);
            txtPesquisa.Name = "txtPesquisa";
            txtPesquisa.Size = new Size(180, 23);
            txtPesquisa.TabIndex = 3;
            txtPesquisa.TextChanged += txtPesquisa_TextChanged;
            txtPesquisa.KeyDown += txtPesquisa_KeyDown;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(597, 28);
            label1.Name = "label1";
            label1.Size = new Size(57, 15);
            label1.TabIndex = 4;
            label1.Text = "Pesquisar";
            // 
            // btnApagar
            // 
            btnApagar.BackColor = Color.Silver;
            btnApagar.Cursor = Cursors.Hand;
            btnApagar.FlatAppearance.BorderSize = 0;
            btnApagar.FlatStyle = FlatStyle.Flat;
            btnApagar.ForeColor = SystemColors.InfoText;
            btnApagar.Location = new Point(1104, 20);
            btnApagar.Name = "btnApagar";
            btnApagar.Size = new Size(135, 30);
            btnApagar.TabIndex = 5;
            btnApagar.Text = "Apagar Categorias";
            btnApagar.UseVisualStyleBackColor = false;
            btnApagar.Click += btnApagar_Click;
            // 
            // btnSubir
            // 
            btnSubir.Cursor = Cursors.Hand;
            btnSubir.FlatAppearance.BorderSize = 0;
            btnSubir.FlatAppearance.MouseOverBackColor = Color.Black;
            btnSubir.FlatStyle = FlatStyle.Flat;
            btnSubir.ForeColor = Color.White;
            btnSubir.Location = new Point(22, 250);
            btnSubir.Name = "btnSubir";
            btnSubir.Size = new Size(133, 30);
            btnSubir.TabIndex = 6;
            btnSubir.Text = "Subir Canal";
            btnSubir.UseVisualStyleBackColor = true;
            btnSubir.Click += btnSubir_Click;
            // 
            // btnDescer
            // 
            btnDescer.Cursor = Cursors.Hand;
            btnDescer.FlatAppearance.BorderSize = 0;
            btnDescer.FlatAppearance.MouseOverBackColor = Color.Black;
            btnDescer.FlatStyle = FlatStyle.Flat;
            btnDescer.ForeColor = Color.White;
            btnDescer.Location = new Point(22, 294);
            btnDescer.Name = "btnDescer";
            btnDescer.Size = new Size(133, 30);
            btnDescer.TabIndex = 7;
            btnDescer.Text = "Descer Canal";
            btnDescer.UseVisualStyleBackColor = true;
            btnDescer.Click += btnDescer_Click;
            // 
            // statusStrip1
            // 
            statusStrip1.Items.AddRange(new ToolStripItem[] { lblStatus });
            statusStrip1.Location = new Point(8, 671);
            statusStrip1.Name = "statusStrip1";
            statusStrip1.Size = new Size(1669, 22);
            statusStrip1.TabIndex = 8;
            statusStrip1.Text = "statusStrip1";
            statusStrip1.ItemClicked += statusStrip1_ItemClicked;
            // 
            // lblStatus
            // 
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(43, 17);
            lblStatus.Text = "Pronto";
            // 
            // btnMesclar
            // 
            btnMesclar.BackColor = Color.Silver;
            btnMesclar.Cursor = Cursors.Hand;
            btnMesclar.FlatAppearance.BorderSize = 0;
            btnMesclar.FlatStyle = FlatStyle.Flat;
            btnMesclar.Location = new Point(173, 20);
            btnMesclar.Name = "btnMesclar";
            btnMesclar.Size = new Size(105, 30);
            btnMesclar.TabIndex = 9;
            btnMesclar.Text = "Mesclar Lista ";
            btnMesclar.UseVisualStyleBackColor = false;
            btnMesclar.Click += btnMesclar_Click;
            // 
            // button2
            // 
            button2.BackColor = Color.Silver;
            button2.Cursor = Cursors.Hand;
            button2.FlatAppearance.BorderSize = 0;
            button2.FlatStyle = FlatStyle.Flat;
            button2.Location = new Point(380, 20);
            button2.Name = "button2";
            button2.Size = new Size(110, 30);
            button2.TabIndex = 10;
            button2.Text = "Configurar EPG";
            button2.UseVisualStyleBackColor = false;
            button2.Click += button2_Click_1;
            // 
            // button3
            // 
            button3.BackColor = Color.Silver;
            button3.Cursor = Cursors.Hand;
            button3.FlatAppearance.BorderSize = 0;
            button3.FlatStyle = FlatStyle.Flat;
            button3.ForeColor = Color.Black;
            button3.Location = new Point(496, 20);
            button3.Name = "button3";
            button3.Size = new Size(95, 30);
            button3.TabIndex = 11;
            button3.Text = "Testar Canais";
            button3.UseVisualStyleBackColor = false;
            button3.Click += button3_Click;
            // 
            // btnSobre
            // 
            btnSobre.BackColor = Color.Silver;
            btnSobre.Cursor = Cursors.Hand;
            btnSobre.FlatAppearance.BorderSize = 0;
            btnSobre.FlatStyle = FlatStyle.Flat;
            btnSobre.Location = new Point(1245, 20);
            btnSobre.Name = "btnSobre";
            btnSobre.Size = new Size(100, 30);
            btnSobre.TabIndex = 12;
            btnSobre.Text = "Sobre / Doar";
            btnSobre.UseVisualStyleBackColor = false;
            btnSobre.Click += btnSobre_Click;
            // 
            // btnListaDoadora
            // 
            btnListaDoadora.BackColor = Color.Silver;
            btnListaDoadora.Cursor = Cursors.Hand;
            btnListaDoadora.FlatAppearance.BorderSize = 0;
            btnListaDoadora.FlatStyle = FlatStyle.Flat;
            btnListaDoadora.Location = new Point(284, 20);
            btnListaDoadora.Name = "btnListaDoadora";
            btnListaDoadora.Size = new Size(90, 30);
            btnListaDoadora.TabIndex = 13;
            btnListaDoadora.Text = "Lista Doadora";
            btnListaDoadora.UseVisualStyleBackColor = false;
            btnListaDoadora.Click += button4_Click_1;
            // 
            // button4
            // 
            button4.Cursor = Cursors.Hand;
            button4.FlatAppearance.BorderSize = 0;
            button4.FlatAppearance.MouseOverBackColor = Color.Black;
            button4.FlatStyle = FlatStyle.Flat;
            button4.ForeColor = Color.White;
            button4.Location = new Point(22, 338);
            button4.Name = "button4";
            button4.Size = new Size(133, 30);
            button4.TabIndex = 14;
            button4.Text = "Formatar Nome";
            button4.UseVisualStyleBackColor = true;
            button4.Click += button4_Click_2;
            // 
            // AbrirdaWeb
            // 
            AbrirdaWeb.Cursor = Cursors.Hand;
            AbrirdaWeb.FlatAppearance.BorderSize = 0;
            AbrirdaWeb.FlatAppearance.MouseOverBackColor = Color.Black;
            AbrirdaWeb.FlatStyle = FlatStyle.Flat;
            AbrirdaWeb.ForeColor = Color.White;
            AbrirdaWeb.Location = new Point(22, 206);
            AbrirdaWeb.Name = "AbrirdaWeb";
            AbrirdaWeb.Size = new Size(133, 30);
            AbrirdaWeb.TabIndex = 15;
            AbrirdaWeb.Text = "Abrir Link M3U";
            AbrirdaWeb.UseVisualStyleBackColor = true;
            AbrirdaWeb.Click += button5_Click;
            // 
            // ExportarCategorias
            // 
            ExportarCategorias.Cursor = Cursors.Hand;
            ExportarCategorias.FlatAppearance.BorderSize = 0;
            ExportarCategorias.FlatAppearance.MouseOverBackColor = Color.Black;
            ExportarCategorias.FlatStyle = FlatStyle.Flat;
            ExportarCategorias.ForeColor = Color.White;
            ExportarCategorias.Location = new Point(22, 470);
            ExportarCategorias.Name = "ExportarCategorias";
            ExportarCategorias.Size = new Size(133, 30);
            ExportarCategorias.TabIndex = 16;
            ExportarCategorias.Text = "Exportar Categorias";
            ExportarCategorias.UseVisualStyleBackColor = true;
            ExportarCategorias.Click += button5_Click_1;
            // 
            // OrdemdasCategorias
            // 
            OrdemdasCategorias.Cursor = Cursors.Hand;
            OrdemdasCategorias.FlatAppearance.BorderSize = 0;
            OrdemdasCategorias.FlatAppearance.MouseOverBackColor = Color.Black;
            OrdemdasCategorias.FlatStyle = FlatStyle.Flat;
            OrdemdasCategorias.ForeColor = Color.White;
            OrdemdasCategorias.Location = new Point(22, 558);
            OrdemdasCategorias.Name = "OrdemdasCategorias";
            OrdemdasCategorias.Size = new Size(133, 30);
            OrdemdasCategorias.TabIndex = 17;
            OrdemdasCategorias.Text = "Ordem das Categorias";
            OrdemdasCategorias.UseVisualStyleBackColor = true;
            OrdemdasCategorias.Click += OrdemdasCategorias_Click;
            // 
            // TestadordeM3U
            // 
            TestadordeM3U.BackColor = Color.ForestGreen;
            TestadordeM3U.Cursor = Cursors.Hand;
            TestadordeM3U.FlatAppearance.BorderSize = 0;
            TestadordeM3U.FlatStyle = FlatStyle.Flat;
            TestadordeM3U.ForeColor = Color.White;
            TestadordeM3U.Location = new Point(846, 20);
            TestadordeM3U.Name = "TestadordeM3U";
            TestadordeM3U.Size = new Size(111, 30);
            TestadordeM3U.TabIndex = 18;
            TestadordeM3U.Text = "Testador de M3U";
            TestadordeM3U.UseVisualStyleBackColor = false;
            TestadordeM3U.Click += TestadordeM3U_Click;
            // 
            // ApagarLogo
            // 
            ApagarLogo.Cursor = Cursors.Hand;
            ApagarLogo.FlatAppearance.BorderSize = 0;
            ApagarLogo.FlatAppearance.MouseOverBackColor = Color.Black;
            ApagarLogo.FlatStyle = FlatStyle.Flat;
            ApagarLogo.ForeColor = Color.White;
            ApagarLogo.Location = new Point(22, 382);
            ApagarLogo.Name = "ApagarLogo";
            ApagarLogo.Size = new Size(133, 30);
            ApagarLogo.TabIndex = 19;
            ApagarLogo.Text = "Apagar Logo";
            ApagarLogo.UseVisualStyleBackColor = true;
            ApagarLogo.Click += ApagarLogo_Click;
            // 
            // ApagarIDdoEPG
            // 
            ApagarIDdoEPG.Cursor = Cursors.Hand;
            ApagarIDdoEPG.FlatAppearance.BorderSize = 0;
            ApagarIDdoEPG.FlatAppearance.MouseOverBackColor = Color.Black;
            ApagarIDdoEPG.FlatStyle = FlatStyle.Flat;
            ApagarIDdoEPG.ForeColor = Color.White;
            ApagarIDdoEPG.Location = new Point(22, 514);
            ApagarIDdoEPG.Name = "ApagarIDdoEPG";
            ApagarIDdoEPG.Size = new Size(133, 30);
            ApagarIDdoEPG.TabIndex = 20;
            ApagarIDdoEPG.Text = "Apagar ID do EPG";
            ApagarIDdoEPG.UseVisualStyleBackColor = true;
            ApagarIDdoEPG.Click += ApagarIDdoEPG_Click;
            // 
            // LimparDuplicados
            // 
            LimparDuplicados.Cursor = Cursors.Hand;
            LimparDuplicados.FlatAppearance.BorderSize = 0;
            LimparDuplicados.FlatAppearance.MouseOverBackColor = Color.Black;
            LimparDuplicados.FlatStyle = FlatStyle.Flat;
            LimparDuplicados.ForeColor = Color.White;
            LimparDuplicados.Location = new Point(22, 426);
            LimparDuplicados.Name = "LimparDuplicados";
            LimparDuplicados.Size = new Size(133, 30);
            LimparDuplicados.TabIndex = 21;
            LimparDuplicados.Text = "Limpar Duplicados";
            LimparDuplicados.UseVisualStyleBackColor = true;
            LimparDuplicados.Click += LimparDuplicados_Click;
            // 
            // Desfazer
            // 
            Desfazer.Cursor = Cursors.Hand;
            Desfazer.FlatAppearance.BorderSize = 0;
            Desfazer.FlatAppearance.MouseOverBackColor = Color.Black;
            Desfazer.FlatStyle = FlatStyle.Flat;
            Desfazer.ForeColor = Color.White;
            Desfazer.Location = new Point(22, 118);
            Desfazer.Name = "Desfazer";
            Desfazer.Size = new Size(133, 30);
            Desfazer.TabIndex = 22;
            Desfazer.Text = "⏪ Desfazer";
            Desfazer.UseVisualStyleBackColor = true;
            Desfazer.Click += Desfazer_Click;
            // 
            // Refazer
            // 
            Refazer.Cursor = Cursors.Hand;
            Refazer.FlatAppearance.BorderSize = 0;
            Refazer.FlatAppearance.MouseOverBackColor = Color.Black;
            Refazer.FlatStyle = FlatStyle.Flat;
            Refazer.ForeColor = Color.White;
            Refazer.Location = new Point(22, 162);
            Refazer.Name = "Refazer";
            Refazer.Size = new Size(133, 30);
            Refazer.TabIndex = 23;
            Refazer.Text = "⏩ Refazer";
            Refazer.UseVisualStyleBackColor = true;
            Refazer.Click += Refazer_Click;
            // 
            // GeradordeCatálogo
            // 
            GeradordeCatálogo.BackColor = Color.ForestGreen;
            GeradordeCatálogo.Cursor = Cursors.Hand;
            GeradordeCatálogo.FlatAppearance.BorderSize = 0;
            GeradordeCatálogo.FlatStyle = FlatStyle.Flat;
            GeradordeCatálogo.ForeColor = Color.White;
            GeradordeCatálogo.Location = new Point(963, 20);
            GeradordeCatálogo.Name = "GeradordeCatálogo";
            GeradordeCatálogo.Size = new Size(135, 30);
            GeradordeCatálogo.TabIndex = 25;
            GeradordeCatálogo.Text = "Gerador de Catálogo";
            GeradordeCatálogo.UseVisualStyleBackColor = false;
            GeradordeCatálogo.Click += GeradordeCatálogo_Click;
            // 
            // panel1
            // 
            panel1.BackColor = Color.FromArgb(31, 31, 31);
            panel1.Controls.Add(label2);
            panel1.Controls.Add(pictureBox1);
            panel1.Controls.Add(btnSubir);
            panel1.Controls.Add(Refazer);
            panel1.Controls.Add(btnDescer);
            panel1.Controls.Add(button4);
            panel1.Controls.Add(Desfazer);
            panel1.Controls.Add(ApagarIDdoEPG);
            panel1.Controls.Add(OrdemdasCategorias);
            panel1.Controls.Add(ApagarLogo);
            panel1.Controls.Add(LimparDuplicados);
            panel1.Controls.Add(ExportarCategorias);
            panel1.Controls.Add(AbrirdaWeb);
            panel1.Dock = DockStyle.Left;
            panel1.Location = new Point(8, 8);
            panel1.Name = "panel1";
            panel1.Size = new Size(180, 663);
            panel1.TabIndex = 26;
            panel1.Paint += panel1_Paint;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(22, 639);
            label2.Name = "label2";
            label2.Size = new Size(0, 15);
            label2.TabIndex = 28;
            // 
            // pictureBox1
            // 
            pictureBox1.BackColor = Color.Transparent;
            pictureBox1.Image = (Image)resources.GetObject("pictureBox1.Image");
            pictureBox1.Location = new Point(0, 14);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new Size(180, 100);
            pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox1.TabIndex = 26;
            pictureBox1.TabStop = false;
            // 
            // linkAtualizacao
            // 
            linkAtualizacao.AutoSize = true;
            linkAtualizacao.LinkColor = Color.Red;
            linkAtualizacao.Location = new Point(11, 0);
            linkAtualizacao.Name = "linkAtualizacao";
            linkAtualizacao.Size = new Size(0, 15);
            linkAtualizacao.TabIndex = 29;
            linkAtualizacao.Visible = false;
            linkAtualizacao.LinkClicked += linkAtualizacao_LinkClicked;
            // 
            // panel2
            // 
            panel2.BackColor = Color.White;
            panel2.Controls.Add(button1);
            panel2.Controls.Add(linkAtualizacao);
            panel2.Controls.Add(btnSalvarLista);
            panel2.Controls.Add(btnMesclar);
            panel2.Controls.Add(btnSobre);
            panel2.Controls.Add(GeradordeCatálogo);
            panel2.Controls.Add(button2);
            panel2.Controls.Add(TestadordeM3U);
            panel2.Controls.Add(button3);
            panel2.Controls.Add(btnApagar);
            panel2.Controls.Add(btnListaDoadora);
            panel2.Controls.Add(label1);
            panel2.Controls.Add(txtPesquisa);
            panel2.Dock = DockStyle.Top;
            panel2.Location = new Point(188, 8);
            panel2.Name = "panel2";
            panel2.Size = new Size(1489, 70);
            panel2.TabIndex = 27;
            panel2.Paint += panel2_Paint;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1685, 701);
            Controls.Add(panel2);
            Controls.Add(panel1);
            Controls.Add(statusStrip1);
            Controls.Add(tabelaCanais);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "Form1";
            Padding = new Padding(8);
            Text = "Form1";
            FormClosing += Form1_FormClosing;
            Load += Form1_Load;
            ((System.ComponentModel.ISupportInitialize)tabelaCanais).EndInit();
            contextMenuStrip1.ResumeLayout(false);
            statusStrip1.ResumeLayout(false);
            statusStrip1.PerformLayout();
            panel1.ResumeLayout(false);
            panel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
            panel2.ResumeLayout(false);
            panel2.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button button1;
        private DataGridView tabelaCanais;
        private Button btnSalvarLista;
        private TextBox txtPesquisa;
        private Label label1;
        private Button btnApagar;
        private Button btnSubir;
        private Button btnDescer;
        private ContextMenuStrip contextMenuStrip1;
        private ToolStripMenuItem mudarCategoriaToolStripMenuItem;
        private ToolStripMenuItem excluirCanaisToolStripMenuItem;
        private ToolStripMenuItem limparNomeDoCanalToolStripMenuItem;
        private StatusStrip statusStrip1;
        private ToolStripStatusLabel lblStatus;
        private ToolStripMenuItem trocarURLDoCanalToolStripMenuItem;
        private Button btnMesclar;
        private Button button2;
        private ToolStripMenuItem definirEPGManualmenteToolStripMenuItem;
        private Button button3;
        private Button btnSobre;
        private Button btnListaDoadora;
        private ToolStripMenuItem colarNovoCanalCompletoToolStripMenuItem;
        private ToolStripMenuItem copiarCanalToolStripMenuItem;
        private ToolStripMenuItem colarCanalToolStripMenuItem;
        private Button button4;
        private Button AbrirdaWeb;
        private Button ExportarCategorias;
        private Button OrdemdasCategorias;
        private Button TestadordeM3U;
        private ToolStripMenuItem monitorTécnicoPlayInfoToolStripMenuItem;
        private Button ApagarLogo;
        private Button ApagarIDdoEPG;
        private Button LimparDuplicados;
        private Button Desfazer;
        private Button Refazer;
        private Button GeradordeCatálogo;
        private Panel panel1;
        private Panel panel2;
        private ToolStripMenuItem verInfoDaContaToolStripMenuItem;
        private PictureBox pictureBox1;
        private Label label2;
        private LinkLabel linkAtualizacao;
    }
}
