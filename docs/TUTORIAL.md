# Tutorial Passo-a-Passo / Step-by-Step Tutorial

> Este guia ajuda usuários não técnicos a executar o CataMedia no Windows.
> This guide helps non-technical users run CataMedia on Windows.

## 1) Baixar o pacote / Download the package

- Acesse a página de releases no GitHub do projeto. / Go to the project's GitHub releases page.
- Baixe o arquivo `.zip` mais recente. / Download the latest `.zip` file.
- Extraia o `.zip` em uma pasta simples (exemplo: `C:\catamedia`). / Extract the `.zip` to a simple folder (e.g., `C:\catamedia`).

## 2) Executar / Run

- Dê duplo clique em `catamedia.vbs` (sem janela de console). / Double-click `catamedia.vbs` (no console window).
- Alternativa: `catamedia.bat` (mostra console de fundo). / Alternative: `catamedia.bat` (shows background console).
- Se o Windows SmartScreen aparecer, clique em "Mais informações" e depois em "Executar assim mesmo". / If Windows SmartScreen appears, click "More info" then "Run anyway".

## 3) Idioma / Language

- No canto superior, use o seletor `PT-BR / EN` para trocar o idioma da interface. / At the top, use the `PT-BR / EN` selector to switch the UI language.
- A escolha é salva automaticamente. / The choice is saved automatically.

## 4) Configurar / Configure

- Cole as URLs (uma por linha) no campo principal. / Paste URLs (one per line) in the main field.
- Clique em "Escolher Pasta" e selecione onde salvar os arquivos. / Click "Choose Folder" and select where to save files.
- Em "Tipo de download", escolha: / Under "Download type", choose:
	- "Vídeo" / "Video": baixa em MP4 (720p/1080p) / downloads as MP4 (720p/1080p)
	- "Áudio" / "Audio": baixa apenas o áudio (mp3/wav/flac) / downloads audio only (mp3/wav/flac)
- Se o yt-dlp.exe não estiver encontrado, clique em "Baixar/Atualizar". / If yt-dlp.exe is not found, click "Download/Update".

## 5) FFmpeg (recomendado / obrigatório para áudio | recommended / required for audio)

- No modo "Vídeo": o FFmpeg melhora a qualidade e junta áudio+vídeo em um único MP4. / In "Video" mode: FFmpeg improves quality and merges audio+video into a single MP4.
- No modo "Áudio": é necessário ter FFmpeg + FFprobe para converter para mp3/wav/flac. / In "Audio" mode: FFmpeg + FFprobe are required to convert to mp3/wav/flac.
- Se não estiver instalado, o app oferece baixar o FFmpeg portátil na pasta do yt-dlp.exe. / If not installed, the app offers to download portable FFmpeg to the yt-dlp.exe folder.
- Enquanto baixa/extrai, a barra de progresso fica animada (o app não trava). / While downloading/extracting, the progress bar animates (the app doesn't freeze).

## 6) Baixar / Download

- Se em "Vídeo": escolha a qualidade (720p ou 1080p). / If in "Video": choose quality (720p or 1080p).
- Se em "Áudio" / If in "Audio":
	- Escolha o formato (mp3, wav ou flac) / Choose format (mp3, wav, or flac)
	- Escolha a qualidade (Baixa / Padrão / HQ) / Choose quality (Low / Standard / HQ)
		- Para mp3: HQ=0, Padrão=5, Baixa=7 (`--audio-quality` do yt-dlp) / For mp3: HQ=0, Standard=5, Low=7 (yt-dlp `--audio-quality`)
		- Para wav/flac: qualidade lossless, preset não muda muito / For wav/flac: lossless quality, preset has little effect
- Clique em "Baixar Tudo" / "Download All". / Click "Baixar Tudo" / "Download All".
- Acompanhe o status e progresso na tela. / Follow progress and status on screen.

## 7) Avisos (opcional) / Warnings (optional)

- Por padrão, os avisos técnicos ficam ocultos. / By default, technical warnings are hidden.
- Marque "Mostrar avisos (avançado)" para ver detalhes no log. / Check "Show warnings (advanced)" for log details.

## 8) Logs e configurações / Logs and settings

- Os logs são gravados na pasta de saída. / Logs are saved to the output folder.
- As configurações ficam em `%APPDATA%\catamedia\settings.json`. / Settings are at `%APPDATA%\catamedia\settings.json`.

## Solução de problemas / Troubleshooting

- "yt-dlp.exe não encontrado" / "yt-dlp.exe not found": use o botão "Baixar/Atualizar" ou selecione manualmente. / use "Download/Update" button or browse manually.
- Qualidade baixa / Low quality: instale o FFmpeg quando o app oferecer. / install FFmpeg when the app offers.
- Antivírus bloqueando / Antivirus blocking: adicione exceção para a pasta do programa. / add exception for the program folder.
- Falha em vídeo específico / Specific video failure: tente outro link para testar. / try a different link to test.
