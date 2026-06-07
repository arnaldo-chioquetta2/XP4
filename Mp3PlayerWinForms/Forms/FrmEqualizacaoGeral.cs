
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using XP3.Data;
using XP3.Models;

namespace XP3.Forms
{
    public class FrmEqualizacaoGeral : Form
    {
        private const string TextoSemEqualizacao = "(Sem equalizacao)";

        private readonly Action<int[], bool> _previewAction;
        private readonly Action _restoreAction;

        private readonly TrackBar[] _sliders = new TrackBar[EqualizerPreset.BandCount];
        private readonly Label[] _valueLabels = new Label[EqualizerPreset.BandCount];
        private readonly string[] _labelsBandas = { "60", "170", "310", "600", "1K", "3K", "6K", "8K", "10K", "12K" };

        private ComboBox cboPresets;
        private PictureBox picWave;
        private Button btnOk;
        private Button btnDelete;
        private Button btnSave;
        private CheckBox chkEqualizacaoAtiva;
        private Label lblInfo;

        private List<EqualizerPreset> _presets = new List<EqualizerPreset>();
        private bool _mudando;
        private bool _confirmado;

        public FrmEqualizacaoGeral(Action<int[], bool> previewAction, Action restoreAction)
        {
            _previewAction = previewAction;
            _restoreAction = restoreAction;

            InitializeComponent();

            Load += FrmEqualizacaoGeral_Load;
            FormClosing += FrmEqualizacaoGeral_FormClosing;
        }

        private void InitializeComponent()
        {
            SuspendLayout();

            Text = "Equalizacao geral";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = true;
            MaximizeBox = false;
            ClientSize = new Size(900, 520);
            BackColor = Color.FromArgb(224, 224, 224);

            chkEqualizacaoAtiva = new CheckBox
            {
                Text = "Equalizacao ativa",
                AutoSize = true,
                Location = new Point(24, 18),
                Checked = true
            };
            chkEqualizacaoAtiva.CheckedChanged += ChkEqualizacaoAtiva_CheckedChanged;

            var lblPresets = new Label
            {
                Text = "Presets:",
                AutoSize = true,
                Location = new Point(610, 18)
            };

            cboPresets = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDown,
                Location = new Point(670, 14),
                Size = new Size(210, 24)
            };
            cboPresets.SelectedIndexChanged += CboPresets_SelectedIndexChanged;
            cboPresets.TextUpdate += CboPresets_TextUpdate;
            cboPresets.KeyDown += CboPresets_KeyDown;

            picWave = new PictureBox
            {
                Location = new Point(24, 54),
                Size = new Size(852, 125),
                BackColor = Color.FromArgb(235, 235, 235),
                BorderStyle = BorderStyle.FixedSingle
            };
            picWave.Paint += PicWave_Paint;

            lblInfo = new Label
            {
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Location = new Point(24, 184),
                Size = new Size(852, 18),
                ForeColor = Color.FromArgb(90, 50, 20)
            };

            var panelSliders = new Panel
            {
                Location = new Point(24, 210),
                Size = new Size(852, 235),
                BackColor = Color.Transparent
            };

            for (int i = 0; i < EqualizerPreset.BandCount; i++)
            {
                var valueLabel = new Label
                {
                    Text = "0 dB",
                    TextAlign = ContentAlignment.MiddleCenter,
                    Size = new Size(62, 18),
                    Location = new Point(12 + (i * 82), 0),
                    ForeColor = Color.Black
                };

                var slider = new TrackBar
                {
                    Orientation = Orientation.Vertical,
                    Minimum = -12,
                    Maximum = 12,
                    TickFrequency = 3,
                    LargeChange = 1,
                    SmallChange = 1,
                    Height = 150,
                    Width = 45,
                    Location = new Point(20 + (i * 82), 26),
                    BackColor = BackColor,
                    Tag = i
                };
                slider.Scroll += Slider_Scroll;

                var freqLabel = new Label
                {
                    Text = _labelsBandas[i],
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = Color.White,
                    BackColor = Color.Black,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                    Size = new Size(48, 22),
                    Location = new Point(18 + (i * 82), 185)
                };

                _sliders[i] = slider;
                _valueLabels[i] = valueLabel;

                panelSliders.Controls.Add(valueLabel);
                panelSliders.Controls.Add(slider);
                panelSliders.Controls.Add(freqLabel);
            }

            btnOk = new Button
            {
                Text = "OK",
                Location = new Point(24, 465),
                Size = new Size(120, 32)
            };
            btnOk.Click += BtnOk_Click;

            btnDelete = new Button
            {
                Text = "Deletar EQ",
                Location = new Point(154, 465),
                Size = new Size(120, 32)
            };
            btnDelete.Click += BtnDelete_Click;

            btnSave = new Button
            {
                Text = "Salvar EQ",
                Location = new Point(284, 465),
                Size = new Size(120, 32),
                Enabled = false
            };
            btnSave.Click += BtnSave_Click;

            Controls.Add(chkEqualizacaoAtiva);
            Controls.Add(lblPresets);
            Controls.Add(cboPresets);
            Controls.Add(picWave);
            Controls.Add(lblInfo);
            Controls.Add(panelSliders);
            Controls.Add(btnOk);
            Controls.Add(btnDelete);
            Controls.Add(btnSave);

            ResumeLayout(false);
        }

        private void FrmEqualizacaoGeral_Load(object sender, EventArgs e)
        {
            RecarregarPresets(0);
            chkEqualizacaoAtiva.Checked = EqualizacaoGeralStore.Ativa;
            AplicarBandas(EqualizacaoGeralStore.Bandas ?? EqualizerPreset.CreateFlatBands(), true);
            AtualizarInfo();
            AtualizarEstadoAcoes();
        }

        private void FrmEqualizacaoGeral_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_confirmado)
            {
                _restoreAction?.Invoke();
            }
        }

        private void RecarregarPresets(int presetSelecionado)
        {
            var repo = new TrackRepository();
            _presets = repo.ListarPresetsEqualizacao();

            _mudando = true;
            cboPresets.Items.Clear();
            cboPresets.Items.Add(new PresetListItem(0, TextoSemEqualizacao));

            foreach (var preset in _presets)
            {
                cboPresets.Items.Add(new PresetListItem(preset.Id, preset.Nome));
            }

            int index = 0;
            for (int i = 0; i < cboPresets.Items.Count; i++)
            {
                var item = cboPresets.Items[i] as PresetListItem;
                if (item != null && item.Id == presetSelecionado)
                {
                    index = i;
                    break;
                }
            }

            cboPresets.SelectedIndex = index;
            cboPresets.Text = ((PresetListItem)cboPresets.Items[index]).Nome;
            _mudando = false;
        }

        private void CboPresets_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_mudando) return;

            var item = cboPresets.SelectedItem as PresetListItem;
            if (item == null)
            {
                AtualizarEstadoAcoes();
                return;
            }

            if (item.Id == 0)
            {
                AplicarBandas(EqualizerPreset.CreateFlatBands(), true);
            }
            else
            {
                var repo = new TrackRepository();
                var preset = repo.ObterPresetEqualizacao(item.Id);
                if (preset != null)
                {
                    AplicarBandas(preset.ToBands(), true);
                }
            }

            cboPresets.Text = item.Nome;
            AtualizarEstadoAcoes();
        }

        private void CboPresets_TextUpdate(object sender, EventArgs e)
        {
            if (_mudando) return;

            var item = cboPresets.SelectedItem as PresetListItem;
            if (item != null && !string.Equals(item.Nome, cboPresets.Text, StringComparison.Ordinal))
            {
                _mudando = true;
                cboPresets.SelectedIndex = -1;
                _mudando = false;
            }

            AtualizarEstadoAcoes();
        }

        private void CboPresets_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            e.SuppressKeyPress = true;
            e.Handled = true;

            if (ObterPresetCorrespondente(ObterBandasAtuais()) < 0)
            {
                SalvarPresetPeloTextoAtual();
            }
        }

        private void Slider_Scroll(object sender, EventArgs e)
        {
            if (_mudando) return;

            AtualizarLabelsDosValores();
            SincronizarPresetPelosSliders();
            picWave.Invalidate();
            AtualizarInfo();
            _previewAction?.Invoke(ObterBandasAtuais(), chkEqualizacaoAtiva.Checked);
        }

        private void ChkEqualizacaoAtiva_CheckedChanged(object sender, EventArgs e)
        {
            if (_mudando) return;

            AtualizarInfo();
            AtualizarEstadoAcoes();
            _previewAction?.Invoke(ObterBandasAtuais(), chkEqualizacaoAtiva.Checked);
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            SalvarPresetPeloTextoAtual();
        }

        private void SalvarPresetPeloTextoAtual()
        {
            if (_presets.Count >= 30)
            {
                MessageBox.Show("O limite de 30 equalizacoes salvas foi atingido.", "Equalizacao", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string nome = cboPresets.Text.Trim();
            if (string.IsNullOrWhiteSpace(nome) || string.Equals(nome, TextoSemEqualizacao, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Digite um nome para o preset antes de salvar.", "Equalizacao", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var presetExistente = _presets.FirstOrDefault(p => string.Equals(p.Nome, nome, StringComparison.OrdinalIgnoreCase));
            if (presetExistente != null)
            {
                MessageBox.Show("Ja existe um preset com esse nome.", "Equalizacao", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var repo = new TrackRepository();
            int novoId = repo.InserirPresetEqualizacao(nome, ObterBandasAtuais(), 0);
            RecarregarPresets(novoId);
            AtualizarInfo();
            AtualizarEstadoAcoes();
        }

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            var item = cboPresets.SelectedItem as PresetListItem;
            if (item == null || item.Id <= 0)
            {
                return;
            }

            var resposta = MessageBox.Show(
                "Deseja realmente deletar esta equalizacao?",
                "Deletar EQ",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (resposta != DialogResult.Yes)
            {
                return;
            }

            var repo = new TrackRepository();
            repo.DeletarPresetEqualizacao(item.Id);

            RecarregarPresets(0);
            AplicarBandas(EqualizerPreset.CreateFlatBands(), true);
            AtualizarInfo();
            AtualizarEstadoAcoes();
        }

        private void BtnOk_Click(object sender, EventArgs e)
        {
            EqualizacaoGeralStore.Ativa = chkEqualizacaoAtiva.Checked;
            EqualizacaoGeralStore.Bandas = ObterBandasAtuais();
            EqualizacaoGeralStore.Salvar();
            _confirmado = true;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void AplicarBandas(int[] bandas, bool dispararPreview)
        {
            _mudando = true;

            for (int i = 0; i < EqualizerPreset.BandCount; i++)
            {
                _sliders[i].Value = Math.Max(_sliders[i].Minimum, Math.Min(_sliders[i].Maximum, bandas[i]));
            }

            AtualizarLabelsDosValores();
            picWave.Invalidate();
            _mudando = false;

            AtualizarInfo();
            AtualizarEstadoAcoes();

            if (dispararPreview)
            {
                _previewAction?.Invoke(ObterBandasAtuais(), chkEqualizacaoAtiva.Checked);
            }
        }

        private void AtualizarLabelsDosValores()
        {
            for (int i = 0; i < _valueLabels.Length; i++)
            {
                _valueLabels[i].Text = _sliders[i].Value.ToString("+0;-0;0") + " dB";
            }
        }

        private int[] ObterBandasAtuais()
        {
            var bandas = new int[EqualizerPreset.BandCount];
            for (int i = 0; i < bandas.Length; i++)
            {
                bandas[i] = _sliders[i].Value;
            }

            return bandas;
        }

        private void SincronizarPresetPelosSliders()
        {
            int presetId = ObterPresetCorrespondente(ObterBandasAtuais());

            _mudando = true;
            for (int i = 0; i < cboPresets.Items.Count; i++)
            {
                var item = cboPresets.Items[i] as PresetListItem;
                if (item != null && item.Id == Math.Max(0, presetId))
                {
                    cboPresets.SelectedIndex = i;
                    cboPresets.Text = item.Nome;
                    _mudando = false;
                    AtualizarEstadoAcoes();
                    return;
                }
            }

            cboPresets.SelectedIndex = -1;
            if (presetId == 0)
            {
                cboPresets.Text = TextoSemEqualizacao;
            }
            _mudando = false;
            AtualizarEstadoAcoes();
        }

        private int ObterPresetCorrespondente(int[] bandas)
        {
            bool tudoZero = bandas.All(v => v == 0);
            if (tudoZero)
            {
                return 0;
            }

            foreach (var preset in _presets)
            {
                if (preset.ToBands().SequenceEqual(bandas))
                {
                    return preset.Id;
                }
            }

            return -1;
        }

        private void AtualizarEstadoAcoes()
        {
            int presetId = ObterPresetCorrespondente(ObterBandasAtuais());
            string texto = cboPresets.Text.Trim();

            btnDelete.Enabled = cboPresets.SelectedItem is PresetListItem item && item.Id > 0;
            btnSave.Enabled = presetId < 0
                && _presets.Count < 30
                && !string.IsNullOrWhiteSpace(texto)
                && !string.Equals(texto, TextoSemEqualizacao, StringComparison.OrdinalIgnoreCase);
        }

        private void AtualizarInfo()
        {
            int presetId = ObterPresetCorrespondente(ObterBandasAtuais());
            if (presetId > 0)
            {
                var preset = _presets.FirstOrDefault(p => p.Id == presetId);
                lblInfo.Text = chkEqualizacaoAtiva.Checked
                    ? "Preset reconhecido: " + (preset != null ? preset.Nome : ("ID " + presetId))
                    : "Equalizacao inativa para comparacao. Preset selecionado: " + (preset != null ? preset.Nome : ("ID " + presetId));
            }
            else if (presetId == 0)
            {
                lblInfo.Text = chkEqualizacaoAtiva.Checked
                    ? "Sem equalizacao."
                    : "Equalizacao inativa. O audio esta tocando sem processamento.";
            }
            else
            {
                lblInfo.Text = chkEqualizacaoAtiva.Checked
                    ? "Digite um nome no preset e tecle Enter para salvar."
                    : "Equalizacao inativa. Salve o preset se quiser manter estas bandas.";
            }
        }

        private void PicWave_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.Clear(picWave.BackColor);

            using (var axisPen = new Pen(Color.FromArgb(180, 180, 180)))
            using (var linePen = new Pen(Color.FromArgb(160, 70, 20), 3f))
            {
                int midY = picWave.Height / 2;
                e.Graphics.DrawLine(axisPen, 0, midY, picWave.Width, midY);

                var pontos = new PointF[EqualizerPreset.BandCount];
                for (int i = 0; i < EqualizerPreset.BandCount; i++)
                {
                    float x = 30 + (i * ((picWave.Width - 60f) / (EqualizerPreset.BandCount - 1)));
                    float y = midY - (_sliders[i].Value * 5.2f);
                    pontos[i] = new PointF(x, y);
                }

                if (pontos.Length > 1)
                {
                    e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    e.Graphics.DrawLines(linePen, pontos);
                }
            }
        }

        private sealed class PresetListItem
        {
            public PresetListItem(int id, string nome)
            {
                Id = id;
                Nome = nome;
            }

            public int Id { get; private set; }
            public string Nome { get; private set; }

            public override string ToString()
            {
                return Nome;
            }
        }
    }
}

