# Tutorial Passo-a-Passo (PT-BR)

Este guia ajuda usuarios nao tecnicos a executar o YT-DLP GUI no Windows.

## 1) Baixar o pacote
- Acesse a pagina de releases no GitHub do projeto.
- Baixe o arquivo .zip mais recente.
- Extraia o .zip em uma pasta simples (exemplo: C:\yt-dlp).

## 2) Executar
- De duplo clique em yt-gui.bat.
- Se o Windows SmartScreen aparecer, clique em "Mais informacoes" e depois em "Executar assim mesmo".

## 3) Configurar
- Cole as URLs (uma por linha) no campo principal.
- Clique em "Escolher Pasta" e selecione onde salvar os arquivos.
- Em "Tipo de download", escolha:
	- "Vídeo": baixa em MP4 (com qualidade 720p/1080p)
	- "Áudio": baixa apenas o audio (mp3/wav/flac)
- Se o yt-dlp.exe nao estiver encontrado, clique em "Baixar/Atualizar" para baixar automaticamente.

## 4) FFmpeg (recomendado / obrigatorio no audio)
- No modo "Vídeo": o FFmpeg melhora a qualidade e junta audio+video em um unico MP4.
- No modo "Áudio": e necessario ter FFmpeg + FFprobe para converter para mp3/wav/flac.
- Se nao estiver instalado, o app oferece baixar o FFmpeg portatil na pasta do yt-dlp.exe.
- Enquanto baixa/extrai, a barra de progresso fica animada (o app nao parece travado).

## 5) Baixar
- Se estiver em "Vídeo": escolha a qualidade (720p ou 1080p).
- Se estiver em "Áudio":
	- Escolha o formato (mp3, wav ou flac)
	- Escolha a qualidade (Baixa / Padrao / HQ)
		- Para mp3: HQ=0, Padrao=5, Baixa=7 (equivale ao `--audio-quality` do yt-dlp)
		- Para wav/flac: a qualidade e lossless/uncompressed, entao o preset nao muda muito
- Clique em "Baixar Tudo".
- Acompanhe o status e o progresso na tela.

## 6) Avisos (opcional)
- Por padrao, os avisos tecnicos ficam ocultos.
- Marque "Mostrar avisos (avancado)" para ver detalhes no log.

## 7) Logs e configuracoes
- Os logs sao gravados na pasta de saida.
- As configuracoes ficam em %APPDATA%\yt-gui\settings.json.

## Solucao de problemas
- "yt-dlp.exe nao encontrado": use o botao "Baixar/Atualizar" ou selecione o arquivo manualmente.
- Qualidade baixa: instale o FFmpeg quando o app oferecer.
- Antivirus bloqueando: adicione uma excecao para a pasta onde esta o programa.
- Falha em video especifico: tente outro link para testar.
