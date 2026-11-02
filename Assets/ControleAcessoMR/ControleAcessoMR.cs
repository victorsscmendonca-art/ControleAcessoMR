using System.Collections.Generic;
using System.Text;
using Meta.XR.MRUtilityKit;
using UnityEngine;

// Comanda o app: posiciona o kit virtual (pelo QR code ou na mao),
// cria as etiquetas e liga cartao virtual, ESP32 real e simulacao
// a mesma animacao (FluxoAcesso).
public class ControleAcessoMR : MonoBehaviour
{
    class Etiqueta
    {
        public string titulo;
        public string explicacao;
        public Transform ancora;
        public Transform raiz;
        public TextMesh texto;
        public Renderer borda;
        public Transform bordaT;
        public Renderer fundo;
        public Transform fundoT;
        public Renderer linha;
        public Transform linhaT;
        public Renderer ponto;
    }

    enum Modo { Aguardando, QR, Manual }

    const float TamanhoEtiqueta = 0.0011f;
    const float TamanhoPainel = 0.004f;
    const float TempoVitrine = 40f;   // parado ha 40 s: simula sozinho (modo vitrine da feira)
    const float AlturaEtiqueta = 0.13f;
    const float TempoFechamentoVirtual = 8f;

    // Olhando para baixo a pessoa ve a mesa com o kit real; olhando para a frente,
    // ve o kit virtual um pouco abaixo da linha dos olhos, inclinado como uma vitrine
    // (deitado na altura dos olhos ele apareceria de lado, como uma linha).
    const float AbaixoDosOlhos = 0f;   // centro do kit na linha dos olhos: olhar para frente = kit virtual
    const float InclinacaoKit = 60f;   // quase em pe, de frente para a pessoa
    const float AlemDoQR = 0.1f;          // um pouco para o fundo; cartoes ao alcance da mao
    const float DistanciaManual = 0.7f;

    // A altura acompanha cada visitante (crianca ou adulto), devagar e so quando
    // a diferenca e grande, para o kit nao "pular" a cada movimento da cabeca.
    const float ZonaMortaAltura = 0.06f;
    const float VelocidadeAltura = 0.15f;  // metros por segundo
    // Quase transparente: o kit real, embaixo, precisa continuar visivel.
    static readonly Color CorBordaBandeja = new Color(0.3f, 0.85f, 1f, 0.7f);

    // Botoes na frente-direita do kit (os cartoes ficam na frente-esquerda).
    const float DistanciaBotoes = 0.3f;
    const float LadoBotoes = 0.25f;
    const float AlturaBotoes = -0.1f;   // o kit inclinado desce na frente; botoes acompanham

    static readonly Color Ciano = new Color(0.3f, 0.85f, 1f, 0.95f);
    static readonly Color FundoEtiqueta = new Color(0.02f, 0.12f, 0.2f, 0.85f);
    static readonly Color AmareloPasso = new Color(1f, 0.85f, 0.2f, 0.95f);
    static readonly Color CinzaApagado = new Color(0.5f, 0.6f, 0.65f, 0.5f);

    [Header("QR Code")]
    public string payloadEsperado = "CONTROLE_ACESSO_MR";

    [Header("Ajuste")]
    public float velocidadeMovimento = 0.15f;
    public float velocidadeRotacao = 40f;

    [Header("Diagnostico")]
    public bool mostrarPainel = true;

    [Header("Simulacao com as maos")]
    public Transform botoesSimulacao;

    [Header("Modelos 3D")]
    public GameObject modeloCadeado;
    public GameObject kitVirtualPrefab;

    readonly List<Etiqueta> etiquetas = new List<Etiqueta>();
    Font fonte;
    Material matBase;
    TextMesh painel, passosBoasVindas;
    Transform boasVindas;

    // Cartao de boas-vindas: titulo, frase curta e os 2 passos (o passo pisca devagar).
    static readonly Color CorTitulo = new Color(0.45f, 0.88f, 1f);
    static readonly Color CorTextoSuave = new Color(0.78f, 0.84f, 0.88f);
    static readonly Color CorPassos = new Color(0.55f, 1f, 0.7f);
    float ultimaAtividade;
    Transform raiz, ajuste, cabeca;
    MRUKTrackable qrAtual;
    Modo modo = Modo.Aguardando;
    int passo = -1;
    bool fundosPendentes = true;
    int totalDetectados;
    string ultimoTipo = "(nenhum)";
    string ultimoPayload = "(nenhum)";
    string mensagem = "";
    float mensagemAte;
    float proximaAtualizacao;

    KitVirtual kit;
    EspCliente esp;
    FluxoAcesso fluxo;
    bool proximaSimulacaoNegada;
    string ultimoEsp = "(nenhum)";
    float fecharVirtualEm = -1f;
    float alturaKit = float.NaN;

    void Start()
    {
        fonte = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        cabeca = Cabeca();

        var sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("UI/Default");
        if (sh != null) matBase = new Material(sh);

        ConfigurarDesempenho();
        CriarRaizEKit();
        CarregarPosicaoKit();
        CriarEtiquetas();
        CriarFluxo();

        if (botoesSimulacao != null) botoesSimulacao.gameObject.SetActive(false);

        if (mostrarPainel) CriarPainel();

        AplicarVisual();

        if (MRUK.Instance != null)
        {
            MRUK.Instance.SceneSettings.TrackableAdded.AddListener(OnTrackableAdded);
            MRUK.Instance.SceneSettings.TrackableRemoved.AddListener(OnTrackableRemoved);
        }
    }

    void OnDestroy()
    {
        if (esp != null) esp.NovoEvento -= OnEventoEsp;
        if (kit != null) kit.CartaoLido -= OnCartaoVirtual;

        if (MRUK.Instance != null)
        {
            MRUK.Instance.SceneSettings.TrackableAdded.RemoveListener(OnTrackableAdded);
            MRUK.Instance.SceneSettings.TrackableRemoved.RemoveListener(OnTrackableRemoved);
        }
    }

    // 90 Hz (mais fluido e confortavel; a cena e leve) e foveated rendering
    // (menos detalhe nas bordas da visao, onde o olho nao repara).
    static void ConfigurarDesempenho()
    {
        OVRManager.foveatedRenderingLevel = OVRManager.FoveatedRenderingLevel.High;
        OVRManager.useDynamicFoveatedRendering = true;

        var tela = OVRManager.display;
        if (tela == null || tela.displayFrequenciesAvailable == null) return;

        if (System.Array.IndexOf(tela.displayFrequenciesAvailable, 90f) >= 0)
            tela.displayFrequency = 90f;
    }

    // ---------- Kit virtual ----------
    void CriarRaizEKit()
    {
        raiz = new GameObject("RaizControleAcesso").transform;

        ajuste = new GameObject("Ajuste").transform;
        ajuste.SetParent(raiz, false);

        if (kitVirtualPrefab != null)
        {
            var go = Instantiate(kitVirtualPrefab, ajuste);
            go.name = "KitVirtual";
            kit = go.GetComponent<KitVirtual>();
            kit.CartaoLido += OnCartaoVirtual;
            CriarBandeja(go);
        }
        else
        {
            Debug.LogError("ControleAcessoMR: kitVirtualPrefab nao configurado");
        }

        raiz.gameObject.SetActive(false);
    }

    // "Bandeja holografica" translucida embaixo do kit flutuante: ajuda o olho a
    // entender a altura do kit (sem ela parece solto no ar).
    void CriarBandeja(GameObject kitGo)
    {
        if (matBase == null) return;

        // Neste momento a raiz esta na origem, entao a caixa no mundo = caixa local.
        var rs = kitGo.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return;

        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);

        float y = b.min.y - 0.003f;
        float lx = b.size.x + 0.06f, lz = b.size.z + 0.06f;

        // Contorno brilhante: 4 filetes finos nas bordas.
        const float fio = 0.004f;
        var lados = new[]
        {
            (new Vector3(0f, 0f, lz / 2f), new Vector3(lx, fio, fio)),
            (new Vector3(0f, 0f, -lz / 2f), new Vector3(lx, fio, fio)),
            (new Vector3(lx / 2f, 0f, 0f), new Vector3(fio, fio, lz)),
            (new Vector3(-lx / 2f, 0f, 0f), new Vector3(fio, fio, lz))
        };

        foreach (var (pos, tam) in lados)
        {
            var borda = NovaForma(PrimitiveType.Cube, "BandejaBorda", ajuste, out var bordaT);
            borda.material.color = CorBordaBandeja;
            bordaT.localPosition = new Vector3(b.center.x, y, b.center.z) + pos;
            bordaT.localScale = tam;
        }
    }

    // A posicao do kit em relacao ao QR fica salva (ajuste com os analogicos + Y).
    void CarregarPosicaoKit()
    {
        if (PlayerPrefs.GetInt("v3_ok", 0) != 1) return;

        ajuste.localPosition = new Vector3(
            PlayerPrefs.GetFloat("v3_x", 0f),
            PlayerPrefs.GetFloat("v3_y", 0f),
            PlayerPrefs.GetFloat("v3_z", 0f)
        );
        ajuste.localRotation = Quaternion.Euler(0f, PlayerPrefs.GetFloat("v3_giro", 0f), 0f);
    }

    void SalvarPosicaoKit()
    {
        PlayerPrefs.SetInt("v3_ok", 1);
        PlayerPrefs.SetFloat("v3_x", ajuste.localPosition.x);
        PlayerPrefs.SetFloat("v3_y", ajuste.localPosition.y);
        PlayerPrefs.SetFloat("v3_z", ajuste.localPosition.z);
        PlayerPrefs.SetFloat("v3_giro", ajuste.localEulerAngles.y);
        PlayerPrefs.Save();

        PosicionarBotoes();
        Mostrar("Posicao do kit salva");
    }

    void ApagarPosicaoKit()
    {
        foreach (var k in new[] { "v3_ok", "v3_x", "v3_y", "v3_z", "v3_giro" })
            PlayerPrefs.DeleteKey(k);

        PlayerPrefs.Save();
        ZerarAjuste();
        Mostrar("Posicao salva apagada");
    }

    void ZerarAjuste()
    {
        ajuste.localPosition = Vector3.zero;
        ajuste.localRotation = Quaternion.identity;
        PosicionarBotoes();
    }

    void Mostrar(string texto)
    {
        mensagem = texto;
        mensagemAte = Time.time + 3f;
    }

    // ---------- Loop ----------
    void Update()
    {
        LerControles();
        AtualizarPainel();
        AtualizarBoasVindas();

        if (fecharVirtualEm > 0f && Time.time > fecharVirtualEm)
        {
            fecharVirtualEm = -1f;
            if (kit != null && kit.CancelaAberta) fluxo.Mostrar("FECHAMENTO AUTOMATICO");
        }
    }

    void LateUpdate()
    {
        if (qrAtual != null) AlinharAoQR(qrAtual.transform);

        if (!raiz.gameObject.activeSelf) return;
        if (cabeca == null) cabeca = Cabeca();

        AtualizarAltura();

        foreach (var e in etiquetas)
        {
            Vector3 a = e.ancora.position;
            Vector3 p = a + Vector3.up * AlturaEtiqueta;

            e.raiz.position = p;

            if (cabeca != null)
            {
                Vector3 dirOlho = p - cabeca.position;

                if (dirOlho.sqrMagnitude > 0.0001f)
                    e.raiz.rotation = Quaternion.LookRotation(dirOlho, Vector3.up);
            }

            if (e.linhaT != null)
            {
                Vector3 d = p - a;
                float len = d.magnitude;

                if (len > 0.001f)
                {
                    e.linhaT.position = a + d * 0.5f;
                    e.linhaT.rotation = Quaternion.LookRotation(d, Vector3.up);
                    e.linhaT.localScale = new Vector3(0.0025f, 0.0025f, len);
                }

                e.ponto.transform.position = a;
            }
        }

        if (fundosPendentes) AjustarFundos();
    }

    // O QR fica deitado na mesa. So usa a posicao dele e o "giro" na horizontal,
    // assim o kit fica sempre reto sobre a mesa, qualquer que seja o eixo do QR.
    void AlinharAoQR(Transform qr)
    {
        Vector3 direita = Vector3.ProjectOnPlane(qr.right, Vector3.up);

        if (direita.sqrMagnitude < 0.0001f) return;

        // O kit e montado com os cartoes em -Z: o +Z da raiz aponta para longe de
        // quem le o QR, e o leitor fica a esquerda dessa pessoa.
        Vector3 longe = Vector3.Cross(direita.normalized, Vector3.up);
        Vector3 p = qr.position + longe * AlemDoQR;
        p.y = AlturaAlvo(qr.position.y + 0.6f);

        raiz.SetPositionAndRotation(p, Inclinado(longe));
    }

    // Fundo do kit levantado, frente (cartoes) mais perto e mais baixa.
    static Quaternion Inclinado(Vector3 longe)
    {
        return Quaternion.LookRotation(longe, Vector3.up) * Quaternion.Euler(-InclinacaoKit, 0f, 0f);
    }

    float AlturaAlvo(float semCabeca)
    {
        if (!float.IsNaN(alturaKit)) return alturaKit;
        return cabeca != null ? cabeca.position.y - AbaixoDosOlhos : semCabeca;
    }

    void AtualizarAltura()
    {
        if (cabeca == null) return;

        float alvo = cabeca.position.y - AbaixoDosOlhos;

        if (float.IsNaN(alturaKit))
            alturaKit = alvo;
        else if (Mathf.Abs(alvo - alturaKit) > ZonaMortaAltura)
            alturaKit = Mathf.MoveTowards(alturaKit, alvo, VelocidadeAltura * Time.deltaTime);

        Vector3 p = raiz.position;
        raiz.position = new Vector3(p.x, alturaKit, p.z);
    }

    // ---------- Controles do apresentador ----------
    // Gatilhos e grips ficam livres para pegar os cartoes e apertar botoes.
    void LerControles()
    {
        // Menu (controle esquerdo): simula um cartao, alternando liberado e negado.
        if (OVRInput.GetDown(OVRInput.Button.Start))
            Simular(proximaSimulacaoNegada);

#if UNITY_EDITOR
        var teclado = UnityEngine.InputSystem.Keyboard.current;

        if (teclado != null)
        {
            if (teclado.digit1Key.wasPressedThisFrame) Simular(false);
            if (teclado.digit2Key.wasPressedThisFrame) Simular(true);
        }
#endif

        if (OVRInput.GetDown(OVRInput.Button.One))
            PosicionarManual();

        if (OVRInput.GetDown(OVRInput.Button.Two) && painel != null)
            painel.gameObject.SetActive(!painel.gameObject.activeSelf);

        if (OVRInput.GetDown(OVRInput.Button.Three))
            ZerarAjuste();

        if (OVRInput.GetDown(OVRInput.Button.Four))
            SalvarPosicaoKit();

        if (OVRInput.GetDown(OVRInput.Button.SecondaryThumbstick))
            ProximoPasso();

        if (OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick))
            ApagarPosicaoKit();

        if (modo == Modo.Aguardando) return;

        if (cabeca == null) cabeca = Cabeca();
        if (cabeca == null) return;

        Vector2 esq = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);
        Vector2 dir = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick);
        float dt = Time.deltaTime;

        Vector3 fr = Vector3.ProjectOnPlane(cabeca.forward, Vector3.up);

        if (fr.sqrMagnitude < 0.01f)
            fr = Vector3.forward;

        fr.Normalize();

        Vector3 dr = Vector3.Cross(Vector3.up, fr);

        Vector3 d = (
            dr * Zona(dir.x) +
            fr * Zona(dir.y) +
            Vector3.up * Zona(esq.y)
        ) * velocidadeMovimento * dt;

        if (d.sqrMagnitude > 0f || Zona(esq.x) != 0f)
        {
            ajuste.position += d;
            ajuste.Rotate(Vector3.up, Zona(esq.x) * velocidadeRotacao * dt, Space.World);
            PosicionarBotoes();
        }
    }

    float Zona(float v)
    {
        return Mathf.Abs(v) < 0.2f ? 0f : v;
    }

    void ProximoPasso()
    {
        passo++;

        if (passo >= etiquetas.Count)
            passo = -1;

        AplicarVisual();
    }

    void AplicarVisual()
    {
        for (int i = 0; i < etiquetas.Count; i++)
        {
            var e = etiquetas[i];

            bool ativo = passo == i;
            bool semPasso = passo < 0;

            // No passo a passo, a etiqueta da vez mostra a explicacao.
            e.texto.text = ativo ? e.titulo + "\n" + e.explicacao : e.titulo;
            e.texto.color = semPasso || ativo ? Color.white : CinzaApagado;

            Color cor = semPasso ? Ciano : (ativo ? AmareloPasso : CinzaApagado);

            if (e.borda != null) e.borda.material.color = cor;
            if (e.linha != null) e.linha.material.color = cor;
            if (e.ponto != null) e.ponto.material.color = cor;
        }

        fundosPendentes = true;
    }

    Transform Cabeca()
    {
        var rig = FindFirstObjectByType<OVRCameraRig>();

        if (rig != null)
            return rig.centerEyeAnchor;

        return Camera.main != null ? Camera.main.transform : null;
    }

    // Plano B sem QR: kit na frente da pessoa, um pouco abaixo da linha dos olhos.
    void PosicionarManual()
    {
        if (cabeca == null) cabeca = Cabeca();
        if (cabeca == null) return;

        Vector3 frente = Vector3.ProjectOnPlane(cabeca.forward, Vector3.up);

        if (frente.sqrMagnitude < 0.01f)
            frente = Vector3.forward;

        frente.Normalize();

        qrAtual = null;

        alturaKit = cabeca.position.y - AbaixoDosOlhos;
        raiz.position = new Vector3(0f, alturaKit, 0f) + Vector3.ProjectOnPlane(cabeca.position, Vector3.up) + frente * DistanciaManual;
        raiz.rotation = Inclinado(frente);

        Mostrar(raiz.gameObject.activeSelf ? "Kit reposicionado" : "Kit posicionado");
        Ativar(Modo.Manual);
    }

    void Ativar(Modo novo)
    {
        bool primeiraVez = !raiz.gameObject.activeSelf;

        modo = novo;
        raiz.gameObject.SetActive(true);

        if (primeiraVez && kit != null) kit.DevolverCartoes();

        PosicionarBotoes();
    }

    // Painel de botoes fica parado ao lado do kit, virado para a pessoa.
    // Nao segue a cabeca, para nao fugir do dedo na hora de apertar.
    void PosicionarBotoes()
    {
        if (botoesSimulacao == null || kit == null || !raiz.gameObject.activeSelf) return;
        if (cabeca == null) cabeca = Cabeca();
        if (cabeca == null) return;

        Vector3 centro = Vector3.zero;
        foreach (var a in kit.Ancoras) centro += a.position;
        centro /= kit.Ancoras.Length;

        // O +Z do kit aponta para longe de quem esta na frente dele.
        Vector3 paraPessoa = Vector3.ProjectOnPlane(-ajuste.forward, Vector3.up).normalized;
        Vector3 direitaPessoa = Vector3.Cross(paraPessoa, Vector3.up);

        Vector3 p = centro + direitaPessoa * LadoBotoes + paraPessoa * DistanciaBotoes + Vector3.up * AlturaBotoes;

        // Filho do kit: acompanha quando a altura se ajusta ao visitante.
        if (botoesSimulacao.parent != ajuste) botoesSimulacao.SetParent(ajuste, true);

        botoesSimulacao.position = p;
        botoesSimulacao.rotation = Quaternion.LookRotation(p - cabeca.position, Vector3.up);
        botoesSimulacao.gameObject.SetActive(true);
    }

    // ---------- ESP32, cartao virtual e animacao ----------
    void CriarFluxo()
    {
        fluxo = gameObject.AddComponent<FluxoAcesso>();
        fluxo.alturaPainel = 0.22f;   // kit ja esta perto da linha dos olhos
        fluxo.tamanhoTexto = 0.0026f;
        fluxo.kit = kit;

        Transform[] ancoras = kit != null ? kit.Ancoras : new Transform[0];
        fluxo.Configurar(ajuste, ancoras, matBase, fonte, modeloCadeado);

        esp = gameObject.AddComponent<EspCliente>();
        esp.NovoEvento += OnEventoEsp;
    }

    // Evento do kit REAL (cartao passado no leitor de verdade).
    void OnEventoEsp(string status)
    {
        ultimoEsp = status;
        fecharVirtualEm = -1f;   // quem manda na cancela agora e o ESP32
        fluxo.Mostrar(status);
    }

    // Cartao VIRTUAL encostado no leitor virtual: mesma logica do ESP32.
    void OnCartaoVirtual(bool liberado)
    {
        string status = StatusDoCartao(liberado, kit.CancelaAberta);

        fecharVirtualEm = status == "ENTRADA ABERTA" ? Time.time + TempoFechamentoVirtual : -1f;
        fluxo.Mostrar(status);
    }

    // Mesma regra do ESP32 (processarCartao): negado nao mexe na cancela;
    // liberado abre, ou fecha se ja estiver aberta.
    public static string StatusDoCartao(bool liberado, bool cancelaAberta)
    {
        if (!liberado) return "NAO CADASTRADO";
        return cancelaAberta ? "ENTRADA FECHADA" : "ENTRADA ABERTA";
    }

    // Botoes "Simular" e botao Menu.
    public void Simular(bool negado)
    {
        proximaSimulacaoNegada = !negado;

        if (!raiz.gameObject.activeSelf)
        {
            Mostrar("Posicione o kit (QR ou botao A) para ver a animacao");
            return;
        }

        if (kit != null) OnCartaoVirtual(!negado);
    }

    // ---------- QR Code ----------
    void OnTrackableAdded(MRUKTrackable trackable)
    {
        totalDetectados++;
        ultimoTipo = trackable.TrackableType.ToString();

        if (trackable.TrackableType != OVRAnchor.TrackableType.QRCode)
            return;

        string payload = trackable.MarkerPayloadString;

        ultimoPayload = string.IsNullOrEmpty(payload) ? "(vazio)" : payload;

        Debug.Log("QR detectado: " + payload);

        if (string.IsNullOrEmpty(payload) || payload.Trim() != payloadEsperado)
            return;

        qrAtual = trackable;
        AlinharAoQR(trackable.transform);
        Ativar(Modo.QR);
    }

    void OnTrackableRemoved(MRUKTrackable trackable)
    {
        if (qrAtual == trackable)
        {
            qrAtual = null;
            modo = Modo.Manual;
        }
    }

    // ---------- Etiquetas (estilo da foto de referencia) ----------
    Renderer NovaForma(PrimitiveType tipo, string nome, Transform pai, out Transform t)
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

    void CriarEtiquetas()
    {
        if (kit == null) return;

        string[] titulos = { "LEITOR NFC", "ESP32", "LCD", "SERVO" };
        string[] explicacoes =
        {
            "Le o cartao e\nenvia o codigo dele.",
            "Recebe o codigo e\ndecide se autoriza.",
            "Mostra o resultado:\nliberado ou negado.",
            "Abre a cancela quando\no acesso e liberado."
        };

        for (int i = 0; i < titulos.Length; i++)
        {
            var e = new Etiqueta
            {
                titulo = titulos[i],
                explicacao = explicacoes[i],
                ancora = kit.Ancoras[i]
            };

            var go = new GameObject("Etiqueta_" + e.titulo);
            go.transform.SetParent(ajuste, false);
            e.raiz = go.transform;

            e.texto = ConfigurarTexto(go, e.titulo, TamanhoEtiqueta);
            e.texto.fontStyle = FontStyle.Bold;

            if (matBase != null)
            {
                e.fundo = NovaForma(PrimitiveType.Quad, "Fundo", go.transform, out e.fundoT);
                e.fundo.material.color = FundoEtiqueta;
                e.borda = NovaForma(PrimitiveType.Quad, "Borda", go.transform, out e.bordaT);
                e.linha = NovaForma(PrimitiveType.Cube, "Linha", ajuste, out e.linhaT);
                e.ponto = NovaForma(PrimitiveType.Sphere, "Ponto", ajuste, out var pontoT);
                pontoT.localScale = Vector3.one * 0.012f;
            }

            etiquetas.Add(e);
        }
    }

    void AjustarFundos()
    {
        bool tudoPronto = true;

        foreach (var e in etiquetas)
        {
            if (e.fundoT == null) continue;

            Bounds b = e.texto.GetComponent<MeshRenderer>().localBounds;

            if (b.size.x < 0.001f)
            {
                tudoPronto = false;
                continue;
            }

            Vector3 tam = new Vector3(b.size.x + 0.035f, b.size.y + 0.02f, 1f);

            e.fundoT.localPosition = new Vector3(b.center.x, b.center.y, 0.002f);
            e.fundoT.localScale = tam;

            // Borda = mesmo retangulo um pouco maior, atras do fundo.
            e.bordaT.localPosition = new Vector3(b.center.x, b.center.y, 0.003f);
            e.bordaT.localScale = tam + new Vector3(0.005f, 0.005f, 0f);
        }

        if (tudoPronto)
            fundosPendentes = false;
    }

    TextMesh ConfigurarTexto(GameObject go, string texto, float tamanho)
    {
        var tm = go.AddComponent<TextMesh>();

        tm.font = fonte;
        tm.text = texto;
        tm.fontSize = 80;
        tm.characterSize = tamanho;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = Color.white;

        go.GetComponent<MeshRenderer>().sharedMaterial = fonte.material;

        return tm;
    }

    // ---------- Painel de diagnostico ----------
    void CriarPainel()
    {
        if (cabeca == null) return;

        var go = new GameObject("PainelDiagnostico");

        go.transform.SetParent(cabeca, false);
        go.transform.localPosition = new Vector3(0f, -0.25f, 1.0f);

        painel = ConfigurarTexto(go, "iniciando...", TamanhoPainel);
        painel.gameObject.SetActive(false);   // so para o apresentador: botao B
    }

    // ---------- Boas-vindas e modo vitrine ----------
    void AtualizarBoasVindas()
    {
        if (fluxo == null || !raiz.gameObject.activeSelf)
        {
            if (boasVindas != null) boasVindas.gameObject.SetActive(false);
            ultimaAtividade = Time.time;
            return;
        }

        if (boasVindas == null) CriarBoasVindas();

        bool livre = !fluxo.Ocupado;
        boasVindas.gameObject.SetActive(livre);

        if (livre && cabeca != null)
        {
            // Logo acima do kit (nao cobre as pecas), virado para a pessoa.
            boasVindas.position = ajuste.position + Vector3.up * (fluxo.alturaPainel + 0.09f);
            boasVindas.rotation = Quaternion.LookRotation(boasVindas.position - cabeca.position, Vector3.up);

            var c = CorPassos;
            c.a = 0.65f + 0.35f * Mathf.Sin(Time.time * 3f);
            passosBoasVindas.color = c;
        }

        if (!livre) ultimaAtividade = Time.time;
        else if (Time.time - ultimaAtividade > TempoVitrine) Simular(proximaSimulacaoNegada);
    }

    void CriarBoasVindas()
    {
        boasVindas = new GameObject("BoasVindas").transform;
        boasVindas.SetParent(ajuste, false);

        float s = fluxo.tamanhoTexto * 1.6f;
        Linha("CONTROLE DE ACESSO POR NFC", s * 1.25f, 0.052f, CorTitulo, FontStyle.Bold);
        Linha("Veja o sinal viajar pelos fios", s * 0.8f, 0.012f, CorTextoSuave, FontStyle.Normal);
        passosBoasVindas = Linha("1  Pegue um cartao      2  Encoste no LEITOR", s * 0.95f, -0.035f, CorPassos, FontStyle.Bold);

        // Cartao escuro quase opaco (le melhor no passthrough) com filete azul em cima.
        var fundo = NovaForma(PrimitiveType.Quad, "Fundo", boasVindas, out var fundoT);
        fundo.material.color = new Color(0.03f, 0.07f, 0.1f, 0.95f);
        fundoT.localPosition = new Vector3(0f, 0.01f, 0.003f);
        fundoT.localScale = new Vector3(0.52f, 0.14f, 1f);

        var faixa = NovaForma(PrimitiveType.Quad, "Faixa", boasVindas, out var faixaT);
        faixa.material.color = CorBordaBandeja;
        faixaT.localPosition = new Vector3(0f, 0.08f, 0.002f);
        faixaT.localScale = new Vector3(0.52f, 0.005f, 1f);
    }

    TextMesh Linha(string texto, float tamanho, float y, Color cor, FontStyle estilo)
    {
        var go = new GameObject("Linha");
        go.transform.SetParent(boasVindas, false);
        go.transform.localPosition = new Vector3(0f, y, 0f);
        var t = ConfigurarTexto(go, texto, tamanho);
        t.color = cor;
        t.fontStyle = estilo;
        return t;
    }

    bool TemPermissaoCena()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return UnityEngine.Android.Permission.HasUserAuthorizedPermission(OVRPermissionsRequester.ScenePermission);
#else
        return true;
#endif
    }

    static string V(Vector3 v)
    {
        return "("
            + v.x.ToString("0.0") + ","
            + v.y.ToString("0.0") + ","
            + v.z.ToString("0.0") + ")";
    }

    void AtualizarPainel()
    {
        if (painel == null || Time.time < proximaAtualizacao)
            return;

        proximaAtualizacao = Time.time + 0.5f;

        var sb = new StringBuilder();

        sb.AppendLine("MRUK: " + (MRUK.Instance != null ? "ok" : "NAO ENCONTRADO"));

        if (MRUK.Instance != null)
        {
            sb.AppendLine(
                "QR suportado: "
                + (MRUK.Instance.QRCodeTrackingSupported ? "sim" : "NAO")
            );
        }

        sb.AppendLine("Permissao de cena: " + (TemPermissaoCena() ? "sim" : "NAO"));

        sb.AppendLine(
            "ESP32: " + (esp != null && esp.Conectado ? "conectado" : "desconectado")
            + " | ultimo: " + ultimoEsp
        );
        sb.AppendLine("Detectados: " + totalDetectados + " | tipo: " + ultimoTipo);
        sb.AppendLine("Ultimo texto: " + ultimoPayload);

        sb.AppendLine(
            "Modo: " + modo
            + " | passo: " + (passo < 0 ? "todos" : (passo + 1).ToString())
            + " | cancela: " + (kit != null && kit.CancelaAberta ? "aberta" : "fechada")
        );

        if (qrAtual != null)
        {
            var t = qrAtual.transform;

            sb.AppendLine("QR up " + V(t.up) + " frente " + V(t.forward));
        }

        if (Time.time < mensagemAte)
            sb.AppendLine(">>> " + mensagem);

        sb.AppendLine("A=posicionar Y=salvar X=zerar B=painel");
        sb.AppendLine("Analogicos=mover kit  Clique dir=passo a passo");
        sb.AppendLine("Clique esq=apagar posicao  Menu=simular cartao");

        painel.text = sb.ToString();
    }
}
