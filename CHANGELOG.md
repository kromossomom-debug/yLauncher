# Histórico de versões

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

- Prote├º├úo da ponte HTTP local com token tempor├írio e valida├º├úo de origem.
- Remo├º├úo da execu├º├úo arbitr├íria pelo protocolo personalizado.
- Proxy do SteamGridDB limitado ├á API oficial; chave padr├úo removida.
- Remo├º├úo de jogos, capas e telemetria fict├¡cia incorporados ao pacote.
- Normaliza├º├úo de dados importados, metadados, imagens e campos personalizados.
- Limites de 5 MB para capas, banners e backups importados.
- Abertura da interface antes da varredura de jogos.
- Menor frequ├¬ncia de polling do controle e do stream de eventos.
- Novo instalador com atualiza├º├úo que preserva `installed_games.json`.
- Novo desinstalador; dados pessoais s├│ s├úo apagados mediante confirma├º├úo ou uso de `/purge`.
- C├│digo-fonte recuperado e build reproduz├¡vel com valida├º├Áes autom├íticas.
- Nova distribui├º├úo port├ítil em ZIP.
- Espa├ºamento corrigido entre o menu superior e a biblioteca.
- Abas superiores com maior contraste, ├írea de clique ampliada e destaque ativo mais vis├¡vel.
- Layout compacto aprimorado para telas de 1280 px.
- ├ürea superior do grid ampliada para evitar o recorte de cards selecionados.
- Primeira linha da Grade Completa protegida contra recorte durante foco e rolagem por controle.
- Bot├úo Buy Me a Coffee mantido vis├¡vel no cabe├ºalho compacto.

## 2.0.0 ÔÇö Edi├º├úo Console

- Interface em tela cheia inspirada na experi├¬ncia de consoles.
- Fundo din├ómico para o jogo selecionado.
- Carrossel otimizado para navega├º├úo por controle.
- Cabe├ºalho adaptado a diferentes tamanhos e escalas de tela.
- Instalador independente para Windows.

## 0.1.0 ÔÇö Edi├º├úo Cl├íssica

- Primeira vers├úo p├║blica do yLauncher.
- Biblioteca em grade com barra lateral.
- Detec├º├úo de jogos e inclus├úo manual de execut├íveis.
- Navega├º├úo por mouse, teclado e controle XInput.
- Favoritos, busca, filtros e personaliza├º├úo de capas.
