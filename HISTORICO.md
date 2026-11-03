# Histórico e decisões do projeto

Este arquivo guarda **o que foi pedido, decidido e feito**, para o projeto continuar mesmo se o chat
com o agente (Claude) for perdido. Leia junto com o [README.md](README.md) e o
[GUIA_DE_TESTE.md](GUIA_DE_TESTE.md).

## Quem é o dono e como trabalhar com ele

- Iniciante em Unity e Blender, fala **português**. Explicar tudo em palavras simples.
- Quer tudo feito **automaticamente**; quando só ele pode fazer algo, dar passo a passo com texto pronto.
- Mostrar o plano antes de mudanças grandes e fazer backup no git antes de mexer.
- Usar **todas as skills úteis automaticamente**, sem perguntar, e dizer em uma linha qual foi usada.
- Sempre que possível, mostrar o resultado com **foto** (captura do Unity/Blender).
- **Não gerar APK até ele pedir.** Quando gerar, publicar o APK no GitHub (ver abaixo).
- Guardar o máximo de informação no GitHub ("nuvem"), para não depender do chat.

## Regra do APK

Como gerar (já automatizado): `Unity.exe -batchmode -nographics -quit -buildTarget Android -projectPath <pasta> -executeMethod CompilarApk.Gerar -logFile build_apk.log` (com o Editor fechado). O APK sai em `Builds/Quest/ControleAcessoMR.apk`. Antes de gerar de novo, subir `AndroidBundleVersionCode` em `ProjectSettings`.

Quando o dono pedir o APK:
1. Gerar o build Android (Quest) pelo Unity.
2. Publicar como **GitHub Release** no repositório privado (`gh release create vX.Y ControleAcessoMR.apk`),
   com notas em português do que mudou.
3. Não colocar o `.apk` dentro do git (arquivo grande): ele fica anexado na Release.
4. Atualizar este arquivo e o README com a versão.

## Decisões tomadas (em ordem)

| Data | Decisão |
|---|---|
| 2026-11 | App MR para Quest 3/3S mostra um **kit virtual 3D** (cópia 2x do kit real) contando como o controle de acesso funciona |
| 2026-11 | O **ESP32 funciona sozinho**, sem o óculos; o óculos só lê `GET /evento` (somente leitura) |
| 2026-11 | Rede própria do ESP32 (`Controle_Acesso`, 192.168.4.1), não depende do Wi-Fi da escola |
| 2026-11 | Posicionamento pelo **QR code** deitado na mesa (`CONTROLE_ACESSO_MR`); botão A como plano B |
| 2026-11 | Kit virtual com o **centro na linha dos olhos**, quase em pé, de frente: olhar para frente = virtual, olhar para baixo = kit real na mesa |
| 2026-11 | Sem fundo escuro de "bancada", só contorno azul |
| 2026-11 | 2 cartões nos dois mundos, estilo cartão de crédito: **LIBERADO** e **NEGADO** |
| 2026-11 | Os cartões são **NFC**: textos dizem "NFC", nunca "RFID" (o RC522 lê NFC 13,56 MHz) |
| 2026-11 | Gatilhos: cartão real (via ESP32), cartão virtual com a mão, botões SIMULAR e Menu |
| 2026-11 | Textos no óculos **só em português** |
| 2026-11 | Funciona **sem controles** (mãos); controles só para o apresentador |
| 2026-11 | Boas-vindas em cartão acima do kit; diagnóstico escondido (botão B); **modo vitrine** após 40 s parado |
| 2026-11 | Cartões virtuais: clique no analógico esquerdo esconde/mostra; **somem sozinhos 30 s** quando chega cartão real |
| 2026-11 | Luz ao lado do ESP32 virtual: **verde = conectado**, **vermelha piscando = sem sinal** |
| 2026-11 | Letras em **TextMeshPro** (nítidas no óculos) |

## O que já foi feito

- Kit virtual modelado no Blender (`Modelos3D/KitVirtual.blend`) igual à foto de referência, com caminhos dos fios.
- Animação: ondas no leitor, luz pelos fios, ESP32 brilha, sinal se divide para LCD e servo, LCD digita,
  cancela sobe, buzzer e sons, legenda contando a história, painel AUTORIZADO/NEGADO com cadeado 3D.
- Interaction SDK: pegar cartões com mão/controle, botões SIMULAR com toque/raio.
- Testes automáticos (EditMode) da lógica liberado/negado e de todos os status do ESP32.
- Meta XR Simulator v207 instalado para testar no PC.
- Guia de teste passo a passo com tabela de botões.

## Pendências

- [ ] Testar no óculos de verdade (QR, mãos, Wi-Fi, desempenho, sons).
- [ ] Gravar o `.ino` no ESP32 e testar `http://192.168.4.1/evento` pelo celular.
- [ ] Trocar as senhas fracas do ESP32 (`WIFI_SENHA`, `ADMIN_SENHA`).
- [x] APK **v0.3.0** gerado e publicado na [Release](https://github.com/victorsscmendonca-art/ControleAcessoMR/releases/tag/v0.3.0) (2026-11-02). Assinado com chave de depuração (serve para instalar no óculos do dono, não para a loja).
- [ ] Para a loja da Meta (se um dia for): criar keystore de release, guardar com backup e gerar de novo.
- [ ] Ensaio completo 1 semana antes da feira (última semana de novembro de 2026).

## Ferramentas instaladas no PC (para o agente)

- Unity 6000.0.66f2 + pacote `com.unity.pipeline` (CLI `unity`) e MCP do Unity.
- Blender 5.2 + MCP do Blender (`uvx mcp-for-blender`).
- Plugins: Meta VR (skills `hz-*`), Unity, Design; skills ponytail, graphify, impeccable e outras.
- Meta XR Simulator em `C:\Program Files\MetaXRSimulator\v207.0`
  (Unity: Window > Meta > Meta XR Simulator > Activate).
- Observação: com o simulador ativo, o Unity às vezes fecha ao sair do Play. É do simulador; é só reabrir.

## Como continuar em um chat novo

Cole isto para o agente:

> Leia README.md, HISTORICO.md e GUIA_DE_TESTE.md do repositório ControleAcessoMR e continue o projeto.
> Fale comigo em português simples; eu sou iniciante.

## Segurança (nota do APK v0.3.0)

- O Immersive Debugger da Meta cria `Assets/Resources/DevAgentSettings.asset` com um token de depuração e o IP da rede local
  (desligado: `enabled: 0`). Ele **não vai mais para o git**. O APK v0.3.0 pode levá-lo dentro: **não compartilhe esse APK em público**.
  Na próxima versão, tirar esse arquivo de `Resources` antes de gerar o APK.
- Repositório e Release são **privados**.
