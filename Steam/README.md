# Shipping Hollowmere on Steam

The split matters: everything under "Code side" is done or scriptable. Everything under
"Your side" requires your identity, your bank details, and your signature — nobody can do
it for you, and no tool should offer to.

---

## Your side (cannot be automated)

1. **Steamworks partner account** — <https://partner.steamgames.com>. Company or sole
   trader details, and identity verification.
2. **Steam Direct fee** — $100 USD per app. Recoupable once the app earns $1,000 adjusted
   gross revenue.
3. **Tax and banking** — W-9 (US) or W-8BEN (non-US), plus payout bank details. Valve will
   not let you release until these clear.
4. **Store page** — capsule art, screenshots, trailer, description, tags, age rating.
5. **The 30-day rule** — once you *publish* the store page, Steam enforces a minimum
   30-day wait before you can release. Publish the page early; it is the long pole.
6. **Build review** — Valve reviews the first build (typically a few business days). They
   check it launches and matches the store page, not whether it is good.
7. **Press the buttons** — set the build live, and hit release.

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
