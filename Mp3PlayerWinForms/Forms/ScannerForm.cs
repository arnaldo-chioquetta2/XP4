using System;
using XP3.Data;
using System.IO;
using XP3.Models;
using XP3.Services;
using System.Windows.Forms;
using System.Threading.Tasks;

namespace XP3.Forms
{
    public partial class ScannerForm : Form
    {
        private FolderScannerService _scanner;
        private TrackRepository _trackRepo;

        public ScannerForm()
        {
            InitializeComponent();
            _scanner = new FolderScannerService();
        }

        private async void btnSelectFolder_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(AppConfig.PastaBase) || !Directory.Exists(AppConfig.PastaBase))
            {
                MessageBox.Show("Pasta Base não configurada no INI.", "Erro");
                return;
            }

            using (var fbd = new FolderBrowserDialog())
            {
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    string pastaOrigem = fbd.SelectedPath;
                    string pastaBase = AppConfig.PastaBase;

                    // Prepara a tela para o processamento
                    btnSelectFolder.Enabled = false;
                    btnOkClose.Enabled = false; // Garante que o OK esteja desativado durante o processo

                    _trackRepo = new TrackRepository();
                    _trackRepo.GetOrCreatePlaylist("AEscolher");

                    txtLog.Clear();
                    progressBar1.Value = 0;

                    try
                    {
                        var progress = new Progress<int>(p => progressBar1.Value = p);
                        var log = new Progress<string>(msg => AppendLog(msg));

                        bool isImportacao = !IsSubfolder(pastaBase, pastaOrigem);

                        if (isImportacao)
                        {
                            AppendLog(">>> MODO IMPORTAÇÃO: Movimentando arquivos...");
                            // REMOVIDA A CONFIRMAÇÃO: Agora ele vai direto
                            await Task.Run(() => _scanner.ImportarEscanear(pastaOrigem, pastaBase, progress, log));
                        }
                        else
                        {
                            AppendLog(">>> MODO ESCANEAMENTO: Atualizando banco local...");
                            await Task.Run(() => _scanner.ScanFolder(pastaOrigem, progress, log));
                        }

                        AppendLog("=== PROCESSO CONCLUÍDO ===");
                        AppendLog("Você pode revisar o log acima. Clique em OK para encerrar.");

                        // --- MUDANÇA AQUI: Libera o botão de OK em vez de fechar ---
                        btnOkClose.Enabled = true;
                    }
                    catch (Exception ex)
                    {
                        AppendLog($"ERRO FATAL: {ex.Message}");
                        btnSelectFolder.Enabled = true;
                        btnOkClose.Enabled = true;
                    }
                }
            }
        }

        private bool IsSubfolder(string parentPath, string childPath)
        {
            var parentUri = new Uri(parentPath.EndsWith("\\") ? parentPath : parentPath + "\\");
            var childUri = new Uri(childPath.EndsWith("\\") ? childPath : childPath + "\\");
            return parentUri.IsBaseOf(childUri);
        }

        private void AppendLog(string msg)
        {
            if (txtLog.IsDisposed) return;
            // Invoke necessário caso venha de outra thread
            this.BeginInvoke(new Action(() => {
                txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");
                txtLog.SelectionStart = txtLog.Text.Length;
                txtLog.ScrollToCaret();
            }));
        }

        private void btnOkClose_Click_1(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}