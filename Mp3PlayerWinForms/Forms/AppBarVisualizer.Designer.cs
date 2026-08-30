namespace XP3.Forms
{
    partial class AppBarVisualizer
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
            this.panelVisualizerHost = new System.Windows.Forms.Panel();
            this.pbTempo = new BarraProgressoAppBar();
            this.btnFechar = new System.Windows.Forms.Button();
            this.btnMinimizar = new System.Windows.Forms.Button();
            this.pnlResize = new System.Windows.Forms.Panel();
            this.SuspendLayout();
            //
            // panelVisualizerHost
            //
            this.panelVisualizerHost.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(20)))), ((int)(((byte)(20)))));
            this.panelVisualizerHost.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelVisualizerHost.Location = new System.Drawing.Point(0, 12);
            this.panelVisualizerHost.Name = "panelVisualizerHost";
            this.panelVisualizerHost.Size = new System.Drawing.Size(800, 104);
            this.panelVisualizerHost.TabIndex = 0;
            //
            // pbTempo
            //
            this.pbTempo.Dock = System.Windows.Forms.DockStyle.Top;
            this.pbTempo.Location = new System.Drawing.Point(0, 6);
            this.pbTempo.Maximum = 1000;
            this.pbTempo.Minimum = 0;
            this.pbTempo.Name = "pbTempo";
            this.pbTempo.Size = new System.Drawing.Size(800, 6);
            this.pbTempo.TabIndex = 7;
            this.pbTempo.TabStop = false;
            this.pbTempo.Visible = true;
            this.pbTempo.Value = 0;
            //
            // btnFechar
            //
            this.btnFechar.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnFechar.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.btnFechar.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(90)))), ((int)(((byte)(90)))));
            this.btnFechar.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnFechar.ForeColor = System.Drawing.Color.White;
            this.btnFechar.Location = new System.Drawing.Point(762, 8);
            this.btnFechar.Name = "btnFechar";
            this.btnFechar.Size = new System.Drawing.Size(30, 30);
            this.btnFechar.TabIndex = 1;
            this.btnFechar.Text = "X";
            this.btnFechar.UseVisualStyleBackColor = false;
            this.btnFechar.Click += new System.EventHandler(this.btnFechar_Click);
            //
            // btnMinimizar
            //
            this.btnMinimizar.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnMinimizar.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.btnMinimizar.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(90)))), ((int)(((byte)(90)))));
            this.btnMinimizar.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnMinimizar.ForeColor = System.Drawing.Color.White;
            this.btnMinimizar.Location = new System.Drawing.Point(726, 8);
            this.btnMinimizar.Name = "btnMinimizar";
            this.btnMinimizar.Size = new System.Drawing.Size(30, 30);
            this.btnMinimizar.TabIndex = 6;
            this.btnMinimizar.Text = "_";
            this.btnMinimizar.UseVisualStyleBackColor = false;
            this.btnMinimizar.Click += new System.EventHandler(this.btnMinimizar_Click);
            //
            // pnlResize
            //
            this.pnlResize.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(20)))), ((int)(((byte)(20)))));
            this.pnlResize.Cursor = System.Windows.Forms.Cursors.SizeNS;
            this.pnlResize.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlResize.Location = new System.Drawing.Point(0, 0);
            this.pnlResize.Name = "pnlResize";
            this.pnlResize.Size = new System.Drawing.Size(800, 6);
            this.pnlResize.TabIndex = 5;
            this.pnlResize.MouseDown += new System.Windows.Forms.MouseEventHandler(this.pnlResize_MouseDown);
            this.pnlResize.MouseMove += new System.Windows.Forms.MouseEventHandler(this.pnlResize_MouseMove);
            this.pnlResize.MouseUp += new System.Windows.Forms.MouseEventHandler(this.pnlResize_MouseUp);
            //
            // AppBarVisualizer
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(20)))), ((int)(((byte)(20)))));
            this.ClientSize = new System.Drawing.Size(800, 110);
            this.Controls.Add(this.btnMinimizar);
            this.Controls.Add(this.btnFechar);
            this.Controls.Add(this.panelVisualizerHost);
            this.Controls.Add(this.pbTempo);
            this.Controls.Add(this.pnlResize);
            this.ControlBox = false;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "AppBarVisualizer";
            this.ShowIcon = false;
            this.Text = "";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            this.TopMost = true;
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panelVisualizerHost;
        private BarraProgressoAppBar pbTempo;
        private System.Windows.Forms.Button btnFechar;
        private System.Windows.Forms.Button btnMinimizar;
        private System.Windows.Forms.Panel pnlResize;
    }
}
