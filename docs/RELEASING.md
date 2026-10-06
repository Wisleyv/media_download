# CataMedia — preparar e publicar releases / release procedure

Estas instruções cobrem a distribuição **C# 3.x Windows x64**. A publicação é uma ação
explícita: preparar documentação ou pacotes não autoriza criar tags, publicar releases,
mesclar PRs ou remover branches/arquivos.

These instructions cover **C# 3.x Windows x64**. Publication is an explicit action;
preparing documentation or packages does not authorize tags, releases, merges or cleanup.

## Português

### 1. Fixar a origem e concluir a aceitação

- Concluir/revisar os recortes autorizados e integrar os PRs na ordem de dependência, com CI aprovada. Não reescrever histórico nem inferir autorização para merges.
- Escolher o commit integrado que será distribuído. Atualizar `Version` em `src/CataMedia.Desktop/CataMedia.Desktop.csproj` antes de fixar esse commit. `3.0.0-stage5` é uma identificação de desenvolvimento, não uma versão final.
- A entrega inicial foi aceita como estável pelo usuário: versão `3.0.0`, tag `v3.0.0`, release sem Pre-release. Prévias futuras devem usar sufixo e marcação próprios.
- Atualizar apresentação, guia PT-BR/EN, capturas, notas da versão e limites conhecidos. O link Manual no GitHub deve apontar para um commit publicado, ou tag existente, que contenha o manual daquela compilação; não usar branch temporária.
- Conferir os cenários pendentes em [WINDOWS_STAGE6.md](WINDOWS_STAGE6.md): instalador em máquina limpa, conta padrão separada, atualização entre versões diferentes, preservação de dados e acessibilidade/DPI real. Não registrá-los como aprovados sem evidência. Limitações aceitas devem constar das notas.

### 2. Compilar e testar em checkout limpo

O empacotador exige árvore limpa, incluindo arquivos não rastreados. Preserve handoffs,
imagens fornecidas pelo usuário e outros arquivos locais: use um checkout/worktree isolado
do commit escolhido. Não apague arquivos para satisfazer esse requisito.

Ferramentas: PowerShell **7** (`pwsh`), SDK definido em `global.json` e compilador oficial Inno Setup **6.7.3**,
validado pelo hash exigido no script. Não alterar SDK/hash nem usar empacotador PowerShell
legado para contornar falhas. Executar da raiz do checkout:

```powershell
dotnet restore CataMedia.sln --locked-mode
dotnet build CataMedia.sln -c Release --no-restore
dotnet test CataMedia.sln -c Release --no-build
pwsh -NoProfile -File scripts/package-windows.ps1 `
  -DotNet 'C:\caminho\dotnet.exe' -Iscc 'C:\caminho\ISCC.exe'
```

Os caminhos das ferramentas são exemplos e precisam ser substituídos. Os testes reais
opcionais não executados devem ser registrados como ignorados, não como aprovados.
O script faz publicação autossuficiente, sem trimming, e usa o mesmo payload no ZIP e
instalador. Não cria tags nem publica releases. Pastas de saída existentes não são sobrescritas.

### 3. Conferir os artefatos

Saída: `artifacts/packages/<versão>-<commit-curto>/`.

| Artefato | Conteúdo/finalidade |
|---|---|
| `CataMedia-<versão>-win-x64-portable.zip` | Programa completo, .NET e marcador `portable.mode` |
| `CataMedia-Setup-<versão>-win-x64.exe` | Instalação por usuário do mesmo payload, com `installed.mode` |
| `SHA256SUMS.txt` | Hashes do ZIP e instalador finais |
| `BUILD.json`, dentro do payload/ZIP | Versão, commit, SDK, compilador e hashes de arquivos |

Conferir versão incorporada, commit do manifesto e futuro alvo da tag. Comparar os hashes
de `SHA256SUMS.txt` com `Get-FileHash -Algorithm SHA256`. Validar o manifesto após extração
e instalação; diferenças entre as apresentações devem se limitar aos marcadores e arquivos
do instalador/desinstalador.

Verificar `GUIDE.txt`, `THIRD-PARTY.txt`, licença MIT, licenças/notices do runtime e ícone.
Não distribuir yt-dlp, FFmpeg/FFprobe, Node.js, mídia, preferências, cookies, logs, testes,
fontes ou handoffs privados. A ausência dos três componentes externos é intencional e
precisa estar explícita nas notas e na primeira execução.

Testar o pacote exato em pasta com espaços/acentos e máquina limpa, sem SDK/.NET instalado;
instalação por usuário, abertura, atualização entre versões diferentes e desinstalação,
conferindo preservação de `data` e mídia. Reinstalar o mesmo pacote não substitui um teste
de atualização entre versões. Os pacotes permanecem sem assinatura digital.

### 4. Publicar somente após autorização

1. Criar a tag da versão no mesmo commit conferido. Não mover/reutilizar tags existentes.
2. No GitHub, preparar uma release em rascunho a partir dessa tag e anexar ZIP, instalador e `SHA256SUMS.txt` daquela saída.
3. Escrever notas PT-BR/EN: o que mudou, como escolher os pacotes, .NET incluído, componentes separados, ausência de assinatura, dados/atualização, limitações e links de manual/suporte.
4. Para a entrega estável 3.0.0, desmarcar **Pre-release** e indicar a release como mais recente. Manter todas as releases PowerShell disponíveis. A consulta automática do CataMedia reconhece releases estáveis C# 3.x com os três assets esperados.
5. Após publicação, conferir tag remota/commit, nomes e hashes dos assets baixados do GitHub. Conferir links do manual e das imagens, não apenas os links no checkout local.

Se a conferência falhar, interromper e investigar. Não reconstruir silenciosamente os
arquivos sob a mesma tag nem declarar release concluída. O histórico PowerShell e as
releases anteriores permanecem disponíveis. Sanitização é um recorte posterior, com
inventário e preservação recuperável; não acompanha automaticamente a publicação.

## English

### 1. Select source and finish acceptance

Review authorized changes and integrate dependent PRs in order, with passing CI and
explicit merge authorization. Choose the integrated source commit. Set `Version` in
`src/CataMedia.Desktop/CataMedia.Desktop.csproj` before fixing that commit; `3.0.0-stage5`
is a development identifier. The user accepted the initial delivery as stable:
version `3.0.0`, tag `v3.0.0`, without Pre-release.

Update presentation, both guide languages, screenshots, release notes and known limits.
Pin the application's manual link to a published commit or existing tag containing that
build's guide. Review pending checks in [WINDOWS_STAGE6.md](WINDOWS_STAGE6.md); do not
claim unperformed clean-machine installer, standard-user, cross-version upgrade or
accessibility/DPI checks passed. Accepted limitations belong in the release notes.

### 2. Build from a clean checkout

Use an isolated checkout/worktree of the selected commit. Preserve local handoffs and
user files; do not delete them to satisfy the clean-tree requirement. Use the SDK in
`global.json` and the validated Inno Setup **6.7.3** compiler. Run the commands in the
Portuguese section above with actual tool paths. Record skipped real tests as skipped.

Run the packager with PowerShell **7** (`pwsh`), not Windows PowerShell 5.1.
`scripts/package-windows.ps1` produces a self-contained, untrimmed win-x64 payload and
uses those same binaries for ZIP and Setup. It does not create tags, publish releases
or overwrite an existing output folder. `scripts/package-release.ps1` is the legacy
PowerShell packager and must not be used for C#.

### 3. Verify packages

Output is `artifacts/packages/<version>-<short-commit>/`: portable ZIP, per-user installer
and `SHA256SUMS.txt`, named as in the table above. `BUILD.json` inside the payload/ZIP
records version, commit, SDK, compiler and file hashes. Match those to the embedded
version and intended tag commit. Verify final hashes and the payload after extraction
and installation; expected differences are distribution markers and installer files.

Verify offline guide, application/runtime licenses and notices, and icon. External tools,
private data, media, preferences, cookies, logs, tests, source and handoffs are excluded.
State clearly that yt-dlp, FFmpeg/FFprobe and Node.js must be obtained separately.

Test these exact packages on a clean machine without SDK/.NET, in paths with spaces and
accents, including per-user installation, launch, upgrade from a different version and
uninstall while preserving data/media. Same-package reinstallation is not cross-version
upgrade validation. Application and installer remain unsigned.

### 4. Publish with explicit authorization

Create a new version tag at the verified commit, then prepare a draft GitHub release and
attach the matching ZIP, installer and checksums. Include PT-BR/EN notes covering changes,
package choice, bundled .NET, separate tools, unsigned distribution, data preservation,
upgrade instructions, limitations and manual/support links.

Publish stable 3.0.0 without **Pre-release** and mark it latest, retaining PowerShell releases.
Automatic application checks recognize stable C# 3.x releases with the three expected assets.
After publication, verify the remote tag/commit, download the assets to compare
hashes, and check published manual/image links. Do not silently replace files or move
an existing tag after a failure. Preserve previous releases/history. Cleanup is a separate
later task with inventory and recoverable preservation.

## Distribuição legada / Legacy distribution

PowerShell packages use `scripts/package-release.ps1`, not the C# script. The historical
v2.0.0 tag predates the application contained in its uploaded ZIP; v2.0.1 corrected
source/tag/package correspondence. Preserve that evidence and the previous releases:
[release v2.0.1](https://github.com/Wisleyv/media_download/releases/tag/v2.0.1).
