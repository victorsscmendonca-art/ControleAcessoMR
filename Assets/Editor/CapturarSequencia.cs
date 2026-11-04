using System.IO;
using UnityEditor;
using UnityEngine;

// Tira uma sequencia de fotos da animacao (para documentar e mostrar ao dono),
// no tempo do proprio jogo, sem depender da velocidade de quem chama.
// Uso (em Play): CapturarSequencia.Iniciar("Capturas/seq/autorizado", negado: false, fotos: 14, intervalo: 0.6f);
public static class CapturarSequencia
{
    static string pasta;
    static int total, feitas;
    static float intervalo, proxima;
    static bool negado, disparou;

    public static void Iniciar(string destino, bool negado, int fotos = 14, float intervalo = 0.6f)
    {
        pasta = destino;
        total = fotos;
        CapturarSequencia.intervalo = intervalo;
        CapturarSequencia.negado = negado;
        feitas = 0;
        disparou = false;
        proxima = Time.time;
        Directory.CreateDirectory(pasta);

        EditorApplication.update -= Passo;
        EditorApplication.update += Passo;
    }

    public static bool Terminou => feitas >= total;

    static void Passo()
    {
        if (!Application.isPlaying) { EditorApplication.update -= Passo; return; }
        if (Time.time < proxima) return;

        // Foto 0 = antes do cartao (tela de boas-vindas); depois dispara a simulacao.
        Foto(Path.Combine(pasta, feitas.ToString("00") + ".png"));
        feitas++;
        proxima = Time.time + intervalo;

        if (!disparou)
        {
            disparou = true;
            Object.FindFirstObjectByType<ControleAcessoMR>()?.Simular(negado);
        }

        if (feitas >= total) EditorApplication.update -= Passo;
    }

    static void Foto(string arquivo)
    {
        var cam = Camera.main;
        if (cam == null) return;

        var rt = new RenderTexture(1280, 720, 24);
        var antes = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = antes;

        var anterior = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        tex.Apply();
        RenderTexture.active = anterior;

        File.WriteAllBytes(arquivo, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        rt.Release();
        Object.DestroyImmediate(rt);
    }
}
