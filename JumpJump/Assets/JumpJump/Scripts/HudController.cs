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
                scoreText.text = T("jump.hud.score", "Score : {score}",
                                   ("score", _lastScore.ToString("00000000")));
            }

            int meters = Mathf.Max(0, Mathf.FloorToInt(game.HeightMeters));
            if (meters != _lastMeters)
            {
                _lastMeters = meters;
                heightText.text = T("jump.hud.height", "Height : {height}m",
                                    ("height", meters.ToString("000000")));
            }

            if (game.Combo != _lastCombo)
            {
                _lastCombo = game.Combo;
                comboText.text = _lastCombo >= 2
                    ? T("jump.hud.combo", "COMBO {combo}   x{multiplier}",
                        ("combo", _lastCombo),
                        ("multiplier", game.Config.ScoreMultiplier(_lastCombo).ToString("0.0")))
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

        /// <summary>
        /// 화면에 나올 글자를 표(strings.csv)에서 꺼냅니다.
        /// 두 번째 인자는 표가 없거나 그 줄이 비었을 때 쓸 기본 문구입니다.
        /// **문구를 바꾸려면 코드가 아니라 D:\00.JumpJump\strings.csv 를 고치세요.**
        /// </summary>
        static string T(string key, string fallback, params (string name, object value)[] values)
        {
            return Arcade.StringTable.Format(key, fallback, values);
        }

        void ApplyState(GameManager game)
        {
            switch (game.State)
            {
                case GameState.Ready:
                    overlay.SetActive(true);
                    titleText.text = T("jump.ready.title", "JUMP JUMP");
                    bodyText.text = T("jump.ready.body", "TAP TO JUMP UP\nRIDE THE MOVING PLATFORM");
                    hintText.text = T("jump.ready.hint", "TAP TO START");
                    break;

                case GameState.Playing:
                    overlay.SetActive(false);
                    break;

                case GameState.GameOver:
                    overlay.SetActive(true);
                    titleText.text = T("jump.over.title", "GAME OVER");
                    bodyText.text = T("jump.over.body",
                        "{reason}\n\nSCORE   {score}\nHEIGHT  {height}m\nBEST    {best}",
                        ("reason", game.LastResultReason),
                        ("score", game.Score.ToString("00000000")),
                        ("height", Mathf.FloorToInt(game.HeightMeters).ToString("000000")),
                        ("best", game.BestScore.ToString("00000000")));
                    hintText.text = T("jump.over.hint", "TAP TO RETRY");
                    break;
            }
        }
    }
}
