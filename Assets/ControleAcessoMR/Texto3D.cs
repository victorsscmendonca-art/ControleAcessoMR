using TMPro;
using UnityEngine;

// Texto 3D nitido no oculos (TextMeshPro, fonte SDF). Substitui o TextMesh antigo,
// que ficava borrado no Quest.
public static class Texto3D
{
    // "tamanho" na mesma escala do antigo TextMesh.characterSize (fontSize 80),
    // medido no Editor: mesmo texto, mesma largura.
    const float EscalaTextMesh = 80f;

    public static TextMeshPro Criar(GameObject go, string texto, float tamanho, bool negrito = true)
    {
        // Num filho: o TextMeshPro troca o Transform por RectTransform, e quem
        // guardou go.transform ficaria com uma referencia destruida.
        var filho = new GameObject("Texto");
        filho.transform.SetParent(go.transform, false);
        var t = filho.AddComponent<TextMeshPro>();
        t.text = texto;
        t.fontSize = tamanho * EscalaTextMesh;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.fontStyle = negrito ? FontStyles.Bold : FontStyles.Normal;
        t.color = Color.white;
        t.rectTransform.sizeDelta = Vector2.zero;   // centralizado no proprio ponto
        return t;
    }

    // Caixa do texto em coordenadas locais (para ajustar fundos ao tamanho das letras).
    public static Bounds Limites(TextMeshPro t)
    {
        t.ForceMeshUpdate();
        return t.textBounds;
    }
}
