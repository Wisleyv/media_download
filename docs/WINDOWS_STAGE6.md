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

## Próxima interação — revisão visual solicitada, ainda não executada

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
