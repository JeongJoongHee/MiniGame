using UnityEngine;
using UnityEngine.UI;

namespace Arcade
{
    /// <summary>
    /// **랭킹 창**입니다. 로비의 게임 칸마다 붙은 랭킹 아이콘으로 열고(<see cref="ShowFor"/>),
    /// 연 게임의 탭이 먼저 골라진 채로 뜹니다. (수정사항_02 — 예전에는 로비 왼쪽 위 아이콘 하나였습니다)
    /// 위쪽의 게임 이름표 탭을 눌러 다른 게임으로 갈아탈 수 있습니다.
    ///
    /// 줄과 글자는 씬을 구울 때 미리 만들어져 있고, 여기서는 **글자만 채웁니다.**
    /// 그래서 창을 열 때 새로 만드는 것이 없어 끊기지 않습니다.
    ///
    /// 어디서 점수를 가져오는지는 <see cref="Services.Ranking"/> 이 정합니다 (서버 또는 폰 안).
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

        /// <summary>
        /// 몇 번째로 보낸 요청인지. 서버 답은 늦게 올 수 있어서, 그 사이 다른 탭을 눌렀다면
        /// 먼저 보낸 요청의 답이 나중 탭 위에 덮어써지는 일이 생깁니다. 마지막 요청의 답만 씁니다.
        /// </summary>
        int _request;

        /// <summary>창이 켜질 때마다 새로 불러옵니다. (점수는 바뀌어 있을 수 있으니까요)</summary>
        void OnEnable()
        {
            if (titleLabel != null)
                titleLabel.text = StringTable.Get("rank.title", "RANKING");

            Refresh();
        }

        /// <summary>
        /// 이 게임의 탭을 골라 둔 채로 창을 엽니다. 로비 게임 칸의 랭킹 아이콘이 부릅니다.
        /// 목록에 없는 게임이면 첫 번째 탭으로 엽니다.
        /// </summary>
        public void ShowFor(string gameId)
        {
            _tab = Mathf.Max(0, gameIds != null ? System.Array.IndexOf(gameIds, gameId) : 0);

            var popup = GetComponentInParent<PopupPanel>(true);
            if (popup != null && !popup.IsOpen) popup.Open();   // 켜지면서 OnEnable 이 Refresh 합니다
            else Refresh();
        }

        /// <summary>지금 골라진 게임 (확인용).</summary>
        public string SelectedGameId => gameIds != null && _tab < gameIds.Length ? gameIds[_tab] : null;

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
            SetMyLine("");

            // 창에 보이는 줄 수만큼만 가져옵니다. 서버는 읽은 줄 수만큼 사용량이 쌓이기 때문입니다.
            int visible = rankLabels != null && rankLabels.Length > 0 ? rankLabels.Length : 1;
            int want = Mathf.Clamp(visible, 1, Mathf.Max(1, ArcadeConfig.Instance.rankingTopCount));

            // 진짜 서버에서는 답이 몇 초 뒤에 옵니다. 그래서 결과를 받아서 채우는 모양으로 둡니다.
            int request = ++_request;
            Services.Ranking.Fetch(gameIds[_tab], want, page =>
            {
                if (request == _request) Fill(page);
            });
        }

        void Fill(RankingPage page)
        {
            // 답이 늦게 왔는데 그 사이 창이 닫혔을 수 있습니다.
            // (에디터 미리보기는 창이 꺼진 상태에서 채우므로 플레이 중일 때만 봅니다)
            if (Application.isPlaying && !isActiveAndEnabled) return;

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
