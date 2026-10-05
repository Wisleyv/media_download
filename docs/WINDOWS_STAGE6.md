# CataMedia Windows — Etapa 6: piloto em andamento

## Primeiro recorte — componentes na máquina limpa

Em 5 de outubro de 2026, o usuário relatou que a versão portátil abriu em uma máquina
limpa, mas não encontrou yt-dlp nem FFmpeg/FFprobe. Após copiar os executáveis para a
pasta extraída, os downloads funcionaram. Na máquina de desenvolvimento, os componentes
foram detectados; a presença no PATH foi indicada como provável pelo usuário.

O pacote exclui essas dependências por decisão do projeto. A descoberta consulta a área
de dependências, a pasta do executável e PATH, além das versões gerenciadas ativas.
Não há busca global no disco. Componentes permite obtenção assistida; Mais opções e
componentes permite selecionar arquivos existentes, sem copiá-los.

Corrigido um problema de orientação: uma consulta bem-sucedida de release yt-dlp podia
substituir o aviso de componentes ausentes pelo aviso de atualização. O aviso de preparação
agora tem prioridade, inclusive quando falta FFprobe ou Node é incompatível. PT-BR/EN e
manual explicam o conteúdo do pacote e as alternativas sem terminal.

O relato valida abertura e download portátil nessa máquina limpa com dependências
fornecidas manualmente. Não valida obtenção assistida nessa máquina, instalador em
máquina limpa, conta padrão separada ou atualização entre versões diferentes.
Esses cenários permanecem pendentes. Os pacotes da etapa 5 não foram substituídos.

## Revisão visual solicitada — executada no quarto recorte abaixo

- Revisar distribuição das seções, espaçamento, hierarquia e dimensões dos controles.
- Reduzir a largura desnecessária dos menus suspensos e afastar campos da barra de rolagem.
- Incluir barra superior de menus com Arquivo, Exibir e Ajuda/Sobre.
- Rever o esquema cinza claro/branco, considerado cansativo e pouco amigável pelo usuário.
- Comparar a proposta com boas práticas e tendências atuais para aplicativos Windows,
  preservando acessibilidade, teclado, ampliação e traduções PT-BR/EN.
- Criar ícone próprio estilizado usando como referência o cata-vento fornecido na conversa:
  pás rosa, laranja, amarelo e azul/turquesa sobre haste clara. A imagem da interface
  fornecida nesta conversa é a referência dos problemas relatados.

As imagens permanecem anexadas à conversa; não foram exportadas como arquivos do repositório.

## Limites

Este recorte inicia o piloto da etapa 6; não conclui a etapa. Comparação de releases
CataMedia C# 3.x, referência publicada estável do manual e demais validações continuam
pendentes. Não publicar, mesclar, mover tags nem sanitizar neste recorte.

## Validação deste recorte

Build Release sem avisos ou erros; 88 testes aprovados e quatro testes reais opcionais
ignorados. O novo teste de interface simula consulta de release bem-sucedida com
componentes ausentes e com FFmpeg sem FFprobe, conferindo orientação em PT-BR/EN,
disponibilidade do botão Baixar e ausência de salvamento implícito de preferências.
Não foi repetido o piloto em máquina limpa com esta correção.

Branch `feature/windows-dotnet-stage6`, baseada no encerramento da etapa 5,
commit `a753b64505557ea849d629632e4629b4c6d79376`. Nenhum pacote novo gerado neste recorte.

## Segundo recorte — continuidade da preparação

Imagens locais `tests/1.png`, `tests/2.png` e `tests/3.png` fornecidas pelo usuário
mostram o aviso inicial, a janela de componentes com Node selecionado e o retorno à
tela principal sem aviso. O usuário relata necessidade de reiniciar entre instalações.
No código, não foi encontrado fechamento automático na seleção ou após a instalação;
esse detalhe permanece sem reprodução. Confirmado o ocultamento incondicional do aviso
ao fechar a janela de componentes, mesmo com itens ausentes.

Correções: consultar automaticamente ao abrir a janela e trocar o componente; explicar
como instalar o próximo item sem sair; conferir novamente os componentes ao retornar à
tela principal e listar os ausentes ou incompatíveis. Download continua exigindo confirmação.
Textos PT-BR/EN e guia acompanham o fluxo.

PATH: alterar variáveis do usuário não exige a permissão necessária ao escopo de máquina
([Microsoft](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_environment_variables)).
Não alterar PATH neste recorte: CataMedia já usa caminhos explícitos e versões gerenciadas.
Expor pastas de versões exigiria atualizar entradas após ativação/retorno e tratar remoção
ou mudança de localização do portátil, com possível interferência em outras instalações.
O benefício seria uso das ferramentas por outros aplicativos, fora da preparação do CataMedia.

Validação: 89 testes aprovados, quatro testes reais opcionais ignorados; build Release sem
avisos ou erros. Testes verificam consulta ao selecionar os três itens, repetição offline,
janela mantida aberta e reabertura do diálogo com orientação preservada na tela principal.
A instalação real sequencial na máquina limpa permanece para repetição pelo usuário.

## Terceiro recorte — progresso de download dos componentes

O usuário confirmou que a preparação sequencial funciona sem reiniciar e que as mensagens
melhoraram. Relatou ausência de progresso durante cerca de cinco minutos ao obter FFmpeg/FFprobe.
O código notificava somente início do download e início da validação; isso não permite
concluir se a espera relatada foi transferência lenta ou interrupção de rede.

Adicionados barra e volume recebido na janela Componentes para todos os componentes.
Com tamanho informado pelo servidor, mostrar MB recebidos/total e percentual. Sem tamanho,
mostrar barra de atividade e MB recebidos, sem inventar percentual. Atualizar o tempo desde
os últimos dados a cada segundo, inclusive durante espera inicial. Informar que fechar
cancela. A validação tem indicação própria, sem tratar 100% recebido como instalação pronta.
Atualizações de bytes limitadas a cinco por segundo, além do início/fim, para não inundar
a interface. Mantidos timeout, limites de tamanho, checksum, cancelamento e ativação segura.

Testes adicionais simulam transferência lenta com e sem tamanho, verificam amostras
intermediárias monotônicas, volume final e transição para validação antes da ativação.

Validação local: 91 testes aprovados, quatro opcionais ignorados; build sem avisos ou erros.
Pasta autossuficiente para teste: `artifacts/stage6-component-progress/`. Este recorte
não gera instalador nem release. A verificação visual na máquina limpa permanece pendente.

O usuário confirmou posteriormente o funcionamento do progresso, volume total/percentual
e conclusão validada do download FFmpeg/FFprobe na máquina limpa.

## Quarto recorte — revisão visual e identidade

Autorizado pelo usuário após a aceitação do fluxo de componentes. Referência atual do
ícone: `tests/base_para_icone.jpg`, fornecida pelo usuário e preservada como arquivo local.

- Paleta azul-petróleo com superfícies claras suavizadas, tipografia Segoe UI,
  hierarquia de títulos, espaçamentos e foco por teclado visível.
- Áreas Preparar download e Fila delimitadas; formato/resolução e formato/qualidade
  de áudio dispostos em pares. Campos afastados da rolagem; largura dos seletores
  proporcional. Campo de resultado oculto até haver um caminho e orientação na fila vazia.
- Menus Arquivo, Exibir e Ajuda, traduzidos. Reutilizam seleção de destino,
  salvamento explícito, componentes, ajuda e fechamento/cancelamento existentes.
  Exibir controla os painéis avançado e de detalhes. Ações incompatíveis continuam
  bloqueadas durante a fila; idioma também permanece bloqueado nesse estado.
- Estilos compartilhados nas janelas principal, Componentes, Ajuda e playlist.
  Controles continuam WPF; templates leves de botão/seleção, sem biblioteca adicional.
- Cata-vento e seta de download gerados com imagegen; PNG transparente e ICO com nove
  tamanhos. Integrados ao executável, janela principal, Ajuda/Sobre, atalhos e configuração
  do Setup. Origem e prompt em `src/CataMedia.Desktop/Assets/README.md`.
- Ao iniciar, dimensões limitadas à área útil informada pelo Windows. Mudanças de alto
  contraste substituem a paleta por cores do sistema; não foi alterada a configuração
  de acessibilidade do computador para testar.

Critérios: [diretrizes Windows](https://learn.microsoft.com/en-us/windows/apps/design/guidelines-overview),
[texto acessível](https://learn.microsoft.com/en-us/windows/apps/design/accessibility/accessible-text-requirements)
e [design inclusivo](https://learn.microsoft.com/en-us/windows/apps/design/accessibility/designing-inclusive-software).
Contraste calculado para a paleta padrão: texto principal/campo 11,25:1; secundário/superfície
5,74:1; texto do botão primário 6,00:1; contorno de campo 3,02:1. Isso não substitui
validação completa com leitor de tela e temas reais do Windows.

Validação: 92 testes aprovados, quatro reais opcionais ignorados; build sem avisos ou erros.
Novos casos cobrem menu/idioma, salvamento explícito pelo menu, painéis, bloqueio durante
fila e abertura/fechamento do seletor pela interface de automação de acessibilidade.
Telas renderizadas e inspecionadas em PT-BR/EN, vídeo/áudio, janela mínima 1000×700,
opções avançadas, aviso inicial, Ajuda, Componentes e playlist. Ampliação de layout 125%
e paleta de alto contraste foram simuladas; não representam testes de DPI/tema no Windows.
Evidências locais em `artifacts/stage6-visual/`.

Inno Setup aceitou o novo ICO em compilação isolada com payload mínimo. Esse instalador
de teste NÃO contém o runtime completo, não foi executado e não é uma distribuição.
Pasta autossuficiente para avaliação: `artifacts/stage6-visual-preview/`.
Não foram gerados novos pacotes oficiais, tags, releases, merges ou limpeza.
Aceitação visual na máquina limpa, teclado completo/leitor de tela, DPI real e os demais
cenários de distribuição continuam pendentes. Este recorte não conclui a etapa 6.

## Correção após aceitação visual — crash ao verificar FFmpeg

O usuário aceitou os aprimoramentos visuais, mas relatou encerramento ao verificar
FFmpeg/FFprobe. O evento .NET Runtime 1026 do Windows confirma `InvalidDataException:
Unrecognized component version` em ReadVersionAsync → CheckSelectedAsync → RunAsync.
O FFmpeg no PATH desta máquina anuncia `N-116720-g5c1c0325cd-20240818`, uma versão de
desenvolvimento que o comparador numérico não reconhece.

O filtro de erros recuperáveis incluía IOException, mas InvalidDataException não deriva
dela e escapava do evento assíncrono da janela. Correção restrita: incluir explicitamente
InvalidDataException no filtro compartilhado da interface e explicar em PT-BR/EN quando
há caminho local, mas sua versão não pode ser identificada/comparada. Não ampliar o parser
nem enfraquecer validação de versões/checksums dos pacotes. Componentes existentes preservados.

94 testes aprovados, quatro opcionais ignorados; build sem avisos ou erros. Novos testes
cobrem versão FFmpeg de desenvolvimento, metadados de release inválidos, ausência de
exceções não tratadas, disponibilidade correta do botão e preservação do arquivo local.
Consulta na janela com o FFmpeg real instalado e metadados HTTPS do fornecedor completou
com versão disponível 9.0.2, sem crash ou ativação de componente. Não foram baixados pacotes.
Harness isolado em `artifacts/stage6-ffmpeg-check/`.

Compilação autossuficiente para repetir o teste: `artifacts/stage6-ffmpeg-check-fix/`.
Confirmação no ambiente do usuário permanece pendente. Nenhuma release ou merge realizado.
