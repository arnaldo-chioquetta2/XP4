using System.Windows.Forms;

namespace XP3.Forms
{
    partial class BandaPaisForm
    {
        private System.ComponentModel.IContainer components = null;
        private Label lblBanda;
        private Label lblBandaValor;
        private Label lblPais;
        private ComboBox cmbPais;
        private Button btnSalvar;
        private Button btnCancelar;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            lblBanda = new Label();
            lblBandaValor = new Label();
            lblPais = new Label();
            cmbPais = new ComboBox();
            btnSalvar = new Button();
            btnCancelar = new Button();
            SuspendLayout();
            // 
            // lblBanda
            // 
            lblBanda.AutoSize = true;
            lblBanda.Location = new System.Drawing.Point(16, 18);
            lblBanda.Name = "lblBanda";
            lblBanda.Size = new System.Drawing.Size(44, 15);
            lblBanda.TabIndex = 0;
            lblBanda.Text = "Banda:";
            // 
            // lblBandaValor
            // 
            lblBandaValor.AutoEllipsis = true;
            lblBandaValor.Location = new System.Drawing.Point(82, 18);
            lblBandaValor.Name = "lblBandaValor";
            lblBandaValor.Size = new System.Drawing.Size(350, 15);
            lblBandaValor.TabIndex = 1;
            lblBandaValor.Text = "-";
            // 
            // lblPais
            // 
            lblPais.AutoSize = true;
            lblPais.Location = new System.Drawing.Point(16, 55);
            lblPais.Name = "lblPais";
            lblPais.Size = new System.Drawing.Size(32, 15);
            lblPais.TabIndex = 2;
            lblPais.Text = "País:";
            // 
            // cmbPais
            // 
            cmbPais.DropDownStyle = ComboBoxStyle.DropDown;
            cmbPais.FormattingEnabled = true;
            cmbPais.Location = new System.Drawing.Point(82, 51);
            cmbPais.Name = "cmbPais";
            cmbPais.Size = new System.Drawing.Size(350, 23);
            cmbPais.TabIndex = 3;
            // 
            // btnSalvar
            // 
            btnSalvar.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnSalvar.Location = new System.Drawing.Point(270, 95);
            btnSalvar.Name = "btnSalvar";
            btnSalvar.Size = new System.Drawing.Size(75, 27);
            btnSalvar.TabIndex = 4;
            btnSalvar.Text = "Salvar";
            btnSalvar.UseVisualStyleBackColor = true;
            btnSalvar.Click += btnSalvar_Click;
            // 
            // btnCancelar
            // 
            btnCancelar.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnCancelar.DialogResult = DialogResult.Cancel;
            btnCancelar.Location = new System.Drawing.Point(357, 95);
            btnCancelar.Name = "btnCancelar";
            btnCancelar.Size = new System.Drawing.Size(75, 27);
            btnCancelar.TabIndex = 5;
            btnCancelar.Text = "Cancelar";
            btnCancelar.UseVisualStyleBackColor = true;
            btnCancelar.Click += btnCancelar_Click;
            // 
            // BandaPaisForm
            // 
            AcceptButton = btnSalvar;
            CancelButton = btnCancelar;
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(450, 140);
            Controls.Add(btnCancelar);
            Controls.Add(btnSalvar);
            Controls.Add(cmbPais);
            Controls.Add(lblPais);
            Controls.Add(lblBandaValor);
            Controls.Add(lblBanda);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "BandaPaisForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "País da Banda";
            ResumeLayout(false);
            PerformLayout();
        }
    }
}
