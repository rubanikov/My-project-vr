Type: research
Status: resolved

## Question

The Meta XR Simulator installer previously failed for this user due to a local file-write block (noted in `PRD.md` and `MISSION.md`); the core Meta XR SDK itself is unaffected. Investigate:

- Is there a known cause/fix for the Meta XR Simulator installer failing with a local file-write block on Windows (permissions, antivirus, install path, etc.)?
- What are the documented alternative testing paths if the Simulator can't be installed — e.g. testing directly on a physical Quest 2 device via Meta XR Operator, or any other no-headset-needed verification path?

Use `meta_docs_search` / metavr docs and any other primary sources. Resolve with findings; the follow-up task ticket (05) acts on them.

## Answer

No documented fix exists for this specific cause. Meta's docs don't mention antivirus, Windows Defender, install-path permissions, or OneDrive as causes of a Simulator installer file-write block. The only generic remediation found: delete the cached install dir (`%APPDATA%\Local\MetaXR\MetaXrSimulator\<version>`) and any leftover `meta_xr_simulator_<version>.zip` in Downloads, then retry — not guaranteed to work. One untested lever: `metavr tools install xrsim --elevate` (requests UAC elevation).

The device-only alternative is fully documented and viable: Unity's Build Profiles → Meta Quest platform → Build and Run deploys straight to a connected Quest 2 (manual `adb install <apk>` as fallback); Meta XR Operator and the `metavr` MCP tools (`metavr_app` install/launch, `take_screenshot`, `get_device_logcat`) run entirely over ADB, no Simulator required.

**Recommendation**: don't block on fixing the Simulator. Try the cache-clear-and-retry or `--elevate` once, low-cost, but default to the device-only loop (Build-and-Run + `metavr_app`/`take_screenshot`/`get_device_logcat`) as the actual test path for this project.

Full findings: branch `research/xr-simulator-testing-path`, commit `b8cad32`, file `.scratch/court-clash/research/xr-simulator-testing-path.md`.
