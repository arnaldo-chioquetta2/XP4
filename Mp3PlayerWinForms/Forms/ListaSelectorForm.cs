using System;
using XP3.Data;
using XP3.Models;
using System.Linq;
using System.Windows.Forms;

namespace XP3.Forms
{
    public partial class ListaSelectorForm : Form
    {
        private int _musicaId;
        private int _listaAtualId;
        private TrackRepository _repo = new TrackRepository();
        public bool DeveRemoverDaGrid { get; private set; } = false;

        public ListaSelectorForm(int musicaId, int listaAtualId)
        {
            InitializeComponent();
            _musicaId = musicaId;
            _listaAtualId = listaAtualId;

            // Configura os botões
            //btnCopiar.Click += (s, e) => Salvar("COPIAR");
            //btnMover.Click += (s, e) => Salvar("MOVER");
            //btnExcluir.Click += (s, e) => Salvar("EXCLUIR");

            CarregarPlaylistsOrdenadas();
        }

        private void CarregarPlaylistsOrdenadas()
        {
            clbPlaylists.Items.Clear();
            clbPlaylists.Items.Add("Adicionar em nova lista");

            var todas = _repo.GetAllPlaylists();
            var atuais = _repo.GetPlaylistsByMusicaId(_musicaId);

            // Conjunto 1: Já pertence (Ordenado A-Z)
            var grupoPertence = todas.Where(t => atuais.Any(a => a.Id == t.Id))
                                     .OrderBy(x => x.Name).ToList();

            // Conjunto 2: Não pertence (Ordenado A-Z)
            var grupoNaoPertence = todas.Where(t => !atuais.Any(a => a.Id == t.Id))
                                        .OrderBy(x => x.Name).ToList();

            foreach (var p in grupoPertence) clbPlaylists.Items.Add(p, true);
            foreach (var p in grupoNaoPertence) clbPlaylists.Items.Add(p, false);

            clbPlaylists.DisplayMember = "Name";
        }

        private void Salvar(string modo)
        {
            if (modo == "EXCLUIR")
            {
                _repo.RemoverMusicaDaLista(_musicaId, _listaAtualId);
                DeveRemoverDaGrid = true;
            }
            else
            {
                // 1. Tratamento de Nova Lista (Inalterado)
                int? novaListaId = null;
                bool marcaramNovaLista = false;
                for (int i = 0; i < clbPlaylists.Items.Count; i++)
                {
                    if (clbPlaylists.GetItemChecked(i) && clbPlaylists.Items[i].ToString() == "Adicionar em nova lista")
                    {
                        marcaramNovaLista = true;
                        break;
                    }
                }

                if (marcaramNovaLista)
                {
                    string nomeNovaLista = ShowInputBox("Nova Lista", "Digite o nome para a nova lista:");
                    if (string.IsNullOrWhiteSpace(nomeNovaLista)) return;
                    novaListaId = _repo.GetOrCreatePlaylist(nomeNovaLista);
                }

                // 2. LÓGICA DE MOVER vs COPIAR
                // No modo MOVER, removemos de absolutamente tudo antes de reinserir nas novas
                if (modo == "MOVER")
                {
                    _repo.LimparMusicaDeTodasPlaylists(_musicaId);
                    DeveRemoverDaGrid = true;
                }
                else // No modo COPIAR
                {
                    // Opcional: Se quiser que o COPIAR apenas adicione sem remover das outras, 
                    // você pode comentar a linha abaixo. 
                    // Mas se o seu COPIAR deve "espelhar" exatamente o que está nos checks:
                    _repo.LimparMusicaDeTodasPlaylists(_musicaId);
                }

                // 3. Reinserção baseada nos Checks
                for (int i = 0; i < clbPlaylists.Items.Count; i++)
                {
                    if (clbPlaylists.GetItemChecked(i))
                    {
                        var item = clbPlaylists.Items[i];
                        int idDestino = -1;

                        if (item is Playlist p) idDestino = p.Id;
                        else if (item.ToString() == "Adicionar em nova lista" && novaListaId.HasValue)
                            idDestino = novaListaId.Value;

                        if (idDestino != -1)
                        {
                            // REGRA DE OURO DO MOVER:
                            // Se estivermos movendo, nunca permite reinserir na lista de origem.
                            if (modo == "MOVER" && idDestino == _listaAtualId)
                                continue;

                            _repo.AddTrackToPlaylist(idDestino, _musicaId);
                        }
                    }
                }
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private string ShowInputBox(string titulo, string prompt)
        {
            Form promptForm = new Form()
            {
                Width = 300,
                Height = 150,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = titulo,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false
            };
            Label lblText = new Label() { Left = 20, Top = 20, Text = prompt, Width = 250 };
            TextBox txtInput = new TextBox() { Left = 20, Top = 45, Width = 240 };
            Button btnOk = new Button() { Text = "OK", Left = 100, Width = 80, Top = 80, DialogResult = DialogResult.OK };
            Button btnCancel = new Button() { Text = "Cancelar", Left = 190, Width = 80, Top = 80, DialogResult = DialogResult.Cancel };

            promptForm.Controls.Add(lblText);
            promptForm.Controls.Add(txtInput);
            promptForm.Controls.Add(btnOk);
            promptForm.Controls.Add(btnCancel);
            promptForm.AcceptButton = btnOk;
            promptForm.CancelButton = btnCancel;

            return promptForm.ShowDialog() == DialogResult.OK ? txtInput.Text : null;
        }

        private void btnExcluir_Click(object sender, EventArgs e)
        {
            try
            {
                // 1. Obtemos os dados da música
                var track = _repo.GetTrackById(_musicaId);

                if (track != null)
                {
                    // 2. Tenta enviar para a Lixeira (sem confirmação do programa)
                    try
                    {
                        if (System.IO.File.Exists(track.FilePath))
                        {

                            System.IO.File.Delete(track.FilePath);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Se falhar (arquivo em uso), agendamos para apagar depois
                        _repo.AdicionarParaApagarDepois(track.FilePath, track.BandName);
                    }

                    // 3. Remove do Banco de Dados definitivamente (método que limpa LisMus, Musica e Banda)
                    _repo.RemoverMusicaDefinitivamente(_musicaId);

                    // 4. Fecha a tela e sinaliza remoção visual
                    this.DeveRemoverDaGrid = true;
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro na exclusão: {ex.Message}");
            }
        }

        private void btnMover_Click(object sender, EventArgs e)
        {
            // Chama a lógica centralizada com o modo MOVER
            Salvar("MOVER");
        }

        private void btnCopiar_Click(object sender, EventArgs e)
        {
            // Chama a lógica centralizada com o modo COPIAR
            Salvar("COPIAR");
        }

    }
}