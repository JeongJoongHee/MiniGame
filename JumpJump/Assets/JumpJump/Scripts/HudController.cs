using UnityEngine;
using UnityEngine.UI;

namespace JumpJump
{
    /// <summary>
    /// 스케치 그대로의 HUD: 좌상단 Score, 우상단 Height, 그 아래 우->좌로 줄어드는 타이머 바.
    /// 문자열 재조립을 최소화하려고 값이 바뀔 때만 갱신합니다.
    /// </summary>
    public class HudController : MonoBehaviour
    {
        [SerializeField] Text scoreText;
        [SerializeField] Text heightText;
        [SerializeField] Text comboText;
        [SerializeField] Image timerFill;
        [SerializeField] GameObject overlay;
        [SerializeField] Text titleText;
        [SerializeField] Text bodyText;
        [SerializeField] Text hintText;

        [Header("타이머 바 색상")]
        [SerializeField] Color timerSafe = new Color(0.36f, 0.86f, 0.50f);
        [SerializeField] Color timerWarn = new Color(0.98f, 0.79f, 0.28f);
        [SerializeField] Color timerDanger = new Color(0.94f, 0.34f, 0.36f);

        int _lastScore = -1;
        int _lastMeters = -1;
        int _lastCombo = -1;
        GameState _lastState = (GameState)(-1);

        public void Refresh(GameManager game)
        {
            if (game.Score != _lastScore)
            {
                _lastScore = game.Score;
                scoreText.text = "Score : " + _lastScore.ToString("00000000");
            }

            int meters = Mathf.Max(0, Mathf.FloorToInt(game.HeightMeters));
            if (meters != _lastMeters)
            {
                _lastMeters = meters;
                heightText.text = "Height : " + meters.ToString("000000") + "m";
            }

            if (game.Combo != _lastCombo)
            {
                _lastCombo = game.Combo;
                comboText.text = _lastCombo >= 2
                    ? "COMBO " + _lastCombo + "   x" + game.Config.ScoreMultiplier(_lastCombo).ToString("0.0")
                    : "";
            }

            float t = Mathf.Clamp01(game.TimeLeft / Mathf.Max(0.01f, game.TimeMax));
            timerFill.fillAmount = t;
            timerFill.color = t > 0.5f
                ? Color.Lerp(timerWarn, timerSafe, (t - 0.5f) * 2f)
                : Color.Lerp(timerDanger, timerWarn, t * 2f);

            if (game.State != _lastState)
            {
                _lastState = game.State;
                ApplyState(game);
            }
        }

        void ApplyState(GameManager game)
        {
            switch (game.State)
            {
                case GameState.Ready:
                    overlay.SetActive(true);
                    titleText.text = "JUMP JUMP";
                    bodyText.text = "TAP TO JUMP UP\nRIDE THE MOVING PLATFORM";
                    hintText.text = "TAP TO START";
                    break;

                case GameState.Playing:
                    overlay.SetActive(false);
                    break;

                case GameState.GameOver:
                    overlay.SetActive(true);
                    titleText.text = "GAME OVER";
                    bodyText.text = game.LastResultReason
                                    + "\n\nSCORE   " + game.Score.ToString("00000000")
                                    + "\nHEIGHT  " + Mathf.FloorToInt(game.HeightMeters).ToString("000000") + "m"
                                    + "\nBEST    " + game.BestScore.ToString("00000000");
                    hintText.text = "TAP TO RETRY";
                    break;
            }
        }

        /// <summary>화면 오른쪽 위의 "< LOBBY" 버튼이 부릅니다. 게임 중에도 항상 눌러 나갈 수 있습니다.</summary>
        public void OnLobbyPressed()
        {
            Arcade.AppFlow.GoToLobby();
        }
    }
}
