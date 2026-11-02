using Oculus.Interaction.Editor.QuickActions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Cria o prefab do kit virtual a partir do modelo do Blender (KitVirtual.fbx):
// tamanho 2x, componente KitVirtual e cartoes que podem ser pegos.
// Repetir pelo menu sempre que o modelo mudar.
public static class MontarKitVirtual
{
    const string CaminhoFbx = "Assets/ControleAcessoMR/Modelos/KitVirtual.fbx";
    const string CaminhoPrefab = "Assets/ControleAcessoMR/KitVirtual.prefab";
    const float Escala = 2f;

    [MenuItem("ControleAcessoMR/Montar kit virtual")]
    public static string Run()
    {
        // Read/Write ligado: o KitVirtual le a direcao da tela do LCD na malha.
        var importador = (ModelImporter)AssetImporter.GetAtPath(CaminhoFbx);
        if (importador != null && !importador.isReadable)
        {
            importador.isReadable = true;
            importador.SaveAndReimport();
        }

        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(CaminhoFbx);
        if (fbx == null) return "ERRO: " + CaminhoFbx + " nao encontrado";

        var raiz = new GameObject("KitVirtual");
        raiz.transform.localScale = Vector3.one * Escala;

        var modelo = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        PrefabUtility.UnpackPrefabInstance(modelo, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        modelo.transform.SetParent(raiz.transform, false);

        string log = Endireitar(raiz.transform, modelo.transform);

        foreach (var nome in new[] { "Cartao_Liberado", "Cartao_Negado" })
        {
            var cartao = Achar(raiz.transform, nome);
            if (cartao == null) return "ERRO: " + nome + " nao esta no modelo";

            // O ponto de controle do cartao precisa ficar no centro do desenho: e por
            // ele que o KitVirtual sabe se o cartao chegou no leitor.
            var malha = cartao.GetComponentInChildren<Renderer>();
            Centralizar(cartao, malha.bounds.center);

            // Colisor do tamanho do cartao, para pegar com mao ou controle.
            var col = cartao.gameObject.AddComponent<BoxCollider>();
            var limites = LimitesLocais(cartao, malha);
            col.center = limites.center;
            col.size = limites.size;

            QuickActionsAPI.AddGrabInteraction(cartao.gameObject);
            QuickActionsAPI.AddRayGrabInteraction(cartao.gameObject);
            log += nome + " pegavel; ";
        }

        raiz.AddComponent<KitVirtual>();

        var prefab = PrefabUtility.SaveAsPrefabAsset(raiz, CaminhoPrefab, out bool ok);
        Object.DestroyImmediate(raiz);
        if (!ok) return "ERRO ao salvar o prefab";

        var controle = Object.FindFirstObjectByType<ControleAcessoMR>();

        if (controle != null)
        {
            var so = new SerializedObject(controle);
            so.FindProperty("kitVirtualPrefab").objectReferenceValue = prefab;
            so.ApplyModifiedProperties();

            var cena = controle.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);
            log += "ligado a cena; ";
        }

        return log + "prefab salvo em " + CaminhoPrefab;
    }

    // A conversao de eixos do FBX pode trazer o kit deitado ou girado.
    // Endireita pelo proprio modelo: a protoboard fica deitada (lado mais fino = cima,
    // com o ESP32 por cima), o leitor fica a esquerda e o servo a direita (+X),
    // e assim os cartoes ficam na frente (+Z, lado da pessoa).
    static string Endireitar(Transform raiz, Transform modelo)
    {
        Vector3 Centro(string n) => Achar(raiz, n).GetComponent<Renderer>().bounds.center;

        var proto = Achar(raiz, "Protoboard_Malha").GetComponent<Renderer>().bounds;
        Vector3 cima = EixoMaisFino(proto.size);
        if (Vector3.Dot(Centro("ESP32_Malha") - proto.center, cima) < 0f) cima = -cima;
        modelo.rotation = Quaternion.FromToRotation(cima, Vector3.up) * modelo.rotation;

        Vector3 aoLongo = Vector3.ProjectOnPlane(Centro("Servo_Malha") - Centro("Leitor_Malha"), Vector3.up);
        modelo.rotation = Quaternion.FromToRotation(aoLongo.normalized, Vector3.right) * modelo.rotation;

        // Confere: cartoes na frente (+Z), protoboard deitada.
        Vector3 cartoes = (Centro("CartaoLiberado_Malha") + Centro("CartaoNegado_Malha")) * 0.5f;
        var p = Achar(raiz, "Protoboard_Malha").GetComponent<Renderer>().bounds;
        return "endireitado (protoboard " + p.size.ToString("F3") + ", cartoes z="
            + (cartoes.z - p.center.z).ToString("F3") + "); ";
    }

    // Move o objeto para 'centro' sem mover o que esta dentro dele.
    static void Centralizar(Transform t, Vector3 centro)
    {
        var filhos = new Transform[t.childCount];
        var posicoes = new Vector3[t.childCount];

        for (int i = 0; i < t.childCount; i++)
        {
            filhos[i] = t.GetChild(i);
            posicoes[i] = filhos[i].position;
        }

        t.position = centro;

        for (int i = 0; i < filhos.Length; i++)
            filhos[i].position = posicoes[i];
    }

    static Vector3 EixoMaisFino(Vector3 tam)
    {
        if (tam.x <= tam.y && tam.x <= tam.z) return Vector3.right;
        if (tam.y <= tam.z) return Vector3.up;
        return Vector3.forward;
    }

    static Transform Achar(Transform raiz, string nome)
    {
        foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
            if (t.name == nome) return t;
        return null;
    }

    // Caixa da malha no espaco do cartao (vale mesmo se a malha vier girada do Blender).
    static Bounds LimitesLocais(Transform espaco, Renderer malha)
    {
        var b = malha.localBounds;
        var t = malha.transform;
        var resultado = new Bounds(espaco.InverseTransformPoint(t.TransformPoint(b.center)), Vector3.zero);

        for (int i = 0; i < 8; i++)
        {
            var canto = b.center + Vector3.Scale(b.extents, new Vector3(
                (i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            resultado.Encapsulate(espaco.InverseTransformPoint(t.TransformPoint(canto)));
        }

        // Um pouco mais grosso que o cartao (0,8 mm), para a mao achar facil.
        resultado.Expand(0.004f);
        return resultado;
    }
}
