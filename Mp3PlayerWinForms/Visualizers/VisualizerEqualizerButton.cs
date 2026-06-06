using System.Windows.Forms;
using System.Drawing;

namespace XP3.Visualizers
{
    public class VisualizerEqualizerButton : Button
    {
        public VisualizerEqualizerButton()
        {
            this.Text = "EQ";
            this.Size = new Size(30, 30);
            this.FlatStyle = FlatStyle.Flat;
            this.FlatAppearance.BorderSize = 0;
            this.BackColor = Color.FromArgb(50, 50, 50);
            this.ForeColor = Color.White;
            this.Font = new Font("Microsoft Sans Serif", 8, FontStyle.Bold);
        }
    }
}
