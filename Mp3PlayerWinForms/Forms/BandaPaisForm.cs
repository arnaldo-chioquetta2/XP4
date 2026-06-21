using System;
using System.Windows.Forms;
using XP3.Data;
using XP3.Models;

namespace XP3.Forms
{
    public partial class BandaPaisForm : Form
    {
        private readonly Band _banda;
        private readonly TrackRepository _trackRepo;

        public BandaPaisForm(Band banda, TrackRepository trackRepo)
        {
            _banda = banda ?? throw new ArgumentNullException(nameof(banda));
            _trackRepo = trackRepo ?? throw new ArgumentNullException(nameof(trackRepo));

            InitializeComponent();

            lblBandaValor.Text = _banda.Name ?? string.Empty;
            CarregarPaises();

            var paisAtual = string.IsNullOrWhiteSpace(_banda.PaisNome) ? string.Empty : _banda.PaisNome.Trim();
            cmbPais.Text = paisAtual;
        }

        private void CarregarPaises()
        {
            cmbPais.BeginUpdate();
            try
            {
                cmbPais.DataSource = null;
                cmbPais.Items.Clear();

                cmbPais.DisplayMember = nameof(Pais.Nome);
                cmbPais.ValueMember = nameof(Pais.Id);
                cmbPais.DataSource = _trackRepo.GetAllPaises();
                cmbPais.SelectedIndex = -1;
                cmbPais.Text = string.Empty;
            }
            finally
            {
                cmbPais.EndUpdate();
            }
        }

        private void btnSalvar_Click(object sender, EventArgs e)
        {
            var texto = cmbPais.Text.Trim();

            if (string.IsNullOrWhiteSpace(texto))
            {
                _trackRepo.UpdateBandPais(_banda.Id, null);
            }
            else
            {
                var paisId = _trackRepo.GetOrInsertPais(texto);
                _trackRepo.UpdateBandPais(_banda.Id, paisId);
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnCancelar_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
