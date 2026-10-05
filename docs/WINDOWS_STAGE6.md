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
