using System.Linq;
using Oculus.Interaction.Editor.QuickActions;
using Oculus.Interaction.OVR.Editor.QuickActions;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Monta na cena (uma vez) o rig de maos/controles do Interaction SDK
// e o painel com os botoes "Simular autorizado" / "Simular negado".
// Pode ser repetido pelo menu: ControleAcessoMR > Montar botoes de simulacao.
public static class MontarBotoesSimulacao
{
    const string NomePainel = "BotoesSimulacao";

    [MenuItem("ControleAcessoMR/Montar botoes de simulacao")]
    public static string Run()
    {
        var controle = Object.FindFirstObjectByType<ControleAcessoMR>();
        if (controle == null) return "ERRO: ControleAcessoMR nao encontrado na cena";

        string log = "";

        if (Object.FindFirstObjectByType<Oculus.Interaction.Input.Hand>() == null)
        {
            var criados = OVRQuickActionsAPI.AddOVRInteractionRig();
            log += "Rig criado: " + string.Join(", ", criados.Select(g => g.name)) + "\n";
        }

        // Em realidade mista ninguem anda no virtual, e o analogico
        // ja e usado para ajustar as etiquetas: desliga a locomocao.
        string[] locomocao =
        {
            "Locomotor",
            "LocomotionControllerInteractorGroup",
            "MicroGesturesLocomotionHandInteractorGroup"
        };

        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (System.Array.IndexOf(locomocao, t.name) >= 0 && t.gameObject.activeSelf &&
                t.GetComponentInParent<OVRCameraRig>(true) != null)
            {
                t.gameObject.SetActive(false);
                log += "Desligado: " + t.name + "\n";
            }
        }

        var antigo = GameObject.Find(NomePainel);
        if (antigo != null) Object.DestroyImmediate(antigo);

        var painel = new GameObject(NomePainel, typeof(RectTransform), typeof(Canvas),
                                    typeof(CanvasScaler), typeof(GraphicRaycaster));
        painel.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;

        var rt = (RectTransform)painel.transform;
        rt.sizeDelta = new Vector2(480, 110);
        rt.localScale = Vector3.one * 0.0007f;  // painel de ~34 cm: cabe o dedo sem tampar o kit
        rt.position = new Vector3(0f, 1f, 0.5f);

        var fundo = painel.AddComponent<Image>();
        fundo.color = new Color(0f, 0f, 0f, 0.55f);

        var aut = CriarBotao(painel.transform, "Autorizado", "SIMULAR\nAUTORIZADO",
                             new Color(0.1f, 0.55f, 0.25f), -155f);
        UnityEventTools.AddBoolPersistentListener(aut.onClick, controle.Simular, false);

        var neg = CriarBotao(painel.transform, "Negado", "SIMULAR\nNEGADO",
                             new Color(0.65f, 0.15f, 0.15f), 0f);
        UnityEventTools.AddBoolPersistentListener(neg.onClick, controle.Simular, true);

        var como = CriarBotao(painel.transform, "ComoFunciona", "COMO\nFUNCIONA?",
                              new Color(0.1f, 0.4f, 0.7f), 155f);
        UnityEventTools.AddVoidPersistentListener(como.onClick, controle.ComoFunciona);

        QuickActionsAPI.AddPokeCanvasInteraction(painel);
        QuickActionsAPI.AddRayCanvasInteraction(painel);

        var so = new SerializedObject(controle);
        so.FindProperty("botoesSimulacao").objectReferenceValue = painel.transform;
        so.ApplyModifiedProperties();

        var cena = controle.gameObject.scene;
        EditorSceneManager.MarkSceneDirty(cena);
        EditorSceneManager.SaveScene(cena);

        return log + "Painel criado e cena salva.";
    }

    static Button CriarBotao(Transform pai, string nome, string texto, Color cor, float x)
    {
        var go = new GameObject("Botao" + nome, typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = (RectTransform)go.transform;
        rt.SetParent(pai, false);
        rt.sizeDelta = new Vector2(140, 80);
        rt.anchoredPosition = new Vector2(x, 0f);

        go.GetComponent<Image>().color = cor;

        var botao = go.GetComponent<Button>();

        var rotulo = new GameObject("Texto", typeof(RectTransform), typeof(TextMeshProUGUI));
        var trt = (RectTransform)rotulo.transform;
        trt.SetParent(rt, false);
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;

        var tmp = rotulo.GetComponent<TextMeshProUGUI>();
        tmp.text = texto;
        tmp.fontSize = 17;
        tmp.enableWordWrapping = false;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        return botao;
    }
}
