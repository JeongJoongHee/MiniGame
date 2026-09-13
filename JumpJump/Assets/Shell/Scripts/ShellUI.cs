using UnityEngine;
using UnityEngine.UI;

namespace Arcade
{
    /// <summary>
    /// 타이틀 / 로비가 공통으로 쓰는 UI 배치 도구입니다.
    ///
    /// 이 앱의 UI 는 전부 **배경 그림 안의 비율 좌표**로 놓입니다.
    /// (0,0) 이 배경 왼쪽 아래, (1,1) 이 오른쪽 위입니다. 픽셀 좌표를 쓰지 않기 때문에
    /// 폰 해상도가 달라도 그림 위 같은 자리에 붙습니다.
    /// </summary>
    public static class ShellUI
    {
        /// <summary>
        /// 부모 사각형 안에서 비율로 자리를 잡습니다.
        /// center / size 는 부모 크기를 1 로 본 값입니다. (center (0.5,0.5) = 한가운데)
        /// </summary>
        public static void Place(RectTransform rt, Vector2 center, Vector2 size)
        {
            Vector2 half = size * 0.5f;
            rt.anchorMin = center - half;
            rt.anchorMax = center + half;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// 가로 폭만 정하고 세로는 그림의 원본 비율대로 계산합니다. 그림이 찌그러지지 않습니다.
        /// </summary>
        /// <param name="frameAspect">부모 사각형의 화면상 가로/세로 비율</param>
        public static float HeightForWidth(Sprite sprite, float widthFraction, float frameAspect)
        {
            if (sprite == null) return widthFraction;
            float spriteAspect = sprite.rect.width / Mathf.Max(1f, sprite.rect.height);
            return widthFraction * frameAspect / spriteAspect;
        }

        public static Image AddImage(RectTransform parent, string name, Sprite sprite, bool raycast = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = raycast;
            image.preserveAspect = false;   // 크기를 우리가 직접 비율대로 잡아 줍니다
            return image;
        }

        /// <summary>
        /// 앱 전체가 쓰는 글꼴입니다. **바꾸고 싶으면 코드가 아니라 파일을 바꾸세요.**
        ///
        /// 작업 폴더에 `@Font.ttf`(또는 `.otf`)를 놓고 Build All Scenes 를 돌리면
        /// `Assets/Shell/Resources/GameFont.*` 로 들어오고, 아래에서 그것을 찾아 씁니다.
        /// 파일이 없으면 Unity 기본 글꼴로 돌아갑니다 — 그래서 폰트를 안 넣어도 앱은 돌아갑니다.
        ///
        /// 점프점프 / 활쏘기 HUD 도 이 값을 씁니다. 글꼴을 정하는 곳은 여기 한 군데뿐입니다.
        /// </summary>
        public static Font GameFont
        {
            get
            {
                if (_gameFont != null) return _gameFont;

                _gameFont = Resources.Load<Font>("GameFont");
                if (_gameFont == null)
                    _gameFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

                return _gameFont;
            }
        }

        static Font _gameFont;

        public static Text AddText(RectTransform parent, string name, int fontSize, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            text.font = GameFont;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
            // 글자를 사각형 안에 맞춰 줄이려면(Best Fit) 가로/세로 모두 넘침을 허용하면 안 됩니다.
            // Overflow 로 두면 폭이 무한대로 계산돼서 글자가 줄어들지 않습니다.
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 8;
            text.resizeTextMaxSize = fontSize;
            return text;
        }

        /// <summary>글자가 배경에 묻히지 않도록 검은 외곽선과 그림자를 입힙니다.</summary>
        public static void AddOutline(Graphic graphic, float thickness, Color color)
        {
            var outline = graphic.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(thickness, thickness);
            outline.useGraphicAlpha = true;
        }

        /// <summary>비어 있지만 터치는 받는 사각형. 카드 전체를 버튼으로 만들 때 씁니다.</summary>
        public static Button AddHitArea(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);   // 보이지 않지만 터치는 받습니다
            image.raycastTarget = true;

            var button = go.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            if (Application.isPlaying) UiClickSound.Attach(button);   // 실행 중에 만든 버튼도 딸깍 소리
            return button;
        }
    }
}
