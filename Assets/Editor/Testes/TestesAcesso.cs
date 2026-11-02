using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

// Testes da logica "liberado/negado" (rodam no Unity, sem oculos e sem ESP32).
// Window > General > Test Runner > EditMode > Run All.
public class TestesAcesso
{
    const int Servo = 3;

    // Todas as respostas que o ESP32 pode mandar precisam ter animacao no oculos.
    // Se alguem renomear um status no .ino, este teste falha antes da feira.
    [Test]
    public void TodoStatusDoEsp32TemAnimacao()
    {
        string ino = Path.Combine(Application.dataPath, "..", "ESP32", "ControleAcesso", "ControleAcesso.ino");
        string codigo = File.ReadAllText(ino);

        var status = Regex.Matches(codigo, @"registrarEvento\([^;]*?""([A-Z ]+)""\s*,\s*""[^""]*""\s*\)")
            .Cast<Match>()
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .Where(s => s != "LEITURA PARA CADASTRO")   // so captura o UID, sem animacao
            .ToList();

        Assert.That(status, Has.Count.GreaterThan(5), "nao achei os status no .ino");

        foreach (var s in status)
            Assert.IsTrue(FluxoAcesso.Rota(s, out _, out _, out _), "sem animacao para: " + s);
    }

    [Test]
    public void CartaoLiberadoAbreACancela()
    {
        string s = ControleAcessoMR.StatusDoCartao(liberado: true, cancelaAberta: false);

        Assert.AreEqual("ENTRADA ABERTA", s);
        Assert.IsTrue(FluxoAcesso.Rota(s, out int[] caminho, out _, out string titulo));
        Assert.AreEqual(Servo, caminho.Last(), "a luz precisa chegar ao servo");
        StringAssert.Contains("AUTORIZADO", titulo);
    }

    [Test]
    public void CartaoLiberadoComCancelaAbertaFecha()
    {
        Assert.AreEqual("ENTRADA FECHADA", ControleAcessoMR.StatusDoCartao(true, true));
    }

    [Test]
    public void CartaoNegadoNaoChegaAoServo()
    {
        foreach (bool aberta in new[] { false, true })
        {
            string s = ControleAcessoMR.StatusDoCartao(liberado: false, cancelaAberta: aberta);

            Assert.AreEqual("NAO CADASTRADO", s);
            Assert.IsTrue(FluxoAcesso.Rota(s, out int[] caminho, out _, out string titulo));
            CollectionAssert.DoesNotContain(caminho, Servo, "negado nao pode mexer na cancela");
            StringAssert.Contains("NEGADO", titulo);
        }
    }
}
