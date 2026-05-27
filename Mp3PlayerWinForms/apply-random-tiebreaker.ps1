# PowerShell script para aplicar RandomTieBreaker nas 3 partes solicitadas.
# Salve como apply-random-tiebreaker.ps1 na raiz do repositório e execute.
param(
    [switch]$WhatIf
)

$root = "D:\Prog\XP3\Mp3PlayerWinForms_Project"
$trackFile = Join-Path $root "Mp3PlayerWinForms\Models\Track.cs"
$inicialFile = Join-Path $root "Mp3PlayerWinForms\Forms\Inicial.cs"
$repoFile = Join-Path $root "Mp3PlayerWinForms\Data\TrackRepository.cs"

function Backup-File($path) {
    if (-not (Test-Path $path)) { Write-Error "Arquivo não encontrado: $path"; return $false }
    $bak = "$path.bak.$((Get-Date).ToString('yyyyMMddHHmmss'))"
    if ($WhatIf) { Write-Host "[WhatIf] Backup $path -> $bak"; return $true }
    Copy-Item -Path $path -Destination $bak -Force
    Write-Host "Backup criado: $bak"
    return $true
}

function Replace-MethodBySignature($filePath, $signature, $newMethodContent) {
    Write-Host "Processando $filePath buscando assinatura: $signature"
    $text = Get-Content $filePath -Raw -ErrorAction Stop

    $idx = $text.IndexOf($signature)
    if ($idx -lt 0) {
        Write-Error "Assinatura não encontrada em $filePath: $signature"
        return $false
    }

    # achar primeira chave '{' após assinatura
    $openPos = $text.IndexOf('{', $idx)
    if ($openPos -lt 0) { Write-Error "Chave de abertura não encontrada após assinatura em $filePath"; return $false }

    # varrer para encontrar '}' correspondente contando profundidade
    $i = $openPos
    $depth = 1
    while ($depth -gt 0 -and $i -lt $text.Length - 1) {
        $i++
        $ch = $text[$i]
        if ($ch -eq '{') { $depth++ }
        elseif ($ch -eq '}') { $depth-- }
    }

    if ($depth -ne 0) { Write-Error "Não encontrou fim do método em $filePath"; return $false }

    $endPos = $i + 1
    $before = $text.Substring(0, $idx)
    $after = $text.Substring($endPos)

    $newText = $before + $newMethodContent + $after

    if ($WhatIf) {
        Write-Host "[WhatIf] Substituir método em $filePath (assinatura encontrada na posição $idx)."
        return $true
    }

    Set-Content -Path $filePath -Value $newText -Encoding UTF8
    Write-Host "Método substituído com sucesso em $filePath"
    return $true
}

# 1) Inserir propriedade RandomTieBreaker em Track.cs (se necessário)
if (-not (Test-Path $trackFile)) { Write-Error "Track.cs não encontrado em $trackFile"; throw }
$trackText = Get-Content $trackFile -Raw
if ($trackText -match "RandomTieBreaker") {
    Write-Host "RandomTieBreaker já presente em Track.cs — pulando inserção."
} else {
    Backup-File $trackFile | Out-Null
    # Insere logo após 'public int Pulado { get; set; }'
    $pattern = "public int Pulado { get; set; }"
    $insert = $pattern + "`r`n        // Campo em memória usado como critério secundário randômico no desempate`r`n        public double RandomTieBreaker { get; set; } = 0.0;"
    if ($WhatIf) {
        Write-Host "[WhatIf] Inserir propriedade RandomTieBreaker em $trackFile após '$pattern'"
    } else {
        $newTrackText = $trackText -replace [regex]::Escape($pattern), [regex]::Escape($insert)
        Set-Content -Path $trackFile -Value $newTrackText -Encoding UTF8
        Write-Host "Propriedade RandomTieBreaker inserida em Track.cs"
    }
}

# 2) Substituir LoadPlaylist em Inicial.cs
if (-not (Test-Path $inicialFile)) { Write-Error "Inicial.cs não encontrado em $inicialFile"; throw }
Backup-File $inicialFile | Out-Null

$newLoadPlaylist = @'
        private void LoadPlaylist(int? id = null)
        {
            try
            {
                // 1. Decisão de qual ID carregar
                if (id.HasValue)
                {
                    _currentPlaylistId = id.Value;
                }
                else
                {
                    _currentPlaylistId = _iniService.ReadInt("Player", "LastPlaylistId", 1);
                }

                LogService.GravarInfo("Database", $"Executando LoadPlaylist para ID: {_currentPlaylistId}");

                _listaAtualId = _currentPlaylistId;

                if (_player != null)
                    _player.CurrentPlaylistId = _currentPlaylistId;

                string nomeLista = _trackRepo.GetPlaylistName(_currentPlaylistId);

                if (lblPlaylistTitle != null)
                    lblPlaylistTitle.Text = nomeLista.ToUpper();

                // 2. Busca os dados do banco para a lista definida
                var tracksDoBanco = _trackRepo.GetTracksByPlaylist(_currentPlaylistId);

                // --- CHECAGEM DE DUPLICATAS ---
                bool duplicataDetectada = false;
                if (tracksDoBanco != null && tracksDoBanco.Count > 1)
                {
                    for (int i = 1; i < tracksDoBanco.Count; i++)
                    {
                        if (tracksDoBanco[i].FilePath == tracksDoBanco[i - 1].FilePath)
                        {
                            duplicataDetectada = true;
                            break;
                        }
                    }
                }

                if (duplicataDetectada)
                {
                    var result = MessageBox.Show(
                        "Foram detectadas músicas duplicadas nesta lista.\n\nDeseja executar o procedimento de limpeza agora?",
                        "Confirmação de Limpeza",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (result == DialogResult.Yes)
                    {
                        _trackRepo.LimparDuplicatasNoBanco();

                        LoadPlaylist(_currentPlaylistId);

                        if (_allTracks.Count > 0 && _player != null)
                        {
                            _player.Play(0);
                        }
                        return;
                    }
                }

                // 3. Processamento e Ordenação
                _allTracks = tracksDoBanco?
                    .Where(t => t.Duration.TotalSeconds > 0)
                    .ToList() ?? new List<Track>();

                // Gera um valor randômico criptográfico por faixa (garante variação a cada carga)
                using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
                {
                    byte[] buf = new byte[4];
                    foreach (var t in _allTracks)
                    {
                        rng.GetBytes(buf);
                        uint v = BitConverter.ToUInt32(buf, 0);
                        t.RandomTieBreaker = v / (double)UInt32.MaxValue;
                    }
                }

                // Ordena: primeiro por Vez (menos tocadas) e, em caso de empate, por RandomTieBreaker
                _allTracks = _allTracks
                    .OrderBy(t => t.Vez)
                    .ThenBy(t => t.RandomTieBreaker)
                    .ToList();

                if (_player != null)
                    _player.SetPlaylist(_allTracks);

                // 4. Interface
                if (lvTracks != null)
                {
                    ConfigurarColunasGrid();
                    lvTracks.VirtualListSize = _allTracks.Count;
                    lvTracks.Invalidate();
                }

                this.CarregandoListas = true;
                RestaurarUltimaMusica();
                this.CarregandoListas = false;

                if (lblTrackCount != null)
                    lblTrackCount.Text = $"{_allTracks.Count} músicas encontradas";

                AtualizarIndicadorProximaProgramacao();
            }
            catch (Exception ex)
            {
                LogService.GravarErro("LoadPlaylist", ex);
                MessageBox.Show("Erro ao carregar lista: " + ex.Message);
            }
        }
'@

$signatureLoad = "private void LoadPlaylist(int? id = null)"
if ($WhatIf) { Write-Host "[WhatIf] Substituir LoadPlaylist em $inicialFile" }
else { Replace-MethodBySignature $inicialFile $signatureLoad $newLoadPlaylist | Out-Null }

# 3) Substituir IntercalarMenosEMaisTocadas em TrackRepository.cs
if (-not (Test-Path $repoFile)) { Write-Error "TrackRepository.cs não encontrado em $repoFile"; throw }
Backup-File $repoFile | Out-Null

$newIntercalar = @'
        private List<Track> IntercalarMenosEMaisTocadas(List<Track> tracksOrdenadasPorMenosTocadas)
        {
            var resultado = new List<Track>(tracksOrdenadasPorMenosTocadas.Count);
            if (tracksOrdenadasPorMenosTocadas == null || tracksOrdenadasPorMenosTocadas.Count == 0)
            {
                return resultado;
            }

            var idsUsados = new HashSet<int>();

            // Gera valor randômico por faixa para desempate nas ordenações
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                byte[] buf = new byte[4];
                foreach (var t in tracksOrdenadasPorMenosTocadas)
                {
                    rng.GetBytes(buf);
                    uint v = BitConverter.ToUInt32(buf, 0);
                    t.RandomTieBreaker = v / (double)UInt32.MaxValue;
                }
            }

            // Ordena as mais tocadas: por Vez decrescente e desempata usando RandomTieBreaker
            var maisTocadas = tracksOrdenadasPorMenosTocadas
                .OrderByDescending(t => t.Vez)
                .ThenBy(t => t.RandomTieBreaker)
                .ThenByDescending(t => t.LastPlayedAt ?? DateTime.MinValue)
                .ThenByDescending(t => t.Id)
                .ToList();

            int indiceMenosTocada = 0;
            int indiceAlternativa = maisTocadas.Count / 2;
            int etapa = 0;

            while (resultado.Count < tracksOrdenadasPorMenosTocadas.Count)
            {
                if (etapa == 1)
                {
                    Track alternativa = ObterProximaAlternativa(maisTocadas, idsUsados, ref indiceAlternativa);
                    if (alternativa != null)
                    {
                        resultado.Add(alternativa);
                        idsUsados.Add(alternativa.Id);
                    }
                }
                else
                {
                    Track menosTocada = ObterProximaMenosTocada(tracksOrdenadasPorMenosTocadas, idsUsados, ref indiceMenosTocada);
                    if (menosTocada != null)
                    {
                        resultado.Add(menosTocada);
                        idsUsados.Add(menosTocada.Id);
                    }
                }

                etapa = (etapa + 1) % 3;

                if (idsUsados.Count >= tracksOrdenadasPorMenosTocadas.Count)
                {
                    break;
                }
            }

            return resultado;
        }
'@

$signatureIntercalar = "private List<Track> IntercalarMenosEMaisTocadas(List<Track> tracksOrdenadasPorMenosTocadas)"
if ($WhatIf) { Write-Host "[WhatIf] Substituir IntercalarMenosEMaisTocadas em $repoFile" }
else { Replace-MethodBySignature $repoFile $signatureIntercalar $newIntercalar | Out-Null }

Write-Host "Script finalizado."
if ($WhatIf) { Write-Host "Nenhuma alteração foi escrita porque -WhatIf foi usado." }
else { Write-Host "Alterações aplicadas. Verifique e compile o projeto no Visual Studio." }