"""Gera o QR code do kit (texto CONTROLE_ACESSO_MR) em PNG e em PDF A4 para imprimir.

Uso:  python Ferramentas/gerar_qr.py
Precisa:  python -m pip install qrcode pillow
O texto TEM que ser igual a PayloadEsperado em ControleAcessoMR.cs.
"""
import qrcode
from PIL import Image, ImageDraw, ImageFont

TEXTO = "CONTROLE_ACESSO_MR"
DPI = 300
A4 = (int(21.0 / 2.54 * DPI), int(29.7 / 2.54 * DPI))   # 2480 x 3508


def cm(v):
    return round(v / 2.54 * DPI)


def fonte(tam, negrito=False):
    nome = "arialbd.ttf" if negrito else "arial.ttf"
    try:
        return ImageFont.truetype("C:/Windows/Fonts/" + nome, tam)
    except OSError:
        return ImageFont.load_default()


def desenhar_qr(lado_cm):
    """QR preto no branco, SEM logo, com a borda branca obrigatoria (4 modulos)."""
    qr = qrcode.QRCode(error_correction=qrcode.constants.ERROR_CORRECT_M, border=0)
    qr.add_data(TEXTO)
    qr.make(fit=True)
    m = qr.get_matrix()
    n = len(m)
    modulo = cm(lado_cm) / n
    borda = 4 * modulo
    total = round(cm(lado_cm) + 2 * borda)
    img = Image.new("RGB", (total, total), "white")
    d = ImageDraw.Draw(img)
    for y, linha in enumerate(m):
        for x, v in enumerate(linha):
            if v:
                d.rectangle([round(borda + x * modulo), round(borda + y * modulo),
                             round(borda + (x + 1) * modulo) - 1, round(borda + (y + 1) * modulo) - 1],
                            fill="black")
    return img, n


def pagina(lado_cm, titulo_extra, instrucoes=True):
    qr, n = desenhar_qr(lado_cm)
    pg = Image.new("RGB", A4, "white")
    d = ImageDraw.Draw(pg)
    d.text((A4[0] // 2, cm(1.8)), "QR DO KIT - ControleAcessoMR", font=fonte(110, True), fill="black", anchor="mm")
    d.text((A4[0] // 2, cm(3.2)), titulo_extra, font=fonte(60), fill=(60, 60, 60), anchor="mm")
    pg.paste(qr, ((A4[0] - qr.width) // 2, cm(4.0)))
    y = cm(4.0) + qr.height + cm(0.9)
    # regua de 10 cm para conferir se a impressora nao mudou a escala
    x0 = (A4[0] - cm(10)) // 2
    d.line([x0, y, x0 + cm(10), y], fill="black", width=8)
    for t in (0, 10):
        d.line([x0 + cm(t), y - 25, x0 + cm(t), y + 25], fill="black", width=8)
    d.text((A4[0] // 2, y + cm(0.8)), "Esta regua deve medir 10 cm. Se nao medir, imprima em 'Tamanho real' (100%).",
           font=fonte(46), fill=(60, 60, 60), anchor="mm")
    linhas = [
        "COMO USAR",
        "1. Imprima em papel branco FOSCO (brilho atrapalha a camera do oculos).",
        "2. Cole deitado e plano na mesa, ao lado do kit real, sem dobras.",
        "3. Boa luz, sem sombra em cima do QR. Nao cubra nem risque.",
        "4. Com o app aberto, olhe para o QR a 30-60 cm por 2-3 segundos.",
        "5. Nao tem como errar: o kit virtual aparece na frente dos seus olhos.",
        "Se nao aparecer, aperte A no controle direito.",
    ]
    y += cm(2.0)
    for i, t in enumerate(linhas if instrucoes else []):
        d.text((cm(2.5), y + i * cm(0.82)), t, font=fonte(50, i == 0), fill="black")
    d.text((A4[0] // 2, A4[1] - cm(0.8)), "Texto do QR: " + TEXTO + "   |   %d x %d modulos" % (n, n),
           font=fonte(40), fill=(110, 110, 110), anchor="mm")
    return pg, qr


p12, qr12 = pagina(12, "Tamanho do QR: 12 cm (recomendado) + borda branca")
p15, qr15 = pagina(15, "Tamanho do QR: 15 cm (reserva: mais facil de detectar) + borda branca", instrucoes=False)
qr12.save("docs/qr/qr_12cm.png", dpi=(DPI, DPI))
qr15.save("docs/qr/qr_15cm.png", dpi=(DPI, DPI))
p12.save("docs/qr/QR_para_imprimir.pdf", save_all=True, append_images=[p15], resolution=DPI)
p12.save("docs/qr/pagina_12cm.png")
print("ok", qr12.size, qr15.size)
