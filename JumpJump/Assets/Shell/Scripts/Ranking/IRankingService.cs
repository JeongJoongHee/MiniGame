using System;
using System.Collections.Generic;

namespace Arcade
{
    /// <summary>랭킹표의 한 줄.</summary>
    public struct RankingRow
    {
        public int rank;
        public string nickname;
        public int score;

        /// <summary>내 줄인지. 화면에서 색을 다르게 칠할 때 씁니다.</summary>
        public bool isMe;

        public RankingRow(int rank, string nickname, int score, bool isMe = false)
        {
            this.rank = rank;
            this.nickname = nickname;
            this.score = score;
            this.isMe = isMe;
        }
    }

    /// <summary>랭킹 한 장 분량. 상위 목록 + 내 순위.</summary>
    public class RankingPage
    {
        /// <summary>불러오기에 성공했는지. 실패하면 화면에 "불러오지 못했습니다" 를 띄웁니다.</summary>
        public bool ok;

        public List<RankingRow> rows = new List<RankingRow>();

        /// <summary>내 순위. 아직 등록한 적이 없으면 0.</summary>
        public int myRank;

        /// <summary>내 최고 점수.</summary>
        public int myScore;

        /// <summary>줄 수가 0 인지. 아무도 없으면 화면에 안내를 띄웁니다.</summary>
        public bool IsEmpty => rows == null || rows.Count == 0;
    }

    /// <summary>별명을 잡으려고 했을 때의 결과.</summary>
    public enum NicknameResult
    {
        /// <summary>내 것으로 잡았습니다.</summary>
        Ok,
        /// <summary>이미 다른 사람이 쓰고 있습니다. 다른 별명을 받아야 합니다.</summary>
        Taken,
        /// <summary>확인하지 못했습니다 (인터넷이 끊겼을 때 등).</summary>
        Failed,
    }

    /// <summary>
    /// 랭킹을 올리고 가져오는 물건의 **겉모양**입니다.
    /// 지금은 폰 안에만 저장하는 <see cref="LocalRankingService"/> 가 들어 있고,
    /// 2단계에서 Firebase 를 쓰는 구현으로 갈아 끼웁니다.
    /// <b>그때 게임 코드와 화면 코드는 한 줄도 고치지 않습니다.</b>
    ///
    /// 결과를 <c>Action</c> 으로 돌려주는 이유는, 진짜 서버는 답이 <b>몇 초 뒤에</b> 오기 때문입니다.
    /// 지금 구현은 곧바로 부르지만, 화면 쪽은 처음부터 "기다렸다 받는" 모양으로 만들어 둡니다.
    /// </summary>
    public interface IRankingService
    {
        /// <summary>쓸 준비가 되었는지. (서버 구현에서는 로그인이 끝났는지)</summary>
        bool IsReady { get; }

        /// <summary>
        /// 점수를 올립니다. <b>지금까지의 내 최고 점수보다 높을 때만 갱신됩니다.</b>
        /// 실패해도 게임은 그대로 굴러가야 하므로, 실패는 <paramref name="done"/> 의 false 로만 알립니다.
        /// </summary>
        void Submit(string gameId, string nickname, int score, Action<bool> done = null);

        /// <summary>상위 <paramref name="count"/> 명과 내 순위를 가져옵니다.</summary>
        void Fetch(string gameId, int count, Action<RankingPage> done);

        /// <summary>
        /// 별명을 **내 것으로 잡습니다. 별명은 앱 전체에서 하나뿐이어야 합니다** —
        /// 남이 쓰고 있으면 <see cref="NicknameResult.Taken"/> 이 돌아오고, 화면은 다시 물어봅니다.
        ///
        /// 진짜 서버에서는 "빈자리인지 확인 + 내 것으로 표시"를 <b>한 번에</b> 처리해야 합니다.
        /// 확인과 저장을 따로 하면 두 사람이 같은 순간에 같은 별명을 잡을 수 있기 때문입니다.
        /// (Firestore 의 트랜잭션으로 처리합니다 — 2단계)
        /// </summary>
        void ReserveNickname(string nickname, Action<NicknameResult> done);
    }
}
