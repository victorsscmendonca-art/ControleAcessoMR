using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

// Testes da logica "liberado/negado" (rodam no Unity, sem oculos e sem ESP32).
// Window > General > Test Runner > EditMode > Run All.
public class TestesAcesso
{
    const int Servo = FluxoAcesso.Servo;

    static bool ChegaAoServo(FluxoAcesso.RotaAcesso r) => r.etapas.Any(e => e.Any(x => x.para == Servo));

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
            Assert.IsNotNull(FluxoAcesso.Rota(s), "sem animacao para: " + s);
    }

    [Test]
    public void CartaoLiberadoAbreACancela()
    {
        string s = ControleAcessoMR.StatusDoCartao(liberado: true, cancelaAberta: false);

        Assert.AreEqual("ENTRADA ABERTA", s);
        var rota = FluxoAcesso.Rota(s);
        Assert.IsNotNull(rota);
        Assert.IsTrue(ChegaAoServo(rota), "a luz precisa chegar ao servo");
        StringAssert.Contains("AUTORIZADO", rota.titulo);
    }

    // O codigo que viaja no fio so aparece quando um cartao e lido (nao no fechamento automatico).
    [Test]
    public void CodigoDoCartaoSoQuandoUmCartaoELido()
    {
        Assert.AreEqual(FluxoAcesso.CodigoLiberado, FluxoAcesso.Rota("ENTRADA ABERTA").codigo);
        Assert.AreEqual(FluxoAcesso.CodigoNegado, FluxoAcesso.Rota("NAO CADASTRADO").codigo);
        Assert.IsNull(FluxoAcesso.Rota("FECHAMENTO AUTOMATICO").codigo);
        Assert.IsNull(FluxoAcesso.Rota("ABERTURA MANUAL").codigo);
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
            var rota = FluxoAcesso.Rota(s);
            Assert.IsNotNull(rota);
            Assert.IsFalse(ChegaAoServo(rota), "negado nao pode mexer na cancela");
            StringAssert.Contains("NEGADO", rota.titulo);
        }
    }
}
