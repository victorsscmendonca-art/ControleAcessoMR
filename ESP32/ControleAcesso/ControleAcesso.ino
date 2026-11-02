#include <WiFi.h>
#include <WebServer.h>
#include <SPI.h>
#include <MFRC522.h>
#include <LiquidCrystal.h>
#include <Preferences.h>
#include <ESP32Servo.h>
#include <esp_system.h>

// ================= CONFIGURACOES =================

const char* WIFI_NOME = "Controle_Acesso";
const char* WIFI_SENHA = "12345678";
const char* ADMIN_USUARIO = "admin";
const char* ADMIN_SENHA = "admin123";

#define RFID_SS   14
#define RFID_RST  22
#define RFID_SCK  18
#define RFID_MISO 19
#define RFID_MOSI 23
#define BUZZER_PIN 21
#define SERVO_PIN  33

const int ANGULO_FECHADO = 20;
const int ANGULO_ABERTO = 110;

WebServer servidor(80);
Preferences memoria;
MFRC522 rfid(RFID_SS, RFID_RST);
LiquidCrystal lcd(16, 17, 25, 26, 27, 32);
Servo servoPorta;

bool servoPronto = false;
bool portaAberta = false;
String uidQueAbriu = "";
String responsavelPorta = "";
String origemPorta = "";

bool fechamentoAutomatico = false;
unsigned long tempoFechamento = 10;
unsigned long momentoAbertura = 0;

// ================= USUARIOS =================

const int MAX_USUARIOS = 20;
int totalUsuarios = 0;

String usuariosUID[MAX_USUARIOS];
String usuariosNome[MAX_USUARIOS];
String usuariosLogin[MAX_USUARIOS];
String usuariosSenha[MAX_USUARIOS];
bool usuariosLiberados[MAX_USUARIOS];

// ================= HISTORICO =================

const int MAX_HISTORICO = 50;

struct Registro {
  String nome;
  String uid;
  String status;
  String hora;
  String origem;
};

Registro historico[MAX_HISTORICO];
int histTotal = 0;

String ultimoUID = "";
String ultimoUIDCartao = "";
String ultimoNome = "";
String ultimoStatus = "";
String ultimaHora = "";
String ultimaOrigem = "";

bool temLeitura = false;
int totalLiberados = 0;
int totalNegados = 0;

unsigned long ultimaLeitura = 0;
const unsigned long intervaloLeitura = 1000;

// ================= RELOGIO =================

bool relogioSincronizado = false;
unsigned long momentoSincronizacao = 0;
unsigned long segundosBase = 0;

// ================= CADASTRO GUIADO =================

// Uma leitura guiada pertence ao administrador que a iniciou.
// Enquanto aguarda, o proximo cartao apenas fornece o UID.

String tokenGuiado = "";
String uidGuiado = "";
bool aguardandoGuiado = false;
unsigned long inicioGuiado = 0;
const unsigned long prazoGuiado = 60000;

// ================= SESSOES =================

struct Sessao {
  String token;
  String tipo;
  int usuario = -1;
};

const int MAX_SESSOES = 8;
Sessao sessoes[MAX_SESSOES];

// ================= BUZZER =================

const int SOM_ABRIR = 0;
const int SOM_FECHAR = 1;
const int SOM_NEGADO = 2;
const int SOM_OCUPADA = 3;
const int TOTAL_SONS = 4;

struct ConfigSom {
  int quantidade;
  unsigned long duracao;
  unsigned long intervalo;
};

ConfigSom sons[TOTAL_SONS] = {
  {1, 120, 120},
  {2, 100, 120},
  {3, 180, 120},
  {2, 300, 180}
};

bool buzzerHabilitado = true;
bool sequenciaBuzzer = false;
bool buzzerLigado = false;
int bipesRestantes = 0;

unsigned long duracaoBipAtual = 0;
unsigned long intervaloBipAtual = 0;
unsigned long momentoBuzzer = 0;

// ================= LCD =================

byte iconeFechado[8] = {
  14, 17, 17, 31, 27, 27, 31, 0
};
byte iconeAberto[8] = {
  14, 17, 16, 31, 27, 27, 31, 0
};
byte iconeOK[8] = {
  0, 1, 2, 20, 8, 0, 0, 0
};
byte iconeX[8] = {
  0, 17, 10, 4, 10, 17, 0, 0
};
byte iconeSeta[8] = {
  0, 4, 2, 31, 2, 4, 0, 0
};
byte iconeMarcador[8] = {
  4, 14, 31, 14, 4, 0, 0, 0
};

bool telaTemporaria = false;
unsigned long inicioTelaTemporaria = 0;
unsigned long duracaoTelaTemporaria = 0;

bool rolandoNome = false;
String textoRolagem = "";
int posicaoRolagem = 0;
unsigned long ultimaRolagem = 0;

unsigned long ultimaAnimacao = 0;
int posicaoAnimacao = 0;
int direcaoAnimacao = 1;
bool faseAnimacao = false;

// ================= DECLARACOES =================

String escaparJSON(String texto);
String jtexto(String texto);
String jsonBool(bool valor);

String gerarToken();
void criarSessao(String tipo, int usuario, String &token);
int procurarSessao(String token);
String tokenRecebido();
bool ehAdmin(int indice);
bool exigirAdmin();
void invalidarVisitante(int indice);

void salvarUsuarios();
void carregarUsuarios();
int procurarUsuarioUID(String uid);
int procurarLogin(String login, String senha);
int loginJaExiste(String login, int ignorar);

String pegarUID();
String pegarHora();
void registrarEvento(String nome, String uid,
                     String status, String origem);

void pararBuzzer();
void iniciarBipes(int quantidade, unsigned long duracao,
                  unsigned long intervalo);
void tocarSom(int evento);
void atualizarBuzzer();
void salvarConfigBuzzer();
void carregarConfigBuzzer();

void salvarConfigCancela();
void carregarConfigCancela();
void abrirPorta(String uid, String nome, String origem);
void fecharPorta();
unsigned long segundosRestantes();
void atualizarCancela();

void escreverLinhaLCD(int linha, String texto);
void mostrarAguardando();
void mostrarLCD(String titulo, String nome,
                int icone, unsigned long tempo);
void atualizarLCD();

void limparGuiado();
void atualizarGuiado();
void rotaGuiadoIniciar();
void rotaGuiadoCancelar();

bool lerNumero(String chave, long minimo,
               long maximo, long &valor);

void paginaPrincipal();
void rotaLogin();
void rotaMe();
void rotaLogout();
void rotaHora();
void rotaDados();
void enviarAdmin();
void enviarVisitante(int indice);

void rotaCadastrar();
void rotaStatus();
void rotaExcluir();

void rotaBuzzerConsultar();
void rotaBuzzerSalvar();
void rotaBuzzerTestar();

void rotaCancelaConsultar();
void rotaCancelaSalvar();
void rotaCancelaComando();

void processarCartao(String uid);
void setup();
void loop();

// ================= JSON =================

String escaparJSON(String texto) {
  String saida;
  saida.reserve(texto.length() + 8);

  for (unsigned int i = 0; i < texto.length(); i++) {
    unsigned char c = texto[i];

    if (c == '\\') saida += "\\\\";
    else if (c == '"') saida += "\\\"";
    else if (c == '\n') saida += "\\n";
    else if (c == '\r') saida += "\\r";
    else if (c == '\t') saida += "\\t";
    else if (c < 32) {
      char buffer[7];
      snprintf(buffer, sizeof(buffer), "\\u%04x", (unsigned int)c);
      saida += buffer;
    } else {
      saida += (char)c;
    }
  }

  return saida;
}

String jtexto(String texto) {
  return String("\"") + escaparJSON(texto) + "\"";
}

String jsonBool(bool valor) {
  return valor ? "true" : "false";
}

// ================= AUTENTICACAO =================

String gerarToken() {
  char buffer[25];

  snprintf(
    buffer, sizeof(buffer), "%08lX%08lX%08lX",
    (unsigned long)esp_random(),
    (unsigned long)esp_random(),
    (unsigned long)esp_random()
  );

  return String(buffer);
}

void criarSessao(String tipo, int usuario, String &token) {
  int pos = 0;

  for (int i = 0; i < MAX_SESSOES; i++) {
    if (sessoes[i].token == "") {
      pos = i;
      break;
    }
  }

  if (sessoes[pos].token == tokenGuiado) limparGuiado();

  token = gerarToken();
  sessoes[pos].token = token;
  sessoes[pos].tipo = tipo;
  sessoes[pos].usuario = usuario;
}

int procurarSessao(String token) {
  if (token == "") return -1;

  for (int i = 0; i < MAX_SESSOES; i++) {
    if (sessoes[i].token == token) return i;
  }

  return -1;
}

String tokenRecebido() {
  return servidor.hasArg("token") ? servidor.arg("token") : "";
}

bool ehAdmin(int indice) {
  return indice >= 0 && indice < MAX_SESSOES &&
         sessoes[indice].tipo == "admin";
}

bool exigirAdmin() {
  if (ehAdmin(procurarSessao(tokenRecebido()))) return true;

  servidor.send(403, "text/plain", "Nao autorizado.");
  return false;
}

void invalidarVisitante(int indice) {
  for (int i = 0; i < MAX_SESSOES; i++) {
    if (sessoes[i].tipo == "visitante" &&
        (indice < 0 || sessoes[i].usuario == indice)) {
      sessoes[i].token = "";
      sessoes[i].tipo = "";
      sessoes[i].usuario = -1;
    }
  }
}

// ================= MEMORIA DOS USUARIOS =================

void salvarUsuarios() {
  memoria.begin("controle", false);
  memoria.putInt("total", totalUsuarios);

  for (int i = 0; i < totalUsuarios; i++) {
    memoria.putString(("uid" + String(i)).c_str(), usuariosUID[i]);
    memoria.putString(("nome" + String(i)).c_str(), usuariosNome[i]);
    memoria.putString(("login" + String(i)).c_str(), usuariosLogin[i]);
    memoria.putString(("senha" + String(i)).c_str(), usuariosSenha[i]);
    memoria.putBool(("lib" + String(i)).c_str(), usuariosLiberados[i]);
  }

  memoria.end();
}

void carregarUsuarios() {
  memoria.begin("controle", true);
  totalUsuarios = memoria.getInt("total", 0);

  if (totalUsuarios < 0) totalUsuarios = 0;
  if (totalUsuarios > MAX_USUARIOS) totalUsuarios = MAX_USUARIOS;

  for (int i = 0; i < totalUsuarios; i++) {
    usuariosUID[i] = memoria.getString(("uid" + String(i)).c_str(), "");
    usuariosNome[i] = memoria.getString(("nome" + String(i)).c_str(), "");
    usuariosLogin[i] = memoria.getString(("login" + String(i)).c_str(), "");
    usuariosSenha[i] = memoria.getString(("senha" + String(i)).c_str(), "");
    usuariosLiberados[i] = memoria.getBool(("lib" + String(i)).c_str(), true);
  }

  memoria.end();
}

int procurarUsuarioUID(String uid) {
  uid.trim();
  uid.toUpperCase();

  for (int i = 0; i < totalUsuarios; i++) {
    if (usuariosUID[i] == uid) return i;
  }

  return -1;
}

int procurarLogin(String login, String senha) {
  for (int i = 0; i < totalUsuarios; i++) {
    if (usuariosLogin[i] == login &&
        usuariosSenha[i] == senha) return i;
  }

  return -1;
}

int loginJaExiste(String login, int ignorar) {
  for (int i = 0; i < totalUsuarios; i++) {
    if (i != ignorar && usuariosLogin[i] == login) return i;
  }

  return -1;
}

// ================= RFID, HORA E HISTORICO =================

String pegarUID() {
  String uid;

  for (byte i = 0; i < rfid.uid.size; i++) {
    if (i > 0) uid += " ";
    if (rfid.uid.uidByte[i] < 16) uid += "0";
    uid += String(rfid.uid.uidByte[i], HEX);
  }

  uid.toUpperCase();
  return uid;
}

String pegarHora() {
  if (!relogioSincronizado) return "--:--:--";

  unsigned long total =
    (segundosBase + (millis() - momentoSincronizacao) / 1000) % 86400;

  char buffer[9];

  snprintf(
    buffer, sizeof(buffer), "%02d:%02d:%02d",
    (int)(total / 3600),
    (int)((total % 3600) / 60),
    (int)(total % 60)
  );

  return String(buffer);
}

void registrarEvento(
  String nome, String uid, String status, String origem
) {
  ultimoNome = nome;
  ultimoUID = uid;
  ultimoStatus = status;
  ultimaHora = pegarHora();
  ultimaOrigem = origem;
  temLeitura = true;

  int limite = histTotal;
  if (limite >= MAX_HISTORICO) limite = MAX_HISTORICO - 1;

  for (int i = limite; i > 0; i--) {
    historico[i] = historico[i - 1];
  }

  historico[0].nome = nome;
  historico[0].uid = uid;
  historico[0].status = status;
  historico[0].hora = ultimaHora;
  historico[0].origem = origem;

  if (histTotal < MAX_HISTORICO) histTotal++;

  Serial.println(nome + " | " + status + " | " + origem);
}

// ================= BUZZER =================

void pararBuzzer() {
  digitalWrite(BUZZER_PIN, LOW);
  sequenciaBuzzer = false;
  buzzerLigado = false;
  bipesRestantes = 0;
}

void iniciarBipes(
  int quantidade, unsigned long duracao, unsigned long intervalo
) {
  pararBuzzer();
  if (quantidade <= 0) return;

  bipesRestantes = quantidade;
  duracaoBipAtual = duracao;
  intervaloBipAtual = intervalo;
  momentoBuzzer = millis();
  sequenciaBuzzer = true;
  buzzerLigado = true;

  digitalWrite(BUZZER_PIN, HIGH);
}

void tocarSom(int evento) {
  if (!buzzerHabilitado) {
    pararBuzzer();
    return;
  }

  if (evento < 0 || evento >= TOTAL_SONS) return;

  iniciarBipes(
    sons[evento].quantidade,
    sons[evento].duracao,
    sons[evento].intervalo
  );
}

void atualizarBuzzer() {
  if (!sequenciaBuzzer) return;

  unsigned long agora = millis();

  if (buzzerLigado) {
    if (agora - momentoBuzzer < duracaoBipAtual) return;

    digitalWrite(BUZZER_PIN, LOW);
    buzzerLigado = false;
    momentoBuzzer = agora;
    bipesRestantes--;

    if (bipesRestantes <= 0) sequenciaBuzzer = false;
  } else {
    if (agora - momentoBuzzer < intervaloBipAtual) return;

    digitalWrite(BUZZER_PIN, HIGH);
    buzzerLigado = true;
    momentoBuzzer = agora;
  }
}

void salvarConfigBuzzer() {
  memoria.begin("buzzer", false);
  memoria.putBool("ativo", buzzerHabilitado);

  for (int i = 0; i < TOTAL_SONS; i++) {
    memoria.putInt(("q" + String(i)).c_str(), sons[i].quantidade);
    memoria.putULong(("d" + String(i)).c_str(), sons[i].duracao);
    memoria.putULong(("i" + String(i)).c_str(), sons[i].intervalo);
  }

  memoria.end();
}

void carregarConfigBuzzer() {
  memoria.begin("buzzer", true);
  buzzerHabilitado = memoria.getBool("ativo", true);

  for (int i = 0; i < TOTAL_SONS; i++) {
    int q = memoria.getInt(("q" + String(i)).c_str(), sons[i].quantidade);
    unsigned long d = memoria.getULong(("d" + String(i)).c_str(), sons[i].duracao);
    unsigned long t = memoria.getULong(("i" + String(i)).c_str(), sons[i].intervalo);

    if (q >= 0 && q <= 10) sons[i].quantidade = q;
    if (d >= 30 && d <= 2000) sons[i].duracao = d;
    if (t >= 30 && t <= 2000) sons[i].intervalo = t;
  }

  memoria.end();
}

// ================= CANCELA =================

void salvarConfigCancela() {
  memoria.begin("cancela", false);
  memoria.putBool("automatico", fechamentoAutomatico);
  memoria.putULong("segundos", tempoFechamento);
  memoria.end();
}

void carregarConfigCancela() {
  memoria.begin("cancela", true);
  fechamentoAutomatico = memoria.getBool("automatico", false);
  tempoFechamento = memoria.getULong("segundos", 10);
  memoria.end();

  if (tempoFechamento < 3 || tempoFechamento > 120) {
    tempoFechamento = 10;
  }
}

void abrirPorta(String uid, String nome, String origem) {
  if (!servoPronto) return;

  servoPorta.write(ANGULO_ABERTO);
  portaAberta = true;
  uidQueAbriu = uid;
  responsavelPorta = nome;
  origemPorta = origem;
  momentoAbertura = millis();
}

void fecharPorta() {
  if (servoPronto) servoPorta.write(ANGULO_FECHADO);

  portaAberta = false;
  uidQueAbriu = "";
  responsavelPorta = "";
  origemPorta = "";
}

unsigned long segundosRestantes() {
  if (!portaAberta || !fechamentoAutomatico) return 0;

  unsigned long limite = tempoFechamento * 1000UL;
  unsigned long passou = millis() - momentoAbertura;

  if (passou >= limite) return 0;
  return (limite - passou + 999) / 1000;
}

void atualizarCancela() {
  if (!portaAberta || !fechamentoAutomatico) return;
  if (millis() - momentoAbertura < tempoFechamento * 1000UL) return;

  String uid = uidQueAbriu;
  String nome = responsavelPorta;

  fecharPorta();
  registrarEvento(nome, uid, "FECHAMENTO AUTOMATICO", "Automático");
  tocarSom(SOM_FECHAR);

  if (!aguardandoGuiado) {
    mostrarLCD("FECHOU SOZINHA", nome, 0, 4000);
  }
}

// ================= LCD =================

void escreverLinhaLCD(int linha, String texto) {
  if (texto.length() > 16) texto = texto.substring(0, 16);
  while (texto.length() < 16) texto += " ";

  lcd.setCursor(0, linha);
  lcd.print(texto);
}

void mostrarAguardando() {
  telaTemporaria = false;
  rolandoNome = false;
  posicaoAnimacao = 0;
  direcaoAnimacao = 1;
  ultimaAnimacao = millis();

  if (aguardandoGuiado) {
    escreverLinhaLCD(0, "MODO CADASTRO");
    escreverLinhaLCD(1, "PASSE O CARTAO");
  } else if (portaAberta) {
    escreverLinhaLCD(0, "  CANCELA ABERTA");
    lcd.setCursor(0, 0);
    lcd.write((uint8_t)1);

    escreverLinhaLCD(
      1, fechamentoAutomatico ? "FECHANDO EM..." : "MESMO: FECHAR"
    );
  } else {
    escreverLinhaLCD(0, "APROXIME CARTAO");
    escreverLinhaLCD(1, "");
    lcd.setCursor(0, 1);
    lcd.write((uint8_t)5);
  }
}

void mostrarLCD(
  String titulo, String nome, int icone, unsigned long tempo
) {
  escreverLinhaLCD(0, "  " + titulo);
  lcd.setCursor(0, 0);
  lcd.write((uint8_t)icone);

  textoRolagem = nome + "   ";
  posicaoRolagem = 0;
  ultimaRolagem = millis();
  rolandoNome = nome.length() > 16;

  escreverLinhaLCD(1, nome);

  telaTemporaria = true;
  inicioTelaTemporaria = millis();
  duracaoTelaTemporaria = tempo;
}

void atualizarLCD() {
  unsigned long agora = millis();

  if (telaTemporaria) {
    if (agora - inicioTelaTemporaria >= duracaoTelaTemporaria) {
      mostrarAguardando();
      return;
    }

    if (rolandoNome && agora - ultimaRolagem >= 350) {
      ultimaRolagem = agora;
      posicaoRolagem++;

      if (posicaoRolagem >= (int)textoRolagem.length()) {
        posicaoRolagem = 0;
      }

      String dupla = textoRolagem + textoRolagem;
      escreverLinhaLCD(
        1, dupla.substring(posicaoRolagem, posicaoRolagem + 16)
      );
    }

    return;
  }

  if (agora - ultimaAnimacao < 200) return;
  ultimaAnimacao = agora;

  if (aguardandoGuiado) {
    faseAnimacao = !faseAnimacao;
    lcd.setCursor(15, 1);
    if (faseAnimacao) lcd.write((uint8_t)4);
    else lcd.print(" ");
    return;
  }

  if (portaAberta) {
    if (fechamentoAutomatico) {
      escreverLinhaLCD(
        1, "FECHA EM " + String(segundosRestantes()) + "s"
      );
    } else {
      faseAnimacao = !faseAnimacao;
      lcd.setCursor(15, 1);
      if (faseAnimacao) lcd.write((uint8_t)4);
      else lcd.print(" ");
    }

    return;
  }

  lcd.setCursor(posicaoAnimacao, 1);
  lcd.print(" ");
  posicaoAnimacao += direcaoAnimacao;

  if (posicaoAnimacao >= 15) {
    posicaoAnimacao = 15;
    direcaoAnimacao = -1;
  } else if (posicaoAnimacao <= 0) {
    posicaoAnimacao = 0;
    direcaoAnimacao = 1;
  }

  lcd.setCursor(posicaoAnimacao, 1);
  lcd.write((uint8_t)5);
}

// ================= CADASTRO GUIADO =================

void limparGuiado() {
  tokenGuiado = "";
  uidGuiado = "";
  aguardandoGuiado = false;
}

void atualizarGuiado() {
  if (tokenGuiado == "") return;

  if (!ehAdmin(procurarSessao(tokenGuiado))) {
    limparGuiado();
    mostrarAguardando();
    return;
  }

  if (aguardandoGuiado &&
      millis() - inicioGuiado >= prazoGuiado) {
    limparGuiado();
    mostrarAguardando();
  }
}

void rotaGuiadoIniciar() {
  if (!exigirAdmin()) return;
  atualizarGuiado();

  String token = tokenRecebido();

  if (aguardandoGuiado && tokenGuiado != token) {
    servidor.send(
      409, "text/plain; charset=utf-8",
      "Outro administrador está aguardando um cartão."
    );
    return;
  }

  tokenGuiado = token;
  uidGuiado = "";
  aguardandoGuiado = true;
  inicioGuiado = millis();

  mostrarAguardando();

  servidor.send(
    200, "text/plain; charset=utf-8",
    "Aproxime o cartão. Esta leitura não movimenta a cancela."
  );
}

void rotaGuiadoCancelar() {
  if (!exigirAdmin()) return;

  if (tokenGuiado != "" && tokenGuiado != tokenRecebido()) {
    servidor.send(403, "text/plain", "Leitura pertence a outra sessao.");
    return;
  }

  bool estavaAguardando = aguardandoGuiado;
  limparGuiado();

  if (estavaAguardando) mostrarAguardando();

  servidor.send(200, "text/plain; charset=utf-8", "Leitura guiada encerrada.");
}

// ================= VALIDACAO =================

bool lerNumero(
  String chave, long minimo, long maximo, long &valor
) {
  if (!servidor.hasArg(chave)) return false;

  String texto = servidor.arg(chave);
  if (texto.length() == 0 || texto.length() > 6) return false;

  for (unsigned int i = 0; i < texto.length(); i++) {
    if (texto[i] < '0' || texto[i] > '9') return false;
  }

  valor = texto.toInt();
  return valor >= minimo && valor <= maximo;
}

// ================= SITE =================

const char PAGINA[] PROGMEM = R"rawliteral(
<!DOCTYPE html>
<html lang="pt-BR">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Controle de acesso</title>
<style>
:root{
 --fundo:#edf1f7;--card:#fff;--texto:#182234;--sub:#64748b;
 --borda:#dde4ee;--sec:#f3f6fa;--azul:#2563eb;
 --verde:#198754;--vermelho:#c43d4d;
}
body.dark{
 --fundo:#101722;--card:#192333;--texto:#edf2fa;
 --sub:#a6b3c7;--borda:#334156;--sec:#233044;
}
*{box-sizing:border-box}
body{margin:0;padding:16px;font-family:Arial,sans-serif;background:var(--fundo);color:var(--texto)}
.container{max-width:700px;margin:auto}
.card{background:var(--card);border:1px solid var(--borda);border-radius:18px;padding:19px;margin-bottom:14px;box-shadow:0 5px 18px #00000008}
h1{font-size:22px;margin:0}h2{font-size:18px;margin:0 0 12px}
.sub,.pequeno{color:var(--sub);line-height:1.5}
.sub{font-size:13px}.pequeno{font-size:12px;margin-top:5px}
input,select{width:100%;padding:12px;margin:6px 0;background:var(--card);color:var(--texto);border:1px solid var(--borda);border-radius:10px;font-size:15px}
input[type=checkbox]{width:20px;height:20px;margin:0}
button{border:0;border-radius:10px;padding:11px 14px;font-size:14px;cursor:pointer}
button:disabled{opacity:.5;cursor:wait}
.azul{background:var(--azul);color:white}
.verde{background:var(--verde);color:white}
.vermelho{background:var(--vermelho);color:white}
.cinza{background:var(--sec);color:var(--texto)}
.escondido{display:none!important}
.login{max-width:390px;margin:65px auto}
.loginTopo{text-align:center;margin-bottom:20px}
.loginIcone{font-size:42px;margin-bottom:10px}
.erro{min-height:20px;color:var(--vermelho);font-size:13px;margin:6px 0}
.topo,.histTopo{display:flex;justify-content:space-between;align-items:center;gap:10px}
.topoBtns,.acoes{display:flex;gap:7px;flex-wrap:wrap}
.online{font-size:12px;margin-top:6px;color:var(--sub)}
.online.ok{color:var(--verde)}.online.falha{color:var(--vermelho)}
.abas{display:flex;gap:7px;overflow-x:auto;margin-bottom:14px}
.abaBotao{white-space:nowrap;background:var(--sec);color:var(--texto)}
.abaBotao.ativa{background:var(--azul);color:white}
.aba{display:none}.aba.ativa{display:block;animation:aparecer .2s ease}
@keyframes aparecer{from{opacity:0;transform:translateY(4px)}to{opacity:1;transform:translateY(0)}}
.status{padding:18px;border-radius:13px;text-align:center;background:var(--sec)}
.status h2{margin:0 0 8px}
.status.ok,.badge.ok{background:#19875420;color:var(--verde)}
.status.erro,.badge.erro{background:#c43d4d20;color:var(--vermelho)}
.status.alerta,.badge.alerta{background:#d99a1720;color:#b77d08}
.contadores{display:grid;grid-template-columns:1fr 1fr;gap:10px;margin-top:14px}
.contador{background:var(--sec);padding:14px;border-radius:12px}
.contador span{font-size:12px;color:var(--sub)}
.contador strong{display:block;font-size:28px;margin-top:5px}
.porta{text-align:center;padding:16px;background:var(--sec);border-radius:12px}
.portaIcone{font-size:35px;margin-bottom:7px}
.porta strong{display:block;font-size:19px}
.linha{border-top:1px solid var(--borda);padding:14px 0}
.linha:first-child{border-top:0}
.nome{font-weight:bold}
.badge{display:inline-block;padding:5px 8px;border-radius:9px;font-size:11px;margin-top:7px}
.acoes{margin-top:10px}
.duplo{display:flex;gap:8px;margin:10px 0}.duplo button{flex:1}
.histHora{font-size:12px;color:var(--sub);white-space:nowrap}
.switch{display:flex;align-items:center;gap:10px;margin:15px 0}
.somCampos{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:8px;margin:10px 0}
.somCampos label{font-size:12px;color:var(--sub)}
.somCampos input{padding:9px}
.aviso{min-height:20px;font-size:13px;color:var(--sub);line-height:1.5;margin-top:9px}
.filtros{display:grid;grid-template-columns:1fr 1fr;gap:8px}
.guiado{padding:14px;border-radius:12px;background:var(--sec);margin:14px 0}
.guiado.aguardando{outline:2px solid var(--azul)}
.passos{font-size:13px;color:var(--sub);line-height:1.6}
.apresentacaoCard{text-align:center;padding:26px}
.apresentacaoCard h1{font-size:clamp(24px,5vw,42px)}
.demoStatus{margin-top:18px;padding:25px}
.demoStatus h2{font-size:clamp(23px,5vw,40px)}
.demoNome{font-size:clamp(20px,4vw,32px);font-weight:bold;overflow-wrap:anywhere}
.demoDetalhes{margin-top:12px;font-size:14px;color:var(--sub)}
.cena{position:relative;height:190px;max-width:430px;margin:22px auto 12px;overflow:hidden}
.pista{position:absolute;bottom:16px;left:0;right:0;height:50px;background:var(--sec);border-radius:12px}
.pista:after{content:"";position:absolute;left:10%;right:10%;top:24px;border-top:3px dashed var(--borda)}
.baseCancela{position:absolute;bottom:30px;left:28px;width:30px;height:95px;border-radius:7px;background:var(--azul)}
.hasteCancela{position:absolute;left:43px;bottom:107px;width:260px;max-width:75%;height:15px;border:2px solid #e0e5ed;border-radius:5px;background:repeating-linear-gradient(45deg,#f8fafc 0px,#f8fafc 18px,#e64c5e 18px,#e64c5e 36px);transform-origin:8px 50%;transition:transform .7s ease}
.cena.aberta .hasteCancela{transform:rotate(-70deg)}
.demoPorta{font-size:20px;font-weight:bold}
body.modoApresentacao .container{max-width:1000px}
body.modoApresentacao #cabecalho,body.modoApresentacao #abasAdmin{display:none}
#adminApresentacao:fullscreen{overflow:auto;background:var(--fundo);padding:20px}
@media(max-width:420px){
 .somCampos,.filtros{grid-template-columns:1fr}
 .topo{align-items:flex-start}
 .apresentacaoCard{padding:17px}
}
@media(prefers-reduced-motion:reduce){
 .aba.ativa{animation:none}
 .hasteCancela{transition:none}
}
</style>
</head>
<body>
<div id="telaLogin" class="login">
 <div class="card">
  <div class="loginTopo">
   <div class="loginIcone">🔐</div>
   <h1>Controle de acesso</h1>
   <div class="sub">Administrador ou visitante</div>
  </div>
  <input id="loginUsuario" placeholder="Usuário" autocomplete="username">
  <input id="loginSenha" type="password" placeholder="Senha" autocomplete="current-password">
  <div id="erroLogin" class="erro"></div>
  <button id="entrarBtn" class="azul" style="width:100%" onclick="entrar()">Entrar</button>
 </div>
</div>

<div id="sistema" class="container escondido">
 <div id="cabecalho" class="card">
  <div class="topo">
   <div>
    <h1>Controle de acesso</h1>
    <div id="nomeConta" class="sub"></div>
    <div id="conexao" class="online">● Conectando...</div>
   </div>
   <div class="topoBtns">
    <button id="temaBtn" class="cinza" onclick="trocarTema()">🌙</button>
    <button class="vermelho" onclick="sair()">Sair</button>
   </div>
  </div>
 </div>

 <div id="abasAdmin" class="abas escondido">
  <button id="btnAPainel" class="abaBotao" onclick="abaAdmin('painel')">Painel</button>
  <button id="btnACad" class="abaBotao" onclick="abaAdmin('cadastro')">Cadastros</button>
  <button id="btnAHist" class="abaBotao" onclick="abaAdmin('historico')">Histórico</button>
  <button id="btnACancela" class="abaBotao" onclick="abaAdmin('cancela')">Cancela</button>
  <button id="btnABuzzer" class="abaBotao" onclick="abaAdmin('buzzer')">🔊 Buzzer</button>
  <button id="btnADemo" class="abaBotao" onclick="abaAdmin('apresentacao')">Apresentação</button>
 </div>

 <div id="abasVisitante" class="abas escondido">
  <button id="btnVInicio" class="abaBotao" onclick="abaVisitante('inicio')">Minha conta</button>
  <button id="btnVHist" class="abaBotao" onclick="abaVisitante('historico')">Meu histórico</button>
 </div>

 <div id="adminPainel" class="aba">
  <div class="card">
   <h2>Último evento</h2>
   <div id="statusAdmin" class="status">
    <h2 id="statusTitulo">Aguardando cartão</h2>
    <div id="statusNome">Nenhum evento</div>
    <div id="statusUID" class="pequeno"></div>
    <div id="statusHora" class="pequeno"></div>
    <div id="statusOrigem" class="pequeno"></div>
   </div>
   <div class="contadores">
    <div class="contador"><span>Autorizações por cartão</span><strong id="liberados">0</strong></div>
    <div class="contador"><span>Cartões negados</span><strong id="negados">0</strong></div>
   </div>
   <div class="pequeno">Aberturas e fechamentos autorizados pelo cartão entram no primeiro contador.</div>
  </div>
  <div class="card">
   <h2>Cancela em tempo real</h2>
   <div class="porta">
    <div id="portaIcone" class="portaIcone">🔒</div>
    <strong id="estadoPortaAdmin">Cancela fechada</strong>
    <div id="responsavelPorta" class="pequeno"></div>
    <div id="contagemPorta" class="pequeno"></div>
   </div>
   <div class="duplo">
    <button id="abrirBtn" class="verde" onclick="comandarCancela('abrir')">Abrir cancela</button>
    <button id="fecharBtn" class="vermelho" onclick="comandarCancela('fechar')">Fechar cancela</button>
   </div>
   <div id="msgComando" class="aviso"></div>
   <div class="sub">O estado acompanha o comando enviado ao servo. Não há sensor de posição instalado.</div>
  </div>
  <div class="card">
   <h2>Sistema</h2>
   <div id="resumoBuzzer" class="sub"></div>
   <div id="resumoServo" class="sub"></div>
   <div id="resumoSistema" class="sub"></div>
  </div>
 </div>

 <div id="adminCadastro" class="aba">
  <div class="card">
   <h2 id="tituloCadastro">Cadastrar cartão</h2>
   <div class="passos">1. Leia o cartão.<br>2. Complete os dados.<br>3. Salve o cadastro.</div>
   <div id="caixaGuiado" class="guiado">
    <div class="nome">Cadastro guiado</div>
    <div class="sub">A leitura guiada captura o UID sem comandar a cancela.</div>
    <div class="duplo">
     <button id="lerCartaoBtn" class="azul" onclick="iniciarGuiado()">Ler próximo cartão</button>
     <button id="cancelarGuiadoBtn" class="cinza" onclick="cancelarGuiado()">Cancelar leitura</button>
    </div>
    <div id="msgGuiado" class="aviso">Clique em “Ler próximo cartão” para começar.</div>
   </div>
   <input id="cadUID" placeholder="UID do cartão">
   <div class="duplo">
    <button class="cinza" onclick="usarUltimoUID()">Usar último UID</button>
    <button class="cinza" onclick="limparCampos()">Limpar formulário</button>
   </div>
   <input id="cadNome" placeholder="Nome da pessoa">
   <input id="cadLogin" placeholder="Login do visitante">
   <input id="cadSenha" type="password" placeholder="Senha: vazia mantém a atual ao editar">
   <button id="salvarCadastroBtn" class="azul" style="width:100%;margin-top:8px" onclick="cadastrar()">Cadastrar / atualizar</button>
   <div id="msgCadastro" class="aviso"></div>
  </div>
  <div class="card">
   <h2>Cartões cadastrados</h2>
   <input id="buscaUsuarios" placeholder="Buscar nome, UID ou login" oninput="renderizarUsuarios()">
   <div id="usuarios"></div>
  </div>
 </div>

 <div id="adminHistorico" class="aba">
  <div class="card">
   <h2>Histórico geral</h2>
   <div class="sub">Últimos 50 eventos desta inicialização. O download respeita os filtros.</div>
   <input id="buscaHistorico" placeholder="Buscar nome ou UID" oninput="renderizarHistoricoAdmin()">
   <div class="filtros">
    <select id="filtroHistorico" onchange="renderizarHistoricoAdmin()">
     <option value="todos">Todos os resultados</option>
     <option value="autorizado">Aberturas e fechamentos</option>
     <option value="negado">Acessos negados</option>
     <option value="outros">Outros eventos</option>
    </select>
    <select id="filtroOrigem" onchange="renderizarHistoricoAdmin()">
     <option value="">Todas as origens</option>
     <option value="Cartão">Cartão</option>
     <option value="Site">Site</option>
     <option value="Automático">Automático</option>
     <option value="Cadastro">Cadastro</option>
    </select>
   </div>
   <button class="azul" onclick="baixarHistorico()">Baixar CSV</button>
   <div id="quantidadeHistorico" class="pequeno"></div>
   <div id="historicoAdmin"></div>
  </div>
 </div>

 <div id="adminCancela" class="aba">
  <div class="card">
   <h2>Fechamento automático</h2>
   <div class="sub">Desligado: o mesmo cartão fecha a cancela. O administrador também pode fechá-la no painel.</div>
   <label class="switch">
    <input id="autoAtivo" type="checkbox">
    <span>Fechar automaticamente após abrir</span>
   </label>
   <label class="sub">Tempo de abertura, em segundos</label>
   <input id="autoTempo" type="number" min="3" max="120" step="1" value="10">
   <button id="salvarCancelaBtn" class="azul" style="width:100%" onclick="salvarCancela()">Salvar configuração</button>
   <div id="msgCancela" class="aviso"></div>
   <div class="sub">Tempo: 3 a 120 segundos. Se salvar enquanto estiver aberta, a contagem recomeça.</div>
  </div>
 </div>

 <div id="adminBuzzer" class="aba">
  <div class="card">
   <h2>Configurações do buzzer</h2>
   <div class="sub">Quantidade zero deixa a ação sem som. Tempos em milissegundos.</div>
   <label class="switch">
    <input id="buzzerAtivo" type="checkbox">
    <span>Ativar sons do sistema</span>
   </label>
   <div id="configSons"></div>
   <button id="salvarBuzzerBtn" class="azul" style="width:100%" onclick="salvarBuzzer()">Salvar configurações</button>
   <div id="msgBuzzer" class="aviso"></div>
   <div class="sub">Testar não salva nem movimenta a cancela. Funciona mesmo com os sons automáticos desligados.</div>
  </div>
 </div>

 <div id="adminApresentacao" class="aba">
  <div class="card apresentacaoCard">
   <div class="topoBtns" style="justify-content:space-between;margin-bottom:24px">
    <button class="cinza" onclick="sairApresentacao()">Voltar ao painel</button>
    <button class="azul" onclick="telaCheia()">Tela cheia</button>
   </div>
   <div class="sub">CIDADANIA DIGITAL</div>
   <h1>Controle de acesso por RFID</h1>
   <div id="demoConexao" class="online">● Conectando...</div>
   <div id="demoStatus" class="status demoStatus">
    <h2 id="demoTitulo">Aproxime seu cartão</h2>
    <div id="demoNome" class="demoNome">O sistema está aguardando</div>
    <div id="demoDetalhes" class="demoDetalhes"></div>
   </div>
   <div id="demoCena" class="cena" aria-hidden="true">
    <div class="pista"></div>
    <div class="baseCancela"></div>
    <div class="hasteCancela"></div>
   </div>
   <div id="demoPorta" class="demoPorta">Cancela fechada</div>
   <div id="demoContagem" class="pequeno"></div>
   <div class="pequeno">A animação acompanha o comando da cancela.</div>
   <div class="contadores">
    <div class="contador"><span>Autorizações por cartão</span><strong id="demoLiberados">0</strong></div>
    <div class="contador"><span>Acessos negados</span><strong id="demoNegados">0</strong></div>
   </div>
   <div id="msgTelaCheia" class="aviso"></div>
  </div>
 </div>

 <div id="visitanteInicio" class="aba">
  <div class="card">
   <h2 id="visitanteNome">Minha conta</h2>
   <div id="permissaoVisitante" class="pequeno"></div>
   <br>
   <div id="visitanteStatusCaixa" class="status">
    <h2 id="visitanteStatus">Nenhum acesso</h2>
    <div id="visitanteHora"></div>
   </div>
   <div id="visitantePorta" class="pequeno" style="text-align:center;margin-top:16px"></div>
  </div>
 </div>

 <div id="visitanteHistorico" class="aba">
  <div class="card">
   <h2>Meu histórico</h2>
   <div class="sub">Somente os eventos vinculados ao seu cartão.</div>
   <div id="historicoVisitante"></div>
  </div>
 </div>
</div>

<script>
var token="";
var tipoConta="";
var ultimoUID="";
var listaUsuariosAtual=[];
var listaHistoricoAtual=[];
var consultando=false;
var encerrando=false;
var comandando=false;
var configBuzzerCarregada=false;
var configCancelaCarregada=false;
var carregandoBuzzer=false;
var carregandoCancela=false;
var guiadoOcupado=false;
var guiadoEstavaAguardando=false;
var capturaAplicada="";

var nomesSons=[
 "Abrir cancela",
 "Fechar cancela",
 "Acesso negado",
 "Outro cartão: cancela já aberta"
];

var descricoesSons=[
 "Abertura pelo cartão ou pelo administrador.",
 "Fechamento pelo cartão, site ou automaticamente.",
 "Cartão desconhecido, bloqueado ou falha no servo.",
 "Outro cartão autorizado com a cancela aberta."
];

var el=function(id){return document.getElementById(id);};

var esc=function(t){
 return String(t==null?"":t)
  .replace(/&/g,"&amp;").replace(/</g,"&lt;")
  .replace(/>/g,"&gt;").replace(/"/g,"&quot;")
  .replace(/'/g,"&#039;");
};

var grupoStatus=function(s){
 if(["ENTRADA ABERTA","ENTRADA FECHADA","ABERTURA MANUAL",
     "FECHAMENTO MANUAL","FECHAMENTO AUTOMATICO",
     "FECHAMENTO POR BLOQUEIO","FECHAMENTO POR EXCLUSAO"].indexOf(s)>=0){
  return "autorizado";
 }
 if(s=="BLOQUEADO"||s=="NAO CADASTRADO")return "negado";
 return "outros";
};

var classeStatus=function(s){
 if(grupoStatus(s)=="autorizado")return "ok";
 if(s=="PORTA JA ABERTA"||s=="LEITURA PARA CADASTRO")return "alerta";
 return s?"erro":"";
};

var textoApresentacao=function(s){
 var textos={
  "ENTRADA ABERTA":"Acesso liberado",
  "ENTRADA FECHADA":"Cancela fechada",
  "NAO CADASTRADO":"Cartão não cadastrado",
  "BLOQUEADO":"Acesso bloqueado",
  "PORTA JA ABERTA":"A cancela já está aberta",
  "FALHA NO SERVO":"Falha na configuração do servo",
  "ABERTURA MANUAL":"Abertura pelo administrador",
  "FECHAMENTO MANUAL":"Fechamento pelo administrador",
  "FECHAMENTO AUTOMATICO":"Fechamento automático",
  "FECHAMENTO POR BLOQUEIO":"Fechamento por bloqueio",
  "FECHAMENTO POR EXCLUSAO":"Fechamento por exclusão",
  "LEITURA PARA CADASTRO":"Cartão lido para cadastro"
 };
 return textos[s]||s;
};

var estadoConexao=function(ok){
 var texto=ok?"● ESP32 online":"● Sem comunicação com o ESP32";
 ["conexao","demoConexao"].forEach(function(id){
  el(id).className="online "+(ok?"ok":"falha");
  el(id).textContent=texto;
 });
};

var trocarTema=function(){
 document.body.classList.toggle("dark");
 var escuro=document.body.classList.contains("dark");
 try{localStorage.setItem("tema",escuro?"dark":"light");}catch(e){}
 el("temaBtn").textContent=escuro?"☀️":"🌙";
};

var carregarTema=function(){
 try{
  if(localStorage.getItem("tema")=="dark"){
   document.body.classList.add("dark");
   el("temaBtn").textContent="☀️";
  }
 }catch(e){}
};

var enviarPost=function(url,dados){
 var corpo=new URLSearchParams();
 corpo.append("token",token);
 Object.keys(dados||{}).forEach(function(k){corpo.append(k,dados[k]);});
 return fetch(url,{method:"POST",body:corpo}).then(function(r){
  return r.text().then(function(t){
   if(!r.ok)throw new Error(t||"Operação recusada.");
   return t;
  });
 });
};

var entrar=function(){
 var usuario=el("loginUsuario").value.trim();
 var senha=el("loginSenha").value;
 el("erroLogin").textContent="";
 if(!usuario||!senha){el("erroLogin").textContent="Digite usuário e senha.";return;}
 if(el("entrarBtn").disabled)return;
 el("entrarBtn").disabled=true;
 var corpo=new URLSearchParams();
 corpo.append("usuario",usuario);corpo.append("senha",senha);
 fetch("/login",{method:"POST",body:corpo}).then(function(r){
  return r.json().then(function(d){
   if(!r.ok)throw new Error(d.erro||"Login inválido.");
   token=d.token;tipoConta=d.tipo;
   try{sessionStorage.setItem("token",token);}catch(e){}
   mostrarSistema(d.tipo,d.nome);
   sincronizarHora();atualizar();
  });
 }).catch(function(e){el("erroLogin").textContent=e.message;})
 .then(function(){el("entrarBtn").disabled=false;});
};

var mostrarSistema=function(tipo,nome){
 el("telaLogin").classList.add("escondido");
 el("sistema").classList.remove("escondido");
 el("nomeConta").textContent=tipo=="admin"?"Administrador":nome;
 el("abasAdmin").classList.toggle("escondido",tipo!="admin");
 el("abasVisitante").classList.toggle("escondido",tipo=="admin");
 document.querySelectorAll(".aba").forEach(function(e){e.classList.remove("ativa");});
 if(tipo=="admin"){
  abaAdmin("painel");carregarBuzzer();carregarCancela();
 }else{abaVisitante("inicio");}
};

var abaAdmin=function(aba){
 var nomes=["painel","cadastro","historico","cancela","buzzer","apresentacao"];
 var caixas=["adminPainel","adminCadastro","adminHistorico","adminCancela","adminBuzzer","adminApresentacao"];
 var botoes=["btnAPainel","btnACad","btnAHist","btnACancela","btnABuzzer","btnADemo"];
 for(var i=0;i<nomes.length;i++){
  el(caixas[i]).classList.toggle("ativa",aba==nomes[i]);
  el(botoes[i]).classList.toggle("ativa",aba==nomes[i]);
 }
 document.body.classList.toggle("modoApresentacao",aba=="apresentacao");
 if(aba=="buzzer"&&!configBuzzerCarregada)carregarBuzzer();
 if(aba=="cancela"&&!configCancelaCarregada)carregarCancela();
};

var sairApresentacao=function(){
 if(document.fullscreenElement&&document.exitFullscreen){
  document.exitFullscreen().catch(function(){});
 }
 abaAdmin("painel");
};

var telaCheia=function(){
 var caixa=el("adminApresentacao");
 if(!caixa.requestFullscreen){
  el("msgTelaCheia").textContent="Este navegador não oferece tela cheia. A apresentação continua funcionando aqui.";
  return;
 }
 caixa.requestFullscreen().then(function(){
  el("msgTelaCheia").textContent="";
 }).catch(function(){
  el("msgTelaCheia").textContent="Não foi possível entrar em tela cheia neste navegador.";
 });
};

var abaVisitante=function(aba){
 el("visitanteInicio").classList.toggle("ativa",aba=="inicio");
 el("visitanteHistorico").classList.toggle("ativa",aba=="historico");
 el("btnVInicio").classList.toggle("ativa",aba=="inicio");
 el("btnVHist").classList.toggle("ativa",aba=="historico");
};

var sincronizarHora=function(){
 var agora=new Date();
 enviarPost("/hora",{segundos:agora.getHours()*3600+agora.getMinutes()*60+agora.getSeconds()})
 .catch(function(){});
};

var atualizar=function(){
 if(!token||consultando||encerrando)return;
 consultando=true;
 fetch("/dados?token="+encodeURIComponent(token),{cache:"no-store"})
 .then(function(r){
  if(r.status==401){
   token="";
   try{sessionStorage.removeItem("token");}catch(e){}
   location.reload();return null;
  }
  if(!r.ok)throw new Error("Falha na consulta.");
  return r.json();
 }).then(function(d){
  if(!d)return;
  estadoConexao(true);
  if(d.tipo=="admin")atualizarAdmin(d);
  else atualizarVisitante(d);
 }).catch(function(){estadoConexao(false);})
 .then(function(){consultando=false;});
};

var atualizarAdmin=function(d){
 ultimoUID=d.ultimoCartao||"";
 el("liberados").textContent=d.liberados;
 el("negados").textContent=d.negados;
 el("portaIcone").textContent=d.portaAberta?"🔓":"🔒";
 el("estadoPortaAdmin").textContent=d.portaAberta?"Cancela aberta":"Cancela fechada";
 el("responsavelPorta").textContent=
  d.portaAberta?"Aberta por "+d.responsavel+" • "+d.origemPorta:"";
 var contagem=d.portaAberta&&d.automatico?
  "Fechamento em "+d.restante+" segundos":
  (d.automatico?"Fechamento automático: "+d.tempo+" segundos":"Fechamento automático desligado");
 el("contagemPorta").textContent=contagem;
 el("abrirBtn").disabled=comandando||!d.servoPronto||d.portaAberta;
 el("fecharBtn").disabled=comandando||!d.servoPronto||!d.portaAberta;
 el("resumoBuzzer").textContent=d.buzzerAtivo?"🔊 Sons ativados":"🔇 Sons desativados";
 el("resumoServo").textContent=d.servoPronto?"Servo configurado":"Falha ao configurar o servo";
 listaUsuariosAtual=d.usuarios||[];
 listaHistoricoAtual=d.historico||[];
 el("resumoSistema").textContent=
  listaUsuariosAtual.length+" cartões • "+d.clientes+" dispositivos no Wi-Fi • Ligado há "+
  Math.floor(d.uptime/3600)+"h "+Math.floor(d.uptime%3600/60)+"min";

 if(d.temLeitura){
  el("statusTitulo").textContent=d.status;
  el("statusNome").textContent=d.nome;
  el("statusUID").textContent=d.uid?"UID: "+d.uid:"";
  el("statusHora").textContent=d.hora;
  el("statusOrigem").textContent="Origem: "+d.origem;
  el("statusAdmin").className="status "+classeStatus(d.status);

  el("demoTitulo").textContent=textoApresentacao(d.status);
  el("demoNome").textContent=d.nome;
  el("demoDetalhes").textContent=d.hora+" • "+d.origem;
  el("demoStatus").className="status demoStatus "+classeStatus(d.status);
 }

 el("demoCena").classList.toggle("aberta",d.portaAberta);
 el("demoPorta").textContent=d.portaAberta?"🔓 Cancela aberta":"🔒 Cancela fechada";
 el("demoContagem").textContent=contagem;
 el("demoLiberados").textContent=d.liberados;
 el("demoNegados").textContent=d.negados;

 if(d.guiadoGlobal){
  el("demoTitulo").textContent="Lendo cartão para cadastro";
  el("demoNome").textContent="Aproxime a tag do leitor";
  el("demoDetalhes").textContent="Esta leitura apenas captura o UID";
  el("demoStatus").className="status demoStatus alerta";
 }

 renderizarUsuarios();
 renderizarHistoricoAdmin();
 atualizarGuiadoSite(d.guiado||{});
};

var atualizarVisitante=function(d){
 el("visitanteNome").textContent="Olá, "+d.nome;
 el("permissaoVisitante").textContent=d.liberado?
  "Seu cartão está liberado.":"Seu cartão está bloqueado.";
 el("visitanteStatus").textContent=d.ultimoStatus||"Nenhum acesso recente";
 el("visitanteHora").textContent=d.ultimaHora||"-";
 el("visitanteStatusCaixa").className="status "+classeStatus(d.ultimoStatus);
 el("visitantePorta").textContent=d.portaDestaPessoa?
  "🔓 Você abriu a cancela":(d.portaAberta?"🔓 Cancela aberta":"🔒 Cancela fechada");
 montarHistorico(d.historico||[],"historicoVisitante",true);
};

var comandarCancela=function(acao){
 if(comandando)return;
 comandando=true;
 el("abrirBtn").disabled=true;el("fecharBtn").disabled=true;
 el("msgComando").textContent="Enviando comando...";
 enviarPost("/cancela/comando",{acao:acao})
 .then(function(t){el("msgComando").textContent=t;})
 .catch(function(e){el("msgComando").textContent=e.message;})
 .then(function(){comandando=false;atualizar();});
};

var iniciarGuiado=function(){
 if(guiadoOcupado)return;
 guiadoOcupado=true;
 capturaAplicada="";
 el("msgGuiado").textContent="Preparando leitura...";
 enviarPost("/cadastro/ler",{}).then(function(t){
  guiadoEstavaAguardando=true;
  el("msgGuiado").textContent=t;
  abaAdmin("cadastro");
 }).catch(function(e){el("msgGuiado").textContent=e.message;})
 .then(function(){guiadoOcupado=false;atualizar();});
};

var cancelarGuiado=function(){
 if(guiadoOcupado)return;
 guiadoOcupado=true;
 enviarPost("/cadastro/cancelar",{}).then(function(t){
  guiadoEstavaAguardando=false;
  capturaAplicada="";
  el("msgGuiado").textContent=t;
 }).catch(function(e){el("msgGuiado").textContent=e.message;})
 .then(function(){guiadoOcupado=false;atualizar();});
};

var preencherCartao=function(uid){
 var existente=null;
 for(var i=0;i<listaUsuariosAtual.length;i++){
  if(listaUsuariosAtual[i].uid==uid){
   existente=listaUsuariosAtual[i];break;
  }
 }
 el("cadUID").value=uid;
 el("cadNome").value=existente?existente.nome:"";
 el("cadLogin").value=existente?existente.login:"";
 el("cadSenha").value="";
 el("tituloCadastro").textContent=existente?"Editar cartão cadastrado":"Cadastrar cartão";
 return existente;
};

var atualizarGuiadoSite=function(g){
 var aguardando=!!g.aguardando;
 el("caixaGuiado").classList.toggle("aguardando",aguardando);
 el("lerCartaoBtn").disabled=guiadoOcupado||aguardando;
 el("cancelarGuiadoBtn").disabled=guiadoOcupado||(!aguardando&&!g.uid);
 el("salvarCadastroBtn").disabled=aguardando||salvandoCadastro;

 if(aguardando){
  guiadoEstavaAguardando=true;
  el("msgGuiado").textContent="Aproxime o cartão. Tempo restante: "+g.restante+"s.";
 }else if(g.uid&&g.uid!=capturaAplicada){
  capturaAplicada=g.uid;
  guiadoEstavaAguardando=false;
  var existente=preencherCartao(g.uid);
  el("msgGuiado").textContent=existente?
   "Cartão já cadastrado. Dados carregados para edição; senha vazia mantém a atual.":
   "Cartão capturado! Complete nome, login e senha e clique em cadastrar.";
 }else if(guiadoEstavaAguardando&&!g.uid){
  guiadoEstavaAguardando=false;
  el("msgGuiado").textContent="A leitura foi encerrada. Clique em “Ler próximo cartão” para tentar novamente.";
 }
};

var renderizarUsuarios=function(){
 var busca=el("buscaUsuarios").value.trim().toLowerCase(),html="";
 for(var i=0;i<listaUsuariosAtual.length;i++){
  var u=listaUsuariosAtual[i];
  if((u.nome+" "+u.uid+" "+u.login).toLowerCase().indexOf(busca)<0)continue;
  html+='<div class="linha"><div class="nome">'+esc(u.nome)+'</div>';
  html+='<div class="pequeno">UID: '+esc(u.uid)+'</div>';
  html+='<div class="pequeno">Login: '+esc(u.login)+'</div>';
  html+='<span class="badge '+(u.liberado?"ok":"erro")+'">'+(u.liberado?"LIBERADO":"BLOQUEADO")+'</span>';
  html+='<div class="acoes"><button class="'+(u.liberado?"vermelho":"verde")+
   '" onclick="mudarStatus('+i+','+(u.liberado?0:1)+')">'+(u.liberado?"Bloquear":"Liberar")+'</button>';
  html+='<button class="cinza" onclick="editarUsuario('+i+')">Editar</button>';
  html+='<button class="cinza" onclick="excluirUsuario('+i+')">Excluir</button></div></div>';
 }
 el("usuarios").innerHTML=html||'<div class="sub">Nenhum cartão encontrado.</div>';
};

var editarUsuario=function(i){
 var u=listaUsuariosAtual[i];if(!u)return;
 abaAdmin("cadastro");
 preencherCartao(u.uid);
 el("cadNome").focus();
};

var montarHistorico=function(lista,id,pessoal){
 var html="";
 lista.forEach(function(h){
  html+='<div class="linha"><div class="histTopo"><div class="nome">'+
   esc(pessoal?h.status:h.nome)+'</div><div class="histHora">'+esc(h.hora)+'</div></div>';
  if(h.uid)html+='<div class="pequeno">UID: '+esc(h.uid)+'</div>';
  html+='<div class="pequeno">Origem: '+esc(h.origem)+'</div>';
  html+='<span class="badge '+classeStatus(h.status)+'">'+esc(h.status)+'</span></div>';
 });
 el(id).innerHTML=html||'<div class="sub">Nenhum evento encontrado.</div>';
};

var historicoFiltrado=function(){
 var busca=el("buscaHistorico").value.trim().toLowerCase();
 var grupo=el("filtroHistorico").value;
 var origem=el("filtroOrigem").value;
 return listaHistoricoAtual.filter(function(h){
  return (h.nome+" "+h.uid).toLowerCase().indexOf(busca)>=0&&
   (grupo=="todos"||grupoStatus(h.status)==grupo)&&(!origem||h.origem==origem);
 });
};

var renderizarHistoricoAdmin=function(){
 var lista=historicoFiltrado();
 el("quantidadeHistorico").textContent=lista.length+" eventos exibidos";
 montarHistorico(lista,"historicoAdmin",false);
};

var campoCSV=function(t){
 var s=String(t==null?"":t);
 if(/^[\s]*[=+\-@]/.test(s))s="'"+s;
 return '"'+s.replace(/"/g,'""')+'"';
};

var baixarHistorico=function(){
 var lista=historicoFiltrado();
 if(!lista.length){alert("Não há registros para baixar.");return;}
 var linhas=["Hora;Nome;UID;Resultado;Origem"];
 lista.forEach(function(h){
  linhas.push([h.hora,h.nome,h.uid,h.status,h.origem].map(campoCSV).join(";"));
 });
 var blob=new Blob(["﻿"+linhas.join("\r\n")],{type:"text/csv;charset=utf-8"});
 var url=URL.createObjectURL(blob),a=document.createElement("a");
 a.href=url;a.download="historico_acessos.csv";
 document.body.appendChild(a);a.click();a.remove();
 setTimeout(function(){URL.revokeObjectURL(url);},1000);
};

var usarUltimoUID=function(){
 if(!ultimoUID){alert("Aproxime um cartão primeiro.");return;}
 preencherCartao(ultimoUID);
};

var limparCampos=function(){
 ["cadUID","cadNome","cadLogin","cadSenha"].forEach(function(id){el(id).value="";});
 el("tituloCadastro").textContent="Cadastrar cartão";
};

var salvandoCadastro=false;

var cadastrar=function(){
 if(salvandoCadastro||guiadoEstavaAguardando)return;
 var dados={
  uid:el("cadUID").value.trim(),nome:el("cadNome").value.trim(),
  login:el("cadLogin").value.trim(),senha:el("cadSenha").value
 };
 if(!dados.uid||!dados.nome||!dados.login){
  el("msgCadastro").textContent="Preencha UID, nome e login.";return;
 }
 salvandoCadastro=true;
 el("salvarCadastroBtn").disabled=true;
 el("msgCadastro").textContent="Salvando...";
 enviarPost("/cadastrar",dados).then(function(t){
  el("msgCadastro").textContent=t;
  limparCampos();
  el("msgGuiado").textContent="Cadastro salvo. Você pode ler outro cartão.";
 }).catch(function(e){el("msgCadastro").textContent=e.message;})
 .then(function(){
  salvandoCadastro=false;el("salvarCadastroBtn").disabled=false;atualizar();
 });
};

var mudarStatus=function(i,liberar){
 var u=listaUsuariosAtual[i];if(!u)return;
 enviarPost("/status",{uid:u.uid,liberar:liberar})
 .then(function(){atualizar();}).catch(function(e){alert(e.message);});
};

var excluirUsuario=function(i){
 var u=listaUsuariosAtual[i];
 if(!u||!confirm("Excluir este cartão e a conta dele?"))return;
 enviarPost("/excluir",{uid:u.uid})
 .then(function(){atualizar();}).catch(function(e){alert(e.message);});
};

var carregarCancela=function(){
 if(tipoConta!="admin"||!token||carregandoCancela)return;
 carregandoCancela=true;el("msgCancela").textContent="Carregando...";
 fetch("/cancela?token="+encodeURIComponent(token),{cache:"no-store"})
 .then(function(r){if(!r.ok)throw new Error("Falha ao carregar.");return r.json();})
 .then(function(d){
  el("autoAtivo").checked=d.automatico;el("autoTempo").value=d.tempo;
  configCancelaCarregada=true;el("msgCancela").textContent="";
 }).catch(function(e){el("msgCancela").textContent=e.message;})
 .then(function(){carregandoCancela=false;});
};

var salvarCancela=function(){
 if(el("salvarCancelaBtn").disabled)return;
 if(!configCancelaCarregada){el("msgCancela").textContent="Aguarde o carregamento.";return;}
 var tempo=Number(el("autoTempo").value);
 if(!Number.isInteger(tempo)||tempo<3||tempo>120){
  el("msgCancela").textContent="Escolha de 3 a 120 segundos.";return;
 }
 el("salvarCancelaBtn").disabled=true;el("msgCancela").textContent="Salvando...";
 enviarPost("/cancela",{automatico:el("autoAtivo").checked?1:0,tempo:tempo})
 .then(function(t){el("msgCancela").textContent=t;atualizar();})
 .catch(function(e){el("msgCancela").textContent=e.message;})
 .then(function(){el("salvarCancelaBtn").disabled=false;});
};

var montarConfigSons=function(lista){
 var html="";
 for(var i=0;i<nomesSons.length;i++){
  var s=lista[i];
  html+='<div class="linha"><div class="nome">'+esc(nomesSons[i])+'</div>';
  html+='<div class="pequeno">'+esc(descricoesSons[i])+'</div><div class="somCampos">';
  html+='<label>Bipes<input id="somQ'+i+'" type="number" min="0" max="10" step="1" value="'+s.quantidade+'"></label>';
  html+='<label>Duração (ms)<input id="somD'+i+'" type="number" min="30" max="2000" step="1" value="'+s.duracao+'"></label>';
  html+='<label>Intervalo (ms)<input id="somI'+i+'" type="number" min="30" max="2000" step="1" value="'+s.intervalo+'"></label>';
  html+='</div><button class="cinza" onclick="testarSom('+i+')">▶ Testar</button></div>';
 }
 el("configSons").innerHTML=html;
};

var carregarBuzzer=function(){
 if(tipoConta!="admin"||!token||carregandoBuzzer)return;
 carregandoBuzzer=true;el("msgBuzzer").textContent="Carregando...";
 fetch("/buzzer?token="+encodeURIComponent(token),{cache:"no-store"})
 .then(function(r){if(!r.ok)throw new Error("Falha ao carregar os sons.");return r.json();})
 .then(function(d){
  el("buzzerAtivo").checked=d.ativo;montarConfigSons(d.sons);
  configBuzzerCarregada=true;el("msgBuzzer").textContent="";
 }).catch(function(e){el("msgBuzzer").textContent=e.message;})
 .then(function(){carregandoBuzzer=false;});
};

var lerSom=function(i){
 var qEl=el("somQ"+i),dEl=el("somD"+i),iEl=el("somI"+i);
 if(!qEl||!dEl||!iEl)throw new Error("Aguarde o carregamento.");
 if(qEl.value===""||dEl.value===""||iEl.value==="")throw new Error("Preencha os campos.");
 var q=Number(qEl.value),d=Number(dEl.value),t=Number(iEl.value);
 if(!Number.isInteger(q)||q<0||q>10)throw new Error("Bipes: 0 a 10.");
 if(!Number.isInteger(d)||d<30||d>2000)throw new Error("Duração: 30 a 2000 ms.");
 if(!Number.isInteger(t)||t<30||t>2000)throw new Error("Intervalo: 30 a 2000 ms.");
 return {quantidade:q,duracao:d,intervalo:t};
};

var salvarBuzzer=function(){
 if(el("salvarBuzzerBtn").disabled)return;
 var dados={ativo:el("buzzerAtivo").checked?1:0};
 try{
  for(var i=0;i<nomesSons.length;i++){
   var s=lerSom(i);
   dados["q"+i]=s.quantidade;dados["d"+i]=s.duracao;dados["i"+i]=s.intervalo;
  }
 }catch(e){el("msgBuzzer").textContent=e.message;return;}
 el("salvarBuzzerBtn").disabled=true;el("msgBuzzer").textContent="Salvando...";
 enviarPost("/buzzer",dados).then(function(t){el("msgBuzzer").textContent=t;atualizar();})
 .catch(function(e){el("msgBuzzer").textContent=e.message;})
 .then(function(){el("salvarBuzzerBtn").disabled=false;});
};

var testarSom=function(i){
 var s;
 try{s=lerSom(i);}catch(e){el("msgBuzzer").textContent=e.message;return;}
 enviarPost("/buzzer/testar",s).then(function(t){el("msgBuzzer").textContent=t;})
 .catch(function(e){el("msgBuzzer").textContent=e.message;});
};

var finalizarSaida=function(){
 token="";
 try{sessionStorage.removeItem("token");}catch(e){}
 location.reload();
};

var sair=function(){
 if(encerrando)return;
 encerrando=true;
 enviarPost("/logout",{}).then(finalizarSaida,finalizarSaida);
};

var restaurar=function(){
 try{token=sessionStorage.getItem("token")||"";}catch(e){}
 if(!token)return;
 fetch("/me?token="+encodeURIComponent(token),{cache:"no-store"})
 .then(function(r){
  if(!r.ok){
   token="";
   try{sessionStorage.removeItem("token");}catch(e){}
   return null;
  }
  return r.json();
 }).then(function(d){
  if(!d)return;
  tipoConta=d.tipo;mostrarSistema(d.tipo,d.nome);
  sincronizarHora();atualizar();
 }).catch(function(){
  token="";
  el("erroLogin").textContent="Não foi possível reconectar. Entre novamente.";
 });
};

document.addEventListener("keydown",function(e){
 if(e.key=="Enter"&&!el("telaLogin").classList.contains("escondido"))entrar();
});
carregarTema();
restaurar();
setInterval(atualizar,1000);
</script>
</body>
</html>
)rawliteral";

// ================= LOGIN =================

void paginaPrincipal() {
  servidor.send(200, "text/html; charset=utf-8", PAGINA);
}

void rotaLogin() {
  String login = servidor.arg("usuario");
  String senha = servidor.arg("senha");
  login.trim();

  bool admin = login == ADMIN_USUARIO && senha == ADMIN_SENHA;
  int indice = admin ? -1 : procurarLogin(login, senha);

  if (!admin && indice < 0) {
    servidor.send(
      401, "application/json",
      "{\"erro\":\"Usuario ou senha incorretos\"}"
    );
    return;
  }

  String token;
  String tipo = admin ? "admin" : "visitante";
  String nome = admin ? String("Administrador") : usuariosNome[indice];

  criarSessao(tipo, indice, token);

  servidor.send(
    200, "application/json; charset=utf-8",
    "{\"token\":" + jtexto(token) +
    ",\"tipo\":" + jtexto(tipo) +
    ",\"nome\":" + jtexto(nome) + "}"
  );
}

void rotaMe() {
  int sessao = procurarSessao(tokenRecebido());

  if (sessao < 0) {
    servidor.send(401, "application/json", "{\"erro\":\"Sessao invalida\"}");
    return;
  }

  String tipo = "admin";
  String nome = "Administrador";

  if (!ehAdmin(sessao)) {
    int i = sessoes[sessao].usuario;

    if (i < 0 || i >= totalUsuarios) {
      servidor.send(401, "application/json", "{\"erro\":\"Usuario invalido\"}");
      return;
    }

    tipo = "visitante";
    nome = usuariosNome[i];
  }

  servidor.sendHeader("Cache-Control", "no-store");
  servidor.send(
    200, "application/json; charset=utf-8",
    "{\"tipo\":" + jtexto(tipo) + ",\"nome\":" + jtexto(nome) + "}"
  );
}

void rotaLogout() {
  String token = tokenRecebido();
  int i = procurarSessao(token);

  if (token != "" && token == tokenGuiado) {
    bool estavaAguardando = aguardandoGuiado;
    limparGuiado();
    if (estavaAguardando) mostrarAguardando();
  }

  if (i >= 0) {
    sessoes[i].token = "";
    sessoes[i].tipo = "";
    sessoes[i].usuario = -1;
  }

  servidor.send(200, "text/plain", "OK");
}

void rotaHora() {
  if (procurarSessao(tokenRecebido()) < 0) {
    servidor.send(401, "text/plain", "Nao autorizado");
    return;
  }

  long segundos;

  if (!lerNumero("segundos", 0, 86399, segundos)) {
    servidor.send(400, "text/plain", "Hora invalida");
    return;
  }

  segundosBase = segundos;
  momentoSincronizacao = millis();
  relogioSincronizado = true;
  servidor.send(200, "text/plain", "OK");
}

// ================= DADOS =================

void enviarAdmin() {
  atualizarGuiado();

  String json;
  json.reserve(15000);

  json = "{\"tipo\":\"admin\"";
  json += ",\"liberados\":" + String(totalLiberados);
  json += ",\"negados\":" + String(totalNegados);
  json += ",\"portaAberta\":" + jsonBool(portaAberta);
  json += ",\"servoPronto\":" + jsonBool(servoPronto);
  json += ",\"buzzerAtivo\":" + jsonBool(buzzerHabilitado);
  json += ",\"automatico\":" + jsonBool(fechamentoAutomatico);
  json += ",\"tempo\":" + String(tempoFechamento);
  json += ",\"restante\":" + String(segundosRestantes());
  json += ",\"responsavel\":" + jtexto(responsavelPorta);
  json += ",\"origemPorta\":" + jtexto(origemPorta);
  json += ",\"uptime\":" + String(millis() / 1000);
  json += ",\"clientes\":" + String(WiFi.softAPgetStationNum());
  json += ",\"temLeitura\":" + jsonBool(temLeitura);
  json += ",\"ultimoCartao\":" + jtexto(ultimoUIDCartao);
  json += ",\"uid\":" + jtexto(ultimoUID);
  json += ",\"nome\":" + jtexto(ultimoNome);
  json += ",\"status\":" + jtexto(ultimoStatus);
  json += ",\"hora\":" + jtexto(ultimaHora);
  json += ",\"origem\":" + jtexto(ultimaOrigem);

  bool meuGuiado = tokenGuiado != "" && tokenGuiado == tokenRecebido();
  unsigned long restanteGuiado = 0;

  if (meuGuiado && aguardandoGuiado) {
    unsigned long passou = millis() - inicioGuiado;
    if (passou < prazoGuiado) {
      restanteGuiado = (prazoGuiado - passou + 999) / 1000;
    }
  }

  json += ",\"guiadoGlobal\":" + jsonBool(aguardandoGuiado);
  json += ",\"guiado\":{\"aguardando\":";
  json += jsonBool(meuGuiado && aguardandoGuiado);
  json += ",\"uid\":" + jtexto(meuGuiado ? uidGuiado : String(""));
  json += ",\"restante\":" + String(restanteGuiado) + "}";
  json += ",\"usuarios\":[";

  for (int i = 0; i < totalUsuarios; i++) {
    if (i) json += ",";

    json += "{\"uid\":" + jtexto(usuariosUID[i]);
    json += ",\"nome\":" + jtexto(usuariosNome[i]);
    json += ",\"login\":" + jtexto(usuariosLogin[i]);
    json += ",\"liberado\":" + jsonBool(usuariosLiberados[i]) + "}";
  }

  json += "],\"historico\":[";

  for (int i = 0; i < histTotal; i++) {
    if (i) json += ",";

    json += "{\"nome\":" + jtexto(historico[i].nome);
    json += ",\"uid\":" + jtexto(historico[i].uid);
    json += ",\"status\":" + jtexto(historico[i].status);
    json += ",\"hora\":" + jtexto(historico[i].hora);
    json += ",\"origem\":" + jtexto(historico[i].origem) + "}";
  }

  json += "]}";

  servidor.sendHeader("Cache-Control", "no-store");
  servidor.send(200, "application/json; charset=utf-8", json);
}

void enviarVisitante(int indice) {
  if (indice < 0 || indice >= totalUsuarios) {
    servidor.send(401, "application/json", "{\"erro\":\"Usuario invalido\"}");
    return;
  }

  String uid = usuariosUID[indice];
  String status = "";
  String hora = "";

  for (int i = 0; i < histTotal; i++) {
    if (historico[i].uid == uid &&
        historico[i].status != "LEITURA PARA CADASTRO") {
      status = historico[i].status;
      hora = historico[i].hora;
      break;
    }
  }

  String json;
  json.reserve(10000);

  json = "{\"tipo\":\"visitante\"";
  json += ",\"nome\":" + jtexto(usuariosNome[indice]);
  json += ",\"liberado\":" + jsonBool(usuariosLiberados[indice]);
  json += ",\"portaAberta\":" + jsonBool(portaAberta);
  json += ",\"portaDestaPessoa\":" + jsonBool(portaAberta && uidQueAbriu == uid);
  json += ",\"ultimoStatus\":" + jtexto(status);
  json += ",\"ultimaHora\":" + jtexto(hora);
  json += ",\"historico\":[";

  bool primeiro = true;

  for (int i = 0; i < histTotal; i++) {
    if (historico[i].uid != uid ||
        historico[i].status == "LEITURA PARA CADASTRO") continue;

    if (!primeiro) json += ",";
    primeiro = false;

    json += "{\"uid\":" + jtexto(historico[i].uid);
    json += ",\"status\":" + jtexto(historico[i].status);
    json += ",\"hora\":" + jtexto(historico[i].hora);
    json += ",\"origem\":" + jtexto(historico[i].origem) + "}";
  }

  json += "]}";

  servidor.sendHeader("Cache-Control", "no-store");
  servidor.send(200, "application/json; charset=utf-8", json);
}

void rotaDados() {
  int s = procurarSessao(tokenRecebido());

  if (s < 0) {
    servidor.send(401, "application/json", "{\"erro\":\"Nao autorizado\"}");
    return;
  }

  if (ehAdmin(s)) enviarAdmin();
  else enviarVisitante(sessoes[s].usuario);
}

// ================= CADASTROS =================

void rotaCadastrar() {
  if (!exigirAdmin()) return;

  if (aguardandoGuiado && tokenGuiado == tokenRecebido()) {
    servidor.send(409, "text/plain", "Leia o cartao ou cancele a leitura primeiro.");
    return;
  }

  String uid = servidor.arg("uid");
  String nome = servidor.arg("nome");
  String login = servidor.arg("login");
  String senha = servidor.arg("senha");

  uid.trim();
  uid.toUpperCase();
  nome.trim();
  login.trim();

  if (uid == "" || nome == "" || login == "") {
    servidor.send(400, "text/plain", "UID, nome e login sao obrigatorios.");
    return;
  }

  if (uid.length() > 40 || nome.length() > 80 ||
      login.length() > 40 || senha.length() > 80) {
    servidor.send(400, "text/plain", "Um campo ultrapassou o tamanho permitido.");
    return;
  }

  if (login == ADMIN_USUARIO) {
    servidor.send(400, "text/plain", "Login reservado ao administrador.");
    return;
  }

  int i = procurarUsuarioUID(uid);

  if (loginJaExiste(login, i) >= 0) {
    servidor.send(400, "text/plain", "Esse login ja esta sendo usado.");
    return;
  }

  if (i >= 0) {
    usuariosNome[i] = nome;
    bool mudouLogin = usuariosLogin[i] != login;
    usuariosLogin[i] = login;

    if (senha != "") usuariosSenha[i] = senha;
    if (mudouLogin || senha != "") invalidarVisitante(i);

    if (portaAberta && uidQueAbriu == uid) responsavelPorta = nome;

    salvarUsuarios();
    if (tokenGuiado == tokenRecebido()) limparGuiado();

    servidor.send(200, "text/plain; charset=utf-8", "Cadastro atualizado!");
    return;
  }

  if (totalUsuarios >= MAX_USUARIOS) {
    servidor.send(400, "text/plain", "Limite de usuarios atingido.");
    return;
  }

  if (senha == "") {
    servidor.send(400, "text/plain", "Digite uma senha para o novo visitante.");
    return;
  }

  usuariosUID[totalUsuarios] = uid;
  usuariosNome[totalUsuarios] = nome;
  usuariosLogin[totalUsuarios] = login;
  usuariosSenha[totalUsuarios] = senha;
  usuariosLiberados[totalUsuarios] = true;
  totalUsuarios++;

  salvarUsuarios();
  if (tokenGuiado == tokenRecebido()) limparGuiado();

  servidor.send(200, "text/plain; charset=utf-8", "Cartão e conta cadastrados!");
}

void rotaStatus() {
  if (!exigirAdmin()) return;

  String uid = servidor.arg("uid");
  uid.trim();
  uid.toUpperCase();

  int i = procurarUsuarioUID(uid);
  String valor = servidor.arg("liberar");

  if (i < 0) {
    servidor.send(404, "text/plain", "Cartao nao encontrado.");
    return;
  }

  if (valor != "0" && valor != "1") {
    servidor.send(400, "text/plain", "Status invalido.");
    return;
  }

  usuariosLiberados[i] = valor == "1";

  if (!usuariosLiberados[i] && portaAberta && uidQueAbriu == uid) {
    fecharPorta();
    registrarEvento(usuariosNome[i], uid, "FECHAMENTO POR BLOQUEIO", "Cadastro");
    tocarSom(SOM_FECHAR);
    if (!aguardandoGuiado) {
      mostrarLCD("CANCELA FECHADA", usuariosNome[i], 0, 4000);
    }
  }

  salvarUsuarios();
  servidor.send(200, "text/plain", "Status atualizado.");
}

void rotaExcluir() {
  if (!exigirAdmin()) return;

  String uid = servidor.arg("uid");
  uid.trim();
  uid.toUpperCase();

  int indice = procurarUsuarioUID(uid);

  if (indice < 0) {
    servidor.send(404, "text/plain", "Usuario nao encontrado.");
    return;
  }

  if (portaAberta && uidQueAbriu == uid) {
    fecharPorta();
    registrarEvento(usuariosNome[indice], uid, "FECHAMENTO POR EXCLUSAO", "Cadastro");
    tocarSom(SOM_FECHAR);
    if (!aguardandoGuiado) {
      mostrarLCD("CANCELA FECHADA", usuariosNome[indice], 0, 4000);
    }
  }

  for (int i = indice; i < totalUsuarios - 1; i++) {
    usuariosUID[i] = usuariosUID[i + 1];
    usuariosNome[i] = usuariosNome[i + 1];
    usuariosLogin[i] = usuariosLogin[i + 1];
    usuariosSenha[i] = usuariosSenha[i + 1];
    usuariosLiberados[i] = usuariosLiberados[i + 1];
  }

  totalUsuarios--;

  usuariosUID[totalUsuarios] = "";
  usuariosNome[totalUsuarios] = "";
  usuariosLogin[totalUsuarios] = "";
  usuariosSenha[totalUsuarios] = "";
  usuariosLiberados[totalUsuarios] = false;

  salvarUsuarios();
  invalidarVisitante(-1);

  servidor.send(200, "text/plain; charset=utf-8", "Usuário excluído.");
}

// ================= ROTAS BUZZER =================

void rotaBuzzerConsultar() {
  if (!exigirAdmin()) return;

  String json = "{\"ativo\":" + jsonBool(buzzerHabilitado);
  json += ",\"sons\":[";

  for (int i = 0; i < TOTAL_SONS; i++) {
    if (i) json += ",";

    json += "{\"quantidade\":" + String(sons[i].quantidade);
    json += ",\"duracao\":" + String(sons[i].duracao);
    json += ",\"intervalo\":" + String(sons[i].intervalo) + "}";
  }

  json += "]}";
  servidor.sendHeader("Cache-Control", "no-store");
  servidor.send(200, "application/json", json);
}

void rotaBuzzerSalvar() {
  if (!exigirAdmin()) return;

  String ativo = servidor.arg("ativo");

  if (ativo != "0" && ativo != "1") {
    servidor.send(400, "text/plain", "Estado invalido.");
    return;
  }

  ConfigSom novos[TOTAL_SONS];

  for (int i = 0; i < TOTAL_SONS; i++) {
    long q, d, t;

    if (!lerNumero("q" + String(i), 0, 10, q) ||
        !lerNumero("d" + String(i), 30, 2000, d) ||
        !lerNumero("i" + String(i), 30, 2000, t)) {
      servidor.send(400, "text/plain", "Bipes: 0 a 10. Tempos: 30 a 2000 ms.");
      return;
    }

    novos[i].quantidade = q;
    novos[i].duracao = d;
    novos[i].intervalo = t;
  }

  pararBuzzer();
  buzzerHabilitado = ativo == "1";

  for (int i = 0; i < TOTAL_SONS; i++) sons[i] = novos[i];

  salvarConfigBuzzer();
  servidor.send(200, "text/plain; charset=utf-8", "Configurações salvas!");
}

void rotaBuzzerTestar() {
  if (!exigirAdmin()) return;

  long q, d, t;

  if (!lerNumero("quantidade", 0, 10, q) ||
      !lerNumero("duracao", 30, 2000, d) ||
      !lerNumero("intervalo", 30, 2000, t)) {
    servidor.send(400, "text/plain", "Valores invalidos.");
    return;
  }

  iniciarBipes(q, d, t);

  servidor.send(
    200, "text/plain; charset=utf-8",
    q == 0 ? "Quantidade zero: sem som." : "Teste iniciado!"
  );
}

// ================= ROTAS CANCELA =================

void rotaCancelaConsultar() {
  if (!exigirAdmin()) return;

  servidor.sendHeader("Cache-Control", "no-store");
  servidor.send(
    200, "application/json",
    "{\"automatico\":" + jsonBool(fechamentoAutomatico) +
    ",\"tempo\":" + String(tempoFechamento) + "}"
  );
}

void rotaCancelaSalvar() {
  if (!exigirAdmin()) return;

  String ativo = servidor.arg("automatico");
  long tempo;

  if ((ativo != "0" && ativo != "1") ||
      !lerNumero("tempo", 3, 120, tempo)) {
    servidor.send(400, "text/plain", "Tempo permitido: 3 a 120 segundos.");
    return;
  }

  fechamentoAutomatico = ativo == "1";
  tempoFechamento = tempo;
  if (portaAberta) momentoAbertura = millis();

  salvarConfigCancela();
  if (!telaTemporaria) mostrarAguardando();

  servidor.send(200, "text/plain; charset=utf-8", "Configuração salva!");
}

void rotaCancelaComando() {
  if (!exigirAdmin()) return;

  if (!servoPronto) {
    servidor.send(503, "text/plain", "Servo nao configurado.");
    return;
  }

  String acao = servidor.arg("acao");

  if (acao == "abrir") {
    if (portaAberta) {
      servidor.send(409, "text/plain", "A cancela ja esta aberta.");
      return;
    }

    abrirPorta("", "Administrador", "Site");
    registrarEvento("Administrador", "", "ABERTURA MANUAL", "Site");
    tocarSom(SOM_ABRIR);

    if (!aguardandoGuiado) {
      mostrarLCD("CANCELA ABERTA", "ADMINISTRADOR", 1, 4000);
    }

    servidor.send(200, "text/plain; charset=utf-8", "Comando de abertura enviado.");
    return;
  }

  if (acao == "fechar") {
    if (!portaAberta) {
      servidor.send(409, "text/plain", "A cancela ja esta fechada.");
      return;
    }

    String uid = uidQueAbriu;

    fecharPorta();
    registrarEvento("Administrador", uid, "FECHAMENTO MANUAL", "Site");
    tocarSom(SOM_FECHAR);

    if (!aguardandoGuiado) {
      mostrarLCD("CANCELA FECHADA", "ADMINISTRADOR", 0, 4000);
    }

    servidor.send(200, "text/plain; charset=utf-8", "Comando de fechamento enviado.");
    return;
  }

  servidor.send(400, "text/plain", "Comando invalido.");
}

// ================= PROCESSAR CARTAO =================

void processarCartao(String uid) {
  ultimoUIDCartao = uid;
  atualizarGuiado();

  // Cadastro guiado: captura somente uma leitura.
  if (aguardandoGuiado) {
    uidGuiado = uid;
    aguardandoGuiado = false;

    int existente = procurarUsuarioUID(uid);
    String nome = existente >= 0 ?
      usuariosNome[existente] : String("Novo cartao");

    registrarEvento(nome, uid, "LEITURA PARA CADASTRO", "Cadastro");
    mostrarLCD("UID CAPTURADO", "COMPLETE NO SITE", 2, 4000);
    return;
  }

  int i = procurarUsuarioUID(uid);

  if (i < 0) {
    totalNegados++;
    registrarEvento("Cartao desconhecido", uid, "NAO CADASTRADO", "Cartão");
    tocarSom(SOM_NEGADO);
    mostrarLCD("ACESSO NEGADO", "NAO CADASTRADO", 3, 3000);
    return;
  }

  String nome = usuariosNome[i];

  if (!usuariosLiberados[i]) {
    totalNegados++;
    registrarEvento(nome, uid, "BLOQUEADO", "Cartão");
    tocarSom(SOM_NEGADO);
    mostrarLCD("BLOQUEADO", nome, 3, 5500);
    return;
  }

  if (!servoPronto) {
    registrarEvento(nome, uid, "FALHA NO SERVO", "Cartão");
    tocarSom(SOM_NEGADO);
    mostrarLCD("FALHA NO SERVO", nome, 3, 5000);
    return;
  }

  if (!portaAberta) {
    abrirPorta(uid, nome, "Cartão");
    totalLiberados++;
    registrarEvento(nome, uid, "ENTRADA ABERTA", "Cartão");
    tocarSom(SOM_ABRIR);
    mostrarLCD("ACESSO LIBERADO", nome, 2, 5000);
    return;
  }

  if (uid == uidQueAbriu) {
    fecharPorta();
    totalLiberados++;
    registrarEvento(nome, uid, "ENTRADA FECHADA", "Cartão");
    tocarSom(SOM_FECHAR);
    mostrarLCD("CANCELA FECHADA", nome, 0, 5000);
    return;
  }

  registrarEvento(nome, uid, "PORTA JA ABERTA", "Cartão");
  tocarSom(SOM_OCUPADA);
  mostrarLCD("JA ESTA ABERTA", nome, 1, 4000);
}

// ================= SETUP =================

void setup() {
  pinMode(BUZZER_PIN, OUTPUT);
  pararBuzzer();

  Serial.begin(115200);
  delay(500);

  lcd.begin(16, 2);
  lcd.createChar(0, iconeFechado);
  lcd.createChar(1, iconeAberto);
  lcd.createChar(2, iconeOK);
  lcd.createChar(3, iconeX);
  lcd.createChar(4, iconeSeta);
  lcd.createChar(5, iconeMarcador);

  escreverLinhaLCD(0, "CONTROLE ACESSO");
  escreverLinhaLCD(1, "INICIANDO...");

  SPI.begin(RFID_SCK, RFID_MISO, RFID_MOSI, RFID_SS);
  rfid.PCD_Init();
  delay(300);
  rfid.PCD_DumpVersionToSerial();

  carregarUsuarios();
  carregarConfigBuzzer();
  carregarConfigCancela();

  servoPorta.setPeriodHertz(50);
  servoPorta.attach(SERVO_PIN, 500, 2400);
  servoPronto = servoPorta.attached();

  if (servoPronto) servoPorta.write(ANGULO_FECHADO);

  portaAberta = false;
  uidQueAbriu = "";

  WiFi.mode(WIFI_AP);
  WiFi.softAP(WIFI_NOME, WIFI_SENHA);

  servidor.on("/", HTTP_GET, paginaPrincipal);
  servidor.on("/login", HTTP_POST, rotaLogin);
  servidor.on("/logout", HTTP_POST, rotaLogout);
  servidor.on("/me", HTTP_GET, rotaMe);
  servidor.on("/dados", HTTP_GET, rotaDados);
  servidor.on("/hora", HTTP_POST, rotaHora);

  servidor.on("/cadastrar", HTTP_POST, rotaCadastrar);
  servidor.on("/status", HTTP_POST, rotaStatus);
  servidor.on("/excluir", HTTP_POST, rotaExcluir);

  servidor.on("/cadastro/ler", HTTP_POST, rotaGuiadoIniciar);
  servidor.on("/cadastro/cancelar", HTTP_POST, rotaGuiadoCancelar);

  servidor.on("/buzzer", HTTP_GET, rotaBuzzerConsultar);
  servidor.on("/buzzer", HTTP_POST, rotaBuzzerSalvar);
  servidor.on("/buzzer/testar", HTTP_POST, rotaBuzzerTestar);

  servidor.on("/cancela", HTTP_GET, rotaCancelaConsultar);
  servidor.on("/cancela", HTTP_POST, rotaCancelaSalvar);
  servidor.on("/cancela/comando", HTTP_POST, rotaCancelaComando);

  servidor.onNotFound([]() {
    servidor.send(404, "text/plain", "Pagina nao encontrada.");
  });

  servidor.begin();
  mostrarAguardando();

  Serial.print("Rede: ");
  Serial.println(WIFI_NOME);
  Serial.print("Site: http://");
  Serial.println(WiFi.softAPIP());
}

// ================= LOOP =================

void loop() {
  atualizarBuzzer();
  atualizarGuiado();
  atualizarCancela();

  servidor.handleClient();

  atualizarBuzzer();
  atualizarGuiado();
  atualizarCancela();
  atualizarLCD();

  if (!rfid.PICC_IsNewCardPresent()) return;
  if (!rfid.PICC_ReadCardSerial()) return;

  if (ultimaLeitura != 0 &&
      millis() - ultimaLeitura < intervaloLeitura) {
    rfid.PICC_HaltA();
    rfid.PCD_StopCrypto1();
    return;
  }

  ultimaLeitura = millis();
  processarCartao(pegarUID());

  rfid.PICC_HaltA();
  rfid.PCD_StopCrypto1();
}
