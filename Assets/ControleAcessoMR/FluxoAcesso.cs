using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Animacao do sinal passando pelos fios (leitor -> ESP32 -> LCD e servo),
// legenda contando cada etapa e painel de resultado acima do kit.
public class FluxoAcesso : MonoBehaviour
{
    // Indices das ancoras, na ordem do KitVirtual.
    public const int Leitor = 0, Esp = 1, Lcd = 2, Servo = 3;

    public struct Salto
    {
        public int de, para;
        public Salto(int de, int para) { this.de = de; this.para = para; }
    }

    // O que acontece para uma resposta do ESP32. Cada etapa e um grupo de saltos que
    // correm juntos (no ESP32 o sinal se divide: LCD e servo ao mesmo tempo).
    public class RotaAcesso
    {
        public Salto[][] etapas;
        public Color cor;
        public string titulo;
        public string[] legendas;   // uma no inicio + uma por etapa
        public bool cadeadoAberto;
    }

    const float VelocidadeSinal = 0.25f;  // metros por segundo
    const float DuracaoMinima = 0.45f;    // segundos por trecho
    const float PausaNoNo = 0.35f;        // tempo para ler a legenda em cada peca
    const float TempoPainel = 5f;
    const float TamanhoPulso = 0.016f;
    const float TamanhoLegenda = 0.0032f;
    const float AbaixoDoPainel = 0.075f;

    // Ajustados por quem configura.
    public float alturaPainel = 0.28f;
    public float tamanhoTexto = 0.0022f;

    // Kit 3D que reage a cada etapa (LCD, cancela, buzzer) e da o caminho dos fios.
    public KitVirtual kit;

    const float AlturaModeloCadeado = 1f;   // altura do modelo no Blender
    const float EspacoCadeado = 0.02f;

    static readonly Color CorFechadura = new Color(0.05f, 0.05f, 0.05f, 1f);
    static readonly Color FundoLegenda = new Color(0f, 0f, 0f, 0.7f);

    static readonly Color Verde = new Color(0.25f, 1f, 0.45f, 0.95f);
    static readonly Color Vermelho = new Color(1f, 0.3f, 0.3f, 0.95f);
    static readonly Color Amarelo = new Color(1f, 0.85f, 0.2f, 0.95f);
    static readonly Color Cinza = new Color(0.8f, 0.8f, 0.8f, 0.95f);

    // Ocupado = animando ou mostrando o resultado (a tela de boas-vindas espera).
    public bool Ocupado => atual != null;

    Transform[] ancoras;
    Transform painel;
    TextMeshPro texto;
    Transform fundoT;
    Renderer fundo;
    Coroutine atual;

    readonly List<Transform> pulsos = new List<Transform>();
    readonly List<Renderer> pulsosR = new List<Renderer>();
    readonly List<TrailRenderer> rastros = new List<TrailRenderer>();

    Transform legenda;
    TextMeshPro legendaTexto;
    Camera cam;
    Transform legendaFundoT;

    Transform cadeadoT;
    GameObject cadeadoAberto, cadeadoFechado;
    Renderer[] pecasCadeado;

    public void Configurar(Transform pai, Transform[] ancoras, Material matBase, GameObject modeloCadeado)
    {
        this.ancoras = ancoras;

        // Dois pulsos: no ESP32 o sinal segue para o LCD e o servo ao mesmo tempo.
        if (matBase != null)
            for (int i = 0; i < 2; i++) CriarPulso(pai, matBase);

        var go = new GameObject("PainelResultado");
        painel = go.transform;
        painel.SetParent(pai, false);
        texto = Texto3D.Criar(go, "", tamanhoTexto);

        if (matBase != null)
            fundo = NovaForma(PrimitiveType.Quad, "Fundo", painel, matBase, out fundoT);

        if (modeloCadeado != null && matBase != null)
            CriarCadeado(modeloCadeado, matBase);

        go.SetActive(false);

        var lg = new GameObject("Legenda");
        legenda = lg.transform;
        legenda.SetParent(pai, false);
        legendaTexto = Texto3D.Criar(lg, "", TamanhoLegenda, negrito: false);
        legendaTexto.color = Color.white;

        if (matBase != null)
        {
            var f = NovaForma(PrimitiveType.Quad, "Fundo", legenda, matBase, out legendaFundoT);
            f.material.color = FundoLegenda;
        }

        lg.SetActive(false);
    }

    void CriarPulso(Transform pai, Material matBase)
    {
        var r = NovaForma(PrimitiveType.Sphere, "PulsoSinal", pai, matBase, out var t);
        t.localScale = Vector3.one * TamanhoPulso;

        // Rastro de luz atras do pulso, como as setas brilhantes da foto.
        var rastro = t.gameObject.AddComponent<TrailRenderer>();
        rastro.time = 0.4f;
        rastro.startWidth = TamanhoPulso * 0.8f;
        rastro.endWidth = 0f;
        rastro.material = new Material(matBase);
        rastro.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rastro.receiveShadows = false;

        t.gameObject.SetActive(false);
        pulsos.Add(t);
        pulsosR.Add(r);
        rastros.Add(rastro);
    }

    void CriarCadeado(GameObject modelo, Material matBase)
    {
        var inst = Instantiate(modelo, painel);
        inst.name = "Cadeado";
        cadeadoT = inst.transform;

        // A frente do modelo (fechadura) vem do Blender virada para longe de quem olha.
        cadeadoT.localRotation = Quaternion.Euler(0f, 180f, 0f);

        cadeadoAberto = cadeadoT.Find("CadeadoAberto")?.gameObject;
        cadeadoFechado = cadeadoT.Find("CadeadoFechado")?.gameObject;

        // Material sem luz, como o resto do painel; a fechadura fica escura.
        pecasCadeado = inst.GetComponentsInChildren<Renderer>(true);

        foreach (var r in pecasCadeado)
        {
            r.material = new Material(matBase);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            if (r.name.Contains("Fechadura")) r.material.color = CorFechadura;
        }
    }

    void PrepararCadeado(bool aberto, Color cor)
    {
        if (cadeadoT == null) return;

        if (cadeadoAberto != null) cadeadoAberto.SetActive(aberto);
        if (cadeadoFechado != null) cadeadoFechado.SetActive(!aberto);

        foreach (var r in pecasCadeado)
        {
            if (!r.name.Contains("Fechadura")) r.material.color = cor;
        }
    }

    // Retorna false para eventos que nao tem animacao (ex.: leitura para cadastro).
    public bool Mostrar(string status)
    {
        var rota = Rota(status);
        if (rota == null) return false;

        // Novo evento no meio de uma animacao: recomeca do zero.
        if (atual != null) StopCoroutine(atual);
        foreach (var p in pulsos) p.gameObject.SetActive(false);
        if (kit != null) kit.Inicio(status);
        atual = StartCoroutine(Animar(rota));
        return true;
    }

    static Salto[] S(params int[] deParaPares)
    {
        var r = new Salto[deParaPares.Length / 2];
        for (int i = 0; i < r.Length; i++) r[i] = new Salto(deParaPares[i * 2], deParaPares[i * 2 + 1]);
        return r;
    }

    const string LeLe = "1. O leitor NFC lê o código do cartão";

    // Para cada resposta do ESP32: por onde o sinal passa, a cor, o painel e a legenda.
    // Separado do resto para poder ser testado sem o oculos.
    public static RotaAcesso Rota(string status)
    {
        var ida = S(Leitor, Esp);
        var lcdEServo = S(Esp, Lcd, Esp, Servo);
        var soLcd = S(Esp, Lcd);
        var soServo = S(Esp, Servo);

        switch (status)
        {
            case "ENTRADA ABERTA":
                return new RotaAcesso
                {
                    etapas = new[] { ida, lcdEServo }, cor = Verde, titulo = "ACESSO\nAUTORIZADO", cadeadoAberto = true,
                    legendas = new[] { LeLe, "2. O ESP32 confere: cartão cadastrado!", "3. O LCD avisa e o servo abre a cancela" }
                };
            case "ABERTURA MANUAL":
                return new RotaAcesso
                {
                    etapas = new[] { lcdEServo }, cor = Verde, titulo = "ABERTURA\nPELO ADMIN", cadeadoAberto = true,
                    legendas = new[] { "O administrador mandou abrir pelo site", "O LCD avisa e o servo abre a cancela" }
                };
            case "NAO CADASTRADO":
            case "BLOQUEADO":
                return new RotaAcesso
                {
                    etapas = new[] { ida, soLcd }, cor = Vermelho, titulo = "ACESSO\nNEGADO",
                    legendas = new[]
                    {
                        LeLe,
                        status == "BLOQUEADO" ? "2. O ESP32 confere: cartão bloqueado!" : "2. O ESP32 confere: cartão NÃO cadastrado!",
                        "3. O LCD avisa e a cancela continua fechada"
                    }
                };
            case "FALHA NO SERVO":
                return new RotaAcesso
                {
                    etapas = new[] { ida, soLcd }, cor = Vermelho, titulo = "FALHA\nNO SERVO",
                    legendas = new[] { LeLe, "2. O ESP32 percebe um problema no servo", "3. O LCD avisa a falha" }
                };
            case "PORTA JA ABERTA":
                return new RotaAcesso
                {
                    etapas = new[] { ida, soLcd }, cor = Amarelo, titulo = "CANCELA\nJÁ ABERTA", cadeadoAberto = true,
                    legendas = new[] { LeLe, "2. O ESP32 vê que a cancela já está aberta", "3. O LCD avisa" }
                };
            case "ENTRADA FECHADA":
                return new RotaAcesso
                {
                    etapas = new[] { ida, lcdEServo }, cor = Cinza, titulo = "CANCELA\nFECHADA",
                    legendas = new[] { "1. O mesmo cartão passa de novo no leitor", "2. O ESP32 reconhece quem abriu", "3. O servo fecha a cancela" }
                };
            case "FECHAMENTO MANUAL":
            case "FECHAMENTO AUTOMATICO":
            case "FECHAMENTO POR BLOQUEIO":
            case "FECHAMENTO POR EXCLUSAO":
                return new RotaAcesso
                {
                    etapas = new[] { lcdEServo }, cor = Cinza, titulo = "CANCELA\nFECHADA",
                    legendas = new[]
                    {
                        status == "FECHAMENTO AUTOMATICO" ? "O tempo acabou: o ESP32 manda fechar" : "O ESP32 manda fechar a cancela",
                        "O servo fecha a cancela"
                    }
                };
            default:
                return null;
        }
    }

    IEnumerator Animar(RotaAcesso rota)
    {
        painel.gameObject.SetActive(false);
        MostrarLegenda(rota.legendas[0]);

        int primeiro = rota.etapas[0][0].de;
        if (kit != null) kit.ChegouNo(primeiro);
        yield return new WaitForSeconds(PausaNoNo);

        for (int e = 0; e < rota.etapas.Length; e++)
        {
            var saltos = rota.etapas[e];
            var caminhos = new Vector3[saltos.Length][];
            float duracao = DuracaoMinima;

            for (int i = 0; i < saltos.Length && i < pulsos.Count; i++)
            {
                caminhos[i] = Caminho(saltos[i]);
                duracao = Mathf.Max(duracao, Comprimento(caminhos[i]) / VelocidadeSinal);

                pulsosR[i].material.color = rota.cor;
                rastros[i].material.color = rota.cor;
                pulsos[i].position = caminhos[i][0];
                rastros[i].Clear();
                pulsos[i].gameObject.SetActive(true);
            }

            // Todos os pulsos da etapa chegam juntos, cada um no seu fio.
            for (float t = 0f; t < 1f; t += Time.deltaTime / duracao)
            {
                float s = Mathf.SmoothStep(0f, 1f, t);

                for (int i = 0; i < saltos.Length && i < pulsos.Count; i++)
                {
                    pulsos[i].position = PontoNoCaminho(caminhos[i], s);
                    pulsos[i].localScale = Vector3.one * TamanhoPulso * (1f + 0.3f * Mathf.Sin(t * Mathf.PI * 6f));
                }

                yield return null;
            }

            foreach (var p in pulsos) p.gameObject.SetActive(false);

            if (kit != null)
                foreach (var sl in saltos) kit.ChegouNo(sl.para);

            MostrarLegenda(rota.legendas[Mathf.Min(e + 1, rota.legendas.Length - 1)]);
            yield return new WaitForSeconds(PausaNoNo);
        }

        if (kit != null) kit.Terminou();

        texto.text = rota.titulo;
        texto.color = rota.cor;

        if (fundo != null)
            fundo.material.color = new Color(rota.cor.r * 0.12f, rota.cor.g * 0.12f, rota.cor.b * 0.12f, 0.8f);

        PrepararCadeado(rota.cadeadoAberto, rota.cor);
        painel.gameObject.SetActive(true);
        PosicionarTextos();

        // O texto so atualiza os limites no frame seguinte.
        yield return null;
        AjustarFundo();

        yield return new WaitForSeconds(TempoPainel);

        painel.gameObject.SetActive(false);
        legenda.gameObject.SetActive(false);
        atual = null;
    }

    Vector3[] Caminho(Salto s)
    {
        if (kit != null) return kit.Caminho(s.de, s.para);
        return new[] { ancoras[s.de].position, ancoras[s.para].position };
    }

    static float Comprimento(Vector3[] c)
    {
        float l = 0f;
        for (int i = 1; i < c.Length; i++) l += Vector3.Distance(c[i - 1], c[i]);
        return l;
    }

    // Ponto a uma fracao 's' (0..1) do comprimento total do caminho.
    static Vector3 PontoNoCaminho(Vector3[] c, float s)
    {
        float alvo = Comprimento(c) * s;

        for (int i = 1; i < c.Length; i++)
        {
            float trecho = Vector3.Distance(c[i - 1], c[i]);
            if (alvo <= trecho && trecho > 0f) return Vector3.Lerp(c[i - 1], c[i], alvo / trecho);
            alvo -= trecho;
        }

        return c[c.Length - 1];
    }

    void MostrarLegenda(string frase)
    {
        legendaTexto.text = frase;
        legenda.gameObject.SetActive(true);
        PosicionarTextos();
        StartCoroutine(AjustarFundoLegenda());
    }

    IEnumerator AjustarFundoLegenda()
    {
        yield return null;
        if (legendaFundoT == null) yield break;

        Bounds b = Texto3D.Limites(legendaTexto);
        legendaFundoT.localPosition = new Vector3(b.center.x, b.center.y, 0.002f);
        legendaFundoT.localScale = new Vector3(b.size.x + 0.03f, b.size.y + 0.015f, 1f);
    }

    void LateUpdate()
    {
        if (legenda != null && legenda.gameObject.activeInHierarchy)
            PosicionarTextos();
    }

    void PosicionarTextos()
    {
        Vector3 centro = Vector3.zero;
        foreach (var a in ancoras) centro += a.position;
        centro /= ancoras.Length;

        Vector3 p = centro + Vector3.up * alturaPainel;
        painel.position = p;
        legenda.position = p - Vector3.up * AbaixoDoPainel;

        if (cam == null) cam = Camera.main;   // guardada: Camera.main busca por tag a cada chamada
        if (cam == null) return;

        Vector3 dir = p - cam.transform.position;
        if (dir.sqrMagnitude > 0.0001f)
        {
            var giro = Quaternion.LookRotation(dir, Vector3.up);
            painel.rotation = giro;
            legenda.rotation = giro;
        }
    }

    void AjustarFundo()
    {
        if (fundoT == null) return;

        Bounds b = Texto3D.Limites(texto);

        // Cadeado a esquerda do texto, da mesma altura que ele.
        float extra = 0f;

        if (cadeadoT != null)
        {
            float altura = b.size.y * 0.9f;
            float largura = altura * 0.8f;

            cadeadoT.localScale = Vector3.one * (altura / AlturaModeloCadeado);
            cadeadoT.localPosition = new Vector3(
                b.min.x - EspacoCadeado - largura * 0.5f,
                b.center.y - altura * 0.5f,
                -0.01f
            );

            extra = largura + EspacoCadeado;
        }

        fundoT.localPosition = new Vector3(b.center.x - extra * 0.5f, b.center.y, 0.002f);
        fundoT.localScale = new Vector3(b.size.x + 0.06f + extra, b.size.y + 0.04f, 1f);
    }

    static Renderer NovaForma(PrimitiveType tipo, string nome, Transform pai, Material matBase, out Transform t)
    {
        var go = GameObject.CreatePrimitive(tipo);

        Destroy(go.GetComponent<Collider>());

        go.name = nome;
        t = go.transform;
        t.SetParent(pai, false);

        var r = go.GetComponent<MeshRenderer>();

        r.material = new Material(matBase);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;

        return r;
    }
}
