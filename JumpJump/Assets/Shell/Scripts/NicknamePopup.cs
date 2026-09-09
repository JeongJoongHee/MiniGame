using System;
using UnityEngine;
using UnityEngine.UI;

namespace Arcade
{
    /// <summary>
    /// **처음 랭킹에 오를 때 한 번** 별명을 받는 창입니다.
    /// 앱을 켜자마자 묻지 않는 이유는, 아직 아무것도 안 해 본 사람에게 입력을 시키면
    /// 그대로 나가 버리기 때문입니다. 점수가 났을 때 물어보는 편이 자연스럽습니다.
    ///
    /// <b>별명은 앱 전체에서 하나뿐이어야 합니다.</b> 남이 쓰고 있으면 "이미 있는 별명입니다"를
    /// 보여 주고 다시 받습니다. 겹치는지 확인하는 일은 <see cref="Services.Ranking"/> 이 합니다 —
    /// 지금은 곧바로 답하지만, 2단계에서는 서버에 물어보고 <b>몇 초 뒤에</b> 답이 옵니다.
    /// 그래서 처음부터 "기다렸다 받는" 모양으로 만들어 두었습니다.
    /// </summary>
    public class NicknamePopup : MonoBehaviour
    {
        [SerializeField] PopupPanel popup;
        [SerializeField] Text titleLabel;
        [SerializeField] Text hintLabel;
        [SerializeField] Text messageLabel;
        [SerializeField] InputField field;

        Action<bool> _done;
        bool _busy;

        /// <summary>
        /// 별명을 받습니다. 다 되면 <paramref name="done"/> 을 부릅니다
        /// (true = 별명이 정해짐, false = 사용자가 그만둠).
        /// </summary>
        public void Ask(Action<bool> done)
        {
            _done = done;
            if (popup != null) popup.Open();
        }

        void OnEnable()
        {
            Refresh();
            if (field != null) field.ActivateInputField();
        }

        /// <summary>
        /// 글자를 채우고 입력 칸을 비웁니다. 창이 켜질 때 저절로 불리고,
        /// **배치 모드 미리보기**에서도 직접 불러 화면을 찍습니다
        /// (에디터에서는 OnEnable 이 불리지 않기 때문입니다).
        /// </summary>
        public void Refresh()
        {
            _busy = false;

            if (titleLabel != null) titleLabel.text = StringTable.Get("nick.title", "CHOOSE A NICKNAME");
            if (hintLabel != null) hintLabel.text = StringTable.Format(
                "nick.hint", "UP TO {max} CHARACTERS", ("max", ArcadeConfig.Instance.nicknameMaxLength));

            SetMessage("");

            if (field != null)
            {
                field.characterLimit = Mathf.Max(1, ArcadeConfig.Instance.nicknameMaxLength);
                field.text = PlayerIdentity.Nickname;
            }
        }

        /// <summary>[확인] 버튼.</summary>
        public void OnConfirmPressed()
        {
            if (_busy) return;

            string wanted = PlayerIdentity.Sanitize(field != null ? field.text : "");
            if (wanted.Length == 0)
            {
                SetMessage(StringTable.Get("nick.empty", "PLEASE ENTER A NICKNAME"));
                return;
            }

            _busy = true;
            SetMessage(StringTable.Get("nick.checking", "CHECKING..."));

            Services.Ranking.ReserveNickname(wanted, result =>
            {
                _busy = false;

                switch (result)
                {
                    case NicknameResult.Ok:
                        PlayerIdentity.Nickname = wanted;
                        Finish(true);
                        break;

                    case NicknameResult.Taken:
                        SetMessage(StringTable.Get("nick.taken", "THAT NICKNAME IS TAKEN"));
                        break;

                    default:
                        SetMessage(StringTable.Get("nick.failed", "COULD NOT REGISTER RIGHT NOW"));
                        break;
                }
            });
        }

        /// <summary>[취소] 버튼. 별명 없이 넘어갑니다 — 이번 판은 랭킹에 오르지 않습니다.</summary>
        public void OnCancelPressed()
        {
            Finish(false);
        }

        void Finish(bool ok)
        {
            var done = _done;
            _done = null;

            if (popup != null) popup.Close();

            done?.Invoke(ok);
        }

        void SetMessage(string text)
        {
            if (messageLabel == null) return;
            messageLabel.text = text;
            messageLabel.enabled = !string.IsNullOrEmpty(text);
        }
    }
}
