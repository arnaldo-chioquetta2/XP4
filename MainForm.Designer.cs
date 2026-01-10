namespace Mp3PlayerWinForms.Forms
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.lvTracks = new System.Windows.Forms.ListView();
            this.colMusica = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colBanda = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colDuracao = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.pnlControls = new System.Windows.Forms.Panel();
            this.lblStatus = new System.Windows.Forms.Label();
            this.btnNext = new System.Windows.Forms.Button();
            this.btnPause = new System.Windows.Forms.Button();
            this.btnPlay = new System.Windows.Forms.Button();
            this.btnImportar = new System.Windows.Forms.Button();
            this.spectrum = new Mp3PlayerWinForms.Controls.SpectrumControl();
            this.pnlControls.SuspendLayout();
            this.SuspendLayout();
            // 
            // lvTracks
            // 
            this.lvTracks.AllowDrop = true;
            this.lvTracks.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colMusica,
            this.colBanda,
            this.colDuracao});
            this.lvTracks.Dock = System.Windows.Forms.DockStyle.Top;
            this.lvTracks.FullRowSelect = true;
            this.lvTracks.HideSelection = false;
            this.lvTracks.Location = new System.Drawing.Point(0, 0);
            this.lvTracks.Name = "lvTracks";
            this.lvTracks.Size = new System.Drawing.Size(600, 250);
            this.lvTracks.TabIndex = 0;
            this.lvTracks.UseCompatibleStateImageBehavior = false;
            this.lvTracks.View = System.Windows.Forms.View.Details;
            // 
            // colMusica
            // 
            this.colMusica.Text = "Música";
            this.colMusica.Width = 250;
            // 
            // colBanda
            // 
            this.colBanda.Text = "Banda";
            this.colBanda.Width = 150;
            // 
            // colDuracao
            // 
            this.colDuracao.Text = "Duração";
            this.colDuracao.Width = 80;
            // 
            // pnlControls
            // 
            this.pnlControls.Controls.Add(this.btnImportar);
            this.pnlControls.Controls.Add(this.lblStatus);
            this.pnlControls.Controls.Add(this.btnNext);
            this.pnlControls.Controls.Add(this.btnPause);
            this.pnlControls.Controls.Add(this.btnPlay);
            this.pnlControls.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.pnlControls.Location = new System.Drawing.Point(0, 440);
            this.pnlControls.Name = "pnlControls";
            this.pnlControls.Size = new System.Drawing.Size(600, 60);
            this.pnlControls.TabIndex = 1;
            // 
            // lblStatus
            // 
            this.lblStatus.AutoSize = true;
            this.lblStatus.Location = new System.Drawing.Point(260, 18);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(38, 13);
            this.lblStatus.TabIndex = 3;
            this.lblStatus.Text = "Pronto";
            // 
            // btnNext
            // 
            this.btnNext.Location = new System.Drawing.Point(170, 10);
            this.btnNext.Name = "btnNext";
            this.btnNext.Size = new System.Drawing.Size(75, 30);
            this.btnNext.TabIndex = 2;
            this.btnNext.Text = "Próxima";
            this.btnNext.UseVisualStyleBackColor = true;
            // 
            // btnPause
            // 
            this.btnPause.Location = new System.Drawing.Point(90, 10);
            this.btnPause.Name = "btnPause";
            this.btnPause.Size = new System.Drawing.Size(75, 30);
            this.btnPause.TabIndex = 1;
            this.btnPause.Text = "Pause";
            this.btnPause.UseVisualStyleBackColor = true;
            // 
            // btnPlay
            // 
            this.btnPlay.Location = new System.Drawing.Point(10, 10);
            this.btnPlay.Name = "btnPlay";
            this.btnPlay.Size = new System.Drawing.Size(75, 30);
            this.btnPlay.TabIndex = 0;
            this.btnPlay.Text = "Play";
            this.btnPlay.UseVisualStyleBackColor = true;
            // 
            // btnImportar
            // 
            this.btnImportar.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnImportar.Location = new System.Drawing.Point(480, 10);
            this.btnImportar.Name = "btnImportar";
            this.btnImportar.Size = new System.Drawing.Size(100, 30);
            this.btnImportar.TabIndex = 4;
            this.btnImportar.Text = "Importar DB";
            this.btnImportar.UseVisualStyleBackColor = true;
            // 
            // spectrum
            // 
            this.spectrum.BackColor = System.Drawing.Color.Black;
            this.spectrum.Dock = System.Windows.Forms.DockStyle.Fill;
            this.spectrum.Location = new System.Drawing.Point(0, 250);
            this.spectrum.Name = "spectrum";
            this.spectrum.Size = new System.Drawing.Size(600, 190);
            this.spectrum.TabIndex = 2;
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(600, 500);
            this.Controls.Add(this.spectrum);
            this.Controls.Add(this.pnlControls);
            this.Controls.Add(this.lvTracks);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Manus MP3 Player";
            this.pnlControls.ResumeLayout(false);
            this.pnlControls.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ListView lvTracks;
        private System.Windows.Forms.ColumnHeader colMusica;
        private System.Windows.Forms.ColumnHeader colBanda;
        private System.Windows.Forms.ColumnHeader colDuracao;
        private System.Windows.Forms.Panel pnlControls;
        private System.Windows.Forms.Button btnNext;
        private System.Windows.Forms.Button btnPause;
        private System.Windows.Forms.Button btnPlay;
        private System.Windows.Forms.Button btnImportar; // Novo Botão
        private System.Windows.Forms.Label lblStatus;
        private Mp3PlayerWinForms.Controls.SpectrumControl spectrum;
    }
}