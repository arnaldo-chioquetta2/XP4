using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace XP3.Controls
{
    public class BigCheckedListBox : ListBox
    {
        // Armazena quais índices estão marcados
        private HashSet<int> _checkedIndices = new HashSet<int>();

        // Define o tamanho do Checkbox (30px é aprox. 50% maior que o padrão)
        public int CheckBoxSize { get; set; } = 30;

        public BigCheckedListBox()
        {
            this.DrawMode = DrawMode.OwnerDrawFixed;
            this.ItemHeight = 60; // Altura da linha para acomodar o check grande
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.ForeColor = Color.White;
            this.BorderStyle = BorderStyle.None;
        }

        public void SetItemChecked(int index, bool isChecked)
        {
            if (isChecked) _checkedIndices.Add(index);
            else _checkedIndices.Remove(index);
            this.Invalidate(); // Força redesenhar
        }

        public bool GetItemChecked(int index)
        {
            return _checkedIndices.Contains(index);
        }

        public void ClearChecked() => _checkedIndices.Clear();

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= Items.Count) return;

            Graphics g = e.Graphics;
            bool isChecked = GetItemChecked(e.Index);
            bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

            // 1. Fundo da Linha
            Color corFundo = isSelected ? Color.FromArgb(65, 65, 65) : this.BackColor;
            using (SolidBrush sb = new SolidBrush(corFundo))
                g.FillRectangle(sb, e.Bounds);

            // 2. Desenho do Checkbox Customizado
            int margem = (e.Bounds.Height - CheckBoxSize) / 2;
            Rectangle rectCheck = new Rectangle(e.Bounds.X + 10, e.Bounds.Y + margem, CheckBoxSize, CheckBoxSize);

            using (Pen p = new Pen(Color.Gray, 2))
                g.DrawRectangle(p, rectCheck);

            if (isChecked)
            {
                // Preenchimento verde sólido para o Check
                using (SolidBrush sb = new SolidBrush(Color.LightGreen))
                    g.FillRectangle(sb, rectCheck.X + 4, rectCheck.Y + 4, CheckBoxSize - 8, CheckBoxSize - 8);
            }

            // 3. Desenho do Texto
            string texto = this.GetItemText(Items[e.Index]);
            using (SolidBrush sb = new SolidBrush(this.ForeColor))
            {
                // Posiciona o texto após o checkbox grande
                float xTexto = rectCheck.Right + 15;
                float yTexto = e.Bounds.Y + (e.Bounds.Height - g.MeasureString(texto, e.Font).Height) / 2;
                g.DrawString(texto, e.Font, sb, xTexto, yTexto);
            }
        }
    }
}