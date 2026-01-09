# Player de Música MP3 - C# WinForms

Este é um player de música MP3 completo desenvolvido em C# utilizando WinForms e .NET Framework 4.8.

## 🚀 Funcionalidades

- **Reprodução de MP3:** Controle total com Play, Pause e Próxima.
- **Banco de Dados SQLite:** Armazenamento persistente de músicas e bandas.
- **Spectrum Analyzer:** Visualização gráfica do áudio em tempo real.
- **Drag & Drop:** Adicione músicas arrastando arquivos .mp3 diretamente para a lista.
- **Tags ID3:** Leitura automática de título, banda e duração usando TagLibSharp.
- **Atalho Global:** Use a tecla **F10** para Play/Pause mesmo com o programa em segundo plano.
- **Modo Tela Cheia:** Clique duplo no Spectrum para alternar para o modo imersivo.
- **Persistência:** Salva a última playlist utilizada em um arquivo `config.ini`.

## 🛠️ Tecnologias Utilizadas

- **C# / WinForms** (.NET Framework 4.8)
- **NAudio:** Para reprodução e processamento de áudio.
- **System.Data.SQLite:** Para persistência de dados.
- **TagLibSharp:** Para leitura de metadados de arquivos MP3.
- **GDI+:** Para renderização do Spectrum Analyzer.

## 📦 Como Compilar

1. Abra a solução `Mp3PlayerWinForms.sln` no Visual Studio.
2. Restaure os pacotes NuGet:
   - `NAudio`
   - `System.Data.SQLite`
   - `TagLibSharp`
3. Compile o projeto em modo `Debug` ou `Release`.
4. Execute o arquivo `Mp3PlayerWinForms.exe`.

## 📂 Estrutura do Projeto

O projeto segue uma arquitetura organizada por camadas:
- `Data/`: Acesso ao banco de dados SQLite.
- `Models/`: Entidades de dados (Track, Band, Playlist).
- `Services/`: Lógica de negócio (Áudio, Hotkeys, Configurações).
- `Controls/`: Componentes visuais customizados.
- `Forms/`: Interface principal do usuário.

---
Desenvolvido como parte de um desafio técnico para criação de ferramentas desktop funcionais.
