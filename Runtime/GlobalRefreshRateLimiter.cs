using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace UnityEssentials
{
    public static partial class GlobalRefreshRateLimiter
    {
        public static event Action<float> OnFrameLimiter;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            SetTargetFPS(_targetFPS);
            QueryPerformanceCounter(out _lastFrameTicks);
            QualitySettings.vSyncCount = 0;
            PlayerLoopHook.Add<Update>(FrameLimiter);
        }

        private static void Tick()
        {
            OnFrameLimiter?.Invoke(Time.deltaTime);

            if (!Application.isPlaying)
                Clear();
        }

        public static void Clear()
        {
            PlayerLoopHook.Remove<Update>(Tick);

            OnFrameLimiter = null;
            OnRender = null;
        }
    }

    public static partial class GlobalRefreshRateLimiter
    {
        public static Action OnRender;

        private static float _targetFPS = 120.0f;
        private static long _targetFrameTimeTicks;
        private static long _lastFrameTicks;
        private static long _frequency;

        [DllImport("kernel32.dll")]
        private static extern bool QueryPerformanceCounter(out long lpPerformanceCount);

        [DllImport("kernel32.dll")]
        private static extern bool QueryPerformanceFrequency(out long lpFrequency);

        public static void SetTargetFPS(float newFPS)
        {
            _targetFPS = newFPS;
            QueryPerformanceFrequency(out _frequency);
            _targetFrameTimeTicks = (long)(_frequency / (double)_targetFPS);
        }

        private static void FrameLimiter()
        {
            long currentTicksStart;
            QueryPerformanceCounter(out currentTicksStart);

            OnRender?.Invoke();

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