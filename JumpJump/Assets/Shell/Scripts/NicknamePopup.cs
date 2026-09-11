using System;
using UnityEngine;
using UnityEngine.UI;

namespace Arcade
{
    /// <summary>
    /// **별명을 받는 창**입니다. 두 곳에서 뜹니다.
    ///  - 미니게임 : 처음 랭킹에 오를 때 한 번 (<see cref="Ask"/>)
    ///  - 로비     : 설정 창의 [별명 바꾸기] (<see cref="AskChange"/>). 횟수 제한은 없습니다.
    ///
    /// 앱을 켜자마자 묻지 않는 이유는, 아직 아무것도 안 해 본 사람에게 입력을 시키면
    /// 그대로 나가 버리기 때문입니다. 점수가 났을 때 물어보는 편이 자연스럽습니다.
    ///
    /// <b>별명은 앱 전체에서 하나뿐이어야 합니다.</b> 남이 쓰고 있으면 "이미 있는 별명입니다"를
    /// 보여 주고 다시 받습니다. 겹치는지 확인하는 일은 <see cref="Services.Ranking"/> 이 서버에 물어서 하고,
    /// 금지어는 <see cref="NicknameRules"/>(<c>badword.csv</c>)가 서버에 묻기 전에 거릅니다.
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
        bool _changing;
        string _openingMessage = "";

        /// <summary>
        /// 별명을 받습니다. 다 되면 <paramref name="done"/> 을 부릅니다
        /// (true = 별명이 정해짐, false = 사용자가 그만둠).
        /// </summary>
        /// <param name="taken">true 면 "이미 있는 별명입니다" 를 띄운 채로 엽니다 (예전 별명을 남이 가져갔을 때)</param>
        public void Ask(Action<bool> done, bool taken = false)
        {
            Open(done, changing: false,
                 taken ? StringTable.Get("nick.taken", "THAT NICKNAME IS TAKEN") : "");
        }

        /// <summary>설정 창의 [별명 바꾸기]. 지금 별명이 칸에 채워진 채로 열립니다.</summary>
        public void AskChange(Action<bool> done = null)
        {
            Open(done, changing: true, "");
        }

        void Open(Action<bool> done, bool changing, string message)
        {
            _done = done;
            _changing = changing;
            _openingMessage = message;

            if (popup != null) popup.Open();
            Refresh();   // 창이 이미 켜져 있었으면 OnEnable 이 안 불리므로 직접 채웁니다
        }

        void OnEnable()
        {
            Refresh();
            if (field != null) field.ActivateInputField();
        }

        /// <summary>
        /// 폰의 뒤로 버튼으로 닫히면 [취소] 를 누른 것과 같게 칩니다.
        /// (뒤로 버튼은 이 창을 거치지 않고 팝업을 바로 끄기 때문에, 여기서 알려 주지 않으면
        ///  기다리던 쪽이 영영 답을 못 받습니다)
        /// </summary>
        void OnDisable()
        {
            if (_done == null) return;

            var done = _done;
            _done = null;
            _openingMessage = "";
            done(false);
        }

        /// <summary>
        /// 글자를 채우고 입력 칸을 비웁니다. 창이 켜질 때 저절로 불리고,
        /// **배치 모드 미리보기**에서도 직접 불러 화면을 찍습니다
        /// (에디터에서는 OnEnable 이 불리지 않기 때문입니다).
        /// </summary>
        public void Refresh()
        {
            _busy = false;

            if (titleLabel != null)
                titleLabel.text = _changing
                    ? StringTable.Get("nick.title.change", "CHANGE YOUR NICKNAME")
                    : StringTable.Get("nick.title", "CHOOSE A NICKNAME");

            if (hintLabel != null) hintLabel.text = StringTable.Format(
                "nick.hint", "UP TO {max} CHARACTERS", ("max", ArcadeConfig.Instance.nicknameMaxLength));

            SetMessage(_openingMessage);

            if (field != null)
            {
                field.characterLimit = Mathf.Max(1, ArcadeConfig.Instance.nicknameMaxLength);
                field.text = PlayerIdentity.Nickname;
            }
        }

        /// <summary>미리보기가 "바꾸기" 모양으로 찍을 때 씁니다.</summary>
        public void SetChangingForPreview(bool changing)
        {
            _changing = changing;
            _openingMessage = "";
        }

        /// <summary>[확인] 버튼.</summary>
        public void OnConfirmPressed()
        {
            if (_busy) return;

            string wanted = PlayerIdentity.Sanitize(field != null ? field.text : "");

            switch (NicknameRules.Check(wanted, out string matched))
            {
                case NicknameProblem.Empty:
                    SetMessage(StringTable.Get("nick.empty", "PLEASE ENTER A NICKNAME"));
                    return;

                case NicknameProblem.BadWord:
                    // 어느 말에 걸렸는지는 화면에 보여 주지 않습니다 (금지어 목록을 알려 주는 셈이라서).
                    Debug.Log("[Arcade] 금지어에 걸린 별명: \"" + wanted + "\" (걸린 말: " + matched + ")");
                    SetMessage(StringTable.Get("nick.bad", "THAT NICKNAME IS NOT ALLOWED"));
                    return;

                case NicknameProblem.Invalid:
                    SetMessage(StringTable.Get("nick.invalid", "THAT NICKNAME CANNOT BE USED"));
                    return;
            }

            // 지금 별명을 그대로 두고 [확인] 을 누른 것이면 서버에 묻지 않고 닫습니다.
            if (_changing && wanted == PlayerIdentity.Nickname && PlayerIdentity.IsNicknameRegistered)
            {
                Finish(true);
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

        /// <summary>[취소] 버튼. 별명을 정하지(바꾸지) 않고 닫습니다.</summary>
        public void OnCancelPressed()
        {
            Finish(false);
        }

        void Finish(bool ok)
        {
            var done = _done;
            _done = null;
            _openingMessage = "";

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
