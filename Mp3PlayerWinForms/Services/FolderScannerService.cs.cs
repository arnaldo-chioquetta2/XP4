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

        // =========================================================================
        // MÉTODO 1: ESCANEAMENTO SIMPLES (Quando a pasta JÁ É a pasta base)
        // =========================================================================
        public void ScanFolder(string folderPath, IProgress<int> progress, IProgress<string> log)
        {
            _arquivosProcessados = 0;
            _logFull.Clear();

            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log_scan_local.txt");

            RegistrarLog(log, "=== INICIANDO CATALOGAÇÃO LOCAL (SEM MOVER ARQUIVOS) ===");
            RegistrarLog(log, $"Pasta selecionada: {folderPath}");

            int playlistId = _repo.GetOrCreatePlaylist("AEscolher");
            var dirInfo = new DirectoryInfo(folderPath);

            if (!dirInfo.Exists)
            {
                RegistrarLog(log, "ERRO: Pasta não encontrada.");
                return;
            }

            // 1. Processar arquivos na RAIZ
            var rootFiles = dirInfo.GetFiles("*.mp3", SearchOption.TopDirectoryOnly);
            RegistrarLog(log, $"Encontrados {rootFiles.Length} arquivos na raiz.");

            foreach (var file in rootFiles)
            {
                string bandName = "Desconhecida";
                string songTitle = Path.GetFileNameWithoutExtension(file.Name);
                TratarNomeBandaMusica(ref bandName, ref songTitle);

                ProcessFile(file, bandName, songTitle, playlistId, log);
                progress.Report(5);
            }

            // 2. Processar SUB-PASTAS
            var subDirs = dirInfo.GetDirectories();
            RegistrarLog(log, $"Escaneando {subDirs.Length} sub-pastas...");

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

                int pct = 10 + (int)((double)dirIdx / subDirs.Length * 90);
                progress.Report(pct);
            }

            RegistrarLog(log, $"=== CATALOGAÇÃO FINALIZADA. Total: {_arquivosProcessados} músicas. ===");
            SalvarLogEmDisco("log_scan_local.txt");
        }

        // =========================================================================
        // MÉTODO 2: IMPORTAÇÃO (Quando vem de PENDRIVE/DOWNLOADS -> PASTA BASE)
        // =========================================================================
        public void ImportarEscanear(string pastaOrigem, string pastaBase, IProgress<int> progress, IProgress<string> log)
        {
            _arquivosProcessados = 0;
            _logFull.Clear();

            RegistrarLog(log, "==========================================");
            RegistrarLog(log, "=== INICIANDO IMPORTAÇÃO E MOVIMENTAÇÃO ===");
            RegistrarLog(log, "==========================================");

            // 1. Validação
            var dirOrigem = new DirectoryInfo(pastaOrigem);
            if (!dirOrigem.Exists) { RegistrarLog(log, "ERRO CRÍTICO: Pasta origem sumiu."); return; }

            RegistrarLog(log, $"ORIGEM: {dirOrigem.FullName}");

            // 2. Define o Destino (Nome da pasta vira nome da Banda)
            string nomeBandaSujo = dirOrigem.Name;
            string nomeBandaLimpo = NormalizarNome(nomeBandaSujo);

            // Segurança para nome vazio
            if (string.IsNullOrWhiteSpace(nomeBandaLimpo) || nomeBandaLimpo.Length < 2)
                nomeBandaLimpo = "Importados_" + DateTime.Now.ToString("yyyyMMdd");

            string caminhoDestinoBanda = Path.Combine(pastaBase, nomeBandaLimpo);
            RegistrarLog(log, $"DESTINO (Banda): {caminhoDestinoBanda}");

            // Cria pasta da banda se não existir
            if (!Directory.Exists(caminhoDestinoBanda))
            {
                Directory.CreateDirectory(caminhoDestinoBanda);
                RegistrarLog(log, "STATUS: Pasta da banda criada.");
            }
            else
            {
                RegistrarLog(log, "STATUS: Pasta da banda já existe. Mesclando...");
            }

            RegistrarLog(log, "------------------------------------------");

            // 3. Move arquivos
            var arquivos = dirOrigem.GetFiles("*.mp3");
            int total = arquivos.Length;
            int contadorNomeRuim = 1;
            int playlistId = _repo.GetOrCreatePlaylist("AEscolher");

            for (int i = 0; i < total; i++)
            {
                var arquivo = arquivos[i];
                try
                {
                    // A) Normaliza nome do arquivo
                    string nomeOriginal = Path.GetFileNameWithoutExtension(arquivo.Name);
                    string nomeNormalizado = NormalizarNome(nomeOriginal);

                    if (string.IsNullOrWhiteSpace(nomeNormalizado) || nomeNormalizado.Replace("_", "").Length == 0)
                        nomeNormalizado = $"Faixa_{contadorNomeRuim++}";

                    string novoNomeComExtensao = nomeNormalizado + ".mp3";
                    string caminhoFinal = Path.Combine(caminhoDestinoBanda, novoNomeComExtensao);

                    // B) Trata duplicidade (Arquivo já existe no destino?)
                    if (File.Exists(caminhoFinal))
                    {
                        int dup = 1;
                        string tempNome = nomeNormalizado;
                        while (File.Exists(caminhoFinal))
                        {
                            nomeNormalizado = $"{tempNome}_{dup++}";
                            caminhoFinal = Path.Combine(caminhoDestinoBanda, nomeNormalizado + ".mp3");
                        }
                        RegistrarLog(log, $"AVISO: Duplicata detectada. Renomeando para {nomeNormalizado}.mp3");
                    }

                    // C) LOG VISUAL DA MOVIMENTAÇÃO (O que você pediu)
                    RegistrarLog(log, $"[MOVER] '{arquivo.Name}'");
                    RegistrarLog(log, $"   --> '{Path.GetFileName(caminhoFinal)}'");

                    // D) Move fisicamente
                    File.Move(arquivo.FullName, caminhoFinal);

                    // E) Cataloga no banco (usando o arquivo no NOVO local)
                    var novoFile = new FileInfo(caminhoFinal);
                    string tituloMusica = Path.GetFileNameWithoutExtension(novoFile.Name);

                    // Como a pasta JÁ É a banda, passamos o nome da banda limpo
                    ProcessFile(novoFile, nomeBandaLimpo, tituloMusica, playlistId, log);

                    progress.Report((int)((double)(i + 1) / total * 100));
                }
                catch (Exception ex)
                {
                    RegistrarLog(log, $"!!! ERRO AO MOVER '{arquivo.Name}': {ex.Message}");
                }
            }

            RegistrarLog(log, "==========================================");
            RegistrarLog(log, "IMPORTAÇÃO CONCLUÍDA.");
            SalvarLogEmDisco("log_importacao.txt");
        }

        // =========================================================================
        // AUXILIARES
        // =========================================================================

        private void TratarNomeBandaMusica(ref string bandName, ref string songTitle)
        {
            if (songTitle.Contains(" - "))
            {
                var parts = songTitle.Split(new[] { " - " }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    bandName = parts[0].Trim();
                    songTitle = parts[1].Trim();
                }
            }
        }

        private void ProcessFile(FileInfo fileInfo, string bandName, string songTitle, int playlistId, IProgress<string> log)
        {
            try
            {
                _arquivosProcessados++;
                fileInfo.Refresh();

                bandName = NormalizarNome(bandName);
                songTitle = NormalizarNome(songTitle);

                // Se o título começa com a banda, limpa
                if (songTitle.StartsWith(bandName, StringComparison.OrdinalIgnoreCase))
                {
                    songTitle = songTitle.Substring(bandName.Length).Trim();
                    songTitle = songTitle.TrimStart(new[] { '-', '_', ' ' });
                }

                if (string.IsNullOrEmpty(songTitle))
                    songTitle = Path.GetFileNameWithoutExtension(fileInfo.Name);

                TimeSpan duration = TimeSpan.Zero;
                try
                {
                    using (var tfile = TagLib.File.Create(fileInfo.FullName))
                        duration = tfile.Properties.Duration;
                }
                catch { }

                int bandId = _repo.GetOrInsertBand(bandName);
                var track = new Track
                {
                    Title = songTitle,
                    BandId = bandId,
                    FilePath = fileInfo.FullName,
                    Duration = duration
                };

                int newTrackId = _repo.AddTrack(track);
                _repo.AddTrackToPlaylist(playlistId, newTrackId);

                // Log simplificado para o banco, já que o log de mover é detalhado
                _logFull.AppendLine($"DB OK: {bandName} | {songTitle}");
            }
            catch (Exception ex)
            {
                RegistrarLog(log, $"Erro DB em {fileInfo.Name}: {ex.Message}");
            }
        }

        private string NormalizarNome(string nome)
        {
            if (string.IsNullOrWhiteSpace(nome)) return "SemTitulo";

            // Limpa caracteres proibidos em arquivos
            nome = nome.Replace("\"", "'").Replace("/", " ").Replace("\\", " ")
                       .Replace(":", " ").Replace("*", " ").Replace("?", " ")
                       .Replace("<", " ").Replace(">", " ").Replace("|", " ");

            const string letrasValidas = "áàéèêâíìîóòôúùüãõçÁÀÉÈÊÂÍÌÎÓÒÔÚÙÜÃÕÇ0123456789";
            StringBuilder sb = new StringBuilder();

            foreach (char c in nome)
            {
                int codLetra = (int)c;
                if ((codLetra >= 65 && codLetra <= 90) || // A-Z
                    (codLetra >= 97 && codLetra <= 122) || // a-z
                    (codLetra >= 48 && codLetra <= 57) || // 0-9
                    (codLetra == 32) || // Espaço
                    letrasValidas.Contains(c))
                {
                    sb.Append(c);
                }
                else
                {
                    sb.Append('_');
                }
            }

            string resultado = sb.ToString().Trim();
            while (resultado.Contains("__")) resultado = resultado.Replace("__", "_");
            if (resultado.StartsWith("0 ")) resultado = resultado.Substring(2).Trim();

            return resultado;
        }

        private void RegistrarLog(IProgress<string> prog, string msg)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string linha = $"[{timestamp}] {msg}";
            _logFull.AppendLine(linha);
            prog?.Report(msg);
        }

        private void SalvarLogEmDisco(string nomeArquivo)
        {
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, nomeArquivo);
                File.WriteAllText(logPath, _logFull.ToString(), Encoding.UTF8);
            }
            catch { }
        }
    }
}