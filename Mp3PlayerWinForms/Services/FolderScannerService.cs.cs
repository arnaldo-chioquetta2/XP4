using System;
using System.IO;
using System.Linq;
using System.Text;
using XP3.Data;
using XP3.Models;

namespace XP3.Services
{
    public class FolderScannerService
    {
        private TrackRepository _repo;
        private int _arquivosProcessados = 0;
        private StringBuilder _logFull = new StringBuilder();

        public FolderScannerService()
        {
            _repo = new TrackRepository();
        }

        public void ScanFolder(string folderPath, IProgress<int> progress, IProgress<string> log)
        {
            _arquivosProcessados = 0;
            _logFull.Clear();

            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log_importacao_completa.txt");

            RegistrarLog(log, "=== INICIANDO CATALOGAÇÃO COMPLETA ===");
            RegistrarLog(log, $"Pasta selecionada: {folderPath}");

            int playlistId = _repo.GetOrCreatePlaylist("AEscolher");
            var dirInfo = new DirectoryInfo(folderPath);

            if (!dirInfo.Exists)
            {
                RegistrarLog(log, "ERRO: Pasta não encontrada.");
                return;
            }

            // 1. Processar arquivos na RAIZ (Banda: Desconhecida)
            var rootFiles = dirInfo.GetFiles("*.mp3", SearchOption.TopDirectoryOnly);
            RegistrarLog(log, $"Encontrados {rootFiles.Length} arquivos na raiz.");

            foreach (var file in rootFiles)
            {
                string bandName = "Desconhecida";
                string songTitle = Path.GetFileNameWithoutExtension(file.Name);

                // Tenta separar "Banda - Musica" se houver hífen
                if (songTitle.Contains(" - "))
                {
                    var parts = songTitle.Split(new[] { " - " }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        bandName = parts[0].Trim();
                        songTitle = parts[1].Trim();
                    }
                }

                // Se o título começa exatamente com o nome da banda, removemos a repetição
                if (songTitle.StartsWith(bandName, StringComparison.OrdinalIgnoreCase))
                {
                    songTitle = songTitle.Substring(bandName.Length).Trim();
                }

                ProcessFile(file, bandName, songTitle, playlistId, log);

                // Atualiza progresso proporcional à raiz (baseado em 30% do total estimado de pastas + arquivos)
                progress.Report(5);
            }

            // 2. Processar SUB-PASTAS (Banda: Nome da Pasta)
            var subDirs = dirInfo.GetDirectories();
            RegistrarLog(log, $"Escaneando {subDirs.Length} sub-pastas de bandas...");

            int dirIdx = 0;
            foreach (var dir in subDirs)
            {
                dirIdx++;
                string bandName = dir.Name;
                var files = dir.GetFiles("*.mp3");

                foreach (var file in files)
                {
                    string songTitle = Path.GetFileNameWithoutExtension(file.Name);
                    ProcessFile(file, bandName, songTitle, playlistId, log);
                }

                // Progresso dinâmico para as subpastas
                int pct = 10 + (int)((double)dirIdx / subDirs.Length * 90);
                progress.Report(pct);
            }

            RegistrarLog(log, $"=== CATALOGAÇÃO FINALIZADA. Total: {_arquivosProcessados} músicas. ===");

            try
            {
                File.WriteAllText(logPath, _logFull.ToString(), Encoding.UTF8);
                RegistrarLog(log, $"Arquivo de log gerado com sucesso em: {logPath}");
            }
            catch (Exception ex)
            {
                RegistrarLog(log, $"Erro ao gravar log no disco: {ex.Message}");
            }
        }

        private void ProcessFile(FileInfo fileInfo, string bandName, string songTitle, int playlistId, IProgress<string> log)
        {
            try
            {
                _arquivosProcessados++;
                fileInfo.Refresh();

                // 1. Normalização inspirada no seu código VB6
                bandName = NormalizarNome(bandName);
                songTitle = NormalizarNome(songTitle);

                // 2. Limpeza de nomes grudados
                if (songTitle.StartsWith(bandName, StringComparison.OrdinalIgnoreCase))
                {
                    songTitle = songTitle.Substring(bandName.Length).Trim();
                }

                if (string.IsNullOrEmpty(songTitle))
                    songTitle = Path.GetFileNameWithoutExtension(fileInfo.Name);

                // 3. Obter Duração
                TimeSpan duration = TimeSpan.Zero;
                try
                {
                    using (var tfile = TagLib.File.Create(fileInfo.FullName))
                        duration = tfile.Properties.Duration;
                }
                catch { }

                // 4. Salvar no Banco (O segredo está em capturar o retorno de AddTrack)
                int bandId = _repo.GetOrInsertBand(bandName);

                var track = new Track
                {
                    Title = songTitle,
                    BandId = bandId,
                    FilePath = fileInfo.FullName,
                    Duration = duration
                };

                // CORREÇÃO AQUI: AddTrack já devolve o ID gerado pelo banco
                int newTrackId = _repo.AddTrack(track);

                // Agora usamos o ID que acabamos de receber
                _repo.AddTrackToPlaylist(playlistId, newTrackId);

                _logFull.AppendLine($"OK: {bandName} - {songTitle} | Tempo: {duration:mm\\:ss}");
            }
            catch (Exception ex)
            {
                RegistrarLog(log, $"Erro em {fileInfo.Name}: {ex.Message}");
            }
        }

        private string NormalizarNome(string nome)
        {
            if (string.IsNullOrWhiteSpace(nome)) return "Sem Título";

            // 1. Substitui caracteres de escape comuns e aspas (como no seu código)
            nome = nome.Replace("\"", "'").Replace("/", " ").Replace("\\", " ");

            const string letrasValidas = "áàéèêâíìîóòôúùüãõçÁÀÉÈÊÂÍÌÎÓÒÔÚÙÜÃÕÇ";
            StringBuilder sb = new StringBuilder();

            foreach (char c in nome)
            {
                int codLetra = (int)c;

                // Mantém letras ASCII básicas (32 a 122) 
                // OU letras acentuadas válidas
                if ((codLetra >= 32 && codLetra <= 122) || letrasValidas.Contains(c))
                {
                    sb.Append(c);
                }
                else
                {
                    // Substitui o que for "estranho" por underline (exatamente como o VB6)
                    sb.Append('_');
                }
            }

            string resultado = sb.ToString().Trim();

            // 2. Remove o "0 " no início (conforme sua lógica original)
            if (resultado.StartsWith("0 "))
            {
                resultado = resultado.Substring(2).Trim();
            }

            return resultado;
        }

        private void RegistrarLog(IProgress<string> prog, string msg)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string linha = $"[{timestamp}] {msg}";
            _logFull.AppendLine(linha);
            prog.Report(msg);
        }
    }
}