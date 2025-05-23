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
        /// <summary>
        /// Occurs when the frame limiter triggers a tick event.
        /// </summary>
        /// <remarks>This event is invoked at regular intervals determined by the frame limiter's
        /// configuration. Subscribers can use this event to execute logic that needs to run on each tick.</remarks>
        public static Action OnFrameLimiterTick;

        /// <summary>
        /// Initializes the application settings and prepares the runtime environment before the first scene is loaded.
        /// </summary>
        /// <remarks>This method is automatically invoked before the first scene is loaded, as specified
        /// by the  <see cref="RuntimeInitializeOnLoadMethodAttribute"/> with <see
        /// cref="RuntimeInitializeLoadType.BeforeSceneLoad"/>. It configures the target frame rate, disables vertical
        /// synchronization, and sets up a custom update loop.</remarks>
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

        /// <summary>
        /// Advances the application's state by performing a single update tick.
        /// </summary>
        /// <remarks>This method enforces frame rate limiting and clears application state if the
        /// application is not in play mode.</remarks>
        private static void Tick()
        {
            FrameLimiter();

            if (!Application.isPlaying)
                Clear();
        }

        /// <summary>
        /// Clears the current frame limiter state by removing the update hook and resetting the event handler.
        /// </summary>
        /// <remarks>This method removes the <see cref="Update"/> hook associated with the frame limiter
        /// and sets the <see cref="OnFrameLimiterTick"/> event to <see langword="null"/>. Call this method to release
        /// resources or reset the frame limiter to its initial state.</remarks>
        private static void Clear()
        {
            OnFrameLimiterTick = null;

            PlayerLoopHook.Remove<Update>(Tick);
        }
    }

    /// <summary>
    /// Provides functionality for globally limiting the refresh rate of rendering operations.
    /// </summary>
    /// <remarks>The <see cref="GlobalRefreshRateLimiter"/> class allows developers to control the target
    /// frames per second (FPS) for rendering operations by setting a global frame rate limit. The <see
    /// cref="OnFrameLimiterTick"/> action is invoked during each frame, and the frame limiter ensures that rendering adheres to
    /// the specified target FPS.</remarks>
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