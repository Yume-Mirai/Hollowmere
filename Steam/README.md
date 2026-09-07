# Shipping Hollowmere on Steam

The split matters: everything under "Code side" is done or scriptable. Everything under
"Your side" requires your identity, your bank details, and your signature — nobody can do
it for you, and no tool should offer to.

---

## Your side (cannot be automated)

1. **Steamworks partner account** — <https://partner.steamgames.com>. Company or sole
   trader details, and identity verification.
2. **Steam Direct fee** — $100 USD per app. Recoupable once the app earns $1,000 adjusted
   gross revenue. **Being free does not waive this.** Every app pays it, and since a free
   game with no monetisation never reaches $1,000 of revenue, treat the $100 as a flat,
   non-recoverable cost of publishing.
3. **Tax and banking** — W-9 (US) or W-8BEN (non-US), plus payout bank details. Valve will
   not let you release until these clear.
4. **Store page** — capsule art, screenshots, trailer, description, tags, age rating.
5. **The 30-day rule** — once you *publish* the store page, Steam enforces a minimum
   30-day wait before you can release. Publish the page early; it is the long pole.
6. **Build review** — Valve reviews the first build (typically a few business days). They
   check it launches and matches the store page, not whether it is good.
7. **Press the buttons** — set the build live, and hit release.

## Pricing: free

Hollowmere ships **free**. Decided 2026-09-08.

On the partner site set the price to $0 rather than choosing the *Free To Play* app type —
F2P is intended for games with in-app purchases and carries expectations Hollowmere does
not meet. A plain $0 price is the right shape for a game with no monetisation.

What free changes:

- **Nothing in the code.** There is no monetisation, no IAP, no DRM to strip out.
- Still costs the $100 Steam Direct fee, which will never be recouped (see above).
- Tax and banking details are still required by Valve before release, even with no revenue.
- No refund exposure and no price-related review complaints, which removes most of the
  commercial pressure from a first release — a good trade for a debut project.
- Free games get less storefront visibility than paid ones. Wishlists and outside traffic
  matter more, so the store page and trailer carry more weight, not less.

## Code side (done, or one command)

### Enabling Steamworks

The game builds and runs with no Steam SDK present — `SteamManager` compiles to no-ops.
To turn it on:

1. Download **Steamworks.NET** — <https://github.com/rlabrecque/Steamworks.NET/releases>
   (the `.unitypackage`), import into `Assets/Plugins/`.
2. **Project Settings → Player → Scripting Define Symbols**, add `STEAMWORKS_NET`.
3. Put your real AppID in `Assets/Scripts/Steam/SteamManager.cs`
   (`PlaceholderAppId`, currently `480` — Valve's public Spacewar test app).
4. Create `steam_appid.txt` next to the built `.exe`, containing just the AppID digits.
   Required for local testing; **do not ship it** in the depot.

### Achievements

Defined in `Achievements`. Create each on the partner site with the *exact* API name:

| API name | Trigger |
|---|---|
| `ACH_RESTORE_POWER` | All fuses installed |
| `ACH_ESCAPE` | Reach the exit |
| `ACH_ESCAPE_UNDER_5` | Escape in under 5 minutes |
| `ACH_NEVER_SEEN` | Not yet wired up — no code path unlocks this one |

### Uploading a build

```bash
Unity.exe -quit -batchmode -projectPath . -executeMethod Hollowmere.EditorTools.BuildScript.BuildWindows64 -logFile build.log
```

Then edit `app_build.vdf` and `depot_build.vdf` (replace `REPLACE_ME_APPID` and
`REPLACE_ME_DEPOTID`), and:

```bash
steamcmd +login <account> +run_app_build ..\Steam\app_build.vdf +quit
```

`app_build.vdf` ships with **`"preview" "1"`** — it will do a dry run and change nothing
until you set it to `0`. Leave `setlive` empty and promote the build from the partner
site's Builds page, so a bad upload can never go straight to players.

## Pre-release checklist

- [ ] `steam_appid.txt` excluded from the depot
- [ ] `.pdb` and `*_BurstDebugInformation_DoNotShip*` excluded (already in `depot_build.vdf`)
- [ ] Product name, version, and icon set in Player Settings
- [ ] Game runs on a machine with no Unity installed
- [ ] Game runs with the Steam client closed (it should — Steam init failure is non-fatal)
- [ ] Settings persist across restarts (`%USERPROFILE%\AppData\LocalLow\Nolan\Hollowmere`)
- [ ] Alt-tab, minimise, and non-16:9 resolutions behave
