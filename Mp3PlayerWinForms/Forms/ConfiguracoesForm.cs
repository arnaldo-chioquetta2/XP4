using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using XP3.Services;
using XP3.Visualizers;

namespace XP3.Forms
{
    public sealed class VisualizerConfigItem
    {
        public string Id { get; set; }
        public string Nome { get; set; }
        public Type Tipo { get; set; }
        public bool Enabled { get; set; }
    }

    public class ConfiguracoesForm : Form
    {
        private readonly IniFileService _iniService;
        private readonly List<VisualizerConfigItem> _defaultItems;
        private readonly ListView _lista;
        private readonly Button _btnSubir;
        private readonly Button _btnDescer;
        private readonly Button _btnSalvar;
        private readonly Button _btnCancelar;
        private readonly Button _btnRestaurar;
        private readonly Button _btnPrevisualizar;
        private VisualizerBase _previewWindow;
        private Type _previewType;
        private Timer _previewTimer;
        private float _previewTime;
        private readonly Random _previewRandom = new Random();

        public List<VisualizerConfigItem> Items { get; private set; }

        public ConfiguracoesForm(IEnumerable<VisualizerConfigItem> items, IEnumerable<VisualizerConfigItem> defaultItems, IniFileService iniService)
        {
            _iniService = iniService;
            Items = items == null
                ? new List<VisualizerConfigItem>()
                : items.Select(CloneItem).ToList();
            _defaultItems = defaultItems == null
                ? new List<VisualizerConfigItem>()
                : defaultItems.Select(CloneItem).ToList();

            Text = "Configurações";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(470, 390);

            Label titulo = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                Location = new Point(18, 15),
                Text = "Visualizações"
            };
            Controls.Add(titulo);

            _lista = new ListView
            {
                CheckBoxes = true,
                FullRowSelect = true,
                GridLines = true,
                HideSelection = false,
                Location = new Point(18, 48),
                MultiSelect = false,
                Size = new Size(434, 245),
                View = View.Details
            };
            _lista.Columns.Add("Visualização", 410);
            _lista.SelectedIndexChanged += (s, e) => AtualizarEstadoBotoes();
            Controls.Add(_lista);

            _btnSubir = CriarBotao("Subir", 18, 305, BtnSubir_Click);
            _btnDescer = CriarBotao("Descer", 101, 305, BtnDescer_Click);
            _btnRestaurar = CriarBotao("Restaurar padrão", 184, 305, BtnRestaurar_Click);
            _btnPrevisualizar = CriarBotao("Previsualizar", 18, 345, BtnPrevisualizar_Click, 100);
            _btnCancelar = CriarBotao("Cancelar", 286, 345, (s, e) => DialogResult = DialogResult.Cancel);
            _btnSalvar = CriarBotao("Salvar", 369, 345, BtnSalvar_Click);

            FormClosed += ConfiguracoesForm_FormClosed;

            PreencherLista(Items);

            if (_lista.Items.Count > 0)
            {
                _lista.Items[0].Selected = true;
            }
            AtualizarEstadoBotoes();
        }

        private Button CriarBotao(string texto, int x, int y, EventHandler handler, int largura = 75)
        {
            Button button = new Button
            {
                Text = texto,
                Location = new Point(x, y),
                Size = new Size(texto == "Restaurar padrão" ? 95 : largura, 30)
            };
            button.Click += handler;
            Controls.Add(button);
            return button;
        }

        private void BtnPrevisualizar_Click(object sender, EventArgs e)
        {
            if (_lista.SelectedItems.Count == 0)
            {
                return;
            }

            VisualizerConfigItem item = _lista.SelectedItems[0].Tag as VisualizerConfigItem;
            if (item == null || item.Tipo == null)
            {
                return;
            }

            if (_previewWindow != null && !_previewWindow.IsDisposed && _previewType == item.Tipo)
            {
                _previewWindow.Close();
                return;
            }

            FecharPrevisualizacao();

            try
            {
                VisualizerBase preview = Activator.CreateInstance(item.Tipo) as VisualizerBase;
                if (preview == null)
                {
                    return;
                }

                preview.Text = "Pré-visualização - " + item.Nome;
                preview.ShowInTaskbar = false;
                preview.TopMost = true;
                preview.StartPosition = FormStartPosition.CenterScreen;
                preview.ClientSize = new Size(640, 360);
                preview.FormBorderStyle = FormBorderStyle.Sizable;
                preview.FormClosed += Previsualizacao_FormClosed;

                _previewType = item.Tipo;
                _previewWindow = preview;
                preview.Show(this);
                preview.Activate();
                IniciarAnimacaoPrevisualizacao();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Não foi possível abrir a pré-visualização.\n" + ex.Message,
                    "Pré-visualização",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                FecharPrevisualizacao();
            }
        }

        private void Previsualizacao_FormClosed(object sender, FormClosedEventArgs e)
        {
            VisualizerBase closedPreview = sender as VisualizerBase;
            if (ReferenceEquals(_previewWindow, closedPreview))
            {
                PararAnimacaoPrevisualizacao();
                _previewWindow = null;
                _previewType = null;
            }
        }

        private void IniciarAnimacaoPrevisualizacao()
        {
            PararAnimacaoPrevisualizacao();

            _previewTime = 0f;
            _previewTimer = new Timer { Interval = 33 };
            _previewTimer.Tick += PreviewTimer_Tick;
            _previewTimer.Start();
        }

        private void PreviewTimer_Tick(object sender, EventArgs e)
        {
            if (_previewWindow == null || _previewWindow.IsDisposed)
            {
                PararAnimacaoPrevisualizacao();
                return;
            }

            _previewTime += 0.05f;
            float[] data = GerarDadosFakePreview();
            float maxVol =
                0.55f
                + 0.35f * (float)Math.Abs(Math.Sin(_previewTime * 1.7f))
                + 0.10f * (float)Math.Abs(Math.Sin(_previewTime * 4.3f));

            if (maxVol > 1f)
            {
                maxVol = 1f;
            }

            _previewWindow.UpdateData(data, maxVol);
            _previewWindow.Invalidate();
        }

        private float[] GerarDadosFakePreview()
        {
            const int length = 256;
            float[] data = new float[length];
            float bassPulse = 0.75f + 0.25f * (float)Math.Sin(_previewTime * 2.0f);
            float midPulse = 0.55f + 0.45f * (float)Math.Sin(_previewTime * 3.1f + 1.2f);
            float treblePulse = 0.45f + 0.55f * (float)Math.Sin(_previewTime * 6.0f + 0.7f);

            for (int i = 0; i < length; i++)
            {
                float band;
                if (i < 24)
                {
                    band = 0.65f * bassPulse;
                }
                else if (i < 96)
                {
                    band = 0.45f * midPulse;
                }
                else
                {
                    band = 0.30f * treblePulse;
                }

                float wave =
                    0.20f * (float)Math.Abs(Math.Sin(_previewTime * 1.8f + i * 0.09f))
                    + 0.15f * (float)Math.Abs(Math.Sin(_previewTime * 3.7f + i * 0.031f));
                float noise = (float)_previewRandom.NextDouble() * 0.08f;
                data[i] = Math.Min(1f, Math.Max(0f, band + wave + noise));
            }

            return data;
        }

        private void PararAnimacaoPrevisualizacao()
        {
            if (_previewTimer == null)
            {
                return;
            }

            _previewTimer.Stop();
            _previewTimer.Tick -= PreviewTimer_Tick;
            _previewTimer.Dispose();
            _previewTimer = null;
        }

        private void FecharPrevisualizacao()
        {
            VisualizerBase preview = _previewWindow;
            _previewWindow = null;
            _previewType = null;
            PararAnimacaoPrevisualizacao();

            if (preview != null && !preview.IsDisposed)
            {
                preview.FormClosed -= Previsualizacao_FormClosed;
                preview.Close();
            }
        }

        private void ConfiguracoesForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            FecharPrevisualizacao();
        }

        private static VisualizerConfigItem CloneItem(VisualizerConfigItem item)
        {
            return new VisualizerConfigItem
            {
                Id = item.Id,
                Nome = item.Nome,
                Tipo = item.Tipo,
                Enabled = item.Enabled
            };
        }

        private void BtnSubir_Click(object sender, EventArgs e)
        {
            MoverItem(-1);
        }

        private void BtnDescer_Click(object sender, EventArgs e)
        {
            MoverItem(1);
        }

        private void MoverItem(int delta)
        {
            if (_lista.SelectedIndices.Count == 0)
            {
                return;
            }

            int index = _lista.SelectedIndices[0];
            int novoIndex = index + delta;
            if (novoIndex < 0 || novoIndex >= _lista.Items.Count)
            {
                return;
            }

            ListViewItem item = _lista.Items[index];
            _lista.Items.RemoveAt(index);
            _lista.Items.Insert(novoIndex, item);
            item.Selected = true;
            item.Focused = true;
            AtualizarEstadoBotoes();
        }

        private void BtnRestaurar_Click(object sender, EventArgs e)
        {
            Items = _defaultItems.Select(CloneItem).ToList();
            PreencherLista(Items);
            if (_lista.Items.Count > 0)
            {
                _lista.Items[0].Selected = true;
            }
            AtualizarEstadoBotoes();
        }

        private void PreencherLista(IEnumerable<VisualizerConfigItem> items)
        {
            _lista.Items.Clear();
            foreach (VisualizerConfigItem item in items)
            {
                ListViewItem row = new ListViewItem(item.Nome);
                row.Tag = item;
                row.Checked = item.Enabled;
                _lista.Items.Add(row);
            }
        }

        private void BtnSalvar_Click(object sender, EventArgs e)
        {
            int enabledCount = 0;
            List<string> order = new List<string>();
            List<string> disabled = new List<string>();

            for (int i = 0; i < _lista.Items.Count; i++)
            {
                VisualizerConfigItem item = (VisualizerConfigItem)_lista.Items[i].Tag;
                item.Enabled = _lista.Items[i].Checked;
                order.Add(item.Id);
                if (item.Enabled)
                {
                    enabledCount++;
                }
                else
                {
                    disabled.Add(item.Id);
                }
            }

            if (enabledCount == 0)
            {
                MessageBox.Show("Deixe pelo menos uma visualização habilitada.", "Visualizações", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _iniService.Write("Visualizadores", "Ordem", string.Join("|", order));
            _iniService.Write("Visualizadores", "Desabilitados", string.Join("|", disabled));
            DialogResult = DialogResult.OK;
        }

        private void AtualizarEstadoBotoes()
        {
            int index = _lista.SelectedIndices.Count == 0 ? -1 : _lista.SelectedIndices[0];
            _btnSubir.Enabled = index > 0;
            _btnDescer.Enabled = index >= 0 && index < _lista.Items.Count - 1;
        }
    }
}
