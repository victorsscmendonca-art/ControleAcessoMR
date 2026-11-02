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
    const float AlturaPainel = 0.28f;
    const float TamanhoTexto = 0.0022f;
    const float TamanhoPulso = 0.025f;

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
    Coroutine atual;

    public void Configurar(Transform pai, Transform[] ancoras, Material matBase, Font fonte)
    {
        this.ancoras = ancoras;

        if (matBase != null)
        {
            pulso = NovaForma(PrimitiveType.Sphere, "PulsoSinal", pai, matBase, out pulsoT);
            pulsoT.localScale = Vector3.one * TamanhoPulso;
            pulsoT.gameObject.SetActive(false);
        }

        var go = new GameObject("PainelResultado");
        painel = go.transform;
        painel.SetParent(pai, false);

        texto = go.AddComponent<TextMesh>();
        texto.font = fonte;
        texto.fontSize = 80;
        texto.characterSize = TamanhoTexto;
        texto.anchor = TextAnchor.MiddleCenter;
        texto.alignment = TextAlignment.Center;
        texto.fontStyle = FontStyle.Bold;
        go.GetComponent<MeshRenderer>().sharedMaterial = fonte.material;

        if (matBase != null)
            fundo = NovaForma(PrimitiveType.Quad, "Fundo", painel, matBase, out fundoT);

        go.SetActive(false);
    }

    // Retorna false para eventos que nao tem animacao (ex.: leitura para cadastro).
    public bool Mostrar(string status)
    {
        int[] caminho;
        Color cor;
        string titulo;

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
                return false;
        }

        if (atual != null) StopCoroutine(atual);
        atual = StartCoroutine(Animar(caminho, cor, titulo));
        return true;
    }

    IEnumerator Animar(int[] caminho, Color cor, string titulo)
    {
        painel.gameObject.SetActive(false);

        if (pulso != null)
        {
            pulso.material.color = cor;
            pulsoT.gameObject.SetActive(true);

            for (int i = 0; i + 1 < caminho.Length; i++)
            {
                Transform a = ancoras[caminho[i]];
                Transform b = ancoras[caminho[i + 1]];

                for (float t = 0f; t < 1f; t += Time.deltaTime / DuracaoTrecho)
                {
                    pulsoT.position = Vector3.Lerp(a.position, b.position, Mathf.SmoothStep(0f, 1f, t));

                    // Leve "respiracao" para parecer energia passando.
                    float s = TamanhoPulso * (1f + 0.3f * Mathf.Sin(t * Mathf.PI));
                    pulsoT.localScale = Vector3.one * s;
                    yield return null;
                }
            }

            pulsoT.gameObject.SetActive(false);
        }

        texto.text = titulo;
        texto.color = cor;

        if (fundo != null)
            fundo.material.color = new Color(cor.r * 0.12f, cor.g * 0.12f, cor.b * 0.12f, 0.8f);

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

        Vector3 p = centro + Vector3.up * AlturaPainel;
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

        fundoT.localPosition = new Vector3(b.center.x, b.center.y, 0.002f);
        fundoT.localScale = new Vector3(b.size.x + 0.06f, b.size.y + 0.04f, 1f);
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
