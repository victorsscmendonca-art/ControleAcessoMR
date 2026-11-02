using System.Collections.Generic;
using System.Text;
using Meta.XR.MRUtilityKit;
using UnityEngine;

public class ControleAcessoMR : MonoBehaviour
{
    class Item
    {
        public string titulo;
        public string explicacao;
        public Vector3 padrao;
        public Vector3 pos;
        public Vector3 tamanho;
        public Transform ancora;
        public TextMesh texto;
        public Renderer fundo;
        public Transform fundoT;
        public Renderer caixa;
        public Transform caixaT;
        public Renderer linha;
        public Transform linhaT;
    }

    enum Modo { Aguardando, QR, Manual }

    const float TamanhoBase = 0.001f;
    const float TamanhoPainel = 0.004f;
    const float AlturaEtiqueta = 0.10f;

    [Header("QR Code")]
    public string payloadEsperado = "CONTROLE_ACESSO_MR";

    [Header("Ajuste")]
    public float velocidadeMovimento = 0.15f;
    public float velocidadeRotacao = 40f;

    [Header("Diagnostico")]
    public bool mostrarPainel = true;

    readonly List<Item> itens = new List<Item>();
    Font fonte;
    Material matBase;
    TextMesh painel;
    Transform raiz, ajuste, cabeca;
    MRUKTrackable qrAtual;
    Modo modo = Modo.Aguardando;
    int passo = -1;
    int alvo = -1;
    float escala = 1f;
    float escalaCaixa = 1f;
    bool fundosPendentes = true;
    int totalDetectados;
    string ultimoTipo = "(nenhum)";
    string ultimoPayload = "(nenhum)";
    string mensagem = "";
    float mensagemAte;
    float proximaAtualizacao;

    EspCliente esp;
    FluxoAcesso fluxo;
    bool proximaSimulacaoNegada;
    string ultimoEsp = "(nenhum)";

    static readonly Color FundoNormal = new Color(0f, 0f, 0f, 0.65f);
    static readonly Color FundoAlvo = new Color(0.1f, 0.35f, 0.9f, 0.85f);
    static readonly Color Ciano = new Color(0.2f, 0.8f, 1f, 0.22f);
    static readonly Color Amarelo = new Color(1f, 0.9f, 0.1f, 0.40f);
    static readonly Color Cinza = new Color(0.3f, 0.3f, 0.3f, 0.10f);
    static readonly Color Azul = new Color(0.2f, 0.5f, 1f, 0.45f);

    void Start()
    {
        fonte = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        cabeca = Cabeca();

        var sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("UI/Default");
        if (sh != null) matBase = new Material(sh);

        CriarItensPadrao();
        CarregarCalibracao();
        CriarRaizEEtiquetas();
        CriarFluxo();

        if (mostrarPainel) CriarPainel();

        AplicarEscala();
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

        if (MRUK.Instance != null)
        {
            MRUK.Instance.SceneSettings.TrackableAdded.RemoveListener(OnTrackableAdded);
            MRUK.Instance.SceneSettings.TrackableRemoved.RemoveListener(OnTrackableRemoved);
        }
    }

    // POSICOES PROVISORIAS: a calibracao no oculos substitui estes valores.
    void CriarItensPadrao()
    {
        Adicionar(
            "1. RC522 (leitor RFID)",
            "Le o cartao e\nenvia o codigo dele.",
            -0.30f,
            new Vector3(0.06f, 0.015f, 0.04f)
        );

        Adicionar(
            "2. ESP32",
            "Recebe o codigo e\ndecide se autoriza.",
            -0.10f,
            new Vector3(0.05f, 0.015f, 0.03f)
        );

        Adicionar(
            "3. LCD",
            "Mostra o resultado:\nautorizado ou negado.",
            0.10f,
            new Vector3(0.08f, 0.02f, 0.04f)
        );

        Adicionar(
            "4. Servo (porta)",
            "Abre a porta quando\no acesso e autorizado.",
            0.30f,
            new Vector3(0.04f, 0.03f, 0.03f)
        );
    }

    void Adicionar(string titulo, string explicacao, float x, Vector3 tamanho)
    {
        var p = new Vector3(x, 0.04f, 0.04f);

        itens.Add(new Item
        {
            titulo = titulo,
            explicacao = explicacao,
            padrao = p,
            pos = p,
            tamanho = tamanho
        });
    }

    // ---------- Calibracao salva ----------
    void CarregarCalibracao()
    {
        if (PlayerPrefs.GetInt("v2_ok", 0) != 1) return;

        escala = PlayerPrefs.GetFloat("v2_escala", 1f);
        escalaCaixa = PlayerPrefs.GetFloat("v2_caixa", 1f);

        for (int i = 0; i < itens.Count; i++)
        {
            itens[i].pos = new Vector3(
                PlayerPrefs.GetFloat("v2_x" + i, itens[i].padrao.x),
                PlayerPrefs.GetFloat("v2_y" + i, itens[i].padrao.y),
                PlayerPrefs.GetFloat("v2_z" + i, itens[i].padrao.z)
            );
        }
    }

    void SalvarCalibracao()
    {
        var novas = new List<Vector3>();

        foreach (var it in itens)
            novas.Add(raiz.InverseTransformPoint(it.ancora.position));

        ajuste.localPosition = Vector3.zero;
        ajuste.localRotation = Quaternion.identity;

        PlayerPrefs.SetInt("v2_ok", 1);
        PlayerPrefs.SetFloat("v2_escala", escala);
        PlayerPrefs.SetFloat("v2_caixa", escalaCaixa);

        for (int i = 0; i < itens.Count; i++)
        {
            itens[i].pos = novas[i];
            itens[i].ancora.localPosition = novas[i];

            PlayerPrefs.SetFloat("v2_x" + i, novas[i].x);
            PlayerPrefs.SetFloat("v2_y" + i, novas[i].y);
            PlayerPrefs.SetFloat("v2_z" + i, novas[i].z);
        }

        PlayerPrefs.Save();
        Mostrar("Calibracao salva");
    }

    void ApagarCalibracao()
    {
        PlayerPrefs.DeleteKey("v2_ok");
        PlayerPrefs.DeleteKey("v2_escala");
        PlayerPrefs.DeleteKey("v2_caixa");

        for (int i = 0; i < itens.Count; i++)
        {
            PlayerPrefs.DeleteKey("v2_x" + i);
            PlayerPrefs.DeleteKey("v2_y" + i);
            PlayerPrefs.DeleteKey("v2_z" + i);
        }

        PlayerPrefs.Save();

        ajuste.localPosition = Vector3.zero;
        ajuste.localRotation = Quaternion.identity;
        escala = 1f;
        escalaCaixa = 1f;

        foreach (var it in itens)
        {
            it.pos = it.padrao;
            it.ancora.localPosition = it.padrao;
        }

        AplicarEscala();
        Mostrar("Calibracao apagada");
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
    }

    void LateUpdate()
    {
        if (qrAtual != null)
        {
            raiz.SetPositionAndRotation(
                qrAtual.transform.position,
                qrAtual.transform.rotation
            );
        }

        if (!raiz.gameObject.activeSelf) return;
        if (cabeca == null) cabeca = Cabeca();

        foreach (var it in itens)
        {
            Vector3 a = it.ancora.position;
            Vector3 p = a + Vector3.up * AlturaEtiqueta;

            it.texto.transform.position = p;

            if (cabeca != null)
            {
                Vector3 dirOlho = p - cabeca.position;

                if (dirOlho.sqrMagnitude > 0.0001f)
                {
                    it.texto.transform.rotation = Quaternion.LookRotation(
                        dirOlho,
                        Vector3.up
                    );
                }
            }

            if (it.linhaT != null)
            {
                Vector3 d = p - a;
                float len = d.magnitude;

                if (len > 0.001f)
                {
                    it.linhaT.position = a + d * 0.5f;
                    it.linhaT.rotation = Quaternion.LookRotation(d, Vector3.up);
                    it.linhaT.localScale = new Vector3(0.003f, 0.003f, len);
                }
            }
        }

        if (fundosPendentes) AjustarFundos();
    }

    // ---------- Controles ----------
    void LerControles()
    {
        // Botao Menu (controle esquerdo): simula o cartao,
        // alternando autorizado e negado. Plano B sem o ESP32.
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
        {
            ajuste.localPosition = Vector3.zero;
            ajuste.localRotation = Quaternion.identity;
        }

        if (OVRInput.GetDown(OVRInput.Button.Four))
            SalvarCalibracao();

        if (OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger))
            ProximoPasso();

        if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger))
            ProximoAlvo();

        if (OVRInput.GetDown(OVRInput.Button.SecondaryHandTrigger))
            MudarEscala(1.25f);

        if (OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger))
            MudarEscala(0.8f);

        if (OVRInput.GetDown(OVRInput.Button.SecondaryThumbstick))
            MudarCaixa();

        if (OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick))
            ApagarCalibracao();

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

        if (alvo >= 0)
        {
            itens[alvo].ancora.position += d;
        }
        else
        {
            ajuste.position += d;

            ajuste.Rotate(
                Vector3.up,
                Zona(esq.x) * velocidadeRotacao * dt,
                Space.World
            );
        }
    }

    float Zona(float v)
    {
        return Mathf.Abs(v) < 0.2f ? 0f : v;
    }

    void ProximoPasso()
    {
        passo++;

        if (passo >= itens.Count)
            passo = -1;

        AplicarVisual();
    }

    void ProximoAlvo()
    {
        alvo++;

        if (alvo >= itens.Count)
            alvo = -1;

        AplicarVisual();
    }

    void MudarEscala(float fator)
    {
        escala = Mathf.Clamp(escala * fator, 0.3f, 5f);

        AplicarEscala();
        AplicarVisual();
    }

    void MudarCaixa()
    {
        escalaCaixa *= 1.25f;

        if (escalaCaixa > 5f)
            escalaCaixa = 0.4f;

        AplicarEscala();
    }

    void AplicarEscala()
    {
        foreach (var it in itens)
        {
            if (it.caixaT != null)
                it.caixaT.localScale = it.tamanho * escalaCaixa;
        }

        fundosPendentes = true;
    }

    void AplicarVisual()
    {
        for (int i = 0; i < itens.Count; i++)
        {
            var it = itens[i];

            bool ativo = (passo == i);
            bool semPasso = (passo < 0);

            it.texto.color = semPasso
                ? Color.white
                : (ativo ? Color.yellow : new Color(0.6f, 0.6f, 0.6f));

            it.texto.characterSize = TamanhoBase * escala * (ativo ? 1.3f : 1f);

            if (it.fundo != null)
                it.fundo.material.color = (i == alvo) ? FundoAlvo : FundoNormal;

            if (it.caixa != null)
            {
                Color cc = semPasso ? Ciano : (ativo ? Amarelo : Cinza);

                it.caixa.material.color = (i == alvo) ? Azul : cc;
            }

            if (it.linha != null)
            {
                Color lc = semPasso
                    ? new Color(0.2f, 0.8f, 1f, 0.8f)
                    : (
                        ativo
                            ? new Color(1f, 0.9f, 0.1f, 0.9f)
                            : new Color(0.4f, 0.4f, 0.4f, 0.3f)
                    );

                it.linha.material.color = lc;
            }
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

    void PosicionarManual()
    {
        if (cabeca == null) cabeca = Cabeca();
        if (cabeca == null) return;

        Vector3 frente = Vector3.ProjectOnPlane(cabeca.forward, Vector3.up);

        if (frente.sqrMagnitude < 0.01f)
            frente = Vector3.forward;

        frente.Normalize();

        qrAtual = null;

        raiz.position = cabeca.position + frente * 0.8f + Vector3.down * 0.25f;
        raiz.rotation = Quaternion.LookRotation(frente, Vector3.up);

        ajuste.localPosition = Vector3.zero;
        ajuste.localRotation = Quaternion.identity;

        raiz.gameObject.SetActive(true);
        modo = Modo.Manual;
    }

    // ---------- ESP32 e animacao ----------
    void CriarFluxo()
    {
        var ancoras = new Transform[itens.Count];

        for (int i = 0; i < itens.Count; i++)
            ancoras[i] = itens[i].ancora;

        fluxo = gameObject.AddComponent<FluxoAcesso>();
        fluxo.Configurar(ajuste, ancoras, matBase, fonte);

        esp = gameObject.AddComponent<EspCliente>();
        esp.NovoEvento += OnEventoEsp;
    }

    void OnEventoEsp(string status)
    {
        ultimoEsp = status;
        fluxo.Mostrar(status);
    }

    public void Simular(bool negado)
    {
        proximaSimulacaoNegada = !negado;
        fluxo.Mostrar(negado ? "NAO CADASTRADO" : "ENTRADA ABERTA");

        if (!raiz.gameObject.activeSelf)
            Mostrar("Posicione as etiquetas (A ou QR) para ver a animacao");
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

        raiz.SetPositionAndRotation(
            trackable.transform.position,
            trackable.transform.rotation
        );

        raiz.gameObject.SetActive(true);
        modo = Modo.QR;
    }

    void OnTrackableRemoved(MRUKTrackable trackable)
    {
        if (qrAtual == trackable)
        {
            qrAtual = null;
            modo = Modo.Manual;
        }
    }

    // ---------- Criacao dos objetos ----------
    Renderer NovaForma(
        PrimitiveType tipo,
        string nome,
        Transform pai,
        out Transform t
    )
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

    void CriarRaizEEtiquetas()
    {
        raiz = new GameObject("RaizControleAcesso").transform;

        ajuste = new GameObject("Ajuste").transform;
        ajuste.SetParent(raiz, false);

        foreach (var it in itens)
        {
            var a = new GameObject("Ancora_" + it.titulo).transform;

            a.SetParent(ajuste, false);
            a.localPosition = it.pos;
            it.ancora = a;

            var go = new GameObject("Etiqueta_" + it.titulo);

            go.transform.SetParent(ajuste, false);

            it.texto = ConfigurarTexto(
                go,
                it.titulo + "\n" + it.explicacao,
                TamanhoBase * escala
            );

            if (matBase != null)
            {
                it.fundo = NovaForma(
                    PrimitiveType.Quad,
                    "Fundo",
                    go.transform,
                    out it.fundoT
                );

                it.caixa = NovaForma(
                    PrimitiveType.Cube,
                    "Caixa",
                    a,
                    out it.caixaT
                );

                it.linha = NovaForma(
                    PrimitiveType.Cube,
                    "Linha",
                    ajuste,
                    out it.linhaT
                );
            }
        }

        raiz.gameObject.SetActive(false);
    }

    void AjustarFundos()
    {
        bool tudoPronto = true;

        foreach (var it in itens)
        {
            if (it.fundoT == null) continue;

            Bounds b = it.texto.GetComponent<MeshRenderer>().localBounds;

            if (b.size.x < 0.001f)
            {
                tudoPronto = false;
                continue;
            }

            it.fundoT.localPosition = new Vector3(
                b.center.x,
                b.center.y,
                0.002f
            );

            it.fundoT.localScale = new Vector3(
                b.size.x + 0.03f,
                b.size.y + 0.02f,
                1f
            );
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
        );

        sb.AppendLine(
            "Mover: " + (alvo < 0 ? "grupo todo" : "item " + (alvo + 1))
            + " | texto x" + escala.ToString("0.0")
            + " | caixa x" + escalaCaixa.ToString("0.0")
        );

        if (qrAtual != null)
        {
            var t = qrAtual.transform;

            sb.AppendLine("QR up " + V(t.up) + " frente " + V(t.forward));
        }

        if (Time.time < mensagemAte)
            sb.AppendLine(">>> " + mensagem);

        sb.AppendLine("A=posicionar Y=salvar X=zerar B=painel");
        sb.AppendLine("Gat.dir=passo Gat.esq=trocar item");
        sb.AppendLine("Grip=texto +/- Clique dir=caixa Clique esq=apagar");
        sb.AppendLine("Menu=simular cartao (autorizado/negado)");

        painel.text = sb.ToString();
    }
}
