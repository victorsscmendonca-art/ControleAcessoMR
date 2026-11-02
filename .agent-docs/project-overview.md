# Project Overview
Last updated: 2026-11-02

- **Unity:** 6000.0.66f2 (Unity 6.0 LTS)
- **Target:** Android / Meta Quest (quest, quest2, questpro, quest3, quest3s); package `com.victor.controleacessomr`, version 0.2.0
- **Render pipeline:** URP 17.0.4, Mobile_RPAsset (MSAA 4x, render scale 0.8, HDR off), Vulkan first then GLES3
- **XR:** OpenXR 1.16.1 with Meta XR feature, foveation, subsampled layout, Oculus Touch profile. Android min SDK 32, target 34, IL2CPP, ARM64
- **Meta SDK:** `com.meta.xr.sdk.all` 207.0.0 (Core, MRUK, Interaction SDK available)
- **Features in OculusProjectConfig:** hand tracking = supported (not required), anchors on, scene = required, passthrough supported
- **Input handling:** new Input System; gameplay reads `OVRInput` directly (controllers only)

## Key packages and how they are used
- **MRUK** (`MRUK` object in scene): scene data from device, QR code tracking enabled, world lock on; script listens to `TrackableAdded/Removed`
- **Building Blocks:** Camera Rig (OVRCameraRig) and Passthrough
- **TextMesh (legacy)** with built-in `LegacyRuntime.ttf` for labels; TextMesh Pro essentials imported but not used
- `com.unity.xr.mock-hmd` present (only Standalone loader); `com.unity.visualscripting`, `ai.navigation`, `multiplayer.center` unused

## Notes
- `Build/` holds a previous APK build (~194 MB) and is git-ignored. `Assets.zip` is an older copy of Assets/ProjectSettings.
- Company name is still `DefaultCompany`.
