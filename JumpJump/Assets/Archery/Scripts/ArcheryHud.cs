using UnityEngine;
using UnityEngine.UI;

namespace Archery
{
    /// <summary>
    /// 기획서 그대로의 HUD: 우상단에 SCORE / HIGHEST, 좌하단에 남은 화살,
    /// 그 옆에 과녁 크기(%), 맞힌 자리에 "+5 PT".
    ///
    /// 글자는 전부 영어와 숫자입니다. 한글 폰트를 프로젝트에 넣지 않았기 때문에
    /// (CLAUDE.md 참고) 폰마다 다르게 보일 위험을 피하려는 것입니다.
    ///
    /// 문자열 재조립을 최소화하려고 값이 바뀔 때만 갱신합니다.
    /// </summary>
    public class ArcheryHud : MonoBehaviour
    {
        [SerializeField] Text scoreText;
        [SerializeField] Text bestText;
        [SerializeField] Text sizeText;
        [SerializeField] Text ammoText;
        [SerializeField] Text hitPopup;
        [SerializeField] Image[] ammoIcons = new Image[0];
        [SerializeField] GameObject overlay;
        [SerializeField] Text titleText;
        [SerializeField] Text bodyText;
        [SerializeField] Text hintText;

        [Header("남은 화살 색")]
        [SerializeField] Color ammoFull = new Color(1f, 1f, 1f, 1f);
        [SerializeField] Color ammoSpent = new Color(1f, 1f, 1f, 0.16f);

        int _lastScore = -1;
        int _lastBest = -1;
        int _lastAmmo = -1;
        int _lastAmmoMax = -1;
        int _lastSize = -1;
        GameState _lastState = (GameState)(-1);

        public void Refresh(ArcheryGame game)
        {
            if (game.Score != _lastScore)
            {
                _lastScore = game.Score;
                scoreText.text = T("archery.hud.score", "SCORE : {score} PT", ("score", _lastScore));
            }

            if (game.BestScore != _lastBest)
            {
                _lastBest = game.BestScore;
                bestText.text = T("archery.hud.best", "HIGHEST : {best} PT", ("best", _lastBest));
            }

            int size = Mathf.RoundToInt(game.SizePercent);
            if (size != _lastSize)
            {
                _lastSize = size;
                sizeText.text = T("archery.hud.target", "TARGET {size}%", ("size", size));
            }

            if (game.Ammo != _lastAmmo || game.AmmoMax != _lastAmmoMax)
            {
                _lastAmmo = game.Ammo;
                _lastAmmoMax = game.AmmoMax;
                ammoText.text = T("archery.hud.ammo", "AMMO {ammo}/{ammoMax}",
                                  ("ammo", _lastAmmo), ("ammoMax", _lastAmmoMax));
                RefreshAmmoIcons(_lastAmmo, _lastAmmoMax);
            }

            RefreshHitPopup(game);

            if (game.State != _lastState)
            {
                _lastState = game.State;
                ApplyState(game);
            }
        }

        /// <summary>화살 아이콘은 최대치만큼만 보여 주고, 쓴 화살은 흐리게 남깁니다.</summary>
        void RefreshAmmoIcons(int ammo, int ammoMax)
        {
            if (ammoIcons == null) return;

            for (int i = 0; i < ammoIcons.Length; i++)
            {
                if (ammoIcons[i] == null) continue;

                bool slotUsed = i < ammoMax;
                if (ammoIcons[i].gameObject.activeSelf != slotUsed)
                    ammoIcons[i].gameObject.SetActive(slotUsed);
                if (slotUsed) ammoIcons[i].color = i < ammo ? ammoFull : ammoSpent;
            }
        }

        /// <summary>맞힌 자리 위에 "+5 PT" 를 잠깐 띄웁니다.</summary>
        void RefreshHitPopup(ArcheryGame game)
        {
            if (hitPopup == null) return;

            bool show = game.HitPopupTimer > 0f && game.LastHitPoints > 0;
            if (hitPopup.gameObject.activeSelf != show) hitPopup.gameObject.SetActive(show);
            if (!show) return;

            hitPopup.text = T("archery.hit.popup", "+{point} PT", ("point", game.LastHitPoints));

            // 화면 폭에서 화살이 지나간 자리(월드 x)를 비율로 바꿔 그 위에 띄웁니다.
            float half = Mathf.Max(0.01f, game.Config.playHalfWidth + game.Config.targetRadius);
            float ratio = Mathf.Clamp(game.LastHitX / half, -1f, 1f);
            var rt = hitPopup.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f + ratio * 0.42f, 0.66f);   // 과녁 바로 아래

            float fade = Mathf.Clamp01(game.HitPopupTimer / Mathf.Max(0.01f, game.Config.hitPopupSeconds));
            var color = hitPopup.color;
            color.a = fade;
            hitPopup.color = color;
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

        void ApplyState(ArcheryGame game)
        {
            switch (game.State)
            {
                case GameState.Ready:
                    overlay.SetActive(true);
                    titleText.text = T("archery.ready.title", "ARCHERY");
                    bodyText.text = T("archery.ready.body",
                        "TAP TO SHOOT\nHIT THE MOVING TARGET\n\nCENTER 5 PT / EDGE 1 PT\nA HIT REFILLS YOUR ARROWS");
                    hintText.text = T("archery.ready.hint", "TAP TO START");
                    break;

                case GameState.Playing:
                    overlay.SetActive(false);
                    break;

                case GameState.GameOver:
                    overlay.SetActive(true);
                    titleText.text = T("archery.over.title", "GAME OVER");
                    bodyText.text = T("archery.over.body",
                        "{reason}\n\nSCORE     {score} PT\nHITS      {hits} / {shots}\nACCURACY  {accuracy}%\nBEST      {best} PT",
                        ("reason", game.LastResultReason),
                        ("score", game.Score), ("hits", game.Hits), ("shots", game.Shots),
                        ("accuracy", game.Accuracy.ToString("0")), ("best", game.BestScore));
                    hintText.text = T("archery.over.hint", "TAP TO RETRY");
                    break;
            }
        }
    }
}
