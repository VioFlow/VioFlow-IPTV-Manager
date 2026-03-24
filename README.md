# 🦅 VioFlow IPTV Manager (v1.0.0)

A ferramenta **definitiva, portátil e de altíssima performance** para limpar, organizar, editar e testar suas listas IPTV (.m3u / .m3u8) diretamente no Windows. 

Construído do zero em C# (.NET 10), o VioFlow foi desenhado para lidar com listas gigantescas (500k+ linhas) sem travar o seu PC, utilizando um consumo mínimo de Memória RAM e Processador.

⚠️ **AVISO LEGAL:** O VioFlow é exclusivamente um editor de texto e gestor de links local. **Não fornecemos, vendemos, hospedamos ou contemos nenhuma lista, link ou conteúdo de mídia.**

## 📥 Como Baixar e Usar
1. Vá até a aba [Releases](https://github.com/VioFlow/VioFlow-IPTV-Manager/releases/latest) e baixe o arquivo **`VioFlow_v1.0.0_Portable.zip`**.
2. Extraia a pasta em qualquer lugar do seu computador (ou pendrive).
3. Clique duas vezes no executável `VioFlow IPTV Manager.exe` e comece a editar! (Não requer instalação).

---

## 🔥 Tudo o que o VioFlow faz por você:

### 🚀 Performance e Motor Principal
* **Lazy Loading de Imagens:** Baixa apenas as logos visíveis na tela. Rolagem suave mesmo com centenas de milhares de canais.
* **Cache Dinâmico Inteligente:** Limite seguro de imagens na memória (evita que o programa feche sozinho por falta de RAM).
* **Sistema Anti-Bloqueio (User-Agent):** Disfarce de navegador nativo para contornar bloqueios de segurança ao baixar imagens e testar links.
* **Máquina do Tempo:** Sistema completo de **Desfazer (Undo)** e **Refazer (Redo)** para qualquer alteração na lista.
* **Atualizador Automático:** O programa avisa na barra de status sempre que uma nova versão for lançada aqui no GitHub.

### 📡 Testes e Monitoramento
* **Radar Pro (Testador em Massa):** Testa milhares de canais automaticamente para ver se estão ON (Verde) ou OFF (Vermelho). Possui detecção inteligente contra páginas HTML falsas de operadoras.
* **Monitor Técnico:** Player integrado (LibVLC) para assistir ao canal dentro do app. Mostra resolução, FPS, Codec de Vídeo (H.264/HEVC), Codec de Áudio e número de canais sonoros.
* **Ver Info da Conta:** Extrai dados de conexões Xtream Codes e mostra Validade, Status da Conta, Máximo de Telas e Telas em Uso.

### 🛠️ Edição e Organização em Massa
* **Caçador de Duplicatas:** Detecta links ou nomes repetidos. Permite apagar as cópias, adicionar a tag `[Alt]` automaticamente ou renomear manualmente.
* **Organizador de Categorias:** Mude a ordem de categorias inteiras com cliques simples.
* **Limpeza de Nomes:** Remova textos indesejados (ex: `[FHD]`, `[HD]`) de milhares de canais de uma só vez.
* **Formatação de Texto:** Formata o nome dos canais selecionados com a Primeira Letra Maiúscula (Title Case).
* **Múltiplas Ações Rápidas:** Apagar Logos, Apagar IDs do EPG, Mudar Categoria em massa, Subir/Descer canais individuais e Copiar/Colar canais.

### 🧬 Central de Transplantes (Mesclar Listas)
Abra uma segunda lista "Doadora" ao mesmo tempo e faça mágicas:
* **💉 Injetar URL:** Substitua um link quebrado do seu canal pelo link funcionando da lista doadora.
* **🧬 Clonar Novo:** Encontre canais inéditos que você não tem e puxe-os para a sua lista.
* **🎨 Roubar Logo:** Interface visual de "Antes e Depois" para transferir logos de canais doadores para os seus canais sem logo (com motor anti-fantasma).

### 🌐 Extração Web e Xtream Codes
* **Baixar da Web:** Cole a URL da sua lista e o VioFlow baixa tudo.
* **Filtro Xtream Codes:** Se o link tiver Usuário e Senha, escolha entre baixar a Lista Completa (VOD + Séries) ou extrair **Apenas TV ao Vivo** em segundos, ignorando filmes pesados.
* **Testador M3U Externo:** Cole vários links soltos. O VioFlow testa quais contas estão ativas e deixa você extraí-las com um clique.

### 📋 Exportação e EPG
* **Exportação Expressa:** Salve apenas categorias específicas (Ex: Salvar só "Esportes" e "Notícias").
* **Gerador de Catálogo:** Cria um arquivo `.txt` elegante e organizado com todos os seus canais divididos por categoria para você enviar aos clientes.
* **Configuração de EPG:** Adicione até 2 URLs de EPG. O VioFlow baixa a programação para a memória e permite Mapear IDs Manualmente com busca rápida.
* **Preservação de Cabeçalho:** Salva a sua lista mantendo a ordem da tela ou a original, preservando a tag `#EXTM3U x-tvg-url=`.

### 🌍 Internacionalização
* Tradução instantânea de toda a interface para **Português 🇧🇷, Inglês 🇺🇸 e Espanhol 🇪🇸**.

---

## 💙 Apoie o Projeto
Gostou do VioFlow e ele economizou horas do seu dia? Você pode apoiar o desenvolvimento contínuo (e pagar um café para o desenvolvedor ☕):
* **PIX:** 07879eef-5082-42ac-b528-0f7b64f850bf
* **PayPal:** [Clique aqui para apoiar via PayPal](https://www.paypal.com/donate/?business=SEG7HUXPAQ5AW&no_recurring=0&currency_code=USD)

Desenvolvido com dedicação por **(https://github.com/VioFlow)**.
