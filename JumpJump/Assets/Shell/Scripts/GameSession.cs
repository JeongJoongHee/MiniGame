using System;
using UnityEngine;

namespace Arcade
{
    /// <summary>한 판이 끝났을 때의 결과 한 줄.</summary>
    public struct RunResult
    {
        /// <summary>`GameCatalog.asset` 의 id 와 같은 값 (jumpjump / archery). 랭킹표를 가르는 기준입니다.</summary>
        public string gameId;

        /// <summary>랭킹에 올릴 점수.</summary>
        public int score;

        /// <summary>화면에 곁들일 한 줄 (예: "166m", "명중 32"). 랭킹에는 올리지 않습니다.</summary>
        public string detail;

        /// <summary>왜 끝났는지 (TIME OVER / OUT OF ARROWS 등). 기록용입니다.</summary>
        public string reason;

        public RunResult(string gameId, int score, string reason, string detail = "")
        {
            this.gameId = gameId;
            this.score = score;
            this.reason = reason;
            this.detail = detail;
        }
    }

    /// <summary>
    /// **"한 판이 끝났다" 는 사실이 모이는 단 한 곳**입니다.
    /// 두 게임의 <c>EndRun</c> 이 각각 한 줄씩 여기로 알려 주고,
    /// 랭킹 등록 · 광고 띄우기는 <b>이 신호를 듣는 쪽</b>에서 합니다.
    ///
    /// <b>왜 이렇게 나누는가.</b> 이 프로젝트의 검증은 <c>Step(dt, tap)</c> 을 배치 모드에서
    /// 그대로 재생하는 방식입니다. 봇이 120초 동안 수십 판을 도는데 그 안에서 광고나 네트워크를
    /// 부르면 검증이 통째로 망가집니다. 그래서 <b><c>Step</c> 안에서는 "끝났다"는 사실만 남기고</b>,
    /// 실제 광고·통신은 MonoBehaviour 쪽에서 처리합니다.
    ///
    /// <see cref="Recording"/> 을 끄면 판 수도 세지 않습니다. **자동 플레이테스트가 이것을 끕니다** —
    /// 봇이 돈 판이 광고 카운터에 섞이면 안 되기 때문입니다.
    /// </summary>
    public static class GameSession
    {
        /// <summary>끈 상태에서는 아무것도 세지 않고 아무에게도 알리지 않습니다. (배치 플레이테스트용)</summary>
        public static bool Recording = true;

        /// <summary>한 판이 끝났을 때 불립니다. 랭킹·광고가 여기에 귀를 답니다.</summary>
        public static event Action<RunResult> RunFinished;

        /// <summary>가장 마지막으로 끝난 판. 게임 오버 화면이 참고합니다.</summary>
        public static RunResult Last { get; private set; }

        /// <summary>한 판이 끝났습니다. 각 게임의 <c>EndRun</c> 이 한 줄로 부릅니다.</summary>
        public static void ReportRunFinished(RunResult result)
        {
            if (!Recording) return;

            Last = result;
            PlayCounter.RecordPlay();

            // 듣는 쪽에서 예외가 나도 게임이 멈추면 안 됩니다.
            try
            {
                RunFinished?.Invoke(result);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Arcade] 판 종료를 알리는 중 오류: " + e.Message);
            }
        }
    }
}
