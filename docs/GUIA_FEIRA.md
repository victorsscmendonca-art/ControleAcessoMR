# Guia da feira no Meta Quest 3 e 3S

Prepare cada Quest uma vez, antes de receber o público. Use um lugar marcado no chão para que as pessoas comecem olhando para a bancada. Se houver mais de um Quest, salve a posição em cada um e deixe um controle Touch direito à mão do apresentador.

## APK publicado · 0.4.1-beta

[Baixar a versão atual na página da pré-release](https://github.com/victorsscmendonca-art/ControleAcessoMR/releases/tag/v0.4.1-beta).

1. Abra o app com os controles Touch. Se a bancada já aparecer no lugar salvo, siga para a visita.
2. Para preparar a bancada, aperte **A**, ajuste a distância, a altura e o giro com os analógicos e a escala pelos botões no painel. Aperte **Y** para salvar e confira a indicação de posição no painel.
3. Selecione **Começar nova visita** no painel do apresentador. A pessoa já pode pegar um cartão com uma pinça, levá-lo ao leitor ou tocar num ponto para explorar a peça.
4. Na troca de pessoa, segure **B** no Touch direito por 1,5 segundo. Selecione **Começar nova visita** e entregue o Quest no mesmo ponto de partida. A bancada mantém a posição e a escala da sessão.

## Próxima versão · troca rápida de visitantes

Estes comandos são da atualização em desenvolvimento. Instale o APK correspondente quando ele for publicado para usá-los.

| Ação | Quando usar | O que acontece |
|---|---|---|
| **Entrar no Modo Visitante** | No painel do apresentador, uma vez depois de preparar o Quest. | Limpa a demonstração e inicia a introdução. |
| **Tirar e recolocar o Quest** | Na troca de pessoa, com o app no Modo Visitante. | Quando o Quest detecta que ficou fora da cabeça por pelo menos 1 segundo, prepara a nova visita assim que volta a receber entrada. |
| **A · Próximo visitante** | Aperte **A** no Touch direito no Modo Visitante se quiser reiniciar manualmente, inclusive durante uma animação ou explicação. O painel principal do visitante também tem esse botão. | Devolve os cartões ao suporte, fecha a cancela, limpa os efeitos e reabre a introdução. Mantém a bancada no lugar e a escala da sessão. |
| **Segurar B por 1,5 segundo** | No Modo Visitante, para retornar ao painel técnico. | Abre o Modo Apresentador. |
| **A · Recalibrar** e **Y · Salvar posição** | No Modo Apresentador, se mudar o local da bancada. | A traz a bancada para a frente. Y salva após o ajuste. |

### Roteiro rápido para a fila

1. Prepare e salve cada bancada antes da feira. Confira o alcance com duas pessoas de alturas diferentes.
2. Ative o Modo Visitante e mantenha o app aberto entre as pessoas.
3. Passe o Quest no ponto marcado. Se a introdução continuar da visita anterior, use **A · Próximo visitante**. A pessoa pode começar a interagir durante a introdução ou selecionar **Pular tutorial**.
4. Na primeira rodada de testes, troque de pessoa durante uma animação, com um cartão deslocado e com uma explicação aberta. Confirme a troca automática, o atalho **A**, a volta pelo **B** e a bancada na mesma posição.
5. Se a bancada desaparecer após uma falha de rastreamento, olhe ao redor na área já preparada. Use o painel do apresentador para recuperar a posição se necessário.

A cor grafite identifica a placa do ESP32. O brilho indica quando ela recebe o sinal NFC e processa a autorização.

## Instruções para cada nova versão

Ao publicar um APK, atualize o README e este guia no GitHub. Inclua nas notas da Release o número da versão, o que mudou, uma lista dos novos botões com o modo em que aparecem, como fazer a troca de visitantes e o que já foi verificado no Editor e no Quest. Vincule o guia às notas da Release e registre o APK correspondente.
