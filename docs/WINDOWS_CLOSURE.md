# Encerramento Windows — CataMedia 3.0.0

## Entrega pública

[Release estável 3.0.0](https://github.com/Wisleyv/media_download/releases/tag/v3.0.0),
com instalador por usuário, ZIP portátil e SHA256SUMS.txt. .NET incluído; yt-dlp,
FFmpeg/FFprobe e Node.js obtidos separadamente. Aplicativo e instalador sem assinatura.

Tag imutável `v3.0.0`, commit `e1afffc1c160fa32ac95a0dd29266f93d3c2e18e`.
Ambos os pacotes vêm desse commit/payload; versão incorporada 3.0.0, manifesto com
409 arquivos. Licença MIT e licenças/notices dos runtimes conferidos contra a origem.

Build sem avisos/erros, 109 testes aprovados, 4 reais opcionais ignorados.
[CI do PR #7](https://github.com/Wisleyv/media_download/actions/runs/37392457374) e
[CI do commit publicado](https://github.com/Wisleyv/media_download/actions/runs/37392626237)
aprovadas. Abertura portátil/instalada, instalação por usuário, reinstalação do mesmo
pacote e desinstalação com preservação dos dados/mídia de teste validadas localmente.
Hashes locais conferem com os digests SHA-256 registrados pelo GitHub. Os dois pacotes
e o arquivo de checksums publicados também foram baixados do GitHub; os hashes completos
conferem com os arquivos de origem.

| Arquivo | SHA-256 |
|---|---|
| CataMedia-3.0.0-win-x64-portable.zip | `0f16b1b1fc5ae102add4413998b98422977d78706ac52b835a3d48aa04e3359a` |
| CataMedia-Setup-3.0.0-win-x64.exe | `8d4747fd4873569570dcfd7258fa81420d4d7a31dc3378cf45f9fc778b3555b2` |

[Manual dessa versão](https://github.com/Wisleyv/media_download/blob/v3.0.0/docs/WINDOWS_GUIDE.md)
conferido com HTTP 200; as dez imagens estão na árvore da tag. A aplicação usa esse
mesmo endereço. Publicação estável foi autorizada pelo usuário após aceitação do piloto.

## Sanitização recuperável

Retirados da árvore corrente os 12 itens legados abaixo; todos têm cópia local conferida
por SHA-256 e permanecem recuperáveis no histórico Git. O backup Git completo, incluindo
referências das branches removidas, foi verificado antes da limpeza e fica fora do
repositório. Não houve reescrita de histórico, movimentação de tags ou remoção de releases.

| Itens retirados | Motivo e recuperação |
|---|---|
| catamedia.ps1, catamedia.bat, catamedia.vbs | Aplicação e lançadores PowerShell substituídos pela distribuição C#; consultar v2.0.1 |
| yt-gui.ps1, yt-gui.bat | Implementação/lançador anteriores; consultar histórico Git e backup |
| scripts/package-release.ps1 | Empacotador PowerShell antigo; consultar v2.0.1. O atual é package-windows.ps1 |
| docs/TUTORIAL.pdf, docs/TUTORIAL_PT_BR.pdf, docs/TUTORIAL_PT_BR.rtf | Manuais antigos PowerShell; consultar v2.0.1. Os guias atuais são Markdown PT-BR/EN |
| releases/yt-gui-release-v1.0.1.zip, v1.0.2.zip, v1.1.0.zip | Artefatos gerados antigos; cópia recuperável e histórico preservados |

Branches das seis etapas, `audio-only` e `release/windows-3.0.0` retiradas localmente e
no GitHub após confirmar que seus commits pertencem à main. PRs #1–#7 mesclados;
referências originais também preservadas no bundle. A branch deste recorte de limpeza
será removida após seu merge com CI aprovada.

Compilações temporárias, previews e pacotes candidatos antigos são arquivados fora do
workspace com inventário e conferência de hashes antes da remoção. Pacotes estáveis,
mídia de teste, dependências existentes, documentos locais e imagens fornecidas pelo
usuário são preservados. Históricos das etapas são registros técnicos úteis e permanecem.
O commit de limpeza posterior à release não altera nem reconstrói os assets publicados.

Recuperação de fontes: consultar a tag histórica ou executar `git show <commit>:<arquivo>`.
Para uma recuperação independente, clonar o bundle local verificado. Inventários locais
indicam a origem, o destino de arquivo e os hashes dos artefatos arquivados; não contêm
credenciais e não são publicados como assets.

## Limites de validação mantidos explícitos

Funcionamento em máquina limpa confirmado pelo usuário durante o piloto. Não se exigiu
nova rodada idêntica para encerrar a sprint. Conta padrão separada, atualização entre
versões diferentes e acessibilidade/DPI ampla não possuem confirmação específica.
A reinstalação do mesmo pacote não comprova atualização entre versões diferentes.

Não executar instâncias simultâneas sobre o mesmo destino/dados. Controle entre
instâncias, limite para preferências artificialmente enormes e prevenção de colisões
entre fontes distintas com título/ID iguais não foram adicionados nesta sprint.
Essas limitações não foram registradas como testes aprovados ou recursos implementados.
