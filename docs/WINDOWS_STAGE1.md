# CataMedia Windows — Etapa 1

Esta é a base de desenvolvimento C#/.NET/WPF, não uma substituição funcional da versão PowerShell 2.0.1. Ainda não baixa arquivos, instala dependências ou oferece instalador. A tela bilíngue provisória permite testar leitura e salvamento explícito das preferências; a interface final e a troca completa de idioma pertencem à Etapa 3.

## Estrutura e ferramentas

- `src/CataMedia.Core`: preferências e validação, sem interface ou acesso a arquivos.
- `src/CataMedia.Windows`: resolução de pastas e persistência JSON.
- `src/CataMedia.Desktop`: ponto de entrada WPF (`WinExe`, sem console) e tela provisória.
- `tests/CataMedia.Tests`: testes de persistência, isolamento e abertura/salvamento da janela.

Usar Windows x64 e SDK .NET 10.0.401, definido em `global.json`, com atualizações de correção compatíveis. Versões NuGet são explícitas e arquivos `packages.lock.json` fixam a resolução. Nenhuma biblioteca externa de execução foi adicionada ao aplicativo; xUnit e o SDK de testes são dependências de desenvolvimento.

Neste ambiente, o SDK foi instalado em `%LOCALAPPDATA%/CataMediaDevelopment/dotnet`, sem alteração permanente do PATH. Para usar os comandos abaixo nesta sessão PowerShell:

```powershell
$env:PATH = (Join-Path $env:LOCALAPPDATA 'CataMediaDevelopment/dotnet') + ';' + $env:PATH
```

```powershell
dotnet restore CataMedia.sln --locked-mode
dotnet build CataMedia.sln -c Release --no-restore
dotnet test CataMedia.sln -c Release --no-build
dotnet run --project src/CataMedia.Desktop
```

## Modos e preferências

O diretório do executável deve conter exatamente um marcador: `portable.mode` ou `installed.mode`. Ausência ou conflito impede abertura com uma mensagem, sem escolher uma localização silenciosamente. A compilação desta etapa fornece o marcador portátil. O futuro instalador usará o marcador instalado.

- Portátil: `data/preferences.json` junto ao executável.
- Instalado: `%LOCALAPPDATA%/CataMedia/data/preferences.json`.
- Temporários futuros: `temp` junto ao executável portátil ou `%LOCALAPPDATA%/CataMedia/temp`. Nenhuma pasta temporária ou log é criado nesta etapa.

Abrir e editar a tela não grava preferências. Salvar valida as escolhas, prepara um arquivo temporário na mesma pasta e substitui o anterior mantendo `preferences.json.bak`. Falhas de leitura ou versão de esquema desconhecida preservam o arquivo e bloqueiam a gravação. Recuperação assistida será uma etapa posterior; o mantenedor pode restaurar a cópia válida após fechar a aplicação. A pasta deve permitir gravação; não há mudança automática para outro local.

Não se lê, migra ou altera `%APPDATA%/catamedia/settings.json`, nem configurações antigas `yt-gui`. A importação eventual será explícita. Cada apresentação terá seu próprio arquivo; não há sincronização entre modos. Uso simultâneo de várias instâncias editando as mesmas preferências não faz parte do fluxo validado desta etapa.

## Referência funcional a preservar

Base: commit `0eb5b6613643d69aa06f3044cf2f14022bcd25d7`, release v2.0.1.

| Área | Comportamento atual / requisito da migração |
|---|---|
| Vídeo | Opções 720p/1080p; preferência MP4 e junção com FFmpeg. Rever alternativas de formato para não trocar silenciosamente a escolha. |
| Áudio | MP3, WAV, FLAC; MP3 usa níveis internos 7/5/0 para baixa/padrão/alta. |
| Fila | Várias URLs, execução sequencial, resultado por item e cancelamento. |
| Dependências | yt-dlp externo atualizável pelo GitHub oficial; FFmpeg/FFprobe externos; detectar ambiente JavaScript compatível. |
| Preferências | Idioma, destino e escolhas lembradas; dados antigos não são sobrescritos. |
| Interface | PT-BR/EN, formato e resolução acessíveis, pasta de destino e opções avançadas. |
| Diagnóstico | Mudança intencional: não transportar os logs automáticos no destino para a nova aplicação. |
| Distribuição | Futura versão portátil e instalável, ambas sem assinatura digital. |

Os scripts e lançadores PowerShell permanecem inalterados. A Etapa 2 implementará um fluxo real de vídeo; nada desta etapa deve ser publicado como release final do aplicativo.

## Controle de versão

Implementação em branch dedicada, revisão por pull request e verificações Windows antes de merge. Não mover tags existentes nem criar release para esta base. Publicar somente fontes, documentação, testes e arquivos de controle; ignorar binários de compilação, dados produzidos e artefatos. Documentos locais de análise/plano continuam excluídos pelo `.git/info/exclude` do checkout.
