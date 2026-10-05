# CataMedia Windows — Etapa 3

Esta prévia acrescenta áudio, fila sequencial, repetição de falhas, confirmação de playlists e interface PT-BR/EN à Etapa 2. O PowerShell e a release estável continuam preservados. Instalador e gestão/atualização de dependências não são implementados nesta etapa.

## Uso

Compilar com as instruções de `WINDOWS_STAGE1.md` e abrir `src/CataMedia.Desktop/bin/Release/net10.0-windows/CataMedia.exe`.

1. Escolher o idioma e colar um ou mais links, um por linha.
2. Escolher o destino e vídeo MP4 (até 720p/1080p) ou áudio (MP3/WAV/FLAC).
3. Para MP3, escolher baixa, padrão ou alta; os níveis internos mantêm 7/5/0. WAV/FLAC não mostram um seletor de qualidade sem efeito. Converter para um formato sem perdas não recupera qualidade perdida na origem.
4. Em “Mais opções”, localizar os componentes existentes quando necessário. FFmpeg/FFprobe e yt-dlp continuam externos. Node.js compatível é usado para suporte ao YouTube. Cookies do navegador são opcionais, desativados por padrão e só usados quando selecionados explicitamente para acesso autorizado; somente a escolha do navegador pode ser salva, não os cookies.
5. Iniciar. A fila mostra título, formato e estado. Vídeo e áudio são verificados com FFprobe antes de anunciar conclusão.

A consulta de metadados precede a transferência. Uma lista exige uma escolha explícita entre baixar a lista, somente o vídeo indicado no link (quando disponível) ou cancelar. Até 500 itens são aceitos; dados grandes, incompletos ou URLs inválidas são rejeitados sem iniciar uma lista silenciosamente. Cada transferência individual também está limitada ao primeiro item, como proteção adicional contra URLs que se transformem em listas.

Falhas de consulta aparecem como itens com falha, sem bloquear outros links válidos. “Repetir falhas” conserva formato e destino originais e não repete os concluídos. Itens cuja consulta não funcionou são consultados novamente: se passarem a revelar uma playlist, a confirmação continua obrigatória. Um cancelamento na confirmação cancela a preparação antes de iniciar novas transferências.

Cancelar durante a fila interrompe o item atual e não inicia os seguintes. Arquivos parciais podem permanecer para nova tentativa. Esta etapa não oferece pausa ou fila persistente: para repetir itens cancelados/pendentes, envie novamente os links. Abrir a pasta aplica-se ao resultado selecionado na fila.

Arquivos existentes não são sobrescritos. Os três perfis MP3 usam nomes diferentes para evitar reutilizar acidentalmente um MP3 produzido com outra qualidade. As regras de vídeo da Etapa 2 continuam válidas, incluindo resolução máxima e informação da resolução efetiva.

## Interface e dados

O idioma muda textos, opções, estados, cabeçalhos e confirmação de listas. Diagnósticos técnicos do motor podem estar em outro idioma e ficam em uma área de detalhes, enquanto a tela principal apresenta resumo e contagens traduzidos. A fila e os botões permanecem visíveis; componentes avançados ficam em uma área expansível e rolável. Títulos e estados longos usam quebra de linha.

Escolhas só são persistidas em “Salvar preferências”; arquivos das etapas anteriores sem escolha de navegador permanecem compatíveis. Preferências PowerShell não são lidas ou migradas. Nenhum log por download é criado, e os diagnósticos ficam limitados em memória durante a sessão.

Não há novas bibliotecas de execução. Os contratos da Etapa 2 foram ampliados e renomeados de vídeo para mídia para atender às duas modalidades; executor de processos, persistência e controles existentes foram reaproveitados.

## Validação

Testes sem rede cobrem MP3/WAV/FLAC, qualidade MP3, codecs e conteúdo, fila sem paralelismo, continuidade após falha, cancelamento, repetição somente de falhas, consentimento de playlist, limites de metadados, idioma e escolhas da tela. A verificação de regressão inclui os testes das etapas anteriores e compatibilidade de preferências.

Testes reais são opt-in e separados do CI. Usar as variáveis descritas em `WINDOWS_STAGE2.md`, acrescentando `CATAMEDIA_SECOND_TEST_URL` para o segundo link autorizado, e executar em um destino isolado e vazio:

```powershell
dotnet test tests/CataMedia.Tests/CataMedia.Tests.csproj -c Release --no-build --filter FullyQualifiedName~Real
```

Na validação de 5 de outubro de 2026, foram confirmados MP4 720p com áudio, MP3, WAV e FLAC com o primeiro link fornecido. O segundo link retornou “Private video”; serviu como caso real de falha na fila. A fila concluiu o item público e registrou a falha do privado. O teste também aceita que o segundo vídeo se torne público futuramente. Não foi usada autenticação para acessar esse vídeo.

Mídia, JSONs de evidência e imagens de conferência visual ficam em `artifacts/`, ignorado pelo Git. Links do usuário continuam no documento local `TEST_LINKS_LOCAL.md`, também ignorado. Não houve teste de download de uma playlist real; confirmação, expansão e repetição de playlists foram validadas com metadados e serviços simulados.

A interface foi conferida visualmente em PT-BR e EN. Testes automatizados exercitam seus controles e alvos de teclado, mas ainda é necessária uma rodada com usuários leigos e validação manual abrangente com leitor de tela e escalas de exibição antes da distribuição final.

## Decisão incorporada para a Etapa 4

Adotar a sugestão do usuário: verificar no GitHub oficial, em segundo plano ao abrir, se há release estável mais recente do yt-dlp. A consulta deve ter timeout curto, usar cache/metadados condicionais e evitar consultas repetidas se o aplicativo for reaberto em intervalo curto. Uma falha, limite de API ou ausência de rede não impede usar a versão existente.

Quando houver uma versão mais recente, mostrar um aviso não modal com versão instalada e disponível, “Atualizar” e “Mais tarde”. Explicação proposta: “Os sites mudam e o yt-dlp recebe adaptações. Uma versão antiga pode causar falhas; atualizar é recomendado, mas não garante acesso a todo conteúdo.” Disponibilizar também “Verificar agora”. Não rebaixar versões já mais recentes nem baixar/substituir componentes sem escolha do usuário.

Se houver fila ativa, a atualização fica indisponível até o término. Após consentimento, validar o pacote, ativar a nova versão com recuperação da anterior e conferir a versão executável. Atualizar o motor não deve alterar preferências, mídia nem código CataMedia. Aplicar o mesmo princípio de manutenção independente ao conjunto FFmpeg/FFprobe; as fontes e a validação específicas serão tratadas na Etapa 4.

Os binários CataMedia e o futuro instalador continuarão sem assinatura digital conforme a decisão do projeto; verificação de procedência/integridade de dependências externas é um assunto separado. Nenhum atualizador foi implementado nesta etapa.

## Controle de versão

Branch e PR próprias, baseadas na Etapa 2 enquanto as PRs anteriores estiverem abertas. Integrar em ordem, com testes Windows e sem mover tags ou publicar uma release final. A próxima ação de desenvolvimento será a Etapa 4 mediante solicitação.
