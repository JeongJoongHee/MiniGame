using UnityEngine;
using UnityEngine.UI;

namespace Arcade
{
    /// <summary>
    /// **랭킹 창**입니다. 로비의 게임 칸마다 붙은 랭킹 아이콘으로 엽니다(<see cref="ShowFor"/>).
    /// 창 위쪽에는 **그 게임의 이름이 글자로** 나옵니다 — "랭킹" 아래 "점프점프".
    /// (수정사항_03 — 예전에는 게임 이름표 그림이 탭으로 늘어서 있었습니다. 게임 칸마다 여는 버튼이 생겨서
    ///  창 안에서 게임을 갈아탈 필요가 없어졌습니다)
    ///
    /// 게임 이름은 strings.csv 의 <c>game.{게임id}.name</c> 줄에서 오고, 그 줄이 없으면 카탈로그의 이름을 씁니다.
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

        [Tooltip("\"랭킹\" 아래 게임 이름 글자")]
        [SerializeField] Text gameNameLabel;

        [Tooltip("줄마다 순위 / 별명 / 점수 글자 세 개. 셋의 길이는 같습니다")]
        [SerializeField] Text[] rankLabels;
        [SerializeField] Text[] nickLabels;
        [SerializeField] Text[] scoreLabels;

        [Tooltip("이 창이 보여 줄 수 있는 게임들. gameNames 와 순서가 같습니다")]
        [SerializeField] string[] gameIds;
        [Tooltip("strings.csv 에 이름 줄이 없을 때 쓰는 이름 (카탈로그의 displayName)")]
        [SerializeField] string[] gameNames;

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
        /// 이 게임의 랭킹으로 창을 엽니다. 로비 게임 칸의 랭킹 아이콘이 부릅니다.
        /// 목록에 없는 게임이면 첫 번째 게임으로 엽니다.
        /// </summary>
        public void ShowFor(string gameId)
        {
            _tab = Mathf.Max(0, gameIds != null ? System.Array.IndexOf(gameIds, gameId) : 0);
            ShowGameName();   // 게임 이름은 서버를 기다릴 필요가 없으니 창이 뜨기 전에 먼저 바꿔 둡니다

            var popup = GetComponentInParent<PopupPanel>(true);
            if (popup != null && !popup.IsOpen) popup.Open();   // 켜지면서 OnEnable 이 Refresh 합니다
            else Refresh();
        }

        /// <summary>지금 골라진 게임 (확인용).</summary>
        public string SelectedGameId => gameIds != null && _tab < gameIds.Length ? gameIds[_tab] : null;

        /// <summary>지금 창 위쪽에 나오는 게임 이름 (확인용).</summary>
        public string SelectedGameName => gameNameLabel != null ? gameNameLabel.text : null;

        /// <summary>몇 번째 게임을 보여 줄지. 미리보기와 자체 점검이 씁니다.</summary>
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
            ShowGameName();
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
                Set(scoreLabels, i, ScoreText(row.score), color);
            }

            SetMyLine(page.myRank > 0
                ? StringTable.Format("rank.me", "MY RANK : {rank}   {score}",
                                     ("rank", page.myRank), ("score", ScoreText(page.myScore)))
                : StringTable.Get("rank.me.none", "NO RECORD YET"));
        }

        /// <summary>
        /// 점수 글자. strings.csv 에 <c>game.{게임id}.score</c> 줄이 있으면 그 모양으로 씁니다
        /// (검 강화는 "+{score}" — 점수가 곧 강화 수치라서 "+23" 으로 보여 줍니다). 없으면 숫자만.
        /// </summary>
        string ScoreText(int score)
        {
            string number = score.ToString("N0");
            string id = SelectedGameId;
            return string.IsNullOrEmpty(id)
                ? number
                : StringTable.Format("game." + id + ".score", "{score}", ("score", number));
        }

        void ShowGameName()
        {
            if (titleLabel != null) titleLabel.text = StringTable.Get("rank.title", "RANKING");
            if (gameNameLabel == null) return;

            string id = SelectedGameId;
            string fallback = gameNames != null && _tab < gameNames.Length ? gameNames[_tab] : id;
            gameNameLabel.text = string.IsNullOrEmpty(id) ? "" : StringTable.Get("game." + id + ".name", fallback);
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
