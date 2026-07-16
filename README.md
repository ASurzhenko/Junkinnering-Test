# Junkinnering — Tap-the-Object Test Task

A small Unity game built for the *Senior Unity Developer* test task. One 3D object sits on screen wearing an
image loaded **asynchronously via Addressables**. Tap it → score goes up and a new image loads (the next
round). Tap empty space → the object flashes red and keeps its image. If a new round starts before the
previous image finished downloading, the stale download is cancelled so it can never overwrite the newer one.

## Stack

- **Unity `6000.3.5f2`**, **URP 17.3.0**, Android target (runs in the Editor with mouse).
- **Addressables 2.8.1** — async asset loading, local emulation + optional remote (S3/CloudFront) delivery.
- **Input System 1.17.0** — `Pointer.current` unifies mouse (Editor) and touch (device).
- Plain C# `async/await` + one coroutine (the flash). No third-party packages.

## Run it

1. Open the project in Unity `6000.3.5f2`.
2. Open `Assets/Scenes/SampleScene.unity`.
3. **Addressables ▸ Groups ▸ Play Mode Script = "Use Asset Database (fastest)"** (default local mode — no
   content build needed).
4. Press **Play**. Status shows `Loading…` → `Tap the object!`; the sphere appears with an image, score `0`.
5. **Click the sphere** → score increments, a new image loads. **Click empty space** → the object flashes red.

Local mode works out of the box. Remote delivery (the spec's optional point 3) is configured and documented
under [Remote delivery](#remote-delivery-optional) below.

## Controls

- **Tap / left-click the object** — correct selection: `+1` score, next round (new image).
- **Tap / click empty space** — incorrect: red flash, image and score unchanged.

## Architecture

Deliberately small — a handful of focused classes, namespace `Junkinnering`, no framework scaffolding.

| Script | Role |
|--------|------|
| `AddressableAssetService` | Loads the **prefab** and the **fallback texture** exactly **once** at startup, holds their handles, releases them on teardown. Status-checked, cancellable. |
| `RoundImageLoader` | Resolves the `round_image` Addressables **label** to its texture locations once, then hands out a **random** texture load per round. Returns the handle without throwing; the caller checks status and owns the release. |
| `GameController` | Orchestrates rounds, score, status text, the red flash, and the **per-round stale-download cancellation**. Owns all lifecycle. |
| `TapInput` | One tap per frame from `Pointer.current` (mouse + primary touch), null-safe. |
| `TextureApplier` | Applies a texture (`_BaseMap`) / tint (`_BaseColor`) via a `MaterialPropertyBlock` — no per-object material instance, no leak. |

### Assets

- `Assets/Prefabs/TargetObject.prefab` — the Addressable sphere (with collider).
- `Assets/Textures/fallback.png` — the Addressable fallback texture.
- `Assets/Textures/Rounds/img_0…3.png` — the round images, all tagged with the Addressables label
  `round_image`.

## Key design decisions

- **Non-blocking async, failure via status — never `WaitForCompletion`.** Every load `await`s
  `handle.Task`. Because `AsyncOperationHandle.Task` completes *without throwing* on a failed load, the code
  checks `handle.Status == Succeeded` rather than relying on try/catch; a genuine failure applies the
  **fallback** texture, distinct from a cancellation.
- **Stale-download cancellation (the crux).** Each round owns a `CancellationTokenSource` linked to a master
  token. Starting a new round cancels the previous one; after the `await`, the round checks its **own
  captured** token (not a shared field) and discards its result if it was superseded — so even a superseded
  load that finishes *after* a newer round already applied can never overwrite it. The freshest round always
  wins.
- **Addressables lifecycle.** Prefab + fallback are loaded once and released on teardown; each per-round image
  handle is released the moment it is replaced, superseded, or fails — the new texture is applied *before* the
  old handle is released, so the object never shows a blank frame.
- **Input abstraction.** `Pointer.current` means the identical tap→raycast path serves mouse in the Editor and
  touch on device.
- **URP.** Textures/tints go through a `MaterialPropertyBlock` on `_BaseMap` / `_BaseColor` (URP's slots),
  avoiding a leaked material instance.

## Spec coverage

| # | Requirement | Where |
|---|-------------|-------|
| a | 1 object spawned from an Addressable prefab | `GameController.Start` → `AddressableAssetService` |
| b | Texture loaded via Addressables, async, non-blocking | `RoundImageLoader` / `GameController.StartRoundAsync` |
| c | Fallback Addressable texture on download failure | `StartRoundAsync` `Status != Succeeded` branch |
| d | Prefab + fallback loaded once, released on teardown | `AddressableAssetService` + `GameController.OnDestroy` |
| e | Status text (`Loading…` / `Tap the object!`) | `GameController` status updates |
| f | Score text, incremented per correct tap | `GameController.OnCorrectTap` |
| g | Hit → new image + round; miss → negative flash, image unchanged | `GameController.Update` → `OnCorrectTap` / `OnIncorrectTap` |
| h | New round cancels the previous unfinished download | per-round `CancellationTokenSource`, captured-local-token check |

## Remote delivery (optional)

The round images live in an Addressables **`Remote Images`** group; the prefab + fallback stay local (so the
app always starts, including offline — an unreachable remote simply falls back to the fallback texture). The
profile's `RemoteLoadPath` points at an HTTPS CloudFront host, and **Build Remote Catalog** is enabled with
its Build/Load paths set to the Remote profile variables, so the catalog is self-hosted alongside the bundles.

To publish the remote content:

1. Confirm the host serves the target prefix over HTTPS (`curl -I` a probe object → `200`) **before** building
   — the load path is baked into the catalog at build time.
2. Set the active build target to **Android**, then **Addressables ▸ Groups ▸ Build ▸ New Build ▸ Default
   Build Script**. The remote bundles + `catalog_*.bin` + `catalog_*.hash` land in `ServerData/Android/`.
3. Upload them to the host: `aws s3 sync ServerData/Android/ s3://<bucket>/<prefix>/Android/`.
4. Verify each artifact resolves over HTTPS (`curl -I … → 200`), then run on an Android device (network trace
   shows the `.hash` + bundle requests) and check the offline path shows the fallback texture.

Switch the Play Mode Script back to **"Use Asset Database (fastest)"** for local iteration at any time.
