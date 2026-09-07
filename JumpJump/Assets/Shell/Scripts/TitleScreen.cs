using UnityEngine;
using UnityEngine.UI;

namespace Arcade
{
    /// <summary>
    /// 앱을 켜면 제일 먼저 보이는 화면입니다.
    /// 배경 그림 + 게임 이름 그림(@Title_Text.png) + START 버튼. START 를 누르면 로비로 갑니다.
    ///
    /// 글자와 버튼 위치는 전부 배경 그림 안의 비율이라, 폰마다 그림 위 같은 자리에 붙습니다.
    /// (수치는 기획서 "게임 실행 및 입장 기획서_01" 의 목업에서 그대로 잰 값입니다)
    /// </summary>
    public class TitleScreen : MonoBehaviour
    {
        [SerializeField] RectTransform backdrop;
        [SerializeField] Image startButton;
        [SerializeField] Image titleImage;

        [Header("게임 이름 그림 — 여기만 '화면' 기준입니다 (0~1)")]
        [Tooltip("게임 이름 그림의 중심. 화면 기준이라 폰이 길어도 잘리지 않습니다")]
        [SerializeField] Vector2 titleCenter = new Vector2(0.500f, 0.808f);
        [Tooltip("게임 이름 그림의 가로 폭. 1.0 = 화면 폭에 꽉 참")]
        [SerializeField] float titleWidth = 0.940f;
        [Tooltip("이름 그림을 담아 둘 칸의 세로 높이. 그림은 이 칸 안에서 원본 비율을 지키며 " +
                 "가운데 정렬되므로, 넉넉하게 잡아 두기만 하면 됩니다 (찌그러지지 않습니다)")]
        [SerializeField] float titleBoxHeight = 0.220f;

        [Header("배경 그림 안에서의 자리 (0~1, 왼쪽 아래가 0,0)")]
        [Tooltip("START 버튼 그림의 중심")]
        [SerializeField] Vector2 startCenter = new Vector2(0.497f, 0.196f);
        [Tooltip("START 버튼의 가로 폭. 세로는 원본 비율로 계산합니다")]
        [SerializeField] float startWidth = 0.321f;

        void Start()
        {
            Apply();
        }

        /// <summary>에디터 미리보기에서도 같은 배치를 쓰려고 밖으로 열어 둔 함수입니다.</summary>
        public void Apply()
        {
            if (backdrop == null) return;

            var fitter = backdrop.GetComponent<AspectFitter>();
            float frameAspect = fitter != null ? fitter.Aspect : 0.5625f;

            // 게임 이름만은 배경이 아니라 **화면** 기준으로 놓습니다.
            // 타이틀 배경은 Cover(화면을 꽉 채우고 넘치는 쪽은 자름)라서, 요즘 폰(20:9)에서는
            // 배경의 좌우가 20% 가까이 잘려 나갑니다. 이름을 배경 기준으로 크게 잡으면
            // 그때 글자의 첫 자와 끝 자가 같이 잘립니다. 화면 기준이면 그럴 일이 없습니다.
            //
            // 높이는 계산하지 않고 넉넉한 칸을 준 뒤 preserveAspect 에 맡깁니다.
            // 화면 비율을 직접 읽어 계산하면, 그 값이 아직 준비되지 않은 순간(에디터 미리보기 등)에
            // 그림이 찌그러집니다. 이렇게 두면 어떤 상황에서도 원본 비율이 유지됩니다.
            if (titleImage != null)
            {
                titleImage.preserveAspect = true;
                ShellUI.Place(titleImage.rectTransform, titleCenter, new Vector2(titleWidth, titleBoxHeight));
            }

            if (startButton != null)
            {
                float h = ShellUI.HeightForWidth(startButton.sprite, startWidth, frameAspect);
                ShellUI.Place(startButton.rectTransform, startCenter, new Vector2(startWidth, h));
            }
        }

        public void OnStartPressed()
        {
            AppFlow.GoToLobby();
        }
    }
}
