using System;
using System.Diagnostics;
using System.Threading;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace UnityEssentials
{
    /// <summary>
    /// Provides global functionality for managing and limiting the frame rate of the application.
    /// </summary>
    /// <remarks>This class allows for setting a target frame rate and provides an event to notify subscribers
    /// of frame updates. It is initialized automatically before the first scene load and integrates with the Unity
    /// player loop.</remarks>
    public static partial class GlobalRefreshRateLimiter
    {
        public static Action OnFrameLimiterTick;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            SetTargetFrameRate(_targetFrameRate);
            _lastFrameTicks = Stopwatch.GetTimestamp();
            QualitySettings.vSyncCount = 0;
            PlayerLoopHook.Add<Update>(Tick);
        }

        /// <summary>
        /// Sets the target frames per second (FPS) for the application.
        /// </summary>
        /// <remarks>This method adjusts the internal timing calculations to achieve the specified frame
        /// rate. Providing a value less than or equal to 0 may result in undefined behavior.</remarks>
        /// <param name="frameRate">The desired target FPS. Must be greater than 0.</param>
        public static void SetTargetFrameRate(float frameRate)
        {
            // Guard against invalid values to prevent division by zero
            if (frameRate <= 0f)
            {
                UnityEngine.Debug.LogWarning($"GlobalRefreshRateLimiter: Invalid frameRate {frameRate}. Falling back to 60 FPS.");
                frameRate = 60f;
            }

            _targetFrameRate = frameRate;
            _frequency = Stopwatch.Frequency;
            _targetFrameTimeTicks = (long)(_frequency / (double)_targetFrameRate);
        }

        private static void Tick()
        {
            FrameLimiter();

            if (!Application.isPlaying)
                Clear();
        }

        private static void Clear()
        {
            OnFrameLimiterTick = null;
            PlayerLoopHook.Remove<Update>(Tick);
        }
    }

    public static partial class GlobalRefreshRateLimiter
    {
        private static float _targetFrameRate = 60.0f;
        private static long _targetFrameTimeTicks;
        private static long _lastFrameTicks;
        private static long _frequency;

        /// <summary>
        /// Regulates the frame rate by ensuring a consistent time interval between frames.
        /// </summary>
        /// <remarks>This method calculates the time elapsed since the last frame and, if necessary,
        /// delays execution  to maintain the target frame rate. It invokes the <see cref="OnFrameLimiterTick"/> delegate to
        /// perform rendering  operations during each frame. The method uses high-resolution performance counters to
        /// measure time  intervals accurately.</remarks>
        private static void FrameLimiter()
        {
            // invoke tick handlers
            OnFrameLimiterTick?.Invoke();

            long currentTicksAfterRender = Stopwatch.GetTimestamp();

            long elapsedTicks = currentTicksAfterRender - _lastFrameTicks;
            long remainingTicks = _targetFrameTimeTicks - elapsedTicks;

            if (remainingTicks > 0)
            {
                long targetTicks = currentTicksAfterRender + remainingTicks;
                HighPrecisionWait.WaitUntil(targetTicks, _frequency);
                _lastFrameTicks = targetTicks;
            }
            else _lastFrameTicks = currentTicksAfterRender;
        }
    }

    // Cross-platform high precision wait helpers
    internal static class HighPrecisionWait
    {
        // Spin threshold in nanoseconds: we sleep until this near the target, then busy-spin to finish precisely
        private const long SpinThresholdNs = 80_000; // 0.08 ms

        public static void WaitUntil(long targetTimestamp, long stopwatchFrequency)
        {
            while (true)
            {
                long now = Stopwatch.GetTimestamp();
                long remainingTicks = targetTimestamp - now;
                if (remainingTicks <= 0)
                    return;

                // Convert remaining to nanoseconds using Stopwatch frequency
                long remainingNs = (long)((remainingTicks * 1_000_000_000.0) / stopwatchFrequency);

                if (remainingNs > SpinThresholdNs)
                {
                    // Sleep most of the remaining time, keep a small margin for an accurate final spin
                    long sleepNs = remainingNs - SpinThresholdNs;
                    SleepNs(sleepNs);
                    continue;
                }

                // Final tight spin (very short)
                SpinUntil(targetTimestamp);
                return;
            }
        }

        private static void SpinUntil(long targetTimestamp)
        {
            // Tiny spin loop for the last ~0.08ms
            while (Stopwatch.GetTimestamp() < targetTimestamp)
            {
                // Hint to CPU that we're in a spin-wait loop
                Thread.SpinWait(20);
            }
        }

        private static void SleepNs(long ns)
        {
#if (UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX)
            LinuxSleepNs(ns);
#elif (UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX)
            // macOS: best we can from managed is Thread.Sleep for millisecond parts, then rely on spin for sub-ms
            if (ns >= 1_000_000)
            {
                int ms = (int)(ns / 1_000_000);
                if (ms > 0)
                    Thread.Sleep(ms);
            }
            else
            {
                // For very small sleeps, yield the remainder of the time slice
                Thread.Sleep(0);
            }
#else
            // Windows and other platforms: use Thread.Sleep for ms part, then yield once for sub-ms, final spin does the rest
            if (ns >= 1_000_000)
            {
                int ms = (int)(ns / 1_000_000);
                if (ms > 0)
                    Thread.Sleep(ms);
            }
            else
            {
                Thread.Sleep(0);
            }
#endif
        }

#if (UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX)
        // P/Invoke nanosleep for higher precision relative sleeps on Linux
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct Timespec
        {
            public long tv_sec;   // seconds
            public long tv_nsec;  // nanoseconds
        }

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        private static extern int nanosleep(ref Timespec req, IntPtr rem);

        private static void LinuxSleepNs(long ns)
        {
            if (ns <= 0)
            {
                Thread.Sleep(0);
                return;
            }

            var req = new Timespec
            {
                tv_sec = ns / 1_000_000_000,
                tv_nsec = ns % 1_000_000_000
            };

            // We ignore the remainder arg (rem) and EINTR handling for simplicity; if interrupted, we just return
            _ = nanosleep(ref req, IntPtr.Zero);
        }
#endif
    }
}