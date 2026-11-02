# Access-control overlay
Last updated: 2026-11-02

**Script:** `Assets/ControleAcessoMR/ControleAcessoMR.cs` (on `ControleAcesso` in [SampleScene](../scenes/sample-scene.md))

## Purpose
Shows 4 labelled boxes (RC522, ESP32, LCD, Servo) with a leader line and a billboard caption over
the real kit, anchored to a QR code (`payloadEsperado`, default `CONTROLE_ACESSO_MR`) or placed manually.

## How it works (usage level)
- All visuals are created at runtime (primitives + `TextMesh`), no prefabs.
- Modes: `Aguardando` → `QR` (follows tracked QR) or `Manual` (A button, 0.8 m in front of the head).
- Calibration (positions, text scale, box scale) saved in PlayerPrefs with keys `v2_*`.
- Head-locked diagnostic panel parented to CenterEyeAnchor (local 0, -0.25, 1.0); toggle with B.

## Controls (Touch controllers, via OVRInput)
A place manually · B panel · X reset offset · Y save · right trigger next step · left trigger select item ·
grips text scale +/- · right stick click box size · left stick click erase calibration ·
sticks move/rotate the group or selected item.

## Dependencies
MRUK (trackables), OVRCameraRig, OVRPermissionsRequester (scene permission check).
