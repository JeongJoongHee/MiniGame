using UnityEngine;

namespace Arcade
{
    public enum AspectFitMode
    {
        /// <summary>화면을 꽉 채웁니다. 남는 쪽은 잘려 나갑니다. (풍경 배경용)</summary>
        Cover,
        /// <summary>그림 전체가 보이도록 넣습니다. 남는 쪽에 여백이 생깁니다. (UI 가 올라가는 배경용)</summary>
        Contain,
    }

    /// <summary>
    /// 배경 그림을 원본 비율 그대로 화면에 맞춥니다.
    ///
    /// 이게 왜 필요하냐면 — 로비의 게임 칸(아이콘 / 이름표 / PLAY)은 전부 이 사각형의
    /// **비율 좌표(앵커)** 로 붙어 있습니다. 그래서 폰이 길든 짧든 아이콘이 배경에
    /// 그려진 동그란 자리 위에 정확히 얹힙니다. 배경만 제대로 맞추면 나머지는 따라옵니다.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class AspectFitter : MonoBehaviour
    {
        [SerializeField] AspectFitMode mode = AspectFitMode.Contain;

        [Tooltip("원본 그림의 가로/세로 비율 (예: 1536x2730 이면 0.5626)")]
        [SerializeField] float aspect = 0.5625f;

        RectTransform _rt;
        Vector2 _lastParentSize = new Vector2(-1f, -1f);

        public float Aspect
        {
            get => aspect;
            set { aspect = value; Apply(); }
        }

        void OnEnable() { Apply(); }
        void OnRectTransformDimensionsChange() { Apply(); }

        void Update()
        {
            // 화면 회전 / 창 크기 변경에 대비합니다. 크기가 그대로면 아무 일도 하지 않습니다.
            var parent = Parent();
            if (parent == null) return;
            if (parent.rect.size == _lastParentSize) return;
            Apply();
        }

        RectTransform Parent()
        {
            if (_rt == null) _rt = GetComponent<RectTransform>();
            return _rt != null ? _rt.parent as RectTransform : null;
        }

        public void Apply()
        {
            var parent = Parent();
            if (parent == null || aspect <= 0f) return;

            Vector2 size = parent.rect.size;
            if (size.x <= 0f || size.y <= 0f) return;
            _lastParentSize = size;

            float parentAspect = size.x / size.y;

            // Cover 는 부족한 쪽을 넘치게, Contain 은 넘치는 쪽을 줄여서 맞춥니다.
            bool matchWidth = mode == AspectFitMode.Cover
                ? parentAspect > aspect
                : parentAspect < aspect;

            Vector2 result = matchWidth
                ? new Vector2(size.x, size.x / aspect)
                : new Vector2(size.y * aspect, size.y);

            _rt.anchorMin = _rt.anchorMax = _rt.pivot = new Vector2(0.5f, 0.5f);
            _rt.anchoredPosition = Vector2.zero;
            _rt.sizeDelta = result;
        }
    }
}
