# SampleScene
Last updated: 2026-11-02

- **Role:** the only (main) scene, in build index 0
- **Key GameObjects:**
  - `[BuildingBlock] Camera Rig` — OVRCameraRig (CenterEyeAnchor, hand/controller anchors)
  - `[BuildingBlock] Passthrough` — passthrough layer for MR
  - `MRUK` — loads device scene on startup, QR tracking enabled, world lock enabled
  - `ControleAcesso` — holds [`ControleAcessoMR`](../systems/access-control-overlay.md)
  - `Directional Light`, `Global Volume` — URP template leftovers
