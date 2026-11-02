using System;
using System.Collections;
using Oculus.Interaction;
using UnityEngine;

// Kit de controle de acesso em 3D (modelo do Blender).
// Detecta o cartao virtual no leitor, escreve no LCD, gira a cancela e apita.
// Nao decide nada sozinho: avisa quem esta no comando (ControleAcessoMR),
// que manda a animacao (FluxoAcesso), e reage a cada etapa dela.
public class KitVirtual : MonoBehaviour
{
    const float DistanciaLeitura = 0.08f;     // cartao a menos de 8 cm do leitor
    const float IntervaloLeitura = 2f;
    const float DistanciaDevolver = 0.6f;     // largado longe do kit volta ao lugar
    const float AnguloAberto = 80f;
    const float VelocidadeCancela = 160f;     // graus por segundo
    const float TempoMensagemLcd = 5f;
    const float FrequenciaBip = 2400f;

    // Ordem igual a do FluxoAcesso: leitor, ESP32, LCD, servo.
    public Transform[] Ancoras { get; private set; }

    public bool CancelaAberta { get; private set; }

    // true = cartao LIBERADO, false = NEGADO.
    public event Action<bool> CartaoLido;

    class Cartao
    {
        public bool liberado;
        public Transform t;
        public Grabbable pegavel;
        public Vector3 posInicial;
        public Quaternion rotInicial;
        public bool noLeitor;
    }

    Cartao[] cartoes;
    Transform leitor, pivo, ponta, tela;
    TextMesh textoLcd;
    AudioSource som;
    AudioClip bip;

    Vector3 eixoCancela;
    float sinalAbrir = 1f;
    float anguloAtual, anguloAlvo;
    Quaternion rotFechada;

    float ultimaLeitura = -10f;
    float lcdVoltaEm = -1f;

    string statusAtual;
    bool lcdFeito, cancelaFeita;

    void Awake()
    {
        leitor = Achar("Ancora_Leitor");
        Ancoras = new[] { leitor, Achar("Ancora_ESP32"), Achar("Ancora_LCD"), Achar("Ancora_Servo") };

        pivo = Achar("Servo_Pivo");
        ponta = Achar("Cancela_Malha");
        tela = Achar("LCD_Tela");

        cartoes = new[]
        {
            NovoCartao("Cartao_Liberado", true),
            NovoCartao("Cartao_Negado", false)
        };
    }

    // Start (e nao Awake): roda quando o kit aparece de verdade, ja posicionado.
    // No Awake o kit ainda pode ser desativado logo em seguida, o que mataria a
    // medicao do texto do LCD no meio.
    void Start()
    {
        // "Frente" do kit = perpendicular a linha leitor -> servo, do lado dos cartoes
        // (onde a pessoa fica). Nao depende dos eixos que vieram do Blender.
        Vector3 aoLongo = Vector3.ProjectOnPlane(Ancoras[3].position - Ancoras[0].position, Vector3.up).normalized;
        Vector3 frente = Vector3.Cross(aoLongo, Vector3.up);
        Vector3 centroCartoes = (cartoes[0].t.position + cartoes[1].t.position) * 0.5f;
        if (Vector3.Dot(frente, centroCartoes - Ancoras[2].position) < 0f) frente = -frente;

        PrepararCancela(frente);
        PrepararLcd(frente);
        PrepararSom();
    }

    Transform Achar(string nome)
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
            if (t.name == nome) return t;

        Debug.LogError("KitVirtual: nao achei " + nome);
        return transform;
    }

    Cartao NovoCartao(string nome, bool liberado)
    {
        var t = Achar(nome);

        return new Cartao
        {
            liberado = liberado,
            t = t,
            pegavel = t.GetComponentInChildren<Grabbable>(true),
            posInicial = t.localPosition,
            rotInicial = t.localRotation
        };
    }

    // ---------- Cartoes ----------
    void Update()
    {
        foreach (var c in cartoes)
        {
            bool segurando = c.pegavel != null && c.pegavel.SelectingPointsCount > 0;
            bool perto = Vector3.Distance(c.t.position, leitor.position) < DistanciaLeitura;

            // So conta quando o cartao chega no leitor (e nao enquanto fica parado la).
            if (perto && !c.noLeitor && Time.time - ultimaLeitura > IntervaloLeitura)
            {
                ultimaLeitura = Time.time;
                CartaoLido?.Invoke(c.liberado);
            }

            c.noLeitor = perto;

            if (!segurando && Vector3.Distance(c.t.position, transform.position) > DistanciaDevolver)
                DevolverCartao(c);
        }

        AtualizarCancela();

        if (lcdVoltaEm > 0f && Time.time > lcdVoltaEm)
        {
            lcdVoltaEm = -1f;
            EscreverLcd("APROXIME CARTAO", "");
        }
    }

    void DevolverCartao(Cartao c)
    {
        c.t.localPosition = c.posInicial;
        c.t.localRotation = c.rotInicial;
    }

    public void DevolverCartoes()
    {
        foreach (var c in cartoes) DevolverCartao(c);
    }

    // ---------- Etapas da animacao (chamadas pelo FluxoAcesso) ----------
    public void Inicio(string status)
    {
        statusAtual = status;
        lcdFeito = cancelaFeita = false;
    }

    public void ChegouNo(int no)
    {
        if (no == 1) Bipar(statusAtual);
        if (no == 2) AplicarLcd();
        if (no == 3) AplicarCancela();
    }

    public void Terminou()
    {
        AplicarLcd();
        AplicarCancela();
    }

    void AplicarLcd()
    {
        // Antes do Start (kit ainda nao apareceu) nao ha texto para escrever.
        if (lcdFeito || textoLcd == null) return;
        lcdFeito = true;

        switch (statusAtual)
        {
            case "ENTRADA ABERTA":
                EscreverLcd("ACESSO LIBERADO", "BEM-VINDO!"); break;
            case "ABERTURA MANUAL":
                EscreverLcd("CANCELA ABERTA", "ADMINISTRADOR"); break;
            case "NAO CADASTRADO":
                EscreverLcd("ACESSO NEGADO", "NAO CADASTRADO"); break;
            case "BLOQUEADO":
                EscreverLcd("ACESSO NEGADO", "BLOQUEADO"); break;
            case "FALHA NO SERVO":
                EscreverLcd("FALHA NO SERVO", ""); break;
            case "PORTA JA ABERTA":
                EscreverLcd("JA ESTA ABERTA", ""); break;
            case "FECHAMENTO AUTOMATICO":
                EscreverLcd("FECHOU SOZINHA", ""); break;
            default:
                EscreverLcd("CANCELA FECHADA", ""); break;
        }

        lcdVoltaEm = Time.time + TempoMensagemLcd;
    }

    void AplicarCancela()
    {
        if (cancelaFeita) return;
        cancelaFeita = true;

        switch (statusAtual)
        {
            case "ENTRADA ABERTA":
            case "ABERTURA MANUAL":
                MoverCancela(true); break;
            case "ENTRADA FECHADA":
            case "FECHAMENTO MANUAL":
            case "FECHAMENTO AUTOMATICO":
            case "FECHAMENTO POR BLOQUEIO":
            case "FECHAMENTO POR EXCLUSAO":
                MoverCancela(false); break;
        }
    }

    // ---------- Cancela ----------
    void PrepararCancela(Vector3 frente)
    {
        rotFechada = pivo.localRotation;

        // O eixo do servo aponta para a frente do kit. Descobre para que lado
        // girar para a ponta da cancela SUBIR, sem depender dos eixos do modelo.
        eixoCancela = pivo.InverseTransformDirection(frente);
        Vector3 braco = ponta.GetComponent<Renderer>().bounds.center - pivo.position;
        Vector3 girado = Quaternion.AngleAxis(30f, frente) * braco;
        sinalAbrir = girado.y > braco.y ? 1f : -1f;
    }

    public void MoverCancela(bool abrir)
    {
        CancelaAberta = abrir;
        anguloAlvo = abrir ? AnguloAberto : 0f;
    }

    void AtualizarCancela()
    {
        if (Mathf.Approximately(anguloAtual, anguloAlvo)) return;

        anguloAtual = Mathf.MoveTowards(anguloAtual, anguloAlvo, VelocidadeCancela * Time.deltaTime);
        pivo.localRotation = rotFechada * Quaternion.AngleAxis(anguloAtual * sinalAbrir, eixoCancela);
    }

    // ---------- LCD ----------
    void PrepararLcd(Vector3 frente)
    {
        var go = new GameObject("LCD_Texto");
        go.transform.SetParent(tela, false);

        textoLcd = go.AddComponent<TextMesh>();
        var fonte = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        textoLcd.font = fonte;
        textoLcd.fontSize = 80;
        textoLcd.characterSize = 0.001f;
        textoLcd.anchor = TextAnchor.MiddleCenter;
        textoLcd.alignment = TextAlignment.Center;
        textoLcd.fontStyle = FontStyle.Bold;
        textoLcd.color = new Color(0.04f, 0.1f, 0.02f);
        go.GetComponent<MeshRenderer>().sharedMaterial = fonte.material;

        // Texto deitado sobre a tela, legivel de quem esta na frente do kit.
        // A direcao da tela vem da geometria (cantos da malha, importada com Read/Write):
        // perpendicular as duas bordas, virada para cima. Nao depende dos eixos do Blender
        // nem das normais exportadas.
        var v = tela.GetComponent<MeshFilter>().sharedMesh.vertices;
        Vector3 p0 = tela.TransformPoint(v[0]);
        Vector3 borda1 = tela.TransformPoint(v[1]) - p0;
        Vector3 borda2 = tela.TransformPoint(v[2]) - p0;
        Vector3 normal = Vector3.Cross(borda1, borda2).normalized;
        if (normal.y < 0f) normal = -normal;
        // Topo das letras para o fundo do kit (longe da pessoa), como num LCD de verdade.
        Vector3 cima = Vector3.ProjectOnPlane(-frente, normal).normalized;
        Vector3 centroTela = tela.GetComponent<Renderer>().bounds.center;
        go.transform.SetPositionAndRotation(centroTela + normal * 0.001f, Quaternion.LookRotation(-normal, cima));

        EscreverLcd("APROXIME CARTAO", "");
        StartCoroutine(AjustarTamanhoLcd());
    }

    // Escala o texto para 16 caracteres caberem na largura da tela.
    IEnumerator AjustarTamanhoLcd()
    {
        string atual = textoLcd.text;
        textoLcd.text = "ACESSO LIBERADO";
        yield return null;

        // Diagonal da caixa ~ largura, ja que a tela e o texto sao bem mais largos que altos.
        // Assim a medida vale com o kit girado em qualquer direcao.
        float larguraTela = tela.GetComponent<Renderer>().bounds.size.magnitude;
        float larguraTexto = textoLcd.GetComponent<Renderer>().bounds.size.magnitude;

        if (larguraTexto > 0.0001f)
            textoLcd.transform.localScale *= larguraTela * 0.85f / larguraTexto;

        textoLcd.text = atual;
    }

    void EscreverLcd(string linha1, string linha2)
    {
        textoLcd.text = linha1 + "\n" + linha2;
    }

    // ---------- Buzzer ----------
    void PrepararSom()
    {
        som = Achar("Ancora_Buzzer").gameObject.AddComponent<AudioSource>();
        som.spatialBlend = 1f;
        som.minDistance = 0.3f;
        som.playOnAwake = false;

        int taxa = 44100;
        int n = Mathf.RoundToInt(taxa * 0.12f);
        var dados = new float[n];

        for (int i = 0; i < n; i++)
        {
            float envelope = Mathf.Min(1f, Mathf.Min(i, n - i) / 300f);
            dados[i] = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * FrequenciaBip * i / taxa)) * 0.25f * envelope;
        }

        bip = AudioClip.Create("Bip", n, 1, taxa, false);
        bip.SetData(dados, 0);
    }

    // Mesmos padroes do buzzer real (ESP32): abrir 1, fechar 2, negado 3, ja aberta 2.
    void Bipar(string status)
    {
        if (som == null || !isActiveAndEnabled) return;

        int vezes;

        switch (status)
        {
            case "ENTRADA ABERTA":
            case "ABERTURA MANUAL": vezes = 1; break;
            case "NAO CADASTRADO":
            case "BLOQUEADO":
            case "FALHA NO SERVO": vezes = 3; break;
            default: vezes = 2; break;
        }

        StartCoroutine(Bipes(vezes));
    }

    IEnumerator Bipes(int vezes)
    {
        var espera = new WaitForSeconds(0.24f);

        for (int i = 0; i < vezes; i++)
        {
            som.PlayOneShot(bip);
            yield return espera;
        }
    }
}
