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

                    btnSelectFolder.Enabled = false;
                    btnOkClose.Enabled = false;

                    _trackRepo = new TrackRepository();
                    _trackRepo.GetOrCreatePlaylist("AEscolher");

                    txtLog.Clear();
                    progressBar1.Value = 0;

                    try
                    {
                        var progress = new Progress<int>(p => progressBar1.Value = p);
                        var log = new Progress<string>(msg => AppendLog(msg));

                        // Verifica se é importação (ou seja, NÃO está dentro da pasta base)
                        bool isImportacao = !IsSubfolder(pastaBase, pastaOrigem);

                        if (isImportacao)
                        {
                            AppendLog(">>> MODO IMPORTAÇÃO: Movimentando arquivos...");

                            // Executa a importação
                            await Task.Run(() => _scanner.ImportarEscanear(pastaOrigem, pastaBase, progress, log));

                            // --- NOVA LÓGICA DE LIMPEZA AQUI ---
                            // Como já terminou o await, podemos tentar apagar a pasta com segurança
                            TentarApagarPastaSeVazia(pastaOrigem);
                        }
                        else
                        {
                            AppendLog(">>> MODO ESCANEAMENTO: Atualizando banco local...");
                            await Task.Run(() => _scanner.ScanFolder(pastaOrigem, progress, log));
                        }

                        AppendLog("=== PROCESSO CONCLUÍDO ===");
                        AppendLog("Você pode revisar o log acima. Clique em OK para encerrar.");

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

        private void TentarApagarPastaSeVazia(string caminho)
        {
            try
            {
                // Verifica se a pasta ainda existe
                if (!Directory.Exists(caminho)) return;

                // Verifica se sobraram arquivos (recursivamente)
                string[] arquivosRestantes = Directory.GetFiles(caminho, "*.*", SearchOption.AllDirectories);

                if (arquivosRestantes.Length == 0)
                {
                    // Tenta apagar a pasta e subpastas vazias
                    Directory.Delete(caminho, true);
                    AppendLog($"LIMPEZA: A pasta de origem estava vazia e foi removida: {caminho}");
                }
                else
                {
                    AppendLog($"LIMPEZA: A pasta não foi apagada pois ainda contém {arquivosRestantes.Length} arquivos.");
                }
            }
            catch (Exception ex)
            {
                AppendLog($"AVISO: Não foi possível apagar a pasta vazia. Motivo: {ex.Message}");
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