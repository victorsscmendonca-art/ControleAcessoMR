using System.Collections;
using UnityEngine;

// Animacao do caminho do sinal (leitor -> ESP32 -> LCD -> servo)
// e do painel de resultado flutuando acima do kit.
public class FluxoAcesso : MonoBehaviour
{
    // Indices das ancoras, na ordem criada em ControleAcessoMR.
    const int Leitor = 0, Esp = 1, Lcd = 2, Servo = 3;

    const float DuracaoTrecho = 0.45f;
    const float TempoPainel = 4f;
    const float TamanhoPulso = 0.02f;

    // Ajustados por quem configura (o kit virtual e maior que as etiquetas antigas).
    public float alturaPainel = 0.28f;
    public float tamanhoTexto = 0.0022f;

    // Kit 3D que reage a cada etapa (LCD, cancela, buzzer). Opcional.
    public KitVirtual kit;
    const float AlturaModeloCadeado = 1f;   // altura do modelo no Blender
    const float EspacoCadeado = 0.02f;

    static readonly Color CorFechadura = new Color(0.05f, 0.05f, 0.05f, 1f);

    static readonly Color Verde = new Color(0.25f, 1f, 0.45f, 0.95f);
    static readonly Color Vermelho = new Color(1f, 0.3f, 0.3f, 0.95f);
    static readonly Color Amarelo = new Color(1f, 0.85f, 0.2f, 0.95f);
    static readonly Color Cinza = new Color(0.8f, 0.8f, 0.8f, 0.95f);

    Transform[] ancoras;
    Transform painel;
    TextMesh texto;
    Transform fundoT;
    Renderer fundo;
    Transform pulsoT;
    Renderer pulso;
    TrailRenderer rastro;
    Coroutine atual;

    Transform cadeadoT;
    GameObject cadeadoAberto, cadeadoFechado;
    Renderer[] pecasCadeado;

    public void Configurar(Transform pai, Transform[] ancoras, Material matBase, Font fonte,
                           GameObject modeloCadeado)
    {
        this.ancoras = ancoras;

        if (matBase != null)
        {
            pulso = NovaForma(PrimitiveType.Sphere, "PulsoSinal", pai, matBase, out pulsoT);
            pulsoT.localScale = Vector3.one * TamanhoPulso;

            // Rastro de luz atras do pulso, como as setas brilhantes da foto.
            rastro = pulsoT.gameObject.AddComponent<TrailRenderer>();
            rastro.time = 0.35f;
            rastro.startWidth = TamanhoPulso * 0.8f;
            rastro.endWidth = 0f;
            rastro.material = new Material(matBase);
            rastro.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rastro.receiveShadows = false;

            pulsoT.gameObject.SetActive(false);
        }

        var go = new GameObject("PainelResultado");
        painel = go.transform;
        painel.SetParent(pai, false);

        texto = go.AddComponent<TextMesh>();
        texto.font = fonte;
        texto.fontSize = 80;
        texto.characterSize = tamanhoTexto;
        texto.anchor = TextAnchor.MiddleCenter;
        texto.alignment = TextAlignment.Center;
        texto.fontStyle = FontStyle.Bold;
        go.GetComponent<MeshRenderer>().sharedMaterial = fonte.material;

        if (matBase != null)
            fundo = NovaForma(PrimitiveType.Quad, "Fundo", painel, matBase, out fundoT);

        if (modeloCadeado != null && matBase != null)
            CriarCadeado(modeloCadeado, matBase);

        go.SetActive(false);
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
        if (!Rota(status, out int[] caminho, out Color cor, out string titulo))
            return false;

        // Cadeado aberto quando a cancela abre (ou ja esta aberta).
        bool aberto = cor == Verde || cor == Amarelo;

        if (atual != null) StopCoroutine(atual);
        if (kit != null) kit.Inicio(status);
        atual = StartCoroutine(Animar(caminho, cor, titulo, aberto));
        return true;
    }

    // Para cada resposta do ESP32: por onde a luz passa, a cor e o texto do painel.
    // Separado do resto para poder ser testado sem o oculos.
    public static bool Rota(string status, out int[] caminho, out Color cor, out string titulo)
    {
        switch (status)
        {
            case "ENTRADA ABERTA":
                caminho = new[] { Leitor, Esp, Lcd, Servo };
                cor = Verde; titulo = "ACESSO\nAUTORIZADO";
                break;
            case "ABERTURA MANUAL":
                caminho = new[] { Esp, Lcd, Servo };
                cor = Verde; titulo = "ABERTURA\nPELO ADMIN";
                break;
            case "NAO CADASTRADO":
            case "BLOQUEADO":
                caminho = new[] { Leitor, Esp, Lcd };
                cor = Vermelho; titulo = "ACESSO\nNEGADO";
                break;
            case "FALHA NO SERVO":
                caminho = new[] { Leitor, Esp, Lcd };
                cor = Vermelho; titulo = "FALHA\nNO SERVO";
                break;
            case "PORTA JA ABERTA":
                caminho = new[] { Leitor, Esp, Lcd };
                cor = Amarelo; titulo = "CANCELA\nJÁ ABERTA";
                break;
            case "ENTRADA FECHADA":
                caminho = new[] { Leitor, Esp, Servo };
                cor = Cinza; titulo = "CANCELA\nFECHADA";
                break;
            case "FECHAMENTO MANUAL":
            case "FECHAMENTO AUTOMATICO":
            case "FECHAMENTO POR BLOQUEIO":
            case "FECHAMENTO POR EXCLUSAO":
                caminho = new[] { Esp, Servo };
                cor = Cinza; titulo = "CANCELA\nFECHADA";
                break;
            default:
                caminho = null; cor = default; titulo = null;
                return false;
        }

        return true;
    }

    IEnumerator Animar(int[] caminho, Color cor, string titulo, bool aberto)
    {
        painel.gameObject.SetActive(false);

        if (pulso != null)
        {
            pulso.material.color = cor;
            rastro.material.color = cor;
            pulsoT.position = ancoras[caminho[0]].position;
            rastro.Clear();
            pulsoT.gameObject.SetActive(true);
        }

        if (kit != null) kit.ChegouNo(caminho[0]);

        for (int i = 0; i + 1 < caminho.Length; i++)
        {
            Transform a = ancoras[caminho[i]];
            Transform b = ancoras[caminho[i + 1]];

            for (float t = 0f; t < 1f; t += Time.deltaTime / DuracaoTrecho)
            {
                if (pulso != null)
                {
                    // Sobe num arco entre as pecas, como o sinal passando pelos fios.
                    Vector3 p = Vector3.Lerp(a.position, b.position, Mathf.SmoothStep(0f, 1f, t));
                    float arco = Vector3.Distance(a.position, b.position) * 0.25f;
                    pulsoT.position = p + Vector3.up * arco * Mathf.Sin(t * Mathf.PI);

                    // Leve "respiracao" para parecer energia passando.
                    float s = TamanhoPulso * (1f + 0.3f * Mathf.Sin(t * Mathf.PI));
                    pulsoT.localScale = Vector3.one * s;
                }

                yield return null;
            }

            if (kit != null) kit.ChegouNo(caminho[i + 1]);
        }

        if (pulso != null) pulsoT.gameObject.SetActive(false);
        if (kit != null) kit.Terminou();

        texto.text = titulo;
        texto.color = cor;

        if (fundo != null)
            fundo.material.color = new Color(cor.r * 0.12f, cor.g * 0.12f, cor.b * 0.12f, 0.8f);

        PrepararCadeado(aberto, cor);
        painel.gameObject.SetActive(true);
        PosicionarPainel();

        // O TextMesh so atualiza os limites no frame seguinte.
        yield return null;
        AjustarFundo();

        yield return new WaitForSeconds(TempoPainel);

        painel.gameObject.SetActive(false);
        atual = null;
    }

    void LateUpdate()
    {
        if (painel != null && painel.gameObject.activeInHierarchy)
            PosicionarPainel();
    }

    void PosicionarPainel()
    {
        Vector3 centro = Vector3.zero;
        foreach (var a in ancoras) centro += a.position;
        centro /= ancoras.Length;

        Vector3 p = centro + Vector3.up * alturaPainel;
        painel.position = p;

        var cam = Camera.main;
        if (cam == null) return;

        Vector3 dir = p - cam.transform.position;
        if (dir.sqrMagnitude > 0.0001f)
            painel.rotation = Quaternion.LookRotation(dir, Vector3.up);
    }

    void AjustarFundo()
    {
        if (fundoT == null) return;

        Bounds b = texto.GetComponent<MeshRenderer>().localBounds;

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
