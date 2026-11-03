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

        BuildReport r = BuildPipeline.BuildPlayer(opcoes);
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
}
