using UnityEngine;
using UnityEngine.UI;

namespace Enchant
{
    /// <summary>
    /// 기획서 그대로의 화면입니다.
    ///   - 왼쪽 위 : 동그라미 안의 강화 수치 "+13" (그 아래 최고 기록)
    ///   - 오른쪽 위 : 로비로 나가기 (껍데기가 만드는 공통 화살표 — 게임마다 같은 자리 · 같은 모양)
    ///   - 가운데 : 지금 검. 강화 수치에 따라 표(Image 열)의 그림으로 바뀝니다
    ///   - 아래 : [일반 강화] (왼쪽) / [분해] (가운데) / [안전 강화 xN] (오른쪽)
    ///
    /// 글자는 전부 strings.csv 에서 옵니다 (enchant.* 줄). 규칙은 전혀 모르고 EnchantGame 이 알려 주는 대로 그립니다.
    /// 연출(떨림 · 번쩍임 · 부서짐)은 게임의 시계로 계산하므로 배치 모드 미리보기에서도 똑같이 찍힙니다.
    /// </summary>
    public class EnchantHud : MonoBehaviour
    {
        [Header("위쪽")]
        [SerializeField] Text levelText;
        [SerializeField] Text bestText;

        [Header("검")]
        [SerializeField] RectTransform swordRoot;
        [SerializeField] Image sword;
        [SerializeField] Image glow;
        [SerializeField] Text chanceText;
        [SerializeField] Text bannerText;
        [SerializeField] Image flash;

        [Header("버튼")]
        [SerializeField] Button normalButton;
        [SerializeField] Button safeButton;
        [SerializeField] Button meltButton;
        [SerializeField] Text safeSub;
        [SerializeField] Text meltSub;

        [Header("검이 부서졌을 때")]
        [SerializeField] GameObject brokenOverlay;
        [SerializeField] Text brokenBody;
        [SerializeField] GameObject newSwordButton;

        [Header("분해 확인 창")]
        [SerializeField] Text meltQuestion;

        [Header("검 그림 (씬을 구울 때 Art/Swords 에서 채워집니다)")]
        [SerializeField] string[] swordNames = new string[0];
        [SerializeField] Sprite[] swordSprites = new Sprite[0];

        static readonly Color SuccessColor = new Color(1f, 0.86f, 0.30f);
        static readonly Color SafeFailColor = new Color(0.72f, 0.86f, 1f);
        static readonly Color MeltColor = new Color(0.86f, 0.70f, 1f);
        static readonly Color WorkingColor = new Color(1f, 0.95f, 0.85f);
        static readonly Color BrokenTint = new Color(1f, 0.30f, 0.24f);

        int _lastLevel = -1, _lastBest = -1, _lastScrolls = -1;
        EnchantState _lastState = (EnchantState)(-1);
        EnchantOutcome _lastOutcome = (EnchantOutcome)(-1);
        float _lastOutcomeAge = -1f;
        bool _based;
        Vector2 _swordBase;

        public void Refresh(EnchantGame game)
        {
            var config = game.Config;
            bool broken = game.State == EnchantState.Broken;
            int shown = broken ? game.BrokenLevel : game.Level;

            if (!_based && swordRoot != null)
            {
                _based = true;
                _swordBase = swordRoot.anchoredPosition;
            }

            bool changed = shown != _lastLevel || game.BestLevel != _lastBest || game.SafeScrolls != _lastScrolls ||
                           game.State != _lastState;

            if (changed)
            {
                _lastLevel = shown;
                _lastBest = game.BestLevel;
                _lastScrolls = game.SafeScrolls;
                _lastState = game.State;
                RefreshTexts(game, shown);
            }

            // 결과가 새로 났으면 (같은 결과가 연달아 나도 OutcomeAge 가 0 으로 돌아옵니다) 결과 글자를 바꿉니다.
            if (game.LastOutcome != _lastOutcome || game.OutcomeAge < _lastOutcomeAge)
            {
                _lastOutcome = game.LastOutcome;
                RefreshBannerText(game);
            }
            _lastOutcomeAge = game.OutcomeAge;

            Animate(game, config, shown);
        }

        void RefreshTexts(EnchantGame game, int shown)
        {
            var config = game.Config;
            bool idle = game.State == EnchantState.Idle;
            bool canEnchant = config.CanEnchant(shown);

            levelText.text = T("enchant.hud.level", "+{level}", ("level", shown));
            bestText.text = T("enchant.hud.best", "BEST +{best}", ("best", game.BestLevel));
            sword.sprite = SwordFor(config.ImageFor(shown));

            if (game.State == EnchantState.Broken) chanceText.text = "";
            else if (canEnchant)
                chanceText.text = T("enchant.hud.chance", "+{next}  SUCCESS {prob}%",
                                    ("next", shown + 1), ("prob", (config.SuccessChance(shown) / 100f).ToString("0.##")));
            else chanceText.text = T("enchant.hud.max", "MAX LEVEL");

            normalButton.interactable = idle && canEnchant;
            safeButton.interactable = idle && canEnchant && game.SafeScrolls > 0;
            safeSub.text = T("enchant.btn.safe.sub", "x{count}", ("count", game.SafeScrolls));

            int reward = config.MeltScrolls(shown);
            meltButton.interactable = idle && reward > 0;
            int from = config.FirstMeltLevel;
            meltSub.text = reward > 0
                ? T("enchant.btn.melt.on", "SCROLL +{count}", ("count", reward))
                : (from >= 0 ? T("enchant.btn.melt.off", "FROM +{level}", ("level", from)) : "");

            if (brokenBody != null)
                brokenBody.text = T("enchant.broken.body", "+{level} SWORD WAS DESTROYED\n\nBEST  +{best}",
                                    ("level", game.BrokenLevel), ("best", game.BestLevel));
        }

        void RefreshBannerText(EnchantGame game)
        {
            switch (game.LastOutcome)
            {
                case EnchantOutcome.Success:
                    bannerText.text = T("enchant.result.success", "SUCCESS!");
                    bannerText.color = SuccessColor;
                    break;
                case EnchantOutcome.SafeFail:
                    bannerText.text = T("enchant.result.safefail", "FAILED - SWORD IS SAFE");
                    bannerText.color = SafeFailColor;
                    break;
                case EnchantOutcome.Melted:
                    bannerText.text = T("enchant.result.melt", "SCROLL +{count}", ("count", game.LastScrollsGained));
                    bannerText.color = MeltColor;
                    break;
                case EnchantOutcome.MeltEmpty:
                    bannerText.text = T("enchant.result.meltempty", "NO SCROLL THIS TIME");
                    bannerText.color = MeltColor;
                    break;
                default:
                    bannerText.text = "";
                    break;
            }
        }

        /// <summary>"분해하시겠습니까?" 창의 설명 글자를 지금 검에 맞춰 채웁니다.</summary>
        public void ShowMeltQuestion(EnchantGame game)
        {
            if (meltQuestion == null) return;
            meltQuestion.text = T("enchant.melt.ask", "MELT THE +{level} SWORD?\nYOU GET {count} SAFE SCROLL(S)",
                                  ("level", game.Level), ("count", game.Config.MeltScrolls(game.Level)));
        }

        // ------------------------------------------------------------ 연출

        void Animate(EnchantGame game, EnchantConfig config, int shown)
        {
            float age = game.OutcomeAge;
            Vector2 offset = Vector2.zero;
            float scale = 1f;
            float angle = 0f;
            Color swordColor = Color.white;
            float flashAlpha = 0f;
            Color flashColor = Color.white;

            // 검 뒤의 빛 : 강화 수치가 오를수록 색이 바뀌고 커집니다. 천천히 숨을 쉽니다.
            Color glowColor = config.GlowColor(shown);
            float glowScale = 1f + Mathf.Min(0.35f, shown * 0.008f) + Mathf.Sin(game.Clock * 2.2f) * 0.03f;

            switch (game.State)
            {
                case EnchantState.Working:
                {
                    // 결과를 기다리는 동안 검이 점점 세게 떨리고 빛이 부풀어 오릅니다.
                    float p = game.WorkProgress;
                    float amp = config.workShake * (0.25f + 0.75f * p);
                    offset = new Vector2(Mathf.Sin(game.Clock * 71f) * amp, Mathf.Sin(game.Clock * 53f) * amp * 0.35f);
                    glowScale += 0.35f * p;
                    glowColor.a = Mathf.Min(1f, glowColor.a + 0.3f * p);

                    bannerText.text = T("enchant.working", "ENCHANTING...");
                    bannerText.color = WorkingColor;
                    SetBanner(0.55f + 0.45f * Mathf.Abs(Mathf.Sin(game.Clock * 6f)), 1f);
                    _lastOutcome = (EnchantOutcome)(-1);   // 끝나면 결과 글자로 다시 채우게
                    break;
                }

                case EnchantState.Broken:
                {
                    // 부서짐 : 붉게 번쩍이며 기울어 떨어지고 사라집니다. 그 뒤 결과 창.
                    float t = game.StateClock;
                    float reveal = Mathf.Max(0.05f, config.breakRevealSeconds);
                    float k = Mathf.Clamp01(t / reveal);
                    offset = new Vector2(Mathf.Sin(t * 90f) * 20f * (1f - k), -900f * k * k);
                    angle = -28f * k;
                    swordColor = Color.Lerp(Color.white, BrokenTint, Mathf.Clamp01(t * 6f));
                    swordColor.a = 1f - k;
                    glowColor = BrokenTint;
                    glowColor.a = 0.8f * (1f - k);
                    flashColor = BrokenTint;
                    flashAlpha = 0.55f * Mathf.Clamp01(1f - t / 0.35f);
                    SetBanner(0f, 1f);

                    SetActive(brokenOverlay, t >= reveal);
                    SetActive(newSwordButton, game.CanTakeNewSword);
                    break;
                }

                default:
                {
                    SetActive(brokenOverlay, false);
                    float banner = Mathf.Max(0.05f, config.bannerSeconds);

                    switch (game.LastOutcome)
                    {
                        case EnchantOutcome.Success:
                            // 새 그림으로 바뀐 검이 톡 튀어 오르고 화면이 한 번 번쩍입니다.
                            scale = 1f + 0.28f * Mathf.Clamp01(1f - age / 0.28f);
                            flashAlpha = 0.5f * Mathf.Clamp01(1f - age / 0.22f);
                            glowScale += 0.4f * Mathf.Clamp01(1f - age / 0.5f);
                            break;

                        case EnchantOutcome.SafeFail:
                            // 움찔했다가 제자리. 부서지지 않았다는 느낌.
                            float shake = 26f * Mathf.Clamp01(1f - age / 0.4f);
                            offset = new Vector2(Mathf.Sin(age * 60f) * shake, 0f);
                            swordColor = Color.Lerp(Color.white, new Color(0.7f, 0.8f, 1f), Mathf.Clamp01(1f - age / 0.5f));
                            break;

                        case EnchantOutcome.Melted:
                        case EnchantOutcome.MeltEmpty:
                            // 녹아 없어진 자리에 +0 새 검이 자라납니다.
                            scale = Mathf.Lerp(0.2f, 1f, Mathf.Clamp01(age / 0.35f));
                            flashColor = MeltColor;
                            flashAlpha = 0.4f * Mathf.Clamp01(1f - age / 0.3f);
                            break;
                    }

                    // 결과 글자 : 톡 커졌다가 제자리, 끝날 즈음 사라집니다.
                    bool showing = game.LastOutcome != EnchantOutcome.None && age < banner;
                    float alpha = showing ? Mathf.Clamp01((banner - age) / 0.3f) : 0f;
                    SetBanner(alpha, 1f + 0.12f * Mathf.Clamp01(1f - age / 0.18f));
                    break;
                }
            }

            if (swordRoot != null)
            {
                swordRoot.anchoredPosition = _swordBase + offset;
                swordRoot.localScale = new Vector3(scale, scale, 1f);
                swordRoot.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
            sword.color = swordColor;

            if (glow != null)
            {
                glow.color = glowColor;
                glow.rectTransform.localScale = new Vector3(glowScale, glowScale, 1f);
            }

            if (flash != null)
            {
                flashColor.a = flashAlpha;
                flash.color = flashColor;
                if (flash.enabled != flashAlpha > 0.001f) flash.enabled = flashAlpha > 0.001f;
            }
        }

        void SetBanner(float alpha, float scale)
        {
            var c = bannerText.color;
            c.a = alpha;
            bannerText.color = c;
            bannerText.rectTransform.localScale = new Vector3(scale, scale, 1f);
            if (bannerText.enabled != alpha > 0.001f) bannerText.enabled = alpha > 0.001f;
        }

        static void SetActive(GameObject go, bool on)
        {
            if (go != null && go.activeSelf != on) go.SetActive(on);
        }

        /// <summary>표의 그림 이름으로 검 그림을 찾습니다. 그 이름의 그림이 없으면 첫 그림을 씁니다 (빌드 로그가 경고합니다).</summary>
        Sprite SwordFor(string name)
        {
            if (swordSprites == null || swordSprites.Length == 0) return null;

            if (!string.IsNullOrEmpty(name))
                for (int i = 0; i < swordNames.Length && i < swordSprites.Length; i++)
                    if (string.Equals(swordNames[i], name, System.StringComparison.OrdinalIgnoreCase))
                        return swordSprites[i];

            return swordSprites[0];
        }

        /// <summary>
        /// 화면에 나올 글자를 표(strings.csv)에서 꺼냅니다. 두 번째 인자는 표에 줄이 없을 때의 기본 문구입니다.
        /// **문구를 바꾸려면 코드가 아니라 D:\00.JumpJump\strings.csv 를 고치세요.**
        /// </summary>
        static string T(string key, string fallback, params (string name, object value)[] values)
        {
            return Arcade.StringTable.Format(key, fallback, values);
        }
    }
}
