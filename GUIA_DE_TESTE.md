# Guia de teste — ControleAcessoMR

Passo a passo para testar o app no óculos (Meta Quest 3/3S) com o kit real.
Marque cada item quando funcionar. Se algo falhar, anote o número do passo e me conte.

---

## Antes de começar (checklist)

- [ ] ESP32 com o `.ino` atualizado gravado (Arduino IDE).
- [ ] Kit real ligado: LCD acende e mostra a mensagem inicial.
- [ ] QR code impresso (texto `CONTROLE_ACESSO_MR`), uns 10 cm, deitado na mesa ao lado do kit real.
- [ ] Óculos carregado, com o APK instalado.
- [ ] Os 2 cartões NFC reais à mão (um cadastrado = LIBERADO, um não cadastrado = NEGADO).

---

## Passo 1 — Ligar o óculos no Wi-Fi do ESP32

1. No óculos: **Configurações → Wi-Fi → Controle_Acesso** → digite a senha do ESP32.
2. Quando aparecer "sem internet", escolha **continuar conectado**.
3. No dia da feira, **esqueça a rede da escola** no óculos, para ele não trocar sozinho.

**Esperado:** o óculos fica conectado em `Controle_Acesso`.

## Passo 2 — Abrir o app e posicionar o kit virtual

1. Abra o app **ControleAcessoMR**. Na primeira vez, aceite a permissão de "dados do espaço".
2. Olhe para o QR code na mesa por 2 ou 3 segundos.

**Esperado:** o kit virtual (2x maior) aparece **na frente dos seus olhos**, quase em pé, com a
mensagem **CONTROLE DE ACESSO POR NFC** acima dele. Olhando para baixo você vê o kit real na mesa.

**Se não aparecer:** aperte **A** no controle direito (o kit aparece na sua frente sem o QR).

## Passo 3 — Ver se o ESP32 está conectado

1. Olhe a **bolinha acima do ESP32 virtual**: **verde** = conectado; **vermelha piscando** = sem sinal.
2. Para mais detalhes, aperte **B**: aparece o painel de diagnóstico (letras brancas).
3. Procure a linha **ESP32: conectado**.
4. Aperte **B** de novo para esconder.

**Se mostrar "desconectado":** o óculos não está no Wi-Fi `Controle_Acesso` (volte ao Passo 1).

## Passo 4 — Cartão REAL no leitor REAL (o teste principal)

1. Pegue o cartão real **LIBERADO** com a mão e encoste no leitor do kit real.
2. Olhe para frente, para o kit virtual.

**Esperado em menos de 1 segundo:**
- Ondas saem do leitor virtual e a luz corre pelos fios até o ESP32, que brilha.
- A luz se divide e vai ao mesmo tempo para o LCD e para o servo.
- O LCD virtual escreve letra por letra, a cancela virtual sobe e o buzzer apita.
- Uma legenda embaixo conta cada etapa.
- Aparece o painel verde **ACESSO AUTORIZADO** com o cadeado aberto.

- Os **cartões virtuais somem** por 30 segundos (para não confundir quem está com o cartão real).

3. Repita com o cartão **NEGADO**. **Esperado:** painel vermelho **ACESSO NEGADO**, cadeado fechado e
   a cancela **não** mexe.
4. Passe o LIBERADO de novo: a cancela fecha (o kit real faz o mesmo).

## Passo 5 — Cartão VIRTUAL com a mão (sem controle)

1. Solte os controles. O óculos passa a ver suas mãos.
2. Pegue o cartão virtual **LIBERADO** (feche a mão nele) e encoste no **leitor virtual**.

**Esperado:** a mesma animação do Passo 4, só no kit virtual. O kit real **não** mexe.

3. Solte o cartão longe: ele volta sozinho para o lugar.

## Passo 6 — Botões de simulação (plano B)

1. Toque com o dedo em **SIMULAR AUTORIZADO** ou **SIMULAR NEGADO**, ou aperte **Menu** no controle esquerdo.

**Esperado:** a animação roda sem precisar de cartão nenhum.

## Passo 7 — Esconder os cartões virtuais

1. Clique no **analógico esquerdo** (aperte o joystick para baixo).

**Esperado:** os cartões virtuais somem e aparece o aviso "Cartões virtuais escondidos".
Clique de novo para eles voltarem.

## Passo 8 — Modo vitrine

1. Não mexa em nada por **40 segundos**.

**Esperado:** o app simula um cartão sozinho, alternando liberado e negado, para chamar a atenção.

## Passo 9 — Ajustar e salvar a posição do kit

1. Use os **analógicos** para mover e girar o kit virtual.
2. Aperte **Y** para salvar. Da próxima vez que o QR for visto, o kit volta nesse lugar.
3. Aperte **X** para desfazer o ajuste (volta ao padrão).

## Passo 10 — Teste com outra pessoa

1. Coloque o óculos numa pessoa mais alta ou mais baixa.

**Esperado:** em 1 ou 2 segundos o kit virtual se ajusta à altura dos olhos dela.

---

## Tabela de botões (controles Touch)

| Botão | O que faz |
|---|---|
| **A** (direito) | Coloca o kit na sua frente (sem QR) |
| **B** (direito) | Mostra/esconde o painel de diagnóstico |
| **X** (esquerdo) | Zera o ajuste de posição |
| **Y** (esquerdo) | Salva a posição do kit |
| **Menu** (esquerdo) | Simula um cartão (alterna liberado/negado) |
| **Analógico direito** | Move o kit (frente/trás, lados) |
| **Analógico esquerdo** | Sobe/desce e gira o kit |
| **Clique analógico direito** | Passo a passo: explica cada peça |
| **Clique analógico esquerdo** | Esconde/mostra os cartões virtuais |
| **Segurar analógico esquerdo 2 s** | Apaga a posição salva |
| **Gatilho / grip** | Pegar cartões e apertar botões |

Sem controles: as **mãos** pegam os cartões virtuais e apertam os botões SIMULAR.

---

## Se algo der errado

| Problema | O que fazer |
|---|---|
| Kit virtual não aparece | Olhe para o QR mais de perto; ou aperte **A** |
| Cartão real não anima o óculos | **B** → ver "ESP32: conectado"; conferir o Wi-Fi |
| Kit real não reage ao cartão | Problema no ESP32/fiação (não é do óculos) |
| Kit virtual no lugar errado | Ajuste com os analógicos e salve com **Y** |
| Tudo travado | Feche e abra o app de novo |
