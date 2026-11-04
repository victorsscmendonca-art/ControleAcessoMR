# Versão beta (0.4.0-beta)

## Problema
Como fazer o visitante (principalmente **alunos**, e também professores) sair da feira entendendo como um
controle de acesso funciona, explorando sozinho, sem exagero na tela?

## Direção escolhida (aprovada pelo dono em 2026-11)
- **App separado** no óculos: "Controle de Acesso MR (Beta)" (`com.victor.controleacessomr.beta`), no ramo `beta`.
  A estável (v0.3.1, ramo `main`) continua intacta; as duas ficam instaladas juntas.
- Itens, em ordem:
  1. **Tocar na peça → explicação** (ideia do dono) + botão **"Como funciona?"** (tour pelas 4 peças).
  2. **Código do cartão viajando pelo fio** (código de EXEMPLO; o ESP32 não envia o código real, por privacidade).
  3. **Placar da feira** (cartões de hoje: liberados/negados; o modo vitrine não conta; salvo no óculos).
  4. **Mão-guia animada** — só se der tempo.

## Suposições a validar no óculos
- [ ] Dá para tocar nas peças com o dedo sem disparar sem querer (raio 6 cm na peça, 4 cm na etiqueta).
- [ ] O código "ID 7A 3F 91 C2" é legível em movimento.
- [ ] O placar ao lado dos botões não polui a tela.
- [ ] A estável continua funcionando igual (a beta é outro app).

## Não vamos fazer agora
- Quiz — depende de tempo.
- Narração em voz — feira barulhenta.
- Óculos comandando o kit real — o ESP32 funciona sozinho.
