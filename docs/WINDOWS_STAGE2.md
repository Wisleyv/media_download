# CataMedia Windows — Etapa 2

Esta etapa acrescenta um fluxo completo de vídeo à base C#/.NET/WPF: um link HTTP/HTTPS, MP4 com resolução máxima de 720p ou 1080p, progresso, cancelamento, verificação do arquivo e abertura da pasta. Continua sendo uma prévia de desenvolvimento, sem instalador ou release final.

## Executar e testar

Usar o SDK e os comandos de compilação descritos em `WINDOWS_STAGE1.md`. Após compilar, abrir `src/CataMedia.Desktop/bin/Release/net10.0-windows/CataMedia.exe` ou executar:

```powershell
dotnet run --project src/CataMedia.Desktop
```

Selecione uma pasta existente e gravável, cole um link e escolha a resolução máxima. Em “Componentes instalados”, indique yt-dlp.exe, a pasta que contém ffmpeg.exe e ffprobe.exe e, para YouTube, um Node.js compatível com o yt-dlp. O aplicativo procura componentes na sua área de dependências, junto ao executável e no PATH. Nesta etapa, caminhos escolhidos manualmente são usados na sessão; a gestão persistente dos componentes e a atualização serão implementadas na Etapa 4.

A qualidade indica um limite superior, não uma promessa de resolução que a origem não oferece. A resolução efetiva aparece ao concluir. Não há alternativa de formato que ultrapasse o limite escolhido. Se MP4 com áudio compatível não estiver disponível, a operação falha sem trocar silenciosamente para outro formato.

URLs são passadas como argumentos separados, após `--`, sem execução por um shell. Configurações e plugins externos do yt-dlp são ignorados; não são baixados componentes executáveis remotamente pelo serviço. A integração usa templates estruturados de progresso e confirmação de arquivo, conforme a [orientação do yt-dlp](https://github.com/yt-dlp/yt-dlp#embedding-yt-dlp). O FFprobe confirma MP4, vídeo, áudio, duração positiva e altura dentro do limite, além da presença de arquivo não vazio no destino.

Arquivos existentes não são sobrescritos. Um arquivo já presente pode ser reutilizado pelo motor e é verificado antes de anunciar conclusão; sua resolução real continua visível. Para obter outra versão de um mesmo vídeo, escolha outro destino nesta prévia. Progresso corresponde à transferência corrente; pode reiniciar ao passar de vídeo para áudio, e fica indeterminado quando o tamanho ainda não é conhecido. A junção e a verificação têm estados próprios.

Cancelar ou fechar a janela encerra o processo e sua árvore de subprocessos; a janela aguarda o encerramento antes de fechar. Arquivos parciais de mídia podem permanecer para tentativa posterior. Não são logs e não são apagados automaticamente. A tentativa seguinte conserva o comportamento de retomada do motor.

## Persistência e diagnóstico

Mantêm-se os modos e a política de preferências da Etapa 1. Download e edição da tela não salvam implicitamente preferências. A área de áudio continua apenas como preferências para a próxima etapa; o botão atual baixa somente MP4.

Não são gerados logs no destino, ao lado do executável ou em pasta temporária. Mensagens de falha ficam na sessão, com diagnóstico limitado em memória. Um pequeno arquivo de teste de gravação é excluído automaticamente antes da transferência. Nenhum executável externo é versionado ou incluído no pacote de fontes.

## Validação desta etapa

Os testes comuns não acessam a rede. Usam processos simulados para validar argumentos, falhas, presença e conteúdo do arquivo final, progresso e metadados. Testes de processo real conferem caracteres especiais, os dois canais de saída e cancelamento de um processo com filho. Testes WPF conferem conclusão, cancelamento e fechamento durante a operação, além da persistência já existente.

Há um teste real separado, desativado por padrão. Ative somente com link autorizado e ferramentas instaladas, em um destino isolado e vazio:

```powershell
$env:CATAMEDIA_REAL_TEST_URL = '<link autorizado>'
$env:CATAMEDIA_YTDLP = '<caminho absoluto de yt-dlp.exe>'
$env:CATAMEDIA_FFMPEG = '<pasta absoluta de FFmpeg e FFprobe>'
$env:CATAMEDIA_NODE = '<caminho absoluto de node.exe>'
$env:CATAMEDIA_TEST_OUTPUT = '<pasta isolada e vazia>'
dotnet test tests/CataMedia.Tests/CataMedia.Tests.csproj -c Release --no-build --filter FullyQualifiedName~RealVideoDownloadTests
```

O teste baixa em até 720p, confirma progresso, junção e verificação, e grava `test-result.json` como evidência de teste. Esse JSON não é um log do aplicativo. Arquivos de mídia e evidências locais devem ficar em `artifacts/`, ignorado pelo Git. O teste real não roda no GitHub Actions; a validação remota permanece independente de sites e dependências externas.

Foi validado localmente um link fornecido pelo usuário: MP4 com vídeo 720p, áudio e duração de 204,84644 segundos, em destino com espaços e acentos. Dependências usadas: yt-dlp 2026.08.19, Node.js 22.16.0 e FFmpeg/FFprobe já instalados no ambiente. Os links pessoais de teste estão registrados em um documento local ignorado pelo Git, incluindo o segundo link reservado para a fila da Etapa 3.

## Controle e limite

A branch da Etapa 2 parte da Etapa 1; enquanto a PR anterior não estiver integrada, sua PR usa a branch anterior como base. A integração deve seguir a ordem das etapas. A workflow Windows agora cobre essas branches e PRs.

PowerShell, dependências existentes, tags, releases e main permanecem preservados. Áudio, fila, playlists completas, tradução final, instalador e atualização assistida de dependências continuam nas respectivas etapas futuras. Esta etapa não introduz publicação nem atualização do próprio aplicativo.
