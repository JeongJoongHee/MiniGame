using UnityEngine;
using UnityEngine.UI;

namespace Arcade
{
    /// <summary>
    /// 씬에 구워 둔 글자를 **실행할 때 strings.csv 에서 다시 읽어** 채웁니다.
    /// 그래서 표만 고치고 씬을 다시 굽지 않아도 이 글자는 바뀝니다.
    /// (버튼 위 글자처럼 실행 중에 채워 주는 코드가 따로 없는 곳에 붙입니다)
    /// </summary>
    [RequireComponent(typeof(Text))]
    public class LocalizedText : MonoBehaviour
    {
        [Tooltip("strings.csv 의 Key")]
        [SerializeField] string key;

        [Tooltip("표에 줄이 없을 때 대신 쓰는 글자")]
        [SerializeField] string fallback;

        void OnEnable()
        {
            var text = GetComponent<Text>();
            if (text != null && !string.IsNullOrEmpty(key)) text.text = StringTable.Get(key, fallback);
        }
    }
}
