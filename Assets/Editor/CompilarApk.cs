using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Gera o APK do Quest (sem precisar clicar em nada):
//   Unity.exe -batchmode -nographics -quit -buildTarget Android -projectPath <pasta> \
//             -executeMethod CompilarApk.Gerar -logFile build.log
// O APK vai para Builds/Quest/ControleAcessoMR.apk (essa pasta nao entra no git).
public static class CompilarApk
{
    const string Saida = "Builds/Quest/ControleAcessoMR.apk";

    // Tudo em Resources/ vai para o APK. O Immersive Debugger da Meta guarda ali um token
    // de depuracao e o IP da rede: sai da pasta durante o build e volta no fim.
    static readonly string[] SoParaDesenvolvimento =
    {
        "Assets/Resources/DevAgentSettings.asset",
        "Assets/Resources/DevAgentSettings.asset.meta"
    };
    const string PastaReserva = "Library/FicaForaDoApk";

    public static void Gerar()
    {
        var cenas = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();

        if (cenas.Length == 0)
        {
            Debug.LogError("[APK] Nenhuma cena na lista de build.");
            EditorApplication.Exit(1);
            return;
        }

        EditorUserBuildSettings.buildAppBundle = false;   // APK, nao AAB
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

        Directory.CreateDirectory(Path.GetDirectoryName(Saida));

        var opcoes = new BuildPlayerOptions
        {
            scenes = cenas,
            locationPathName = Saida,
            target = BuildTarget.Android,
            options = BuildOptions.None   // sem Development Build
        };

        TirarDoBuild();
        BuildReport r;

        try { r = BuildPipeline.BuildPlayer(opcoes); }
        finally { DevolverAoProjeto(); }

        var resumo = r.summary;

        Debug.Log("[APK] resultado=" + resumo.result + " tamanho=" + (resumo.totalSize / (1024 * 1024)) + "MB erros=" + resumo.totalErrors);

        if (resumo.result != BuildResult.Succeeded)
        {
            foreach (var passo in r.steps)
                foreach (var m in passo.messages)
                    if (m.type == LogType.Error || m.type == LogType.Exception)
                        Debug.LogError("[APK] " + m.content);

            EditorApplication.Exit(1);
            return;
        }

        EditorApplication.Exit(0);
    }

    static void TirarDoBuild()
    {
        Directory.CreateDirectory(PastaReserva);

        foreach (var f in SoParaDesenvolvimento)
            if (File.Exists(f)) File.Move(f, Path.Combine(PastaReserva, Path.GetFileName(f)));

        AssetDatabase.Refresh();
    }

    static void DevolverAoProjeto()
    {
        foreach (var f in SoParaDesenvolvimento)
        {
            string reserva = Path.Combine(PastaReserva, Path.GetFileName(f));
            if (File.Exists(reserva) && !File.Exists(f)) File.Move(reserva, f);
        }

        AssetDatabase.Refresh();
    }
}
