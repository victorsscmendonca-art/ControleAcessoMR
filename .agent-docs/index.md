# ControleAcessoMR_novo — Agent Documentation

> Auto-generated project knowledge base for AI agent comprehension.
> Last updated: 2026-11-02

## Quick Context
Mixed-reality teaching app for Meta Quest 3/3S. It anchors floating labels over a physical
access-control kit (RC522 RFID reader, ESP32, LCD, servo) using a QR code tracked by MRUK,
with passthrough on. Everything is built at runtime by one script; the user calibrates label
positions with the Touch controllers and the calibration is saved in PlayerPrefs.

## Document Map
- [Project Overview](project-overview.md)
- Scenes
  - [Scene Flow](<scenes/_flow.md>)
  - [SampleScene](<scenes/sample-scene.md>)
- Systems
  - [Access-control overlay](<systems/access-control-overlay.md>)

## Runtime Flow
App starts in SampleScene → MRUK loads scene data and starts QR tracking → `ControleAcessoMR`
builds labels (hidden) → when a QR with payload `CONTROLE_ACESSO_MR` is found (or the user presses A
for manual placement) the labels appear anchored to it → user steps through / adjusts / saves.
