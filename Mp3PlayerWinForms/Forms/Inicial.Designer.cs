using System;
using System.Windows.Forms;

namespace XP3.Forms
{
    partial class Inicial
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Inicial));
            this.lvTracks = new System.Windows.Forms.ListView();
            this.colMusica = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colBanda = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colDuracao = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colT = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colP = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colL = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colUltimaVez = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.pnlControls = new System.Windows.Forms.Panel();
            this.lblStatusCue = new System.Windows.Forms.Label();
            this.btnBandas = new System.Windows.Forms.Button();
            this.btnScan = new System.Windows.Forms.Button();
            this.lblStatus = new System.Windows.Forms.Label();
            this.btnMenos = new System.Windows.Forms.Button();
            this.btnNext = new System.Windows.Forms.Button();
            this.btnPlay = new System.Windows.Forms.Button();
            this.btnPause = new System.Windows.Forms.Button();
            this.pnlHeader = new System.Windows.Forms.Panel();
            this.chkToggleProg = new System.Windows.Forms.CheckBox();
            this.button1 = new System.Windows.Forms.Button();
            this.lblTempoAtual = new System.Windows.Forms.Label();
            this.lblTrackCount = new System.Windows.Forms.Label();
            this.lblPlaylistTitle = new System.Windows.Forms.Label();
            this.timerProgresso = new System.Windows.Forms.Timer(this.components);
            this.pnlControls.SuspendLayout();
            this.pnlHeader.SuspendLayout();
            this.SuspendLayout();
            // 
            // lvTracks
            // 
            this.lvTracks.AllowDrop = true;
            this.lvTracks.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(30)))), ((int)(((byte)(30)))));
            this.lvTracks.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.lvTracks.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colMusica,
            this.colBanda,
            this.colDuracao,
            this.colT,
            this.colP,
            this.colL,
            this.colUltimaVez});
            this.lvTracks.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lvTracks.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lvTracks.ForeColor = System.Drawing.Color.White;
            this.lvTracks.FullRowSelect = true;
            this.lvTracks.HideSelection = false;
            this.lvTracks.Location = new System.Drawing.Point(0, 41);
            this.lvTracks.Name = "lvTracks";
            this.lvTracks.Size = new System.Drawing.Size(800, 349);
            this.lvTracks.TabIndex = 0;
            this.lvTracks.UseCompatibleStateImageBehavior = false;
            this.lvTracks.View = System.Windows.Forms.View.Details;
            // 
            // colMusica
            // 
            this.colMusica.Text = "Música";
            this.colMusica.Width = 310;
            // 
            // colBanda
            // 
            this.colBanda.Text = "Banda";
            this.colBanda.Width = 220;
            // 
            // colDuracao
            // 
            this.colDuracao.Text = "Tempo";
            this.colDuracao.Width = 80;
            // 
            // colT
            // 
            this.colT.Text = "T";
            this.colT.Width = 30;
            // 
            // colP
            // 
            this.colP.Text = "P";
            this.colP.Width = 22;
            // 
            // colL
            // 
            this.colL.Text = "L";
            this.colL.Width = 22;
            // 
            // colUltimaVez
            // 
            this.colUltimaVez.Text = "Última Vez";
            this.colUltimaVez.Width = 135;
            // 
            // pnlControls
            // 
            this.pnlControls.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(45)))), ((int)(((byte)(48)))));
            this.pnlControls.Controls.Add(this.lblStatusCue);
            this.pnlControls.Controls.Add(this.btnBandas);
            this.pnlControls.Controls.Add(this.btnScan);
            this.pnlControls.Controls.Add(this.lblStatus);
            this.pnlControls.Controls.Add(this.btnMenos);
            this.pnlControls.Controls.Add(this.btnNext);
            this.pnlControls.Controls.Add(this.btnPlay);
            this.pnlControls.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.pnlControls.Location = new System.Drawing.Point(0, 390);
            this.pnlControls.Name = "pnlControls";
            this.pnlControls.Size = new System.Drawing.Size(800, 60);
            this.pnlControls.TabIndex = 1;
            this.pnlControls.Resize += new System.EventHandler(this.pnlControls_Resize);
            // 
            // lblStatusCue
            // 
            this.lblStatusCue.AutoSize = true;
            this.lblStatusCue.ForeColor = System.Drawing.Color.GreenYellow;
            this.lblStatusCue.Location = new System.Drawing.Point(313, 3);
            this.lblStatusCue.Name = "lblStatusCue";
            this.lblStatusCue.Size = new System.Drawing.Size(35, 13);
            this.lblStatusCue.TabIndex = 6;
            this.lblStatusCue.Text = "label1";
            // 
            // btnBandas
            // 
            this.btnBandas.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnBandas.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.btnBandas.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnBandas.ForeColor = System.Drawing.Color.White;
            this.btnBandas.Location = new System.Drawing.Point(632, 15);
            this.btnBandas.Name = "btnBandas";
            this.btnBandas.Size = new System.Drawing.Size(75, 30);
            this.btnBandas.TabIndex = 7;
            this.btnBandas.Text = "Bandas";
            this.btnBandas.UseVisualStyleBackColor = false;
            this.btnBandas.Click += new System.EventHandler(this.btnBandas_Click);
            // 
            // btnScan
            // 
            this.btnScan.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnScan.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.btnScan.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnScan.ForeColor = System.Drawing.Color.White;
            this.btnScan.Location = new System.Drawing.Point(713, 15);
            this.btnScan.Name = "btnScan";
            this.btnScan.Size = new System.Drawing.Size(75, 30);
            this.btnScan.TabIndex = 5;
            this.btnScan.Text = "Scanear";
            this.btnScan.UseVisualStyleBackColor = false;
            this.btnScan.Click += new System.EventHandler(this.btnScan_Click_1);
            // 
            // lblStatus
            // 
            this.lblStatus.ForeColor = System.Drawing.Color.LightGray;
            this.lblStatus.Location = new System.Drawing.Point(313, 24);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(428, 27);
            this.lblStatus.TabIndex = 4;
            // 
            // btnMenos
            // 
            this.btnMenos.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnMenos.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.btnMenos.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnMenos.ForeColor = System.Drawing.Color.White;
            this.btnMenos.Location = new System.Drawing.Point(356, 15);
            this.btnMenos.Name = "btnMenos";
            this.btnMenos.Size = new System.Drawing.Size(30, 30);
            this.btnMenos.TabIndex = 10;
            this.btnMenos.Text = "-";
            this.btnMenos.UseVisualStyleBackColor = false;
            this.btnMenos.Click += new System.EventHandler(this.btnMenos_Click);
            // 
            // btnNext
            // 
            this.btnNext.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.btnNext.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnNext.ForeColor = System.Drawing.Color.White;
            this.btnNext.Location = new System.Drawing.Point(93, 15);
            this.btnNext.Name = "btnNext";
            this.btnNext.Size = new System.Drawing.Size(75, 30);
            this.btnNext.TabIndex = 2;
            this.btnNext.Text = ">>";
            this.btnNext.UseVisualStyleBackColor = false;
            this.btnNext.Click += new System.EventHandler(this.btnNext_Click);
            // 
            // btnPlay
            // 
            this.btnPlay.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.btnPlay.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnPlay.ForeColor = System.Drawing.Color.White;
            this.btnPlay.Location = new System.Drawing.Point(12, 15);
            this.btnPlay.Name = "btnPlay";
            this.btnPlay.Size = new System.Drawing.Size(75, 30);
            this.btnPlay.TabIndex = 0;
            this.btnPlay.Text = "Play";
            this.btnPlay.UseVisualStyleBackColor = false;
            // 
            // btnPause
            // 
            this.btnPause.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.btnPause.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnPause.ForeColor = System.Drawing.Color.White;
            this.btnPause.Location = new System.Drawing.Point(93, 15);
            this.btnPause.Name = "btnPause";
            this.btnPause.Size = new System.Drawing.Size(75, 30);
            this.btnPause.TabIndex = 1;
            this.btnPause.Text = "Pause";
            this.btnPause.UseVisualStyleBackColor = false;
            this.btnPause.Visible = false;
            // 
            // pnlHeader
            // 
            this.pnlHeader.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(35)))), ((int)(((byte)(35)))), ((int)(((byte)(38)))));
            this.pnlHeader.Controls.Add(this.chkToggleProg);
            this.pnlHeader.Controls.Add(this.button1);
            this.pnlHeader.Controls.Add(this.lblTempoAtual);
            this.pnlHeader.Controls.Add(this.lblTrackCount);
            this.pnlHeader.Controls.Add(this.lblPlaylistTitle);
            this.pnlHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlHeader.Location = new System.Drawing.Point(0, 0);
            this.pnlHeader.Name = "pnlHeader";
            this.pnlHeader.Size = new System.Drawing.Size(800, 41);
            this.pnlHeader.TabIndex = 6;
            // 
            // chkToggleProg
            // 
            this.chkToggleProg.Appearance = System.Windows.Forms.Appearance.Button;
            this.chkToggleProg.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.chkToggleProg.FlatAppearance.CheckedBackColor = System.Drawing.Color.DarkGreen;
            this.chkToggleProg.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.chkToggleProg.ForeColor = System.Drawing.Color.White;
            this.chkToggleProg.Location = new System.Drawing.Point(89, 4);
            this.chkToggleProg.Name = "chkToggleProg";
            this.chkToggleProg.Size = new System.Drawing.Size(50, 30);
            this.chkToggleProg.TabIndex = 4;
            this.chkToggleProg.Text = "Auto";
            this.chkToggleProg.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.chkToggleProg.UseVisualStyleBackColor = false;
            this.chkToggleProg.CheckedChanged += new System.EventHandler(this.chkToggleProg_CheckedChanged);
            // 
            // button1
            // 
            this.button1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.button1.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.button1.ForeColor = System.Drawing.Color.White;
            this.button1.Location = new System.Drawing.Point(3, 4);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(80, 30);
            this.button1.TabIndex = 3;
            this.button1.Text = "Programação";
            this.button1.UseVisualStyleBackColor = false;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // lblTempoAtual
            // 
            this.lblTempoAtual.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lblTempoAtual.BackColor = System.Drawing.Color.Transparent;
            this.lblTempoAtual.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblTempoAtual.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(192)))), ((int)(((byte)(192)))));
            this.lblTempoAtual.Location = new System.Drawing.Point(563, 10);
            this.lblTempoAtual.Name = "lblTempoAtual";
            this.lblTempoAtual.Size = new System.Drawing.Size(140, 18);
            this.lblTempoAtual.TabIndex = 5;
            this.lblTempoAtual.Text = "00:00 / 00:00";
            this.lblTempoAtual.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.lblTempoAtual.MouseDown += new System.Windows.Forms.MouseEventHandler(this.lblTempoAtual_MouseDown);
            // 
            // lblTrackCount
            // 
            this.lblTrackCount.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lblTrackCount.AutoSize = true;
            this.lblTrackCount.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblTrackCount.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(192)))), ((int)(((byte)(192)))));
            this.lblTrackCount.Location = new System.Drawing.Point(723, 10);
            this.lblTrackCount.Name = "lblTrackCount";
            this.lblTrackCount.Size = new System.Drawing.Size(65, 17);
            this.lblTrackCount.TabIndex = 1;
            this.lblTrackCount.Text = "0 músicas";
            // 
            // lblPlaylistTitle
            // 
            this.lblPlaylistTitle.AutoSize = true;
            this.lblPlaylistTitle.Font = new System.Drawing.Font("Segoe UI", 14.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblPlaylistTitle.ForeColor = System.Drawing.Color.White;
            this.lblPlaylistTitle.Location = new System.Drawing.Point(153, 10);
            this.lblPlaylistTitle.Name = "lblPlaylistTitle";
            this.lblPlaylistTitle.Size = new System.Drawing.Size(156, 25);
            this.lblPlaylistTitle.TabIndex = 0;
            this.lblPlaylistTitle.Text = "NOME DA LISTA";
            // 
            // Inicial
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(20)))), ((int)(((byte)(20)))));
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.lvTracks);
            this.Controls.Add(this.pnlHeader);
            this.Controls.Add(this.pnlControls);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "Inicial";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "XP3 Player";
            this.Resize += new System.EventHandler(this.Inicial_Resize);
            this.pnlControls.ResumeLayout(false);
            this.pnlControls.PerformLayout();
            this.pnlHeader.ResumeLayout(false);
            this.pnlHeader.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ListView lvTracks;
        private System.Windows.Forms.ColumnHeader colMusica;
        private System.Windows.Forms.ColumnHeader colBanda;
        private System.Windows.Forms.ColumnHeader colDuracao;
        private System.Windows.Forms.ColumnHeader colT;
        private System.Windows.Forms.ColumnHeader colP;
        private System.Windows.Forms.ColumnHeader colL;
        private System.Windows.Forms.ColumnHeader colUltimaVez;
        private System.Windows.Forms.Panel pnlControls;
        private System.Windows.Forms.Button btnNext;
        private System.Windows.Forms.Button btnMenos;
        private System.Windows.Forms.Button btnPause;
        private System.Windows.Forms.Button btnPlay;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.Button btnBandas;
        private System.Windows.Forms.Button btnScan;
        private System.Windows.Forms.Panel pnlHeader;
        private System.Windows.Forms.Label lblPlaylistTitle;
        private System.Windows.Forms.Label lblTrackCount;
        private System.Windows.Forms.Timer timerProgresso;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.CheckBox chkToggleProg;
        private System.Windows.Forms.Label lblStatusCue;
        private System.Windows.Forms.Label lblTempoAtual;

    }
}
