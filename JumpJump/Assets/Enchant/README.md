# 검 강화! — 미니게임 3번 (2026-09-11)

기획서 `@기획서_게임_03.pdf` + 강화 표 `@Enchant_Sword.csv` (둘 다 작업 폴더) 를 그대로 옮긴 게임입니다.
로비의 3번 칸(`slot 2`), 게임 id `enchant`, 네임스페이스 `Enchant`, 씬 `EnchantScene`.

## 규칙

| 버튼 | 하는 일 | 실패하면 |
| --- | --- | --- |
| **일반 강화** (왼쪽 아래) | 표의 확률로 +1 | **검 파괴 → +0 새 검** (주문서는 그대로) |
| **안전 강화** (오른쪽 아래) | 주문서 1장을 쓰고 같은 확률로 +1 | 검은 그대로, 주문서만 사라짐 |
| **분해** (가운데) | 표의 `Melt` 가 1 이상인 수치(지금 +10 부터)에서만. 검을 +0 으로, 주문서 `Melt` 장 | — |

- **강화 수치 · 주문서 · 최고 기록은 폰에 저장됩니다** (`Enchant.Level` / `Enchant.SafeScrolls` / `Enchant.BestLevel`).
  로비에 나갔다 와도, 앱을 껐다 켜도 그 검 그대로입니다. (점프점프 · 활쏘기는 판마다 새로 시작)
  결과는 **결과가 나는 순간 저장**하므로, 강화 중에 앱을 꺼서 파괴를 피할 수 없습니다.
- **랭킹 점수 = 강화 수치.** 검 한 자루의 일생(파괴 또는 분해)이 한 판입니다. 검이 끝날 때
  `GameSession.ReportRunFinished` 로 알리고, 랭킹 등록 · 별명 창 · 광고 차례 세기는 껍데기가 합니다.
  새 최고 기록은 그 순간 폰의 랭킹 기록(`LocalRankingService.RecordBest`)에도 적어 둡니다 (통신 없음).
- **광고 자리** — 검이 부서진 뒤 **[새 검 받기]** (`EnchantGame.Update` 의 한 줄) + 로비로 나가기 [확인] (껍데기).
- 부서진 뒤 `newSwordLockSeconds`(1.4초) 동안 [새 검 받기] 가 안 눌립니다. 강화 버튼을 연타하던 손가락이
  곧바로 넘기거나 광고를 누르지 않게 (광고 실수 클릭은 AdMob 정책 위반).
- [분해] 는 `confirmMelt` 가 켜져 있으면 "분해하시겠습니까?" 를 한 번 묻습니다 (기획서에는 없음).
- 나가기는 **다른 게임과 같은 오른쪽 위 ← 화살표** 입니다 (기획서의 "문 모양" 대신 — 모든 게임 같은 자리 · 같은 모양 규칙).

## 강화 표 `@Enchant_Sword.csv`

**원본은 작업 폴더의 `@Enchant_Sword.csv`** 입니다 (`Enchant_Sword.csv` 로 바꿔도 되고, 둘 다 있으면 더 최근에 고친 쪽).
게임이 읽는 사본은 `Assets/Enchant/Enchant_Sword.csv`. 바깥이 더 새로우면 씬 빌드 · 플레이테스트 · 미리보기가 가져옵니다
(**Tools > Enchant > Apply Enchant_Sword.csv** 로 강제). `EnchantConfig.levels` 는 읽은 결과를 구워 둔 거울입니다.

| 열 | 뜻 | 없으면 |
| --- | --- | --- |
| `Enchant_Level` | 강화 수치. **꼭 있어야 합니다** (기획서의 오타 `Enchat_Level` 도 알아듣습니다) | — |
| `Enchant_Prob` | **이 수치에서** 강화 성공 확률, 만분율 (+7 줄 9000 = +7→+8 이 90%). `90%` 로 적어도 됨 | 10000 |
| `Melt` | 이 수치에서 분해하면 받는 안전 강화 주문서 수. 0 = 분해 버튼 꺼짐 | 0 |
| `Melt_Prob` | (선택) 분해했을 때 주문서가 나올 확률, 만분율. 기획서의 "일정 확률로" 를 쓰고 싶을 때 | 10000 |
| `Image` | 이 수치의 검 그림 이름 (`Art/Swords/` 의 같은 이름 그림) | 빈 칸 |

- 빈 칸은 바로 위 줄 값을 이어받습니다. 열 순서 · 메모 열 · BOM · CRLF 상관없습니다 (`EnchantTable.cs`).
- **표보다 높은 수치는 마지막 줄 값**을 씁니다 — 지금은 +51 부터도 30% 로 계속됩니다.
- **확률 0 인 줄 = 최고 단계.** 강화 버튼 둘 다 꺼지고 "더 이상 강화할 수 없습니다". 끝을 만들고 싶으면 표에 한 줄.
- 빌드 로그에 표 요약과 **"일반 강화만으로 한 번에 올라갈 확률"** (+10 72.9% / +20 12.5% / +30 1/458 ...) 이 찍힙니다.

## 그림 — 코드 수정 없이 바꿉니다

| 그림 | 넣는 곳 | 메모 |
| --- | --- | --- |
| 검 | 작업 폴더 또는 `@리소스 추가_N차` 에 `Sword_01.png` (`@Sword_01.png` 도 됨) | 표의 `Image` 이름과 같게. 여백을 잘라 **검 칸 높이에 맞춰** 그립니다 (크기 무관, 위를 향한 세로 그림) |
| 로비 아이콘 | 작업 폴더 `@Game_03_검강화.png` 덮어쓰기 | 지금은 임시 그림. 동그란 그림. 테두리가 그려진 그림이면 `GameCatalog` 의 Icon Scale 을 1 로 |
| 로비 이름표 | 작업 폴더 `@Name_03_검강화.png` 새로 넣기 | 가로:세로 약 2.15:1, 게임 이름이 그려진 그림. 없으면 빈 이름표 + "검 강화!" 글자 |
| 배경 | 작업 폴더나 `@리소스` 폴더에 `BG_03.png` 또는 `@Enchant_BG.png` (또는 `Art/Background.png` 덮어쓰기) | 세로형. 화면을 꽉 채우고(Cover) 넘치는 쪽은 잘림. 2026-09-13 부터 대장간 그림(`@리소스 추가 2차/BG_03.png`) |
| 버튼 셋 | `@리소스` 폴더에 `Normal_Enchant.png` / `Safe_Enchant.png` / `Melting.png` | **글자가 그려진 그림** (2026-09-13). `Art/Buttons/` 로 여백을 잘라 들어오고 칸 안에 비율대로. 없으면 예전의 색 판 + strings.csv 글자 |

- **안전 강화 그림의 "주문서 x {}"** — `{}` 자리는 가져올 때 버튼 바탕색으로 덮고, 화면에서는 그 자리에 가진 주문서 수를 글자로 얹습니다
  (`strings.csv` 의 `enchant.btn.safe.sub` = `{count}`). 자리는 `EnchantArt.SafeCountSlot` — **안전 강화 그림을 다른 모양으로 바꾸면 다시 재야 합니다.**
- [분해] 그림에는 "분해" 만 있어서 "주문서 +1" / "+10부터 가능" 줄은 버튼 바로 아래 글자입니다.
- 버튼을 누르면 앱 공통 **딸깍 소리**가 납니다 (`Arcade.UiClickSound`, 작업 폴더의 `Click.wav`). 이 게임에서 따로 할 일은 없습니다.
- 검 그림 19장(`Sword_01~19`, 대각선 정사각형)은 표의 `Image` 열로 나눠 씁니다 — +0~1 = 01, +2~6 = 02~06, 그 뒤 약 3단계마다 한 장, +48~50 = 19.

- 진짜 검 그림은 `Art/Swords/`, **임시 그림은 `Art/Placeholder/`** 에 따로 있습니다. 진짜 그림이 있는 이름은 날짜와 상관없이 진짜가 이깁니다.
- 임시 그림은 코드가 그린 픽셀아트입니다 (`EnchantArt.SwordPixels`, 등급이 오를수록 길고 넓고 화려해짐).
  **Tools > Enchant > Regenerate Placeholder Art** 로 다시 그립니다 (진짜 그림은 안 건드림).
- 빌드 로그의 "검 그림 6장" 에 이름마다 **진짜 그림 / 임시 그림** 이 찍힙니다.

## 확인하기 (배치 모드)

```bash
# 자동 점검 25건 (규칙 · 표 읽기 · 확률 · 랭킹 신호 · 저장) + 밸런스 봇
"$UNITY" -batchmode -quit -projectPath "$PROJ" \
  -executeMethod Enchant.EditorTools.EnchantPlaytest.Run -logFile <로그>

# 화면 PNG 7장 -> JumpJump_Preview/20_~26_enchant_*.png (26 은 20:9 폰)
"$UNITY" -batchmode -quit -projectPath "$PROJ" \
  -executeMethod Enchant.EditorTools.EnchantPreview.Capture -logFile <로그>
```

- 게임 로직은 `EnchantGame.Step(dt, action)` — 점프점프 · 활쏘기의 `Step(dt, tap)` 자리입니다. 입력이 버튼 셋이라
  `tap` 대신 `EnchantAction`(None / Normal / Safe / Melt / NewSword) 을 받습니다. 버튼은 `Press*()` 로 다음 프레임 입력을 남길 뿐입니다.
- 확률은 `System.Random` 이라 `Seed()` 로 같은 판을 다시 돌릴 수 있고, 점검은 씨앗을 고정해서 매번 같은 결과가 나옵니다.
- **점검 · 미리보기는 `SaveProgress = false`, `GameSession.Recording = false` 로 돕니다** — 폰의 기록 · 판 수를 건드리지 않습니다
  (저장 점검 한 건과 랭킹 신호 점검 한 건만 잠깐 쓰고 원래대로 되돌립니다).
- 확률 점검은 표에 나오는 확률마다 4,000번 눌러 실제 성공률이 표와 맞는지 봅니다 (허용 오차 = 4 표준편차, 최소 2%).

## 튜닝 값 `Assets/Enchant/EnchantConfig.asset`

`Start Safe Scrolls`(처음 주문서, 0) · `Confirm Melt`(분해 확인, 켜짐) · `Work Seconds`(강화 연출 0.6초) ·
`Banner Seconds` · `Break Reveal Seconds` · `New Sword Lock Seconds`(1.4초) · `Glow By Level`(검 뒤 빛 색) · `Work Shake`.

## 파일

```
Assets/Enchant/
  Enchant_Sword.csv      강화 표 사본 (원본은 작업 폴더 @Enchant_Sword.csv)
  EnchantConfig.asset    튜닝 값 + 표를 구워 둔 거울
  Art/                   Background / Badge(강화 수치 동그라미) / Button(9-슬라이스 버튼 판) / Glow
    Swords/              진짜 검 그림 (작업 폴더에서 가져옴)
    Placeholder/         임시 검 그림 (코드가 그림)
  Scenes/EnchantScene.unity   — 손으로 편집하지 말 것 (EnchantSceneBuilder 가 통째로 굽습니다)
  Scripts/
    EnchantGame.cs       규칙 전부 + Step(dt, action) + 저장
    EnchantConfig.cs     튜닝 값 + 표 한 줄(EnchantLevel) + "이 수치의 확률/분해/그림"
    EnchantTable.cs      CSV -> 표
    EnchantHud.cs        화면 (글자 · 버튼 켜고 끄기 · 떨림 · 번쩍임 · 부서짐 연출)
  Editor/
    EnchantSceneBuilder.cs   씬 굽기. 자리 숫자는 위쪽 상수 (1080x1920 기준 픽셀)
    EnchantTableCsv.cs       표 가져오기 (Tools > Enchant > Apply Enchant_Sword.csv)
    EnchantArt.cs            임시 그림 그리기 + 진짜 그림 가져오기 + 로비 아이콘
    EnchantPlaytest.cs       자동 점검 + 봇
    EnchantPreview.cs        화면 캡처
```

## 화면 배치

화면 전체가 UI 입니다 (월드에 움직이는 물체 없음). 위쪽 것은 화면 위에, 아래쪽 것은 화면 아래에 붙고
**검 칸은 그 사이를 세로로 채웁니다** — 세로로 긴 폰에서는 검이 더 크게 나옵니다.
아래 큰 버튼 둘은 **화면 절반씩**이라 좁은 폰에서도 겹치지 않습니다. 숫자는 `EnchantSceneBuilder` 위쪽 상수.
