using System;
using System.Runtime.InteropServices;
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
            SetTargetFPS(_targetFPS);
            QueryPerformanceCounter(out _lastFrameTicks);
            QualitySettings.vSyncCount = 0;
            PlayerLoopHook.Add<Update>(Tick);
        }

        /// <summary>
        /// Sets the target frames per second (FPS) for the application.
        /// </summary>
        /// <remarks>This method adjusts the internal timing calculations to achieve the specified frame
        /// rate. Providing a value less than or equal to 0 may result in undefined behavior.</remarks>
        /// <param name="newFPS">The desired target FPS. Must be greater than 0.</param>
        public static void SetTargetFPS(float newFPS)
        {
            _targetFPS = newFPS;
            QueryPerformanceFrequency(out _frequency);
            _targetFrameTimeTicks = (long)(_frequency / (double)_targetFPS);
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
        private static float _targetFPS = 120.0f;
        private static long _targetFrameTimeTicks;
        private static long _lastFrameTicks;
        private static long _frequency;

        [DllImport("kernel32.dll")]
        private static extern bool QueryPerformanceCounter(out long lpPerformanceCount);

        [DllImport("kernel32.dll")]
        private static extern bool QueryPerformanceFrequency(out long lpFrequency);

        /// <summary>
        /// Regulates the frame rate by ensuring a consistent time interval between frames.
        /// </summary>
        /// <remarks>This method calculates the time elapsed since the last frame and, if necessary,
        /// delays execution  to maintain the target frame rate. It invokes the <see cref="OnFrameLimiterTick"/> delegate to
        /// perform rendering  operations during each frame. The method uses high-resolution performance counters to
        /// measure time  intervals accurately.</remarks>
        private static void FrameLimiter()
        {
            long currentTicksStart;
            QueryPerformanceCounter(out currentTicksStart);

            OnFrameLimiterTick?.Invoke();

            long currentTicksAfterRender;
            QueryPerformanceCounter(out currentTicksAfterRender);

            long elapsedTicks = currentTicksAfterRender - _lastFrameTicks;
            long remainingTicks = _targetFrameTimeTicks - elapsedTicks;

            if (remainingTicks > 0)
            {
                long targetTicks = currentTicksAfterRender + remainingTicks;
                long currentTicks;

                do { QueryPerformanceCounter(out currentTicks); }
                while (currentTicks < targetTicks);

                _lastFrameTicks = targetTicks;
            }
            else _lastFrameTicks = currentTicksAfterRender;
        }
    }
}