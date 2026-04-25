using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using XP3.Models;

namespace XP3.Forms
{
    public class PlaylistEditorForm : Form
    {
        private readonly TreeView _tree;
        private readonly Button _btnOk;
        private readonly Button _btnCancelar;
        private bool _atualizandoChecks;

        public HashSet<int> SelectedTrackIds { get; } = new HashSet<int>();

        public PlaylistEditorForm(string playlistName, IEnumerable<Track> allTracks, IEnumerable<int> selectedTrackIds)
        {
            Text = $"Editar Lista - {playlistName}";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(700, 520);
            MinimumSize = new Size(540, 420);

            _tree = new TreeView
            {
                Dock = DockStyle.Fill,
                CheckBoxes = true,
                HideSelection = false
            };
            _tree.AfterCheck += Tree_AfterCheck;

            _btnOk = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
                Width = 100,
                Height = 34,
                Left = 470,
                Top = 10
            };
            _btnOk.Click += (s, e) => ColetarSelecionados();

            _btnCancelar = new Button
            {
                Text = "Cancelar",
                DialogResult = DialogResult.Cancel,
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
                Width = 100,
                Height = 34,
                Left = 580,
                Top = 10
            };

            var pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 56
            };
            pnlBottom.Controls.Add(_btnOk);
            pnlBottom.Controls.Add(_btnCancelar);

            Controls.Add(_tree);
            Controls.Add(pnlBottom);

            AcceptButton = _btnOk;
            CancelButton = _btnCancelar;

            CarregarArvore(allTracks ?? Enumerable.Empty<Track>(), new HashSet<int>(selectedTrackIds ?? Enumerable.Empty<int>()));
        }

        private void CarregarArvore(IEnumerable<Track> allTracks, HashSet<int> selectedTrackIds)
        {
            _atualizandoChecks = true;
            _tree.Nodes.Clear();

            foreach (var grupo in allTracks.GroupBy(t => string.IsNullOrWhiteSpace(t.BandName) ? "Desconhecida" : t.BandName))
            {
                var bandNode = new TreeNode(grupo.Key)
                {
                    NodeFont = new Font(Font, FontStyle.Bold)
                };

                foreach (var track in grupo.OrderBy(t => t.Title))
                {
                    var trackNode = new TreeNode(track.Title)
                    {
                        Tag = track,
                        Checked = selectedTrackIds.Contains(track.Id)
                    };
                    bandNode.Nodes.Add(trackNode);
                }

                bandNode.Checked = bandNode.Nodes.Count > 0 && bandNode.Nodes.Cast<TreeNode>().All(n => n.Checked);
                _tree.Nodes.Add(bandNode);
            }

            _tree.ExpandAll();
            _atualizandoChecks = false;
        }

        private void Tree_AfterCheck(object sender, TreeViewEventArgs e)
        {
            if (_atualizandoChecks) return;

            _atualizandoChecks = true;

            if (e.Node.Level == 0)
            {
                foreach (TreeNode child in e.Node.Nodes)
                {
                    child.Checked = e.Node.Checked;
                }
            }
            else if (e.Node.Parent != null)
            {
                e.Node.Parent.Checked = e.Node.Parent.Nodes.Cast<TreeNode>().All(n => n.Checked);
            }

            _atualizandoChecks = false;
        }

        private void ColetarSelecionados()
        {
            SelectedTrackIds.Clear();

            foreach (TreeNode bandNode in _tree.Nodes)
            {
                foreach (TreeNode trackNode in bandNode.Nodes)
                {
                    if (!trackNode.Checked || !(trackNode.Tag is Track track)) continue;
                    SelectedTrackIds.Add(track.Id);
                }
            }
        }
    }
}
