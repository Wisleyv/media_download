# CataMedia Windows — Etapa 4

Esta prévia implementa a obtenção e atualização assistidas das dependências externas. Não inclui instalador, ZIP de distribuição ou atualização automática do CataMedia; esses pacotes pertencem à Etapa 5. PowerShell, configurações antigas e releases permanecem preservados.

## Uso sem terminal

1. Abra **Componentes**, disponível na tela principal mesmo sem executáveis instalados.
2. Selecione yt-dlp, FFmpeg + FFprobe ou Node.js 22 e clique em **Verificar agora**.
3. Confira a versão instalada, a disponível e a origem. **Obter / atualizar** pede confirmação antes do download.
4. Repita para os componentes ausentes. Os caminhos são preenchidos após a ativação e redescobertos na próxima abertura.
5. Se uma atualização apresentar incompatibilidade, escolha **Restaurar versão anterior**. Essa recuperação aplica-se às versões obtidas pelo aplicativo; componentes selecionados manualmente nunca são sobrescritos.

Ao abrir, a aplicação verifica o yt-dlp estável em segundo plano. Cache de seis horas evita consultas repetidas; ETag permite ao GitHub responder sem retransmitir metadados inalterados. A consulta tem limite de 12 segundos; consultas locais de versão têm limite de 10 segundos. A janela e os downloads não aguardam a consulta. Rede indisponível ou limite da API não impede usar componentes existentes.

Uma versão nova gera aviso discreto com **Obter / atualizar** e **Mais tarde**. O aviso explica que os sites mudam e que versões antigas podem falhar; atualizar não garante acesso a conteúdo restrito. Não há substituição automática. Versões identificadas iguais ou mais recentes não recebem oferta de rebaixamento. O botão Componentes e as atualizações ficam indisponíveis durante preparação ou execução da fila. A janela de componentes é modal para impedir início de uma fila durante uma atualização.

A primeira execução indica componentes ausentes, FFprobe ausente ou Node.js incompatível. A escolha manual nas opções avançadas continua disponível. Nenhuma credencial de GitHub é necessária ou armazenada.

## Origens e integridade

| Componente | Origem | Validação |
|---|---|---|
| yt-dlp Windows x64 | [GitHub oficial](https://github.com/yt-dlp/yt-dlp/releases) | `SHA2-256SUMS`, cabeçalho Windows x64 e `--version` correspondente à release estável |
| FFmpeg e FFprobe x64 | [Gyan, distribuição Windows indicada pelo FFmpeg](https://www.gyan.dev/ffmpeg/builds/), variante release essentials ZIP | `.sha256`, ambos os executáveis x64 e mesma versão anunciada |
| Node.js x64 | [Distribuição oficial](https://nodejs.org/dist/latest-v22.x/) | `SHASUMS256.txt`, executável x64 e versão correspondente |

O [yt-dlp exige ambiente JavaScript para YouTube](https://github.com/yt-dlp/yt-dlp#dependencies). Esta prévia usa a linha Node.js 22 LTS, já suportada pela integração existente, e descobre novas versões dessa linha sem recompilar. Node.js manual 22 ou superior é aceito. Uma futura troca de linha LTS ou uma mudança incompatível do fornecedor exigirá manutenção explícita; não há promessa de compatibilidade ilimitada.

As conexões usam HTTPS, origens permitidas e validação de cada redirecionamento. Metadados e pacotes têm limites de tamanho; a transferência tem limite de dez minutos e pode ser cancelada ao fechar a janela de componentes. O ZIP só fornece os dois executáveis esperados, sem extrair caminhos arbitrários ou outros arquivos.

Checksums obtidos por HTTPS conferem integridade em relação ao fornecedor. Não são verificação de assinatura GPG nem assinatura digital do CataMedia. Executável e futuro instalador do aplicativo continuarão sem assinatura, conforme a decisão do projeto.

Licenças e condições devem ser consultadas nas origens: [yt-dlp e componentes incorporados](https://github.com/yt-dlp/yt-dlp#licensing), [FFmpeg/Gyan](https://www.gyan.dev/ffmpeg/builds/) e [Node.js e terceiros](https://github.com/nodejs/node/blob/main/LICENSE). Os executáveis Windows yt-dlp e Gyan essentials usam GPLv3+; Node.js usa MIT com licenças adicionais dos componentes incorporados. Os binários não entram no Git nem no pacote da aplicação. A obtenção mantém origem, versão, indicação de licença e checksum em `source.json` junto à versão instalada.

## Ativação e recuperação

Os dados ficam em `data/dependencies` no portátil ou `%LOCALAPPDATA%/CataMedia/data/dependencies` no instalado, usando os caminhos da Etapa 1. Cada componente tem pastas `staging` e `versions`, cache de release e `active.json`.

Primeiro o pacote é preparado em uma pasta exclusiva. Depois são conferidos SHA-256, arquitetura e execução da consulta de versão; FFmpeg e FFprobe só avançam juntos. A pasta pronta é movida para `versions`; uma substituição atômica de `active.json` publica a versão ativa e guarda a referência anterior. Se a preparação ou publicação falhar, o registro anterior e os executáveis em uso permanecem intactos. Não são alteradas preferências nem mídia.

A restauração verifica novamente hashes individuais, arquitetura e versão antes de trocar as referências. Versões antigas não são apagadas automaticamente. Cancelamento e falhas comuns removem a preparação temporária. Uma interrupção abrupta do aplicativo pode deixar uma pasta identificável em `staging`, ou uma versão sem referência se ocorrer entre preparação e publicação; esses arquivos não são ativados na próxima abertura. Não há limpeza automática dessas sobras nesta prévia.

Nenhum log é criado na pasta de downloads. Progresso e erros continuam em memória; metadados de componentes ficam exclusivamente na pasta de dados da aplicação.

## Validação e limites

Em 5 de outubro de 2026:

- 83 testes offline aprovados, mantendo as verificações de vídeo, áudio, fila, cancelamento, preferências e idiomas. Quatro grupos de testes reais ficam desativados por padrão no CI.
- Novos casos cobrem checksum, arquitetura, versão incompatível, par FFmpeg/FFprobe incompleto, falha de rede durante o corpo da resposta, cancelamento, falha de publicação por arquivo bloqueado, cache/ETag, fontes/redirecionamentos rejeitados, restauração e componente anterior alterado.
- Falta de espaço foi simulada por erro de E/S, sem preencher o disco real. Não foi reproduzida queda de energia; o comportamento de interrupção abrupta acima decorre da ordem de publicação e do uso exclusivo do registro ativo.
- Instalação real, validação e ativação: yt-dlp 2026.08.19, FFmpeg/FFprobe 9.0.2 e Node.js 22.23.3, em uma pasta isolada ignorada pelo Git. Restauração com executáveis reais foi exercitada usando duas instalações validadas da mesma release; troca entre versões diferentes foi validada nos testes simulados.
- Cinco testes reais de mídia aprovados com o conjunto recém-obtido: MP4, MP3, WAV, FLAC e fila com os dois links autorizados. O segundo continuou privado e foi tratado como falha, sem autenticação ou tentativa de contornar acesso.
- Tela de componentes e tela principal conferidas visualmente em PT-BR/EN. Teste de interface confirma que atualizações ficam bloqueadas durante a fila e que consulta offline não desabilita downloads.

Para repetir a obtenção real, definir `CATAMEDIA_REAL_DEPENDENCIES=1` e `CATAMEDIA_DEPENDENCY_TEST_OUTPUT` com uma pasta isolada, e filtrar `FullyQualifiedName~RealDependencyTests`. Para downloads reais, usar as variáveis da Etapa 2 e os testes da Etapa 3. Evidências, mídia e imagens ficam em `artifacts/`, ignorado; links fornecidos permanecem apenas no documento local ignorado.

Máquina limpa, instalação/desinstalação, validação manual ampla de acessibilidade e testes com usuários continuam previstos para as próximas etapas. Não foram adicionadas bibliotecas de execução ou pacotes NuGet.

## Controle de versão

Branch `feature/windows-dotnet-stage4`, baseada na Etapa 3, com PR de rascunho e CI Windows. Integrar as etapas em ordem. Esta etapa não move tags, não publica release e não inicia a Etapa 5 automaticamente.
