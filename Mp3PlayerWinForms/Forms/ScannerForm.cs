using System;
using System.Threading.Tasks;
using System.Windows.Forms;
//using Mp3PlayerWinForms.Models;
using XP3.Models;
using XP3.Services;
using XP3.Data;

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
            // Tenta sugerir a pasta base configurada
            string startPath = !string.IsNullOrEmpty(AppConfig.PastaBase) ? AppConfig.PastaBase : "";

            using (var fbd = new FolderBrowserDialog())
            {
                fbd.SelectedPath = startPath;
                fbd.Description = "Selecione a pasta para escanear (Raiz ou Pasta de Músicas)";

                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    btnSelectFolder.Enabled = false;
                    _trackRepo = new TrackRepository();
                    _trackRepo.GetOrCreatePlaylist("AEscolher");
                    txtLog.Clear();
                    progressBar1.Value = 0;

                    try
                    {
                        // Repórteres de Progresso
                        var progress = new Progress<int>(p => progressBar1.Value = p);
                        var log = new Progress<string>(msg => AppendLog(msg));

                        await Task.Run(() => _scanner.ScanFolder(fbd.SelectedPath, progress, log));

                        AppendLog("=== CONCLUÍDO ===");
                        MessageBox.Show("Scan finalizado! A lista 'AEscolher' foi populada.");

                        // Fecha o form retornando OK para a tela inicial saber que deve tocar
                        this.DialogResult = DialogResult.OK;
                        this.Close();
                    }
                    catch (Exception ex)
                    {
                        AppendLog($"ERRO FATAL: {ex.Message}");
                        btnSelectFolder.Enabled = true;
                    }
                }
            }
        }

        private void AppendLog(string msg)
        {
            if (txtLog.IsDisposed) return;
            txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");
            txtLog.SelectionStart = txtLog.Text.Length;
            txtLog.ScrollToCaret();
        }
    }
}