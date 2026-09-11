using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arcade.Online
{
    /// <summary>
    /// **바깥 라이브러리가 부른 일을 게임의 본 흐름으로 옮겨 오는 우체통**입니다.
    ///
    /// 광고 SDK 는 "광고가 닫혔다" 같은 소식을 안드로이드의 다른 일꾼(스레드)에서 알려 줄 때가 있습니다.
    /// Unity 의 화면·게임 물건은 본 흐름에서만 만질 수 있어서, 그 자리에서 곧바로 게임을 다시 시작하면
    /// 앱이 튕길 수 있습니다. 그래서 할 일을 여기에 넣어 두고, <see cref="OnlinePump"/> 가 매 프레임 꺼내 실행합니다.
    /// "몇 초 뒤에 다시 해 보기"(광고 다시 불러오기)도 여기서 합니다.
    /// </summary>
    public static class MainThread
    {
        static readonly object Lock = new object();
        static readonly List<Action> Queue = new List<Action>();
        static readonly List<(double at, Action action)> Later = new List<(double, Action)>();

        /// <summary>다음 프레임에 본 흐름에서 실행합니다. 어느 일꾼에서 불러도 됩니다.</summary>
        public static void Post(Action action)
        {
            if (action == null) return;
            lock (Lock) Queue.Add(action);
        }

        /// <summary><paramref name="seconds"/> 초 뒤에 본 흐름에서 실행합니다.</summary>
        public static void PostDelayed(float seconds, Action action)
        {
            if (action == null) return;
            double at = Now + Mathf.Max(0f, seconds);
            lock (Lock) Later.Add((at, action));
        }

        /// <summary>쌓인 일을 실행합니다. 매 프레임 한 번 불러야 합니다.</summary>
        public static void Pump()
        {
            List<Action> run = null;
            lock (Lock)
            {
                if (Queue.Count > 0)
                {
                    run = new List<Action>(Queue);
                    Queue.Clear();
                }

                double now = Now;
                for (int i = Later.Count - 1; i >= 0; i--)
                {
                    if (Later[i].at > now) continue;
                    (run ??= new List<Action>()).Add(Later[i].action);
                    Later.RemoveAt(i);
                }
            }

            if (run == null) return;
            foreach (var action in run)
            {
                try { action(); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        static double Now => DateTime.UtcNow.Ticks / (double)TimeSpan.TicksPerSecond;
    }
}
