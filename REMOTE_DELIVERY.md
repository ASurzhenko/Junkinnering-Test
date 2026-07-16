# Remote Delivery (spec point 3 — optional)

The baseline game runs Addressables in **local** mode (all assets in `Default Local Group`). This document
sets up **real network delivery**: the per-round images are hosted on S3 and served over HTTPS via
CloudFront, and the Addressables **RemoteLoadPath** points at them. Local mode still works on its own.

Because `RoundImageLoader` loads round images by the `round_image` **label**, it resolves whatever the
catalog says — local or remote. Moving the images to a remote group is therefore a **content/config +
build/upload change with ZERO runtime code change**. `RoundImageLoader` / `GameController` are untouched.

## Hosting

- **Bucket:** `epochreels-ota` (S3, `us-east-1`), reused under the distinct prefix `junkinnering/`. The
  bucket stays **private** (Block Public Access ON) — do not make it public.
- **Delivery:** CloudFront distribution `d2eupgfrfppc7x.cloudfront.net` with OAC serves the private bucket
  over HTTPS. This is the only delivery path. If CloudFront cannot serve `junkinnering/*`, fix CloudFront
  (add a behavior / OAC grant) — never open S3.
- **Isolation:** upload only under `s3://epochreels-ota/junkinnering/`. Do not touch EpochReels OTA keys.

## Prerequisites

1. **AWS CLI** on PATH with credentials that can `PutObject`/`ListBucket` on
   `s3://epochreels-ota/junkinnering/`. Confirm with `aws sts get-caller-identity`.
2. **Unity Editor** open on this project (the Addressables group/profile/settings work is Editor-side).
3. CloudFront distro `d2eupgfrfppc7x.cloudfront.net` (OAC) serving the `junkinnering/` prefix.

## Step 1 — Prove the host first (before anything is baked)

`RemoteLoadPath` is baked into the catalog at **build time**, so a wrong host means a rebuild. Confirm the
host resolves over HTTPS **before** setting it and building:

```sh
echo probe > probe.txt
aws s3 cp probe.txt s3://epochreels-ota/junkinnering/probe.txt --region us-east-1
curl -I https://d2eupgfrfppc7x.cloudfront.net/junkinnering/probe.txt   # expect: 200
```

- `200` → use the CloudFront host (below).
- `403` / `404` → the distro does not serve the prefix. Add a CloudFront behavior / OAC grant for
  `junkinnering/*`, then re-`curl`. **Do not make S3 public.**

## Step 2 — Addressables profile

**Addressables ▸ Profiles**, on the `Default` profile:

- `Remote.LoadPath` = `https://d2eupgfrfppc7x.cloudfront.net/junkinnering/[BuildTarget]`
- `Remote.BuildPath` = `ServerData/[BuildTarget]` (default — the local build output that gets uploaded)

`[BuildTarget]` expands to `Android`. Leave `Local.BuildPath` / `Local.LoadPath` at their defaults.

## Step 3 — Remote group + hosted catalog

1. Create a group **`Remote Images`**. On its `BundledAssetGroupSchema`: `Build Path` = `Remote.BuildPath`,
   `Load Path` = `Remote.LoadPath`, `Include In Build` on, compression LZ4.
2. **Move the four `round_image`-tagged textures** (`img_0`..`img_3`) from `Default Local Group` into
   `Remote Images`. The `round_image` label travels with them (labels are asset-level).
3. Leave the `TargetObject` prefab and `FallbackTexture` in `Default Local Group` — they load at startup and
   must always be present, including offline (the fallback is the offline safety net).
4. **Addressables ▸ Settings** (the `AddressableAssetSettings` inspector): tick **Build Remote Catalog** and
   set its **Build & Load Paths = Remote**. This hosts the catalog per spec point 3.
   - The catalog is **binary** (`Enable Json Catalog` is OFF): the build emits `catalog_<suffix>.bin` +
     `catalog_<suffix>.hash`, not `.json`. Keep the default. `<suffix>` is a timestamp / the Player Version
     Override, not a hash.

## Step 4 — Build & upload

Run only after Step 1 confirmed the host and Step 2 baked it into the profile.

1. Set the active build target to **Android** (`File ▸ Build Settings`).
2. **Addressables ▸ Groups ▸ Build ▸ New Build ▸ Default Build Script.** With Build Remote Catalog ON this
   emits into `ServerData/Android/`: the image **bundle(s)** + the binary **`catalog_*.bin` + `catalog_*.hash`**.
3. Upload the remote build output, mirroring the `[BuildTarget]` layout:
   ```sh
   aws s3 sync ServerData/Android/ s3://epochreels-ota/junkinnering/Android/ --region us-east-1
   ```
4. Read the real artifact names (PowerShell — this repo builds on Windows), then `curl` each over HTTPS:
   ```powershell
   Get-ChildItem ServerData/Android/catalog_*.bin, ServerData/Android/catalog_*.hash, ServerData/Android/*.bundle
   ```
   ```sh
   curl -I https://d2eupgfrfppc7x.cloudfront.net/junkinnering/Android/<catalog>.bin    # 200
   curl -I https://d2eupgfrfppc7x.cloudfront.net/junkinnering/Android/<catalog>.hash   # 200
   curl -I https://d2eupgfrfppc7x.cloudfront.net/junkinnering/Android/<bundle>.bundle  # 200
   ```

Optional one-click: `Tools/Addressables/Build & Upload Remote`
(`Assets/Scripts/Editor/BuildAndUploadRemote.cs`) runs the content build, then `aws s3 sync`. The manual
flow above is the deliverable; the menu item is a convenience.

## Step 5 — Runtime semantics (no code change)

The player ships a **built-in full copy** of the catalog. Because the built-in and hosted catalogs come from
the same build, they share the same hash: at startup Addressables fetches the hosted **`.hash`**, sees it
matches, and keeps using the built-in catalog. The hosted catalog `.bin` is downloaded only when a *later*
build's hash differs (a content update). So on a first install the runtime signal is a **`.hash` request +
the remote bundle download** over HTTPS — **not** a catalog download. "Catalog is hosted" is proven by the
Step 4 `curl`, not by a runtime request.

Offline / host unreachable → the image load fails → the existing `Status != Succeeded` branch applies the
fallback texture (a cancelled load is a distinct, expected path — not the fallback). No new code.

## Step 6 — Verification

1. **Local still works:** Addressables Play-mode script = "Use Asset Database (fastest)" → game runs as before.
2. **Catalog hosted:** the Step 4 `curl -I` on `catalog_*.bin` + `.hash` return `200`.
3. **Remote path, Android device:** install an Android build; in `adb logcat` / a network trace the app
   requests the hosted `.hash` and downloads the image **bundle(s)** over HTTPS from CloudFront.
4. **Stale cancellation under real latency:** rapid-tap during the visibly-longer `Loading image...` window →
   the object ends on the **last tap's** image, no flicker, no growth in live image handles.
5. **Offline:** airplane-mode the device → each round's image load fails → object shows the **fallback
   texture**, status returns to ready, no crash.
6. **Isolation:** confirm the upload wrote only under `s3://epochreels-ota/junkinnering/`.
