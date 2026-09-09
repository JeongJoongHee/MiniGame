using UnityEngine;

namespace Arcade
{
    /// <summary>
    /// **미니게임 씬에서 "한 판이 끝나면 랭킹에 올리는" 흐름**을 맡습니다.
    ///
    ///   판이 끝난다 -> 별명이 없으면 물어본다 -> 점수를 올린다
    ///
    /// 게임 쪽 코드는 <c>EndRun</c> 의 한 줄(<see cref="GameSession.ReportRunFinished"/>)이 전부이고,
    /// 나머지는 전부 여기서 합니다. 그래서 <b>미니게임을 새로 만들어도 이 부품만 씬에 넣으면 됩니다.</b>
    ///
    /// <b><c>Step(dt, tap)</c> 바깥</b>이라는 점이 중요합니다. 통신은 여기서만 일어나므로
    /// 배치 모드 플레이테스트(봇이 수십 판을 도는)는 이 부품 없이 그대로 돌아갑니다.
    /// </summary>
    public class RankingFlow : MonoBehaviour
    {
        [Tooltip("별명이 아직 없을 때 띄울 창. 없으면 별명이 생길 때까지 랭킹 등록을 건너뜁니다")]
        [SerializeField] NicknamePopup nicknamePopup;

        RunResult _pending;
        bool _hasPending;

        void OnEnable()
        {
            GameSession.RunFinished += OnRunFinished;
        }

        void OnDisable()
        {
            GameSession.RunFinished -= OnRunFinished;
        }

        void OnRunFinished(RunResult result)
        {
            // 0점은 올리지 않습니다. 시작하자마자 죽은 판까지 랭킹에 올릴 이유가 없습니다.
            if (result.score <= 0) return;

            if (PlayerIdentity.HasNickname)
            {
                Submit(result);
                return;
            }

            if (nicknamePopup == null) return;

            _pending = result;
            _hasPending = true;

            nicknamePopup.Ask(ok =>
            {
                if (!_hasPending) return;
                _hasPending = false;

                // 별명을 정하지 않고 닫았으면 이번 판은 올리지 않습니다. 다음 판에 다시 물어봅니다.
                if (ok) Submit(_pending);
            });
        }

        void Submit(RunResult result)
        {
            Services.Ranking.Submit(result.gameId, PlayerIdentity.Nickname, result.score, ok =>
            {
                if (!ok) Debug.LogWarning("[Arcade] 점수를 랭킹에 올리지 못했습니다. 게임은 그대로 진행합니다.");
            });
        }
    }
}
