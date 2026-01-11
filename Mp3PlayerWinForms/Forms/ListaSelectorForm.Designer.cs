namespace XP3.Forms
{
    partial class ListaSelectorForm
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
            this.clbPlaylists = new System.Windows.Forms.CheckedListBox();
            this.panelAcoes = new System.Windows.Forms.Panel();
            this.btnExcluir = new System.Windows.Forms.Button();
            this.btnMover = new System.Windows.Forms.Button();
            this.btnCopiar = new System.Windows.Forms.Button();
            this.panelAcoes.SuspendLayout();
            this.SuspendLayout();
            // 
            // clbPlaylists
            // 
            this.clbPlaylists.CheckOnClick = true;
            this.clbPlaylists.Dock = System.Windows.Forms.DockStyle.Fill;
            this.clbPlaylists.FormattingEnabled = true;
            this.clbPlaylists.Location = new System.Drawing.Point(0, 0);
            this.clbPlaylists.Name = "clbPlaylists";
            this.clbPlaylists.Size = new System.Drawing.Size(284, 361);
            this.clbPlaylists.TabIndex = 0;
            // 
            // panelAcoes
            // 
            this.panelAcoes.Controls.Add(this.btnExcluir);
            this.panelAcoes.Controls.Add(this.btnMover);
            this.panelAcoes.Controls.Add(this.btnCopiar);
            this.panelAcoes.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelAcoes.Location = new System.Drawing.Point(0, 361);
            this.panelAcoes.Name = "panelAcoes";
            this.panelAcoes.Size = new System.Drawing.Size(284, 50);
            this.panelAcoes.TabIndex = 1;
            // 
            // btnExcluir
            // 
            this.btnExcluir.Location = new System.Drawing.Point(191, 10);
            this.btnExcluir.Name = "btnExcluir";
            this.btnExcluir.Size = new System.Drawing.Size(85, 30);
            this.btnExcluir.TabIndex = 2;
            this.btnExcluir.Text = "Excluir";
            this.btnExcluir.UseVisualStyleBackColor = true;
            // 
            // btnMover
            // 
            this.btnMover.Location = new System.Drawing.Point(100, 10);
            this.btnMover.Name = "btnMover";
            this.btnMover.Size = new System.Drawing.Size(85, 30);
            this.btnMover.TabIndex = 1;
            this.btnMover.Text = "Mover";
            this.btnMover.UseVisualStyleBackColor = true;
            // 
            // btnCopiar
            // 
            this.btnCopiar.Location = new System.Drawing.Point(9, 10);
            this.btnCopiar.Name = "btnCopiar";
            this.btnCopiar.Size = new System.Drawing.Size(85, 30);
            this.btnCopiar.TabIndex = 0;
            this.btnCopiar.Text = "Copiar";
            this.btnCopiar.UseVisualStyleBackColor = true;
            // 
            // ListaSelectorForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(284, 411);
            this.Controls.Add(this.clbPlaylists);
            this.Controls.Add(this.panelAcoes);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ListaSelectorForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Gerenciar Listas";
            this.panelAcoes.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        public System.Windows.Forms.CheckedListBox clbPlaylists;
        private System.Windows.Forms.Panel panelAcoes;
        public System.Windows.Forms.Button btnExcluir;
        public System.Windows.Forms.Button btnMover;
        public System.Windows.Forms.Button btnCopiar;
    }
}