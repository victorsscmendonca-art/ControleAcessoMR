# ControleAcessoMR

App de **realidade mista** para Meta Quest 3/3S que mostra, "ao vivo", como funciona um kit
de controle de acesso por RFID montado para uma feira escolar.

O visitante coloca o óculos, olha o kit real e vê etiquetas flutuando sobre cada peça,
setas de luz percorrendo o caminho do sinal e um painel **ACESSO AUTORIZADO** (verde)
ou **ACESSO NEGADO** (vermelho) quando alguém passa o cartão.

> **Feira:** última semana de novembro de 2026.
> Dono do projeto: iniciante em Unity/Blender. Explicar tudo em português simples.

---

## O kit físico

| Peça | Função |
|---|---|
| RC522 (leitor RFID) | Lê o cartão e envia o código |
| ESP32 | Recebe o código e decide se autoriza |
| LCD 16x2 | Mostra o resultado |
| Servo (cancela) | Abre quando o acesso é autorizado |
| Buzzer | Bipes de abrir, fechar e negado |

**Regra importante:** o kit funciona **sozinho**, sem o óculos. O óculos só *assiste*
ao que acontece; nunca é obrigatório e nunca comanda o kit.

---

## Pastas

| Pasta | O que tem |
|---|---|
| `Assets/ControleAcessoMR/ControleAcessoMR.cs` | Script principal do app (etiquetas, QR, calibração) |
| `Assets/Scenes/SampleScene.unity` | Única cena do app |
| `ESP32/ControleAcesso/ControleAcesso.ino` | Programa do ESP32 (Arduino IDE) |
| `.agent-docs/` | Documentação técnica para agentes de IA |

---

## Como as partes conversam

```
Cartão ──► RC522 ──► ESP32 ──► LCD / Servo / Buzzer        (funciona sozinho)
                       │
                       └── Wi-Fi próprio "Controle_Acesso" (192.168.4.1)
                               ├── Site de administração  (celular/notebook)
                               └── GET /evento  ◄── óculos consulta ~3x/s
```

- O ESP32 cria a própria rede Wi-Fi (`Controle_Acesso`), não depende do Wi-Fi da escola.
- `http://192.168.4.1/evento` é público e somente leitura. Responde por exemplo:
  `{"numero":7,"status":"ENTRADA ABERTA","origem":"Cartão","portaAberta":true,"restante":0}`
  - `numero` sobe a cada evento (volta a 0 se o ESP32 reiniciar).
  - Não mostra nome nem UID do cartão.
- Status possíveis: `ENTRADA ABERTA`, `ENTRADA FECHADA`, `NAO CADASTRADO`, `BLOQUEADO`,
  `PORTA JA ABERTA`, `FALHA NO SERVO`, `ABERTURA MANUAL`, `FECHAMENTO MANUAL`,
  `FECHAMENTO AUTOMATICO`, `FECHAMENTO POR BLOQUEIO`, `FECHAMENTO POR EXCLUSAO`,
  `LEITURA PARA CADASTRO`.

### Ligações do ESP32

| Peça | Pino → ESP32 |
|---|---|
| RC522 | SDA 14 · SCK 18 · MOSI 23 · MISO 19 · RST 22 · **3V3** (não 5V!) · GND |
| LCD 16x2 (paralelo) | RS 16 · E 17 · D4 25 · D5 26 · D6 27 · D7 32 · RW GND · VDD 5V · V0 potenciômetro 10k · A 5V via 220Ω · K GND |
| Servo | Sinal 33 · + 5V (VIN ou fonte externa) · − GND |
| Buzzer (ativo) | + 21 · − GND |

Todos os GND ligados juntos.

---

## Stack do app

- Unity **6000.0.66f2**, URP, Android (Quest), IL2CPP, ARM64, Vulkan
- Meta XR SDK **v207** (`com.meta.xr.sdk.all`): Camera Rig, Passthrough, MRUK
- MRUK com rastreamento de **QR code**: o kit é localizado por um QR com o texto `CONTROLE_ACESSO_MR`
- Calibração das etiquetas salva em PlayerPrefs (chaves `v2_*`)
- `com.unity.pipeline` instalado para o agente controlar o Editor pela CLI `unity`

### Controles atuais (Touch)
Menu simula cartão (alterna autorizado/negado) · A posicionar · B painel · X zerar ajuste · Y salvar · gatilho dir. próximo passo ·
gatilho esq. escolher item · grips tamanho do texto · clique analógico dir. tamanho da caixa ·
clique analógico esq. apagar calibração · analógicos movem.

---

## Status e próximos passos

- [x] Backup git + repositório privado no GitHub
- [x] Ferramentas instaladas (plugins Meta VR, Unity, Design; Blender + MCP; skills)
- [x] ESP32: rota `/evento` adicionada (**ainda não gravado/testado no hardware**)
- [x] Liberar http **só** para `192.168.4.1` no Android (a config padrão da Meta bloqueia cleartext)
- [x] Script que consulta `/evento` e não trava se o ESP32 sumir (mostra "ESP32 desconectado")
- [x] Animação: setas de luz leitor → ESP32 → LCD → servo + painel verde/vermelho
- [x] Simulação pelo botão Menu (teclas 1/2 no Editor); botões flutuantes para as mãos ainda faltam — (testar sem o kit e plano B na feira)
- [ ] Uso com as mãos (Interaction SDK) para visitantes; controles continuam para o apresentador
- [ ] Modelos 3D leves no Blender (opcional: cadeado, cartão)
- [ ] Teste no óculos com o kit + ensaio, 1 semana antes da feira

### Decisões já tomadas
- Público: visitantes leigos **e** o apresentador.
- Mostrar casos **autorizado e negado**.
- Textos no óculos **só em português**.
- Gatilho da animação: **cartão real** (via ESP32), com simulação como plano B.

### Pendências do dono
- Trocar as senhas fracas do ESP32 (`WIFI_SENHA` e `ADMIN_SENHA`) antes da feira.
- Gravar o `.ino` no ESP32 e testar `http://192.168.4.1/evento` pelo celular.
