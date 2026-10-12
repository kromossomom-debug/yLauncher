# Histórico de versões

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
