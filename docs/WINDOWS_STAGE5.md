# CataMedia Windows — Etapa 5

## Distribuição

`scripts/package-windows.ps1` exige árvore limpa, .NET SDK fixado pelo global.json
e compilador oficial Inno Setup 6.7.3 (hash conferido). Gera uma publicação
autossuficiente win-x64, sem trimming, e usa os mesmos binários no ZIP e instalador.
Executar com `-DotNet caminho/dotnet.exe -Iscc caminho/ISCC.exe`.
Os arquivos ficam em `artifacts/packages/<versão>-<commit>/`; existentes não são sobrescritos.
Não publica releases nem altera tags. O empacotamento PowerShell permanece separado.

BUILD.json registra versão, commit, SDK, compilador e hashes dos arquivos do payload.
SHA256SUMS.txt confere ZIP e instalador finais. Dados pessoais, dependências externas,
logs, testes e fontes não entram nos pacotes. O runtime .NET e suas licenças entram.
O marcador portable.mode acompanha o ZIP; o instalador o substitui por installed.mode.

Instalação por usuário em `%LOCALAPPDATA%\Programs\CataMedia`, sem elevação,
atalho Iniciar e área de trabalho opcional; PT-BR/EN. Desinstalação preserva a pasta
separada `%LOCALAPPDATA%\CataMedia\data` e toda mídia. Não há limpeza recursiva de dados.
Atualização exige fechar o aplicativo e usar a mesma pasta; portátil preserva `data`.
Guia inicial e solução de problemas: [WINDOWS_GUIDE.md](WINDOWS_GUIDE.md).

## Ajuda e pendências de encerramento

Ajuda/Sobre inclui Wisley Vilela, licença, versão, guia offline, manual GitHub e releases.
A consulta de releases abre a página oficial para comparação manual; não compara
automaticamente nem atualiza o programa. Na etapa 6, implementar comparação compatível
com C# 3.x (sem confundir PowerShell 2.x), falha de rede não bloqueante e ajustar o link
do manual para a referência publicada. Atualizações de dependências continuam separadas.

Ao término de todas as etapas, inventariar arquivos locais e remotos e remover os
comprovadamente obsoletos da árvore corrente mediante commit/PR normal. Preservar
histórico GitHub, releases anteriores, dados pessoais e mídia. Para artefatos locais,
verificar caminhos e manter cópia recuperável antes de remover. Não sanitizar nesta etapa.

## Ferramentas e licenças

Publicação autossuficiente inclui runtime e dispensa .NET pré-instalado:
[documentação Microsoft](https://learn.microsoft.com/en-us/dotnet/core/deploying/).
Instalação sem elevação usa [PrivilegesRequired=lowest](https://jrsoftware.org/ishelp/topic_setup_privilegesrequired.htm).
Inno Setup 6.7.3 foi obtido da [distribuição oficial](https://jrsoftware.org/isdl.php),
com assinatura do instalador da ferramenta validada (Pyrsys B.V.). Sua LICENSE.txt
permite uso e redistribuição sob as condições nela descritas; não implica assinar CataMedia.
O aplicativo e seu instalador permanecem sem assinatura, conforme decisão do projeto.

## Validação

Em 5 de outubro de 2026:

- Restore em modo bloqueado aprovado; build Release sem avisos ou erros;
  86 testes offline aprovados, quatro grupos de testes reais desativados por padrão.
- ZIP e instalador produzidos e conferidos por SHA-256. Manifesto de cada arquivo
  validado após extração e instalação; diferenças limitadas ao marcador e arquivos do desinstalador.
- Portátil aberto em caminho com espaços e acentos. Extração repetida preservou dados.
- Instalador executado em pasta isolada, atalho Iniciar conferido, aplicativo aberto;
  reinstalação do mesmo pacote e desinstalação preservaram dados e mídia de teste.
  Não existia instalador de versão anterior; atualização entre versões diferentes permanece para o piloto.
- Abertura com PATH reduzido, DOTNET_ROOT apontando para pasta inexistente e busca
  multinível desativada confirmou uso do runtime incluído. Isso não substitui máquina limpa.
- Ajuda conferida visualmente em português e inglês. A versão empacotada inclui guia offline.
- Verificação inicial de pasta gravável implementada; obstrução de `data` por arquivo
  simulada sem sobrescrevê-lo. ACLs restritivas e usuário Windows sem privilégios
  administrativos não foram exercitados nesta máquina.

Pacotes locais de teste: `artifacts/packages/3.0.0-stage5-2708035d0637/`,
origem `2708035d0637b396e619080f097e2bba75e126af`, registrada em BUILD.json e versão incorporada.
ZIP aproximadamente 65 MB; instalador 47 MB. Evidências e fixtures em `artifacts/`, ignorado.
O commit posterior de documentação não muda o código desses binários.

CI revelou intermitência no teste preexistente de processos: a espera inicial pelo filho
ficava fora do bloco de limpeza, deixando a fixture em uso quando expirava. O teste agora
garante cancelamento e espera de encerramento mesmo nessa falha; a margem de inicialização
é 30 segundos, mantendo o limite original de dez segundos para comprovar cancelamento.
O executor do aplicativo não foi alterado. Essa correção de teste não muda os pacotes.

Este ambiente não oferece Windows Sandbox nem VM limpa disponível. Validação em máquina
limpa, usuário sem privilégios, atualização entre versões e piloto continuam pendentes
antes da release. Etapa 5 tem implementação e testes locais concluídos, com essa ressalva
de aceitação. Nenhuma release foi publicada e a etapa 6 não foi iniciada.
