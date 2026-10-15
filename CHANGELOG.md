# Histórico de versões

## 2.4.0 — Modos de exibição da biblioteca

- **7 modos de exibição** para as abas da biblioteca, com alternador na prateleira e cartões de prévia em Configurações → Aparência:
  - **Fileira** — capas em uma linha com o destaque do jogo em tela cheia (padrão).
  - **Grade** — todas as capas em grade.
  - **Compacta** — grade densa para bibliotecas grandes.
  - **Mosaico** — banners largos com a arte de fundo, nome, loja, tempo jogado e botão Jogar.
  - **Lista** — tabela com tempo jogado, última sessão, tamanho, progresso e nota; clique no título da coluna para ordenar.
  - **Lado a lado** — lista de jogos à esquerda e painel de detalhes à direita (arte, logo oficial, descrição da Steam, estatísticas e ações).
  - **Carrossel 3D** — coverflow com reflexo, capa central em destaque e navegação pela roda do mouse.
- **Opções de exibição**: tamanho das capas (pequeno, médio, grande, enorme), nomes sobre as capas, mostrar/ocultar selos e **agrupar** por loja, progresso, instalação ou letra inicial.
- Navegação espacial pelo controle: em grades e grupos, cima/baixo vai para o cartão visualmente mais próximo; na lista, esquerda/direita pula 10 jogos.
- Atalhos: **L3** ou **G** troca o modo; **R3** ou **Z** troca o tamanho das capas. Novas ordenações: Z–A, avaliação e tamanho.
- O modo e as opções são lembrados entre sessões; a aba Início continua com as prateleiras e respeita o tamanho das capas.
- Corrigido: a roda do mouse não rolava as prateleiras da tela Início.

## 2.3.0 — Tela Início, tempo de jogo real, página do jogo e menu rápido

- **Tela Início** com prateleiras no estilo console: Continuar jogando, Favoritos, Adicionados recentemente, Instalados e uma prateleira por loja. Navegação 2D completa pelo controle (cima/baixo troca de prateleira, mantendo a coluna).
- **Tempo de jogo real**: o `yLauncher.exe` acompanha o processo do jogo (inclusive jogos abertos por Steam, Epic, GOG, Battle.net, EA e Ubisoft) e grava o tempo em `%LOCALAPPDATA%\yLauncher\playtime.json`. Jogos da Steam importam o tempo já registrado pela própria Steam.
  - Indicador "Jogando agora" na barra superior e selo "Jogando" no cartão.
  - O launcher minimiza quando o jogo abre e volta sozinho quando ele fecha; o controle fica bloqueado enquanto o jogo roda.
  - Corrigido: o contador antigo continuava somando minutos mesmo depois de fechar o jogo.
- **Página do jogo**: descrição, gêneros, desenvolvedora, publicadora, lançamento, nota Metacritic, suporte a controle e capturas de tela, buscados na loja Steam (com cache). Jogos de outras lojas são identificados pelo nome.
  - Jogos da Epic, GOG, Battle.net, EA e Ubisoft sem capa recebem automaticamente a arte oficial da Steam quando o jogo existe lá.
  - Clique direito no cartão abre a página do jogo.
- **Menu rápido** (botão Xbox/Guide, F1 ou ícone na barra): relógio, bateria, jogo em execução com "Fechar jogo", volume, brilho (notebooks e portáteis), atalhos e energia — área de trabalho, bloquear, suspender, reiniciar, desligar e sair (ações perigosas pedem confirmação).
- **Personalização**: 8 temas (Midnight, OLED, Aurora, Nebula, Ember, Forest, Arctic, Sakura), 17 cores de destaque + cor livre, arte de fundo nítida/desfocada/discreta/desligada e tamanho da interface de 90% a 140%.
- **Sons**: 6 pacotes de sons de navegação (Clássico, Suave, Console, Retrô 8-bit, Cristal, Mecânico), controle de volume e novos sons para abrir menus, trocar abas e iniciar jogos.
- **Boas-vindas**: assistente na primeira execução (idioma, visual, biblioteca encontrada, chave SteamGridDB e mapa de controles). Pode ser reaberto em Configurações → Geral.
- Teclado: `F` favorita, `I` abre detalhes, `F1`/`Home` abrem o menu rápido.

## 2.2.0 — Novo visual, atualização automática e mais lojas

- **Atualização automática pelo GitHub**:
  - O `yLauncher.exe` verifica as releases de `kromossomom-debug/yLauncher` ao abrir e a cada 6 horas.
  - Baixa o instalador em segundo plano, confere tamanho e SHA-256 (campo `digest` da API ou `checksums.sha256`) e só então marca a atualização como pronta.
  - Instala sozinho, em modo silencioso, quando o launcher é fechado — ou na hora pelo botão "Reiniciar agora". Nunca durante um jogo aberto.
  - Biblioteca, capas, notas, atalhos e "Iniciar com o Windows" são preservados; o launcher reabre na nova versão.
  - Nova aba **Atualizações** nas configurações (status, progresso, novidades da versão e opção para desligar).
  - Cópias portáteis apenas avisam e abrem a página da release.
  - Workflow do GitHub Actions publica a release automaticamente a cada tag `vX.Y.Z`.
- **Mais lojas detectadas**: GOG Galaxy, Battle.net, EA app e Ubisoft Connect, além de Steam e Epic.
  - Epic agora abre pelo launcher oficial (`com.epicgames.launcher://`), ignora DLCs e instalações incompletas e aparece como "Epic" (antes era marcada como EA).
  - Jogos desinstalados somem da biblioteca no próximo scan; jogos novos aparecem sozinhos.
- **Interface redesenhada** (design system 2.2):
  - Tipografia e espaçamento fluidos para TV, monitor e portáteis; ícones vetoriais no lugar de emojis.
  - Barra superior com marca, abas, busca expansível, status do controle e da bateria.
  - Destaque do jogo com selo da plataforma, animações de entrada e arte em tela cheia.
  - Cartões com foco de controle mais claro, estado "não instalado", favoritos e selo da loja.
  - Filtro rápido por loja na prateleira; modo grade lembrado entre sessões.
  - Configurações em layout de duas colunas; modais, studio de capas, estatísticas, roleta e Pix redesenhados.
  - Capas geradas localmente para jogos sem arte (o serviço via.placeholder.com saiu do ar).
  - Tradução PT/EN/RU estendida para modais e dicas.
- **Instalador e desinstalador**: visual alinhado ao app, modo `/update` silencioso, espera o launcher fechar antes de substituir arquivos e registra log em `%LOCALAPPDATA%\yLauncher\updates\install.log`.
- **Correções**:
  - Biblioteca de demonstração (jogos que o usuário não possui) removida.
  - Jogos adicionados manualmente agora aparecem em "Instalados".
  - Status "100% / Platinado" não era salvo.
  - Telemetria lia campos errados (CPU/RAM/bateria nunca apareciam).
  - Cor de destaque e arredondamento ativos não eram marcados nas configurações.
  - Troca de idioma gerava erro ao atualizar o destaque.
  - Esc limpa a busca antes de fechar o launcher.
  - JSON da ponte nativa com escape completo; `/scan-games` e o proxy do SteamGridDB passam a exigir token.
  - Versão centralizada em `src/shared/AppVersion.cs`.

## 2.1.2 — Suporte Multilíngue (Russo, Inglês, Português) e Bloqueio de Entrada em Segundo Plano

- **Suporte Multilíngue Completo**:
  - Adicionado suporte nativo e completo a Russo (RU), Inglês (EN) e Português (PT).
  - Seletor rápido de idiomas no Hub de Configurações com persistência imediata.
  - Tradução completa da interface web, metadados, estúdio SteamGridDB, roleta gamer e mensagens de telemetria.
  - Instalador e desinstalador com detecção automática do idioma do sistema operacional e interface traduzida.
  - Mensagens nativas e caixas de diálogo do Windows traduzidas conforme a localidade do sistema.
- **Bloqueio de Entrada em Segundo Plano (Background Input Lock)**:
  - O launcher agora ignora automaticamente comandos de controle (gamepad) e teclado quando perde o foco da janela.
  - Detecção nativa via Win32 API de janelas e processos de jogos em primeiro plano.
  - Minimização automática da interface do launcher ao iniciar qualquer jogo, restaurando-a ao encerrar.
  - O endpoint de streaming do controle zera os eixos e botões enquanto jogos estiverem em execução ou o launcher estiver em segundo plano.
  - Novo indicador visual no HUD (`🔒 Pausado`) sinalizando quando as entradas estão bloqueadas.
  - Nova opção no Hub de Configurações para ativar/desativar o Bloqueio em Segundo Plano.

## 2.1.1 — Banners Widescreen do SteamGridDB e Navegação por Controle

- Download e aplicação de backgrounds e banners widescreen do SteamGridDB (Heroes 1920x620 e grids horizontais).
- Seletor rápido de arte entre Capa Vertical (600x900) e Background Widescreen.
- Visualização e troca direta de capas e backgrounds a partir do menu de detalhes do jogo.
- Suporte para restauração de background padrão do jogo.
- Navegação otimizada por joystick:
  - Foco inteligente imediato na galeria de artes ao abrir o estúdio.
  - Navegação bidimensional precisa (2 colunas em banners, 4 colunas em capas).
  - Toque em (A) na arte selecionada confirma a aplicação imediatamente.
  - Botão (X) / START para aplicação instantânea da arte focada.
  - Botão (Y) para zoom em tela cheia (Lightbox HD).
  - Gatilhos (LT/RT) para alternar instantaneamente entre Capa e Background.
  - Bumpers (LB/RB) para troca de páginas da galeria.

## 2.1.0 — Segurança, privacidade e manutenção

- Proteção da ponte HTTP local com token temporário e validação de origem.
- Remoção da execução arbitrária pelo protocolo personalizado.
- Proxy do SteamGridDB limitado à API oficial; chave padrão removida.
- Remoção de jogos, capas e telemetria fictícia incorporados ao pacote.
- Normalização de dados importados, metadados, imagens e campos personalizados.
- Limites de 5 MB para capas, banners e backups importados.
- Abertura da interface antes da varredura de jogos.
- Menor frequência de polling do controle e do stream de eventos.
- Novo instalador com atualização que preserva `installed_games.json`.
- Novo desinstalador; dados pessoais só são apagados mediante confirmação ou uso de `/purge`.
- Código-fonte recuperado e build reproduzível com validações automáticas.
- Nova distribuição portátil em ZIP.
- Espaçamento corrigido entre o menu superior e a biblioteca.
- Abas superiores com maior contraste, área de clique ampliada e destaque ativo mais visível.
- Layout compacto aprimorado para telas de 1280 px.
- Área superior do grid ampliada para evitar o recorte de cards selecionados.
- Primeira linha da Grade Completa protegida contra recorte durante foco e rolagem por controle.
- Botão Buy Me a Coffee mantido visível no cabeçalho compacto.

## 2.0.0 — Edição Console

- Interface em tela cheia inspirada na experiência de consoles.
- Fundo dinâmico para o jogo selecionado.
- Carrossel otimizado para navegação por controle.
- Cabeçalho adaptado a diferentes tamanhos e escalas de tela.
- Instalador independente para Windows.

## 0.1.0 — Edição Clássica

- Primeira versão pública do yLauncher.
- Biblioteca em grade com barra lateral.
- Detecção de jogos e inclusão manual de executáveis.
- Navegação por mouse, teclado e controle XInput.
- Favoritos, busca, filtros e personalização de capas.
