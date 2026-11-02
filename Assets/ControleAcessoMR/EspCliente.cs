using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

// Acompanha o ESP32 do kit pela rota /evento.
// So le o que aconteceu; nunca comanda o kit.
// Se o ESP32 nao estiver por perto, apenas marca "desconectado".
public class EspCliente : MonoBehaviour
{
    [Serializable]
    class Evento
    {
        public long numero;
        public string status;
        public string origem;
        public bool portaAberta;
        public int restante;
    }

    public string endereco = "http://192.168.4.1/evento";
    public float intervalo = 0.33f;

    public event Action<string> NovoEvento;

    public bool Conectado { get; private set; }
    public bool PortaAberta { get; private set; }

    long ultimoNumero = -1;

    void OnEnable()
    {
        StartCoroutine(Consultar());
    }

    IEnumerator Consultar()
    {
        var espera = new WaitForSeconds(intervalo);

        while (true)
        {
            using (var req = UnityWebRequest.Get(endereco))
            {
                req.timeout = 2;
                yield return req.SendWebRequest();

                Conectado = req.result == UnityWebRequest.Result.Success;

                if (Conectado)
                {
                    Evento ev = null;

                    try { ev = JsonUtility.FromJson<Evento>(req.downloadHandler.text); }
                    catch (ArgumentException) { }

                    if (ev != null) Processar(ev);
                }
            }

            yield return espera;
        }
    }

    void Processar(Evento ev)
    {
        PortaAberta = ev.portaAberta;

        // Primeira resposta, ou ESP32 reiniciado (contador voltou):
        // so guarda a referencia, sem animar evento antigo.
        if (ultimoNumero < 0 || ev.numero < ultimoNumero)
        {
            ultimoNumero = ev.numero;
            return;
        }

        if (ev.numero == ultimoNumero) return;

        ultimoNumero = ev.numero;
        NovoEvento?.Invoke(ev.status);
    }
}
