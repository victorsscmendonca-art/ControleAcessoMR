// Controle de Acesso MR - prototipo WebXR (navegador do Meta Quest).
// Fluxo: Entrar -> aviso na frente dos olhos -> pinça na mesa coloca o kit
// (ou o kit aparece sozinho em 8 s) -> SIMULAR / cartao real pelo ESP32.
import * as THREE from 'three';

const ESPERA_KIT = 8;           // s sem colocar o kit: aparece sozinho na frente
const INTERVALO_ESP = 0.5;      // s entre consultas ao ESP32
const ENDERECO_ESP = location.hostname === '192.168.4.1' ? '/evento' : 'http://192.168.4.1/evento';

// ---------- Cena ----------
const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
renderer.setSize(innerWidth, innerHeight);
renderer.xr.enabled = true;
document.body.appendChild(renderer.domElement);

const scene = new THREE.Scene();
const camera = new THREE.PerspectiveCamera(60, innerWidth / innerHeight, 0.01, 30);
scene.add(camera);
scene.add(new THREE.HemisphereLight(0xffffff, 0x334455, 2.2));
const sol = new THREE.DirectionalLight(0xffffff, 1.5);
sol.position.set(0.5, 1, 0.8);
scene.add(sol);

addEventListener('resize', () => {
  camera.aspect = innerWidth / innerHeight;
  camera.updateProjectionMatrix();
  renderer.setSize(innerWidth, innerHeight);
});

// ---------- Textos (canvas -> textura) ----------
function painelTexto(largura, altura, pxPorMetro = 1400) {
  const cv = document.createElement('canvas');
  cv.width = Math.round(largura * pxPorMetro);
  cv.height = Math.round(altura * pxPorMetro);
  const tex = new THREE.CanvasTexture(cv);
  tex.colorSpace = THREE.SRGBColorSpace;
  const mesh = new THREE.Mesh(
    new THREE.PlaneGeometry(largura, altura),
    new THREE.MeshBasicMaterial({ map: tex, transparent: true, depthWrite: false })
  );
  mesh.renderOrder = 10;
  mesh.escrever = (linhas, { fundo = 'rgba(5,15,22,0.92)', cor = '#e8f4fa', borda = '#4dd9ff', tamanho = 0.32 } = {}) => {
    const c = cv.getContext('2d');
    c.clearRect(0, 0, cv.width, cv.height);
    if (fundo) {
      c.fillStyle = fundo;
      c.beginPath(); c.roundRect(0, 0, cv.width, cv.height, cv.height * 0.12); c.fill();
      if (borda) { c.fillStyle = borda; c.fillRect(cv.width * 0.04, 0, cv.width * 0.92, cv.height * 0.04); }
    }
    const lista = Array.isArray(linhas) ? linhas : [linhas];
    const h = cv.height * tamanho;
    c.textAlign = 'center'; c.textBaseline = 'middle';
    lista.forEach((l, i) => {
      const txt = typeof l === 'string' ? l : l.t;
      c.fillStyle = (typeof l === 'object' && l.cor) || cor;
      const esc = (typeof l === 'object' && l.esc) || 1;
      c.font = `bold ${Math.round(h * esc)}px system-ui, sans-serif`;
      const y = cv.height / 2 + (i - (lista.length - 1) / 2) * h * 1.25;
      c.fillText(txt, cv.width / 2, y, cv.width * 0.94);
    });
    tex.needsUpdate = true;
  };
  return mesh;
}

// Etiqueta que sempre olha para a pessoa.
const viradas = [];
function etiqueta(texto, pai, pos) {
  const p = painelTexto(0.12, 0.03);
  p.escrever(texto, { tamanho: 0.55, borda: null });
  p.position.copy(pos);
  pai.add(p);
  viradas.push(p);
  return p;
}

// ---------- Kit virtual (versao simples: caixas) ----------
const kit = new THREE.Group();
kit.visible = false;
scene.add(kit);

function caixa(l, a, p, cor, pos, pai = kit, emissiva = 0x000000) {
  const m = new THREE.Mesh(new THREE.BoxGeometry(l, a, p),
    new THREE.MeshStandardMaterial({ color: cor, roughness: 0.6, emissive: emissiva }));
  m.position.copy(pos);
  pai.add(m);
  return m;
}
const V = (x, y, z) => new THREE.Vector3(x, y, z);

// Bandeja translucida com borda ciano (ajuda o olho a ver a altura do kit).
const bandeja = new THREE.Mesh(new THREE.BoxGeometry(0.56, 0.006, 0.34),
  new THREE.MeshBasicMaterial({ color: 0x4dd9ff, transparent: true, opacity: 0.18 }));
bandeja.position.set(0, -0.003, 0);
kit.add(bandeja);

const leitor = caixa(0.09, 0.012, 0.11, 0x1f6fd1, V(-0.19, 0.006, 0.04));
const esp32 = caixa(0.055, 0.01, 0.1, 0x222a33, V(-0.02, 0.005, -0.01));
caixa(0.02, 0.004, 0.02, 0x999999, V(-0.02, 0.012, -0.03), kit);   // chip
const lcd = caixa(0.17, 0.055, 0.015, 0x2b6e3a, V(0.15, 0.04, -0.09));
const telaLcd = painelTexto(0.15, 0.04, 1600);
telaLcd.position.set(0, 0, 0.0081);
lcd.add(telaLcd);
const servo = caixa(0.04, 0.03, 0.04, 0x1a5fb4, V(0.17, 0.015, 0.08));
const pivo = new THREE.Group();
pivo.position.set(0.17, 0.034, 0.08);
kit.add(pivo);
const cancela = caixa(0.2, 0.008, 0.012, 0xff8c1a, V(-0.1, 0, 0), pivo);   // braco aponta para a esquerda

const luzEsp = new THREE.Mesh(new THREE.SphereGeometry(0.007, 16, 12),
  new THREE.MeshBasicMaterial({ color: 0xff3b3b }));
luzEsp.position.set(0.02, 0.012, 0.03);
kit.add(luzEsp);

etiqueta('LEITOR NFC', kit, V(-0.19, 0.06, 0.04));
etiqueta('ESP32', kit, V(-0.02, 0.05, -0.01));
etiqueta('LCD', kit, V(0.15, 0.1, -0.09));
etiqueta('SERVO', kit, V(0.22, 0.06, 0.08));

// Fios (caminhos por onde a luz viaja).
const caminhos = {
  leitorEsp: [V(-0.15, 0.013, 0.04), V(-0.08, 0.013, 0.04), V(-0.08, 0.013, -0.01), V(-0.05, 0.011, -0.01)],
  espLcd: [V(0.01, 0.011, -0.03), V(0.08, 0.011, -0.03), V(0.08, 0.011, -0.08), V(0.1, 0.02, -0.08)],
  espServo: [V(0.01, 0.011, 0.01), V(0.08, 0.011, 0.01), V(0.08, 0.011, 0.08), V(0.15, 0.012, 0.08)],
};
for (const pts of Object.values(caminhos)) {
  const g = new THREE.BufferGeometry().setFromPoints(pts);
  kit.add(new THREE.Line(g, new THREE.LineBasicMaterial({ color: 0x5a7080 })));
}
function pontoNoCaminho(pts, t) {
  const seg = []; let total = 0;
  for (let i = 1; i < pts.length; i++) { const d = pts[i].distanceTo(pts[i - 1]); seg.push(d); total += d; }
  let alvo = THREE.MathUtils.clamp(t, 0, 1) * total;
  for (let i = 0; i < seg.length; i++) {
    if (alvo <= seg[i]) return pts[i].clone().lerp(pts[i + 1], alvo / seg[i]);
    alvo -= seg[i];
  }
  return pts[pts.length - 1].clone();
}
const pulsos = [0, 1].map(() => {
  const m = new THREE.Mesh(new THREE.SphereGeometry(0.008, 16, 12),
    new THREE.MeshBasicMaterial({ color: 0x7ff0ff }));
  m.visible = false; kit.add(m); return m;
});

// Painel de resultado acima do kit.
const painel = painelTexto(0.36, 0.09);
painel.position.set(0, 0.2, -0.05);
kit.add(painel);
viradas.push(painel);

// Botoes de simulacao (tocar com o dedo, pinça apontando ou clique no PC).
const botoes = [];
function botao(texto, cor, x, liberado) {
  const g = new THREE.Group();
  g.position.set(x, 0.02, 0.25);
  const base = caixa(0.13, 0.03, 0.06, cor, V(0, 0, 0), g, 0x000000);
  const rot = painelTexto(0.13, 0.04);
  rot.escrever(texto, { tamanho: 0.4, borda: null, fundo: null, cor: '#ffffff' });
  rot.rotation.x = -Math.PI / 2;
  rot.position.set(0, 0.0152, 0);
  g.add(rot);
  kit.add(g);
  const b = { g, base, liberado, corBase: cor };
  base.userData.botao = b;
  botoes.push(b);
}
botao('LIBERADO', 0x1f9d55, -0.08, true);
botao('NEGADO', 0xc0392b, 0.08, false);

// ---------- Som ----------
let audio;
function bip(freq, dur, quando = 0, tipo = 'square', vol = 0.08) {
  try {
    audio = audio || new AudioContext();
    const t0 = audio.currentTime + quando;
    const o = audio.createOscillator(), g = audio.createGain();
    o.type = tipo; o.frequency.value = freq;
    g.gain.setValueAtTime(vol, t0);
    g.gain.exponentialRampToValueAtTime(0.0001, t0 + dur);
    o.connect(g).connect(audio.destination);
    o.start(t0); o.stop(t0 + dur + 0.02);
  } catch (e) { /* sem som: segue sem */ }
}

// ---------- Animacao do fluxo ----------
let fluxo = null;          // { inicio, liberado, texto }
let anguloCancela = 0;     // 0 fechada, 1 aberta
let cancelaAbertaAte = 0;

function mostrarAguardando() {
  telaLcd.escrever(['APROXIME', 'O CARTAO'], { fundo: '#0e3d1a', cor: '#b8ffb8', borda: null, tamanho: 0.32 });
  painel.escrever([{ t: '1  Pegue um cartão   2  Encoste no LEITOR', esc: 0.75 },
    { t: 'ou toque em LIBERADO / NEGADO', esc: 0.6, cor: '#9db4c0' }], { tamanho: 0.3 });
}
mostrarAguardando();

function iniciarFluxo(liberado, texto) {
  if (fluxo) return false;
  fluxo = { inicio: relogio.elapsedTime, liberado, texto: texto || (liberado ? 'ENTRADA ABERTA' : 'BLOQUEADO'), fase: 0 };
  painel.escrever([{ t: 'Lendo o cartão NFC...', esc: 0.9 }], { tamanho: 0.3 });
  bip(1800, 0.08);
  return true;
}

function atualizarFluxo(agora) {
  const f = fluxo;
  if (!f) return;
  const t = agora - f.inicio;
  // 0-0.8 s: leitor -> ESP32
  if (t < 0.8) {
    pulsos[0].visible = true; pulsos[1].visible = false;
    pulsos[0].position.copy(pontoNoCaminho(caminhos.leitorEsp, t / 0.8));
    return;
  }
  // 0.8-1.3 s: ESP32 "pensa" (brilha)
  if (t < 1.3) {
    pulsos[0].visible = false;
    esp32.material.emissive.setHex(0x3388aa).multiplyScalar(0.5 + 0.5 * Math.sin(t * 30));
    if (f.fase < 1) { f.fase = 1; painel.escrever([{ t: 'ESP32 procura o código na lista', esc: 0.8 }], { tamanho: 0.3 }); }
    return;
  }
  esp32.material.emissive.setHex(0x000000);
  // 1.3-2.1 s: sinal se divide para LCD e servo
  if (t < 2.1) {
    const k = (t - 1.3) / 0.8;
    pulsos[0].visible = pulsos[1].visible = true;
    pulsos[0].position.copy(pontoNoCaminho(caminhos.espLcd, k));
    pulsos[1].position.copy(pontoNoCaminho(caminhos.espServo, k));
    const cor = f.liberado ? 0x6dff9a : 0xff6b6b;
    pulsos[0].material.color.setHex(cor); pulsos[1].material.color.setHex(cor);
    return;
  }
  pulsos[0].visible = pulsos[1].visible = false;
  if (f.fase < 2) {
    f.fase = 2;
    if (f.liberado) {
      telaLcd.escrever(['ACESSO', 'LIBERADO'], { fundo: '#0e3d1a', cor: '#b8ffb8', borda: null, tamanho: 0.32 });
      painel.escrever([{ t: 'AUTORIZADO', cor: '#6dff9a', esc: 1.4 }, { t: f.texto, esc: 0.6, cor: '#9db4c0' }], { tamanho: 0.3, borda: '#6dff9a' });
      cancelaAbertaAte = agora + 4;
      bip(880, 0.12); bip(1320, 0.18, 0.14);
    } else {
      telaLcd.escrever(['ACESSO', 'NEGADO'], { fundo: '#3d0e0e', cor: '#ffb8b8', borda: null, tamanho: 0.32 });
      painel.escrever([{ t: 'NEGADO', cor: '#ff6b6b', esc: 1.4 }, { t: f.texto, esc: 0.6, cor: '#9db4c0' }], { tamanho: 0.3, borda: '#ff6b6b' });
      bip(220, 0.25, 0, 'sawtooth'); bip(180, 0.35, 0.3, 'sawtooth');
    }
  }
  if (t > (f.liberado ? 6 : 3.5)) { fluxo = null; mostrarAguardando(); }
}

// Evento do ESP32 sem passar pelo fluxo completo (ex.: fechamento): so mostra o texto.
function mostrarStatus(texto) {
  if (fluxo) return;
  painel.escrever([{ t: texto, esc: 0.9 }, { t: 'evento do kit real', esc: 0.55, cor: '#9db4c0' }], { tamanho: 0.3 });
  setTimeout(() => { if (!fluxo) mostrarAguardando(); }, 3000);
}

// ---------- ESP32 ----------
let espConectado = false, espUltimoNumero = -1, espProxima = 0, espEsperando = false;
async function consultarEsp() {
  espEsperando = true;
  const ctl = new AbortController();
  const tempo = setTimeout(() => ctl.abort(), 1500);
  try {
    const r = await fetch(ENDERECO_ESP, { cache: 'no-store', signal: ctl.signal });
    const ev = await r.json();
    espConectado = true;
    // Primeira resposta, ou ESP32 reiniciado: so guarda o numero.
    if (espUltimoNumero < 0 || ev.numero < espUltimoNumero) { espUltimoNumero = ev.numero; return; }
    if (ev.numero === espUltimoNumero) return;
    espUltimoNumero = ev.numero;
    const s = String(ev.status || '');
    if (s === 'ENTRADA ABERTA') iniciarFluxo(true, s);
    else if (s === 'BLOQUEADO' || s === 'NAO CADASTRADO') iniciarFluxo(false, s);
    else if (s) mostrarStatus(s);
  } catch (e) {
    espConectado = false;
  } finally {
    clearTimeout(tempo);
    espEsperando = false;
  }
}

// ---------- Colocar o kit ----------
const aviso = painelTexto(0.5, 0.14);
aviso.position.set(0, -0.08, -0.9);
aviso.visible = false;
camera.add(aviso);

const retiCulo = new THREE.Mesh(new THREE.RingGeometry(0.05, 0.065, 40).rotateX(-Math.PI / 2),
  new THREE.MeshBasicMaterial({ color: 0x4dd9ff }));
retiCulo.matrixAutoUpdate = false;
retiCulo.visible = false;
scene.add(retiCulo);

let hitSource = null, inicioXR = 0, kitColocado = false;

function colocarKit(pos) {
  const cab = new THREE.Vector3(); camera.getWorldPosition(cab);
  kit.position.copy(pos);
  // Frente do kit (+Z) virada para a pessoa.
  const dir = cab.clone().sub(pos); dir.y = 0;
  kit.rotation.set(0, Math.atan2(dir.x, dir.z), 0);
  kit.visible = true; kitColocado = true;
  aviso.visible = false; retiCulo.visible = false;
  bip(660, 0.1, 0, 'sine', 0.12);
}

function colocarNaFrente() {
  const cab = new THREE.Vector3(); camera.getWorldPosition(cab);
  const frente = new THREE.Vector3(); camera.getWorldDirection(frente);
  frente.y = 0;
  if (frente.lengthSq() < 0.01) frente.set(0, 0, -1);
  frente.normalize();
  colocarKit(cab.clone().addScaledVector(frente, 0.6).add(V(0, -0.4, 0)));
}

// ---------- Interacao ----------
const raio = new THREE.Raycaster();
let botaoTravadoAte = 0;
function apertar(b) {
  const agora = relogio.elapsedTime;
  if (agora < botaoTravadoAte) return;
  botaoTravadoAte = agora + 1;
  b.base.position.y = -0.008;
  setTimeout(() => (b.base.position.y = 0), 180);
  if (!iniciarFluxo(b.liberado)) bip(300, 0.05);
}
function botaoNoRaio() {
  const hits = raio.intersectObjects(botoes.map((b) => b.base), false);
  return hits.length ? hits[0].object.userData.botao : null;
}

const controles = [0, 1].map((i) => {
  const c = renderer.xr.getController(i);
  c.addEventListener('select', () => {
    if (!kitColocado) {
      if (retiCulo.visible) colocarKit(new THREE.Vector3().setFromMatrixPosition(retiCulo.matrix));
      return;
    }
    const m = new THREE.Matrix4().extractRotation(c.matrixWorld);
    raio.ray.origin.setFromMatrixPosition(c.matrixWorld);
    raio.ray.direction.set(0, 0, -1).applyMatrix4(m);
    const b = botaoNoRaio();
    if (b) apertar(b);
  });
  scene.add(c);
  return c;
});
const maos = [0, 1].map((i) => { const h = renderer.xr.getHand(i); scene.add(h); return h; });

// Dedo indicador encostando no botao.
const tmp = new THREE.Vector3();
function dedoNosBotoes() {
  for (const h of maos) {
    const ponta = h.joints && h.joints['index-finger-tip'];
    if (!ponta || !ponta.visible) continue;
    ponta.getWorldPosition(tmp);
    for (const b of botoes) {
      const local = b.base.worldToLocal(tmp.clone());
      if (Math.abs(local.x) < 0.07 && Math.abs(local.z) < 0.035 && local.y < 0.03 && local.y > -0.02) apertar(b);
    }
  }
}

// No PC: clique nos botoes 3D e arrastar para girar a vista.
const capa = document.getElementById('capa');
const barra = document.getElementById('barra');
let previa = false, girando = false, giroX = 0, ang = 0.0, elev = 0.55, ultimoX = 0, ultimoY = 0;
function posicionarCameraPrevia() {
  const r = 0.75;
  camera.position.set(Math.sin(ang) * Math.cos(elev) * r, Math.sin(elev) * r + 0.05, Math.cos(ang) * Math.cos(elev) * r);
  camera.lookAt(0, 0.05, 0);
}
renderer.domElement.addEventListener('pointerdown', (e) => {
  if (!previa) return;
  girando = true; giroX = 0; ultimoX = e.clientX; ultimoY = e.clientY;
});
addEventListener('pointermove', (e) => {
  if (!girando) return;
  giroX += Math.abs(e.clientX - ultimoX) + Math.abs(e.clientY - ultimoY);
  ang -= (e.clientX - ultimoX) * 0.008;
  elev = THREE.MathUtils.clamp(elev + (e.clientY - ultimoY) * 0.006, 0.1, 1.4);
  ultimoX = e.clientX; ultimoY = e.clientY;
  posicionarCameraPrevia();
});
addEventListener('pointerup', (e) => {
  if (!girando) return;
  girando = false;
  if (giroX > 6) return;   // foi arrasto, nao clique
  const p = new THREE.Vector2((e.clientX / innerWidth) * 2 - 1, -(e.clientY / innerHeight) * 2 + 1);
  raio.setFromCamera(p, camera);
  const b = botaoNoRaio();
  if (b) apertar(b);
});

document.getElementById('previa').onclick = () => {
  previa = true;
  capa.style.display = 'none';
  barra.style.display = 'flex';
  scene.background = new THREE.Color(0x1b2a33);
  kit.position.set(0, 0, 0); kit.rotation.set(0, 0, 0);
  kit.visible = true; kitColocado = true;
  posicionarCameraPrevia();
};
document.getElementById('bLib').onclick = () => iniciarFluxo(true);
document.getElementById('bNeg').onclick = () => iniciarFluxo(false);
// Teclas no PC: 1 = liberado, 2 = negado.
addEventListener('keydown', (e) => { if (e.key === '1') iniciarFluxo(true); if (e.key === '2') iniciarFluxo(false); });

// ---------- Entrar no WebXR ----------
const botaoEntrar = document.getElementById('entrar');
const statusEl = document.getElementById('status');

async function verificarXR() {
  if (!navigator.xr) {
    botaoEntrar.textContent = 'Sem realidade mista aqui';
    statusEl.textContent = isSecureContext
      ? 'Este navegador não tem WebXR. Abra no navegador do Meta Quest.'
      : 'O navegador bloqueou o WebXR porque o endereço não é seguro (https). Veja o README.';
    return;
  }
  const ok = await navigator.xr.isSessionSupported('immersive-ar').catch(() => false);
  botaoEntrar.disabled = !ok;
  botaoEntrar.textContent = ok ? 'Entrar' : 'Realidade mista não suportada';
  if (!ok) statusEl.textContent = 'Use o navegador do Meta Quest 3 ou 3S.';
}
verificarXR();

botaoEntrar.onclick = async () => {
  try {
    const sessao = await navigator.xr.requestSession('immersive-ar', {
      requiredFeatures: ['local-floor'],
      optionalFeatures: ['hit-test', 'hand-tracking'],
    });
    renderer.xr.setReferenceSpaceType('local-floor');
    await renderer.xr.setSession(sessao);
    scene.background = null;
    capa.style.display = 'none';
    kit.visible = false; kitColocado = false;
    inicioXR = relogio.elapsedTime;
    aviso.visible = true;
    try {
      const viewer = await sessao.requestReferenceSpace('viewer');
      hitSource = await sessao.requestHitTestSource({ space: viewer });
    } catch (e) { hitSource = null; }
    sessao.addEventListener('end', () => {
      hitSource = null; capa.style.display = 'flex'; aviso.visible = false; retiCulo.visible = false;
    });
  } catch (e) {
    statusEl.textContent = 'Não foi possível entrar: ' + e.message;
  }
};

// ---------- Loop ----------
const relogio = new THREE.Clock();
renderer.setAnimationLoop((_, frame) => {
  const dt = relogio.getDelta();
  const agora = relogio.elapsedTime;

  if (frame && !kitColocado) {
    const restam = Math.max(0, Math.ceil(ESPERA_KIT - (agora - inicioXR)));
    let achou = false;
    if (hitSource) {
      const hits = frame.getHitTestResults(hitSource);
      const ref = renderer.xr.getReferenceSpace();
      if (hits.length) {
        const pose = hits[0].getPose(ref);
        if (pose) { retiCulo.matrix.fromArray(pose.transform.matrix); retiCulo.visible = true; achou = true; }
      }
    }
    if (!achou) retiCulo.visible = false;
    aviso.escrever([
      { t: achou ? 'Faça a pinça para colocar o kit aqui' : 'Olhe para a mesa', esc: 0.9 },
      { t: `ou o kit aparece sozinho em ${restam} s`, esc: 0.6, cor: '#9db4c0' },
    ], { tamanho: 0.28 });
    if (agora - inicioXR > ESPERA_KIT) colocarNaFrente();
  }

  if (kitColocado) {
    if (frame) dedoNosBotoes();
    atualizarFluxo(agora);

    // Cancela: sobe ate 80 graus quando liberado.
    const alvo = agora < cancelaAbertaAte ? 1 : 0;
    anguloCancela += (alvo - anguloCancela) * Math.min(1, dt * 5);
    pivo.rotation.z = -anguloCancela * THREE.MathUtils.degToRad(80);

    // Luz do ESP32: verde = conectado, vermelha piscando = sem sinal.
    luzEsp.material.color.setHex(espConectado ? 0x3dff7a : 0xff3b3b);
    luzEsp.visible = espConectado || (agora % 1) < 0.5;

    const cab = new THREE.Vector3(); camera.getWorldPosition(cab);
    for (const v of viradas) v.lookAt(cab);
  }

  if (agora > espProxima && !espEsperando) { espProxima = agora + INTERVALO_ESP; consultarEsp(); }

  renderer.render(scene, camera);
});
