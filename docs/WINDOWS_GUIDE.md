# CataMedia Windows — guia da prévia / preview guide

Desenvolvimento / Developer: Wisley Vilela. Licença MIT.
Distribuição oficial / Official releases: https://github.com/Wisleyv/media_download/releases
Suporte / Support: https://github.com/Wisleyv/media_download/issues

## Português

Windows 10 versão 2004 (19041) ou posterior, x64. O pacote inclui o .NET:
não é necessário instalar SDK ou runtime. Esta é uma prévia, não substitui a release PowerShell estável.

**Instalável:** execute CataMedia-Setup, escolha a pasta e abra pelo menu Iniciar.
A instalação é individual e não pede administrador. O atalho na área de trabalho é opcional.
**Portátil:** extraia todo o ZIP em uma pasta gravável; abra CataMedia.exe.
Não execute dentro do ZIP nem copie somente o EXE. Pastas com espaços e acentos são aceitas.

O ZIP e o instalador incluem o .NET, mas **não incluem yt-dlp, FFmpeg/FFprobe ou Node.js**.
Em uma máquina limpa, use **Componentes** para obtê-los com confirmação.
Se já possui essas ferramentas, selecione yt-dlp.exe, a pasta que contém **ffmpeg.exe e
ffprobe.exe juntos** e node.exe em **Mais opções e componentes**. Não é necessário copiá-los
para a pasta do programa. A detecção automática procura na área de dependências do CataMedia,
ao lado de CataMedia.exe e nas pastas do PATH; não procura em todo o computador.

1. Abra Componentes; verifique e obtenha yt-dlp, FFmpeg + FFprobe e Node.js.
   São downloads externos opcionais, com confirmação e validação. Dependências já existentes
   podem ser escolhidas nas opções avançadas. Uma conexão é necessária para obter componentes e mídia.
2. Cole um ou mais links, um por linha. Escolha o destino e vídeo MP4 ou áudio MP3/WAV/FLAC.
3. Confira resolução máxima ou qualidade, depois clique Baixar. Uma fonte pode oferecer resolução menor.
4. Aguarde a fila. Abra a pasta ao terminar. Falhas aparecem nos detalhes; é possível repetir as falhas.
   Cancelar interrompe o trabalho; arquivos parciais podem permanecer para retomada.

Idioma PT-BR/EN, formatos, resolução e opções avançadas continuam disponíveis.
Não são criados logs por download. Conteúdo privado ou restrito pode não estar disponível.
Use apenas conteúdo que você tenha autorização para obter.

**Atualizar:** em Ajuda / Sobre, confira a versão e consulte as releases. A consulta abre o GitHub,
sem comparação automática ou instalação. A release PowerShell 2.x pertence à versão anterior.
Para o CataMedia C#, use somente uma release Windows 3.x compatível quando publicada.
No instalado, feche o aplicativo e execute o novo instalador sobre a mesma pasta.
No portátil, feche o aplicativo, faça cópia de segurança de `data` e extraia o novo ZIP
sobre os arquivos do programa. Preserve `data`; não misture os marcadores de instalação.
Atualização dos componentes é feita separadamente por Componentes. Restaurar versão anterior
permite recuperar um componente gerenciado que apresentou problemas.

**Dados:** portátil usa `data` ao lado do EXE; instalado usa `%LOCALAPPDATA%\CataMedia\data`.
Desinstalar remove programa e atalhos; preserva preferências, componentes e mídia.
Para remover seus dados posteriormente, feche o aplicativo e remova apenas a pasta de dados
que você identificou. Nunca apague sua pasta de mídia para desinstalar.

**Problemas:** se a pasta portátil não permitir gravar, extraia em outra pasta do usuário.
Se as preferências estiverem corrompidas, preserve uma cópia e procure suporte: o programa
não as sobrescreve. Sem internet, os componentes existentes continuam utilizáveis.
Falhas em sites podem exigir atualização do yt-dlp, mas atualizar não garante acesso.

Aplicativo e instalador não possuem assinatura digital. Confira a origem oficial e
SHA256SUMS.txt usando `Get-FileHash -Algorithm SHA256 caminho-do-arquivo` no PowerShell.
O hash confere integridade, não identifica o editor. Em caso de bloqueio pelo Windows,
confira origem e hash e procure suporte; não desative o antivírus.

## English

Windows 10 version 2004 (19041) or newer, x64. .NET is bundled; no SDK/runtime installation
is needed. This preview does not replace the stable PowerShell release.
Run Setup for a per-user installation, or extract the entire portable ZIP into a writable
folder and launch CataMedia.exe. Do not launch inside the ZIP or copy just the EXE.

The ZIP and installer bundle .NET, but **do not include yt-dlp, FFmpeg/FFprobe or Node.js**.
On a clean machine, use **Components** to obtain them with confirmation. For existing tools,
select yt-dlp.exe, a folder containing **both ffmpeg.exe and ffprobe.exe**, and node.exe in
**More options and components**. Copying them into the application folder is unnecessary.
Automatic discovery checks CataMedia's dependency storage, the application folder and PATH;
it does not search the entire computer.

Open Components to check and obtain yt-dlp, FFmpeg + FFprobe and Node.js with confirmation.
Alternatively select existing tools in Advanced options. Paste one link per line, choose
the output folder, format and maximum resolution/audio quality, then Download. Use Open folder
after completion. The sequential queue supports cancellation and retrying failures.
Partial files may remain after cancellation. Download logs are not created.

Help / About shows developer credits, version, offline guide and GitHub manual/releases.
Release checking opens GitHub; it does not automatically compare versions or install updates.
The existing PowerShell 2.x release is a different application generation; use a compatible
Windows 3.x release for the C# application when published.
Close the application before upgrading. Run the new installer in the same folder, or back up
portable `data` and extract the new ZIP over program files, preserving `data`.
Installed data resides in `%LOCALAPPDATA%\CataMedia\data`; portable data is next to the EXE.
Uninstall preserves data, dependencies and downloaded media.

Choose another folder if portable storage is unwritable. Corrupt preferences are preserved,
not overwritten; back them up and seek support. Offline checks do not prevent use of installed
dependencies. Site changes may require yt-dlp updates; updates do not guarantee restricted access.
Only download content you are authorized to obtain.

The application and installer are unsigned. Verify the official source and SHA256SUMS.txt
with PowerShell `Get-FileHash -Algorithm SHA256 file-path`. Hashes check integrity, not publisher
identity. Seek support for security blocks; do not disable antivirus protection.
