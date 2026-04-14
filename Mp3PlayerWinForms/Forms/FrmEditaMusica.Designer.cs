using XP3.Controls;

namespace XP3.Forms
{
    partial class FrmEditaMusica
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.lblMusica = new System.Windows.Forms.Label();
            this.lblBanda = new System.Windows.Forms.Label();
            this.lblInicio = new System.Windows.Forms.Label();
            this.mskInicio = new System.Windows.Forms.MaskedTextBox();
            this.lblFim = new System.Windows.Forms.Label();
            this.mskFim = new System.Windows.Forms.MaskedTextBox();
            this.btnTestar = new System.Windows.Forms.Button();
            this.btnSalvar = new System.Windows.Forms.Button();
            this.btnCancelar = new System.Windows.Forms.Button();
            this.barraProgresso = new XP3.Controls.ModernSeekBar();
            this.SuspendLayout();
            // 
            // lblMusica
            // 
            this.lblMusica.AutoSize = true;
            this.lblMusica.Font = new System.Drawing.Font("Segoe UI", 14F, System.Drawing.FontStyle.Bold);
            this.lblMusica.ForeColor = System.Drawing.Color.White;
            this.lblMusica.Location = new System.Drawing.Point(10, 13);
            this.lblMusica.Name = "lblMusica";
            this.lblMusica.Size = new System.Drawing.Size(160, 25);
            this.lblMusica.TabIndex = 0;
            this.lblMusica.Text = "Nome da Música";
            // 
            // lblBanda
            // 
            this.lblBanda.AutoSize = true;
            this.lblBanda.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.lblBanda.ForeColor = System.Drawing.Color.LightGray;
            this.lblBanda.Location = new System.Drawing.Point(12, 36);
            this.lblBanda.Name = "lblBanda";
            this.lblBanda.Size = new System.Drawing.Size(107, 19);
            this.lblBanda.TabIndex = 1;
            this.lblBanda.Text = "Nome da Banda";
            // 
            // lblInicio
            // 
            this.lblInicio.AutoSize = true;
            this.lblInicio.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblInicio.ForeColor = System.Drawing.Color.MediumTurquoise;
            this.lblInicio.Location = new System.Drawing.Point(12, 76);
            this.lblInicio.Name = "lblInicio";
            this.lblInicio.Size = new System.Drawing.Size(49, 19);
            this.lblInicio.TabIndex = 2;
            this.lblInicio.Text = "Início:";
            // 
            // mskInicio
            // 
            this.mskInicio.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(40)))), ((int)(((byte)(40)))));
            this.mskInicio.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.mskInicio.Font = new System.Drawing.Font("Segoe UI", 12F);
            this.mskInicio.ForeColor = System.Drawing.Color.White;
            this.mskInicio.Location = new System.Drawing.Point(56, 73);
            this.mskInicio.Mask = "00:00";
            this.mskInicio.Name = "mskInicio";
            this.mskInicio.Size = new System.Drawing.Size(47, 29);
            this.mskInicio.TabIndex = 3;
            this.mskInicio.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.mskInicio.ValidatingType = typeof(System.DateTime);
            // 
            // lblFim
            // 
            this.lblFim.AutoSize = true;
            this.lblFim.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblFim.ForeColor = System.Drawing.Color.MediumTurquoise;
            this.lblFim.Location = new System.Drawing.Point(124, 76);
            this.lblFim.Name = "lblFim";
            this.lblFim.Size = new System.Drawing.Size(37, 19);
            this.lblFim.TabIndex = 4;
            this.lblFim.Text = "Fim:";
            // 
            // mskFim
            // 
            this.mskFim.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(40)))), ((int)(((byte)(40)))));
            this.mskFim.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.mskFim.Font = new System.Drawing.Font("Segoe UI", 12F);
            this.mskFim.ForeColor = System.Drawing.Color.White;
            this.mskFim.Location = new System.Drawing.Point(158, 73);
            this.mskFim.Mask = "00:00";
            this.mskFim.Name = "mskFim";
            this.mskFim.Size = new System.Drawing.Size(47, 29);
            this.mskFim.TabIndex = 5;
            this.mskFim.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.mskFim.ValidatingType = typeof(System.DateTime);
            // 
            // btnTestar
            // 
            this.btnTestar.BackColor = System.Drawing.Color.MediumSeaGreen;
            this.btnTestar.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnTestar.FlatAppearance.BorderSize = 0;
            this.btnTestar.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnTestar.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.btnTestar.ForeColor = System.Drawing.Color.White;
            this.btnTestar.Location = new System.Drawing.Point(231, 72);
            this.btnTestar.Name = "btnTestar";
            this.btnTestar.Size = new System.Drawing.Size(81, 27);
            this.btnTestar.TabIndex = 6;
            this.btnTestar.Text = "► Testar";
            this.btnTestar.UseVisualStyleBackColor = false;
            // 
            // btnSalvar
            // 
            this.btnSalvar.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSalvar.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(122)))), ((int)(((byte)(204)))));
            this.btnSalvar.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnSalvar.FlatAppearance.BorderSize = 0;
            this.btnSalvar.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSalvar.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold);
            this.btnSalvar.ForeColor = System.Drawing.Color.White;
            this.btnSalvar.Location = new System.Drawing.Point(74, 141);
            this.btnSalvar.Name = "btnSalvar";
            this.btnSalvar.Size = new System.Drawing.Size(73, 26);
            this.btnSalvar.TabIndex = 8;
            this.btnSalvar.Text = "Salvar";
            this.btnSalvar.UseVisualStyleBackColor = false;
            // 
            // btnCancelar
            // 
            this.btnCancelar.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancelar.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.btnCancelar.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnCancelar.FlatAppearance.BorderSize = 0;
            this.btnCancelar.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnCancelar.Font = new System.Drawing.Font("Segoe UI", 9.5F);
            this.btnCancelar.ForeColor = System.Drawing.Color.White;
            this.btnCancelar.Location = new System.Drawing.Point(156, 141);
            this.btnCancelar.Name = "btnCancelar";
            this.btnCancelar.Size = new System.Drawing.Size(73, 26);
            this.btnCancelar.TabIndex = 9;
            this.btnCancelar.Text = "Cancelar";
            this.btnCancelar.UseVisualStyleBackColor = false;
            // 
            // barraProgresso
            // 
            this.barraProgresso.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.barraProgresso.Cursor = System.Windows.Forms.Cursors.Hand;
            this.barraProgresso.Location = new System.Drawing.Point(14, 124);
            this.barraProgresso.Name = "barraProgresso";
            this.barraProgresso.ProgressColor = System.Drawing.Color.Cyan;
            this.barraProgresso.Size = new System.Drawing.Size(298, 11);
            this.barraProgresso.TabIndex = 7;
            this.barraProgresso.ThumbColor = System.Drawing.Color.White;
            this.barraProgresso.TrackColor = System.Drawing.Color.FromArgb(((int)(((byte)(50)))), ((int)(((byte)(50)))), ((int)(((byte)(50)))));
            this.barraProgresso.Value = 0D;
            // 
            // FrmEditaMusica
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(30)))), ((int)(((byte)(30)))));
            this.ClientSize = new System.Drawing.Size(331, 193);
            this.Controls.Add(this.btnCancelar);
            this.Controls.Add(this.btnSalvar);
            this.Controls.Add(this.barraProgresso);
            this.Controls.Add(this.btnTestar);
            this.Controls.Add(this.mskFim);
            this.Controls.Add(this.lblFim);
            this.Controls.Add(this.mskInicio);
            this.Controls.Add(this.lblInicio);
            this.Controls.Add(this.lblBanda);
            this.Controls.Add(this.lblMusica);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FrmEditaMusica";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Ajuste de Tempo (Auto-Cue)";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblMusica;
        private System.Windows.Forms.Label lblBanda;
        private System.Windows.Forms.Label lblInicio;
        private System.Windows.Forms.MaskedTextBox mskInicio;
        private System.Windows.Forms.Label lblFim;
        private System.Windows.Forms.MaskedTextBox mskFim;
        private System.Windows.Forms.Button btnTestar;
        private System.Windows.Forms.Button btnSalvar;
        private System.Windows.Forms.Button btnCancelar;
        private ModernSeekBar barraProgresso;
    }
}