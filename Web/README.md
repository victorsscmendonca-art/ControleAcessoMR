# Controle de Acesso MR — versão Web (protótipo)

Realidade mista **no navegador do Meta Quest** (WebXR), sem APK e sem Unity.

## O que tem
- Botão **Entrar**: abre a câmera do óculos (passthrough).
- Aviso na frente dos olhos: **"Olhe para a mesa"**. Um anel azul aparece na mesa; **faça a pinça** para colocar o kit.
  Se não der, o kit **aparece sozinho em 8 s** na sua frente. A tela nunca fica vazia.
- Kit simples (caixas): leitor NFC, ESP32, LCD, servo com cancela, fios.
- Botões **LIBERADO** / **NEGADO**: tocar com o dedo, ou apontar e fazer a pinça.
- Animação: luz pelo fio leitor → ESP32 → LCD e servo, LCD muda, cancela sobe, sons, painel AUTORIZADO/NEGADO.
- ESP32: consulta `/evento` a cada 0,5 s. Luz ao lado do ESP32: **verde = conectado**, **vermelha piscando = sem sinal**.
  `ENTRADA ABERTA` → liberado; `BLOQUEADO` / `NAO CADASTRADO` → negado; outros status só aparecem no painel.

## Testar no PC (sem óculos)
Na pasta `Web`: `python -m http.server 8000` e abra `http://localhost:8000`.
Clique em **Ver no PC (sem óculos)**. Arraste para girar; teclas **1** (liberado) e **2** (negado).

## No óculos
O WebXR só funciona em endereço **https** ou `localhost`.
- **Teste em casa:** site hospedado com https (GitHub Pages, Vercel...). Nesse caso o ESP32 **não** conecta
  (o navegador bloqueia https → http); a luz fica vermelha, os botões funcionam.
- **Feira (com ESP32):** site servido pelo ESP32 em `http://192.168.4.1/`. Precisa liberar uma vez no navegador do Quest:
  `chrome://flags` → **Insecure origins treated as secure** → adicionar `http://192.168.4.1` → Enabled → reiniciar o navegador.
  **Ainda não confirmado no Quest:** testar antes da feira.

## Estado
- Testado no Chromium do PC (modo prévia): kit, animação liberado/negado, sem erros.
- **Não testado no óculos** (WebXR, pinça, dedo nos botões, hit-test).
- three.js 0.170.0 local em `lib/` (funciona sem internet).
