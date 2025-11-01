# Unity Essentials

This module is part of the Unity Essentials ecosystem and follows the same lightweight, editor-first approach.
Unity Essentials is a lightweight, modular set of editor utilities and helpers that streamline Unity development. It focuses on clean, dependency-free tools that work well together.

All utilities are under the `UnityEssentials` namespace.

```csharp
using UnityEssentials;
```

## Installation

Install the Unity Essentials entry package via Unity's Package Manager, then install modules from the Tools menu.

- Add the entry package (via Git URL)
    - Window → Package Manager
    - "+" → "Add package from git URL…"
    - Paste: `https://github.com/CanTalat-Yakan/UnityEssentials.git`

- Install or update Unity Essentials packages
    - Tools → Install & Update UnityEssentials
    - Install all or select individual modules; run again anytime to update

---

# Global Refreshrate Limiter

> Quick overview: A global cadence for rendering and time‑sliced work, driven by a high‑precision frame limiter that hooks into the PlayerLoop, and signals subscribers on each tick.

A single, application‑wide frame cadence is maintained using high‑resolution timers. VSync is disabled and a precise sleep/spin wait is applied so that subscribers are notified on `OnFrameLimiterTick` at approximately the requested frequency. Systems like per‑camera frame limiters can subscribe to the tick to schedule rendering consistently across the project.

![screenshot](Documentation/Screenshot.png)

## Features
- Global tick event
  - `GlobalRefreshRateLimiter.OnFrameLimiterTick` is invoked once per target frame; any system can subscribe/unsubscribe
- Configurable target FPS
  - `GlobalRefreshRateLimiter.SetTargetFrameRate(fps)` recalculates timing and applies immediately
  - Invalid values (≤ 0) are guarded and fall back to 60 FPS with a warning
- PlayerLoop integration
  - Initialized automatically before the first scene load and hooked into the `Update` phase
  - Cleanly removed when the application is not playing
- High‑precision pacing
  - Uses `Stopwatch` with nanosecond‑scaled sleeps and a short final spin for accurate intervals
  - Platform‑aware sleeping (nanosleep on Linux via P/Invoke; millisecond sleeps elsewhere)
- VSync disabled
  - `QualitySettings.vSyncCount` is set to 0 so the limiter determines pacing instead of the display

## Requirements
- Unity 6000.0+
- Works in Play Mode and Player
- VSync should be left disabled while the limiter is active (the limiter sets this to 0 at init)

## Usage
1) Set a global target FPS (optional at startup or any time)
   - Call `GlobalRefreshRateLimiter.SetTargetFrameRate(120f)` (default is 120)
2) Subscribe to the global tick
   - In your component: subscribe on enable, unsubscribe on disable
     - `GlobalRefreshRateLimiter.OnFrameLimiterTick += YourHandler;`
     - `GlobalRefreshRateLimiter.OnFrameLimiterTick -= YourHandler;`
3) Do work on tick
   - Use the callback to schedule rendering or other periodic work (e.g., per‑camera render windows)

Notes
- The limiter initializes automatically before the first scene load; no bootstrap code is required
- The tick runs during the `Update` player loop phase

## How It Works
- Initialization
  - On load, VSync is disabled, the default target FPS is applied, and a PlayerLoop hook registers the internal `Tick`
- Tick cycle
  - Each frame, `FrameLimiter` first invokes `OnFrameLimiterTick` to allow subscribers to act
  - Elapsed time since the previous tick is measured; if time remains to reach the target interval, a precise sleep is performed and a short spin completes the interval
  - The next reference timestamp is updated accurately using `Stopwatch`
- Platform timing
  - Sleeps are performed with the best available mechanism per platform; a tight spin finishes the last ~0.08 ms for precision
- Cleanup
  - When the application is not playing, the hook is removed and the event is cleared

## Notes and Limitations
- VSync interaction: VSync is disabled so the limiter controls pacing. Enabling VSync concurrently can cause contention and irregular timing
- Time scaling: Changes in time scale do not affect the limiter; it is based on `Stopwatch` (wall‑clock) rather than game time
- Background load: The final spin‑wait is brief but non‑zero; extremely low target FPS values will still incur minimal CPU activity per frame
- Threading: The tick and sleeps occur on the main thread via PlayerLoop; long subscriber work will affect frame pacing

## Files in This Package
- `Runtime/GlobalRefreshRateLimiter.cs` – Global target FPS, PlayerLoop hook, tick event, and high‑precision timing helpers
- `Runtime/UnityEssentials.GlobalRefreshrateLimiter.asmdef` – Runtime assembly definition

## Tags
unity, framerate, refresh‑rate, limiter, tick, cadence, vsync, playerloop, stopwatch, high‑precision, timing, performance
