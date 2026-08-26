Type: task
Status: resolved
Blocked by: 04

## Question

Given the research findings from ticket 04, either fix the Meta XR Simulator install so it works, or confirm and document the device-only testing pipeline (build → deploy to Quest 2 → Meta XR Operator screenshot/verify) as the actual test loop for this project. Resolved when we know, concretely, how each build iteration will be verified during implementation.

## Status (in progress, not yet resolved)

User is retrying the Simulator install now (per ticket 04's low-cost fix suggestion). Quest 2 hardware arrives tomorrow, giving us the device-only fallback either way. Not blocking implementation start — court/ball/fault-rule work doesn't need either yet. Revisit and close once we know which verification path is actually live (Simulator working, or device connected).

**Update (2026-08-24, later same day)**: Checked current state —
- Meta XR Simulator: still not installed (`%LOCALAPPDATA%\MetaXR\MetaXrSimulator` doesn't exist). The cache-clear-and-retry from ticket 04 hasn't resolved it yet, or hasn't been retried since.
- Quest 2 device: not connected (`metavr_device list` returns empty). Consistent with "arrives 2026-08-25" — hasn't landed yet as of today.
- **New fact**: the Unity MCP bridge (`unity-mcp`, relay-based) is now confirmed live and responding (`Unity_ManageEditor GetState` succeeded against a running Editor on 6000.3.22f1). This was previously unconfirmed per `PRD.md`'s "Check early whether it's connected now" note — now confirmed yes. This unblocks direct Editor-level iteration (compile checks, console logs, scene state) without needing either the Simulator or the device, for any build step that doesn't require actual VR-runtime testing (e.g. court geometry logic, script compilation).

Still not resolved: full VR-runtime verification (grab/throw feel, Guardian boundary sizing, in-headset playtesting) still has no live path — neither Simulator nor device. Still not blocking implementation start. Revisit and close once the Quest 2 arrives (2026-08-25) or the Simulator installs successfully.

## Answer

Fixed the install — Option A. Confirmed:

1. **Meta XR Simulator v205.0 is installed**, at `C:\Program Files\MetaXRSimulator\v205.0`, fully populated (previously-blocked file-write install completed this session, cause/fix undetermined — ticket 04's cache-clear or a plain retry evidently worked).
2. **Activated as the system's OpenXR runtime**: ran `activate_simulator.ps1` elevated (it writes `HKLM:\SOFTWARE\Khronos\OpenXR\1\ActiveRuntime`, hence the admin prompt); verified the registry now points at `C:\Program Files\MetaXRSimulator\v205.0\meta_openxr_simulator.json`.

**Verification path for this project, going forward**: Meta XR Simulator is the primary iteration loop — Unity Play mode against the now-active OpenXR runtime, no headset needed for each build iteration. The device-only pipeline (Build-and-Run to Quest 2, `metavr_app`/`take_screenshot`/`get_device_logcat`) remains the fallback/final-verification path once the Quest 2 hardware is in hand (expected 2026-08-25), for anything that needs real Guardian-boundary data or in-headset feel the Simulator can't fully replicate.

**Not yet done**: an actual Play-mode smoke test (enter Play, confirm the Simulator window opens and tracks) hasn't been run yet — worth doing as the first check when implementation resumes, before relying on this loop for real iteration.
