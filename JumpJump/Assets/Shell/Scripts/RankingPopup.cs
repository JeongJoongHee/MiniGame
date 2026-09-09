using UnityEngine;
using UnityEngine.UI;

namespace Arcade
{
    /// <summary>
    /// 로비 왼쪽 위 아이콘으로 여는 **랭킹 창**입니다.
    /// 게임마다 표가 다르므로 위쪽에 게임 이름표를 탭으로 두고 눌러서 갈아탑니다.
    ///
    /// 줄과 글자는 씬을 구울 때 미리 만들어져 있고, 여기서는 **글자만 채웁니다.**
    /// 그래서 창을 열 때 새로 만드는 것이 없어 끊기지 않습니다.
    ///
    /// 어디서 점수를 가져오는지는 <see cref="Services.Ranking"/> 이 정합니다 —
    /// 지금은 폰 안에 저장된 내 기록이고, 2단계에서 Firebase 로 바뀝니다.
    /// **그때 이 파일은 고치지 않습니다.**
    /// </summary>
    public class RankingPopup : MonoBehaviour
    {
        [SerializeField] Text titleLabel;
        [SerializeField] Text statusLabel;
        [SerializeField] Text myLine;

        [Tooltip("줄마다 순위 / 별명 / 점수 글자 세 개. 셋의 길이는 같습니다")]
        [SerializeField] Text[] rankLabels;
        [SerializeField] Text[] nickLabels;
        [SerializeField] Text[] scoreLabels;

        [Tooltip("게임 이름표 탭. gameIds 와 순서가 같습니다")]
        [SerializeField] Image[] tabs;
        [SerializeField] string[] gameIds;

        static readonly Color TabOn = Color.white;
        static readonly Color TabOff = new Color(0.62f, 0.60f, 0.58f);

        int _tab;

        /// <summary>창이 켜질 때마다 새로 불러옵니다. (점수는 바뀌어 있을 수 있으니까요)</summary>
        void OnEnable()
        {
            if (titleLabel != null)
                titleLabel.text = StringTable.Get("rank.title", "RANKING");

            Refresh();
        }

        /// <summary>탭(게임 이름표)을 눌렀을 때. 탭마다 붙은 <see cref="RankingTab"/> 이 부릅니다.</summary>
        public void SelectTab(int index)
        {
            if (gameIds == null || index < 0 || index >= gameIds.Length) return;

            _tab = index;
            Refresh();
        }

        /// <summary>
        /// 지금 탭의 랭킹을 다시 불러옵니다. 창이 켜질 때 저절로 불리고,
        /// **배치 모드 미리보기**에서도 직접 불러 화면을 찍습니다
        /// (에디터에서는 OnEnable 이 불리지 않기 때문입니다).
        /// </summary>
        public void Refresh()
        {
            ShowTabs();
            Clear();

            if (gameIds == null || gameIds.Length == 0)
            {
                SetStatus(StringTable.Get("rank.empty", "NO RECORDS YET"));
                return;
            }

            SetStatus(StringTable.Get("rank.loading", "LOADING..."));

            // 진짜 서버에서는 답이 몇 초 뒤에 옵니다. 그래서 결과를 받아서 채우는 모양으로 둡니다.
            int want = Mathf.Max(1, ArcadeConfig.Instance.rankingTopCount);
            Services.Ranking.Fetch(gameIds[_tab], want, Fill);
        }

        void Fill(RankingPage page)
        {
            // 답이 늦게 왔는데 그 사이 창이 닫혔거나 다른 탭으로 넘어갔을 수 있습니다.
            if (!isActiveAndEnabled) return;

            if (page == null || !page.ok)
            {
                SetStatus(StringTable.Get("rank.failed", "COULD NOT LOAD RANKING"));
                SetMyLine("");
                return;
            }

            if (page.IsEmpty)
            {
                SetStatus(StringTable.Get("rank.empty", "NO RECORDS YET"));
                SetMyLine("");
                return;
            }

            SetStatus("");

            int shown = rankLabels != null ? rankLabels.Length : 0;
            for (int i = 0; i < shown; i++)
            {
                if (i >= page.rows.Count) break;

                var row = page.rows[i];
                Color color = row.isMe ? new Color(0.78f, 0.28f, 0.10f) : new Color(0.23f, 0.11f, 0.08f);

                Set(rankLabels, i, StringTable.Format("rank.place", "{rank}", ("rank", row.rank)), color);
                Set(nickLabels, i, row.nickname, color);
                Set(scoreLabels, i, row.score.ToString("N0"), color);
            }

            SetMyLine(page.myRank > 0
                ? StringTable.Format("rank.me", "MY RANK : {rank}   {score}",
                                     ("rank", page.myRank), ("score", page.myScore.ToString("N0")))
                : StringTable.Get("rank.me.none", "NO RECORD YET"));
        }

        void ShowTabs()
        {
            if (tabs == null) return;

            for (int i = 0; i < tabs.Length; i++)
                if (tabs[i] != null)
                    tabs[i].color = i == _tab ? TabOn : TabOff;
        }

        void Clear()
        {
            int shown = rankLabels != null ? rankLabels.Length : 0;
            for (int i = 0; i < shown; i++)
            {
                Set(rankLabels, i, "", Color.clear);
                Set(nickLabels, i, "", Color.clear);
                Set(scoreLabels, i, "", Color.clear);
            }
        }

        void Set(Text[] labels, int i, string text, Color color)
        {
            if (labels == null || i >= labels.Length || labels[i] == null) return;
            labels[i].text = text;
            labels[i].color = color;
        }

        void SetStatus(string text)
        {
            if (statusLabel == null) return;
            statusLabel.text = text;
            statusLabel.enabled = !string.IsNullOrEmpty(text);
        }

        void SetMyLine(string text)
        {
            if (myLine == null) return;
            myLine.text = text;
            myLine.enabled = !string.IsNullOrEmpty(text);
        }
    }
}
