using System;
using Arcade.Online;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

namespace Arcade
{
    /// <summary>
    /// **AdMob 전면 광고**입니다 (3단계). 앱이 켜질 때 <see cref="OnlineBoot"/> 가 끼워 넣습니다.
    ///
    /// 켜지면 이 순서로 갑니다.
    ///   1) 동의 확인(UMP) — 유럽 등 동의가 필요한 곳의 사용자에게만 동의 창이 뜹니다. 한국 사용자는 그냥 지나갑니다.
    ///   2) 광고 SDK 시작
    ///   3) 전면 광고 한 장을 **미리 불러 둡니다** — 띄울 때 불러오면 몇 초 멈춰 보이기 때문입니다.
    ///
    /// <b>언제 띄울지는 여기서 정하지 않습니다</b> — <see cref="AdGate"/>(2판마다 · 90초 간격)와
    /// <see cref="AdBreak"/>(다시하기 · 로비로 나가기 순간)가 정하고, 여기는 "띄워 줘" 에 답할 뿐입니다.
    ///
    /// <b>광고가 준비되지 않았으면 그냥 건너뜁니다.</b> 인터넷이 없거나 광고가 없어도 게임은 막히지 않습니다.
    /// </summary>
    public class AdMobService : IAdService
    {
        /// <summary>
        /// 구글이 공개한 **안드로이드 테스트 전면 광고** 단위. 누구나 쓸 수 있고, 눌러도 계정에 문제가 없습니다.
        /// <c>ArcadeConfig.useTestAds</c> 가 켜져 있으면 진짜 광고 단위 대신 이것을 씁니다.
        /// </summary>
        public const string TestInterstitialUnitId = "ca-app-pub-3940256099942544/1033173712";

        /// <summary>광고를 못 불러왔을 때 다시 해 보기까지 기다리는 시간(초). 실패할수록 늘립니다.</summary>
        static readonly float[] RetryDelays = { 10f, 30f, 60f, 120f, 300f };

        readonly string _unitId;
        readonly ArcadeConfig _config;

        InterstitialAd _ad;
        bool _started;
        bool _initialized;
        bool _loading;
        int _failures;

        public AdMobService(ArcadeConfig config)
        {
            _config = config;
            _unitId = config.useTestAds || string.IsNullOrEmpty(config.interstitialUnitId)
                ? TestInterstitialUnitId
                : config.interstitialUnitId.Trim();
        }

        /// <summary>테스트 광고를 쓰고 있는지 (로그·점검용).</summary>
        public bool UsingTestAds => _unitId == TestInterstitialUnitId;

        public bool IsReady => _ad != null && _ad.CanShowAd();

        /// <summary>앱이 켜질 때 한 번. 동의 확인 → SDK 시작 → 광고 미리 불러오기.</summary>
        public void Start()
        {
            if (_started) return;
            _started = true;

            Debug.Log("[Arcade] 광고 준비를 시작합니다 — " + (UsingTestAds ? "테스트 광고" : "진짜 광고") + " (" + _unitId + ")");

            // 광고 SDK 의 소식은 본 흐름에서 받습니다 (MainThread 로도 한 번 더 옮깁니다).
            MobileAds.RaiseAdEventsOnUnityMainThread = true;
            MobileAds.SetRequestConfiguration(new RequestConfiguration
            {
                TagForChildDirectedTreatment = _config.childDirected
                    ? TagForChildDirectedTreatment.True
                    : TagForChildDirectedTreatment.Unspecified,
                MaxAdContentRating = Rating(_config.maxAdContentRating),
            });

            // 지난번에 이미 동의를 받았으면 기다리지 않고 바로 시작합니다.
            if (ConsentInformation.CanRequestAds()) InitializeSdk();

            var request = new ConsentRequestParameters { TagForUnderAgeOfConsent = _config.childDirected };
            ConsentInformation.Update(request, updateError => MainThread.Post(() =>
            {
                if (updateError != null)
                    Debug.LogWarning("[Arcade] 광고 동의 정보를 확인하지 못했습니다: " + updateError.Message);

                ConsentForm.LoadAndShowConsentFormIfRequired(formError => MainThread.Post(() =>
                {
                    if (formError != null)
                        Debug.LogWarning("[Arcade] 광고 동의 창을 띄우지 못했습니다: " + formError.Message);

                    if (ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required)
                        Debug.LogWarning("[Arcade] 이 사용자에게는 '개인정보 설정' 버튼이 필요합니다 (유럽 등). 설정 창에 붙여야 합니다.");

                    if (ConsentInformation.CanRequestAds()) InitializeSdk();
                }));
            }));
        }

        void InitializeSdk()
        {
            if (_initialized) return;
            _initialized = true;

            MobileAds.Initialize(_ => MainThread.Post(Load));
        }

        public void Load()
        {
            if (!_initialized || _loading || IsReady) return;
            _loading = true;

            InterstitialAd.Load(_unitId, new AdRequest(), (ad, error) => MainThread.Post(() =>
            {
                _loading = false;

                if (error != null || ad == null)
                {
                    float delay = RetryDelays[Mathf.Min(_failures, RetryDelays.Length - 1)];
                    _failures++;
                    Debug.LogWarning("[Arcade] 전면 광고를 불러오지 못했습니다 (" + (error != null ? error.GetMessage() : "빈 응답") +
                                     "). " + delay + "초 뒤에 다시 해 봅니다.");
                    MainThread.PostDelayed(delay, Load);
                    return;
                }

                _failures = 0;
                _ad = ad;
            }));
        }

        public void ShowInterstitial(Action onClosed)
        {
            if (!IsReady)
            {
                // 준비가 안 됐으면 건너뜁니다. 광고 때문에 게임이 막히면 안 됩니다.
                onClosed?.Invoke();
                Load();
                return;
            }

            var ad = _ad;
            _ad = null;

            bool finished = false;
            void Finish()
            {
                if (finished) return;
                finished = true;

                ad.Destroy();
                onClosed?.Invoke();
                Load();   // 다음 광고를 미리 불러 둡니다
            }

            ad.OnAdFullScreenContentClosed += () => MainThread.Post(Finish);
            ad.OnAdFullScreenContentFailed += error => MainThread.Post(() =>
            {
                Debug.LogWarning("[Arcade] 전면 광고를 띄우지 못했습니다: " + error.GetMessage());
                Finish();
            });

            ad.Show();
        }

        static MaxAdContentRating Rating(string value)
        {
            switch ((value ?? "").Trim().ToUpperInvariant())
            {
                case "G": return MaxAdContentRating.G;
                case "T": return MaxAdContentRating.T;
                case "MA": return MaxAdContentRating.MA;
                default: return MaxAdContentRating.PG;
            }
        }
    }
}
