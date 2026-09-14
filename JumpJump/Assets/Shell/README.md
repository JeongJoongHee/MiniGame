# Shell — 타이틀 화면과 로비 (미니게임 껍데기)

이 폴더는 **게임이 아니라 게임들을 담는 그릇**입니다.
앱을 켜면 타이틀이 뜨고, START 를 누르면 로비가 뜨고, 로비에서 게임 칸을 누르면
그 미니게임 씬으로 들어갑니다. 미니게임이 늘어나도 이 폴더는 거의 그대로입니다.

```
타이틀(TitleScene) ──START──▶ 로비(LobbyScene) ──PLAY──▶ 미니게임 씬
                                   ▲                        │
                                   └───── ← (확인 팝업) ─────┘
```

지금 들어 있는 미니게임은 세 개입니다.

| 칸 | 게임 | 씬 | 폴더 |
| --- | --- | --- | --- |
| 1 (slot 0) | 점프점프! | `GameScene` | `Assets/JumpJump/` |
| 2 (slot 1) | 활쏘기! | `ArcheryScene` | `Assets/Archery/` |
| 3 (slot 2) | 검 강화! | `EnchantScene` | `Assets/Enchant/` (2026-09-11) |

## 만들고 확인하기

| 메뉴 | 하는 일 |
| --- | --- |
| **Tools > Arcade > Build All Scenes** | 타이틀 / 로비 / 미니게임 씬을 한 번에 굽고 Build Settings 까지 정리합니다. **평소엔 이것만 누르면 됩니다.** |
| Tools > Arcade > Import Shell Art | 작업 폴더의 `@` 그림들을 다시 가져옵니다 |
| Tools > Arcade > Capture Shell Preview PNG | 플레이 모드 없이 타이틀/로비 화면을 PNG 로 뽑습니다 |

배치 모드(에디터를 켜지 않고 확인)로도 같습니다.

```bash
UNITY="C:/Program Files/Unity Hub/6000.0.81f1/Editor/Unity.exe"
PROJ="D:/00.JumpJump/JumpJump"

"$UNITY" -batchmode -quit -projectPath "$PROJ" \
  -executeMethod Arcade.EditorTools.ShellSceneBuilder.BuildAll -logFile <로그>

"$UNITY" -batchmode -quit -projectPath "$PROJ" \
  -executeMethod Arcade.EditorTools.ShellPreview.Capture -logFile <로그>
```

미리보기는 `JumpJump_Preview/` 에 네 장이 떨어집니다.

| 파일 | 화면 비율 |
| --- | --- |
| `00_title.png` / `00_lobby.png` | 9:16 — 기획서 목업과 같은 비율 |
| `00_title_tall.png` / `00_lobby_tall.png` | 20:9 — **요즘 폰의 실제 비율** |

두 장씩 찍는 이유는, 폰마다 세로 길이가 달라서 배경이 잘리거나 여백이 생기기 때문입니다.
아래 "화면 비율" 절을 보세요.

---

## 미니게임을 새로 붙이는 방법

코드를 고칠 필요가 없습니다. 4단계입니다.

1. **게임 씬을 만든다.** 예: `Assets/Games/Flappy/Scenes/FlappyScene.unity`
   씬 안에서 로비로 돌아가려면 버튼에 `Arcade.AppFlow.GoToLobby()` 를 연결합니다.
2. **그림 두 장을 작업 폴더에 넣는다.** 번호는 게임 순서대로 맞춥니다.
   - `D:\00.JumpJump\@Game_02_이름.png` — 로비 칸의 **동그란 아이콘**.
     `@Game_01_Jump.png` 처럼 동그란 그림이어야 배경의 동그란 자리에 딱 맞습니다.
   - `D:\00.JumpJump\@Name_02_이름.png` — 아이콘 아래의 **이름표**.
     **게임 이름이 그림 안에 그려져 있어야 합니다.** 코드는 글자를 따로 얹지 않습니다.
     (`@Name_01_점프점프.png` 을 그대로 흉내 내면 됩니다)
     **이름표를 아직 안 그렸으면 넣지 않아도 됩니다.** 그때는 껍데기가 만들어 두는
     빈 이름표(`Art/NamePlate_Blank.png`) 위에 `Display Name` 이 글자로 찍힙니다.
     **그림 비율은 가로:세로 약 2.15:1 로 맞춰 주세요.** 로비의 이름표는 게임마다
     크기가 같아야 해서 **정해진 칸 크기로 그려 넣기 때문**입니다 (아래 "이름표 크기" 절).
   - 주변 투명 여백은 가져오면서 자동으로 잘라 내므로 신경 쓰지 않아도 됩니다.
3. **Tools > Arcade > Build All Scenes** 를 누른다. (그림이 프로젝트로 들어옵니다)
4. **`Assets/Shell/GameCatalog.asset`** 를 열고 `Games` 목록에 한 줄 추가한다.
   `Icon` 과 `Name Plate` 를 비워 두면 빌드할 때 **목록 순서대로** 자동으로 채워집니다.

| 칸 | 뜻 |
| --- | --- |
| `Id` | 코드용 이름 (예: `flappy`). **영문 소문자 · 숫자 · `_` 만, 32자까지** — 랭킹 서버 규칙이 이 모양만 받습니다. 대문자나 `-` 를 쓰면 그 게임만 점수가 조용히 안 올라갑니다 (자체 점검이 잡아 줍니다). 게임 코드의 `GameId` 와 같아야 하고, `strings.csv` 에 `game.{id}.name` 줄(랭킹 창 제목)도 하나 넣으세요. **별명은 게임마다 따로가 아니라 사람에게 하나라서, 새 게임도 같은 별명으로 랭킹에 오릅니다.** |
| `Display Name` | 게임 이름. 이름표 그림이 없을 때만 글자로 찍힙니다 (예: `날아라!`) |
| `Scene Name` | 씬 파일 이름, 확장자 없이 (예: `FlappyScene`) |
| `Icon` | 방금 가져온 `Art/Games/Game_02_이름.png` |
| `Name Plate` | 방금 가져온 `Art/Names/Name_02_이름.png`. 비워 두면 빈 이름표 + 글자로 대신 나옵니다 |
| `Slot` | 로비의 몇 번째 칸인지. **0 = 왼쪽 위, 8 = 오른쪽 아래** |
| `Available` | 끄면 그 칸은 자물쇠(잠김)로 남습니다 |

> **씬을 Build Settings 에 넣는 것을 잊지 마세요.** 여기만은 코드를 고쳐야 합니다.
> `ShellSceneBuilder` 의 씬 경로 상수에 한 줄, `RegisterBuildSettings()` 목록에 한 줄,
> `BuildAll()` 에 그 게임의 씬 빌더 호출 한 줄 — 활쏘기 · 검 강화를 붙일 때 고친 곳이 그 세 군데입니다.
> (APK 에 넣으려면 `JumpJumpBuild.Scenes` 목록에도 추가해야 합니다)
> 기본 카탈로그 줄은 `LoadOrCreateCatalog()` 에 넣어 두면 빌드할 때 없으면 만들어 줍니다.
> **빠뜨리면 자체 점검(`ArcadeSelfTest`)이 "씬이 Build Settings / APK 목록에 들어 있다" 에서 실패합니다** (2026-09-11 부터).
>
> 랭킹 창의 점수는 숫자로 나옵니다. 모양을 바꾸고 싶으면 `strings.csv` 에 `game.{id}.score` 줄을 넣으세요
> (검 강화는 `+{score}` — "+23"). 없으면 숫자만 나옵니다.

### 로비 칸 번호

배경 그림에 번호가 **그려져 있습니다.** 코드의 `Slot` 번호(0~8)와는 다릅니다.

| 그림에 적힌 번호 | 1 | 2 | 3 | 4 | 5 | 6 | 8 | 9 | 10 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 코드의 `Slot` | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 |

> 그림의 7번째 칸에 **7 대신 8** 이 적혀 있고 마지막이 10 으로 끝납니다.
> 그림 쪽 오타로 보이니, 고치실 거면 `@Lobby.png` 를 수정한 뒤
> Tools > Arcade > Import Shell Art 를 눌러 주세요.

---

## 화면이 어떻게 자리를 잡는가

이 앱의 UI 는 **픽셀 좌표를 쓰지 않습니다.** 전부 "배경 그림 안에서 몇 % 지점" 으로 붙습니다.

1. `AspectFitter` 가 배경 그림을 원본 비율 그대로 화면에 맞춥니다.
2. 버튼·아이콘·이름표는 그 배경 사각형의 **비율(앵커)** 로 붙습니다.

그래서 폰이 길든 짧든 아이콘이 배경에 그려진 동그란 자리 위에 정확히 얹힙니다.
숫자를 픽셀로 박아 두면 해상도마다 어긋나는데, 이 방식은 어긋날 수가 없습니다.

### 화면 비율

| 화면 | 맞추는 방식 | 이유 |
| --- | --- | --- |
| 타이틀 | **Cover** (화면을 꽉 채우고 넘치는 쪽은 자름) | 그냥 풍경이라 조금 잘려도 됩니다 |
| 로비 | **Contain** (그림 전체가 보이게, 남는 쪽은 여백) | 칸이 하나라도 잘리면 안 됩니다 |

> **딱 하나, 타이틀의 게임 이름 그림만은 배경이 아니라 화면에 붙습니다.**
> 타이틀 배경은 Cover 라서 요즘 폰(20:9)에서는 좌우가 20% 가까이 잘려 나갑니다.
> 이름을 배경 기준으로 크게 잡으면 그때 첫 글자와 끝 글자가 같이 잘립니다.
> 그래서 `TitleScreen.titleWidth` 는 **화면 폭 기준**입니다 (1.0 = 화면에 꽉 참).
> 높이는 계산하지 않고 `preserveAspect` 에 맡기므로 어떤 경우에도 찌그러지지 않습니다.

로비 그림(`1536x2730` ≈ 9:16)이 요즘 폰(20:9)보다 짧아서, **위아래에 어두운 띠가 생깁니다.**
배경 테두리 색을 어둡게 깐 것이라 액자처럼 보이긴 하지만, 없애고 싶다면
세로로 더 긴 로비 그림을 새로 그려서 `@Lobby.png` 를 교체하면 됩니다.
(그때는 `LobbyScreen` 의 `Slot Columns / Slot Rows / Slot Size` 를 다시 재야 합니다)

### 이름표 크기

로비의 이름표는 **그림의 원본 비율을 따르지 않고 정해진 칸(`LobbyScreen.plateWidth` x
`plateHeight`)에 맞춰 그려집니다.** 게임마다 그림이 달라도 로비에 늘어선 이름표 크기가
전부 같아야 하기 때문입니다. 그래서 **이름표 그림은 가로:세로 약 2.15:1** 로 그려 주세요.
(`Name_01_점프점프.png` 1761x821 = 2.145 가 기준입니다)

크기를 바꾸고 싶으면 `LobbyScreen` 인스펙터의 `Plate Width` / `Plate Height` 를 같이 고치면 됩니다.

### 나가기 / 설정 팝업 (2026-09-08)

앱을 나가는 길은 전부 **확인 팝업 한 번**을 거칩니다. 실수로 눌러서 판이 날아가지 않도록.

| 어디서 | 누르는 것 | 뜨는 팝업 | [확인] 하면 |
| --- | --- | --- | --- |
| 미니게임 | 오른쪽 맨 위 **←** | 로비로 나가시겠습니까? | 로비로 (진행 상황은 저장 안 함) |
| 로비 | 오른쪽 위 **톱니바퀴** | 설정 (빈 판 + [종료] [닫기]) | — |
| 로비 | 설정의 **[종료]** | 종료하시겠습니까? | 앱 종료 |
| 타이틀 · 로비 | 폰 **뒤로 버튼** | 종료하시겠습니까? | 앱 종료 |
| 미니게임 | 폰 **뒤로 버튼** | 로비로 나가시겠습니까? | 로비로 |

팝업이 떠 있을 때 뒤로 버튼을 누르면 **그 팝업만 닫힙니다.**

설정 팝업의 판 위쪽은 일부러 비워 두었습니다. **나중에 사운드 조절이 들어갈 자리**입니다.

#### 코드에서

- 팝업 한 장 = `PopupPanel` 컴포넌트 하나. 씬에 **꺼진 채로 미리 만들어져** 있다가
  `Open()` / `Close()` 로 켜고 꺼집니다. 실행 중에 만들지 않으므로 첫 등장이 끊기지 않습니다.
- 버튼이 부르는 함수는 전부 `ShellMenu` 에 있습니다. 씬마다 필요한 팝업만 연결되어 있어서
  **같은 컴포넌트가 타이틀·로비·미니게임에서 각각 다르게 동작합니다.**
  ([닫기]와 [취소]는 셋 다 `OnCancelPressed` = "맨 위 팝업 닫기" 하나를 씁니다)
- **미니게임에 붙일 때는 `ShellSceneBuilder.MakeExitToLobbyUI(root)` 한 줄이면 됩니다.**
  화살표 버튼 + 팝업 + `ShellMenu` 가 통째로 만들어집니다. **게임 쪽 코드는 필요 없습니다.**
  (예전에는 HUD 에 `OnLobbyPressed()` 가 있어야 했지만 이제 아닙니다)
- 팝업이 떠 있는 동안 게임이 멈추는 것은 각 게임 `Update` 의 `PopupPanel.AnyOpen` 검사입니다.
  **`Step(dt, tap)` 은 팝업을 전혀 모릅니다** — 그래서 배치 모드 플레이테스트가 그대로 돕니다.
- 팝업 뒤의 어두운 막이 터치를 전부 먹기 때문에, 뒤쪽 버튼이 눌리지 않게 따로 막을 필요가 없습니다.
- 폰의 하드웨어 뒤로 버튼은 Input System 에서 **Esc 키**로 들어옵니다.
  윈도우 에디터에서 Esc 를 눌러도 똑같이 확인할 수 있습니다.

버튼을 누른 것이 점프·발사로 세지 않는 것은 각 게임의 `TapInput.OverUI` 가 막아 줍니다.

### 화면 글자표 `strings.csv` (2026-09-08)

**화면에 나오는 글자는 전부 표 한 장에서 옵니다.** 코드를 고칠 필요가 없습니다.

- 고치는 파일 : `D:\00.JumpJump\strings.csv` (엑셀로 열면 됩니다)
- 게임이 읽는 사본 : `Assets/Shell/Resources/strings.csv`
- 바깥 파일이 더 새로우면 자동으로 가져옵니다 (`ShellStringsCsv.Sync`).
  씬 빌드 · 미리보기 캡처 · 플레이테스트가 전부 이 함수를 거칩니다.
  강제로 다시 읽으려면 **Tools > Arcade > Apply strings.csv**.

표는 `Key,Text` 두 열입니다. **Key 는 코드가 찾는 이름이라 고치면 안 되고, Text 만 고칩니다.**

| 쓰는 법 | 뜻 |
| --- | --- |
| `\n` | 줄바꿈 |
| `{score}` `{reason}` 등 | **숫자가 들어갈 자리.** 옮기거나 지워도 되지만 이름을 틀리면 글자가 그대로 남습니다 |
| `Note` 열 | 무시됩니다. 메모용 열을 마음껏 추가해도 됩니다 |
| `#` 로 시작하는 Key | 주석으로 보고 건너뜁니다 |

쉼표가 든 문장은 엑셀이 알아서 따옴표로 묶어 주고, 파서도 그대로 읽습니다.

#### 엑셀에서 한글이 `?먰봽?먰봽` 처럼 깨져 보일 때

**파일이 깨진 것이 아니라 엑셀이 잘못 읽고 있는 것입니다.**
한글 윈도우의 엑셀은 CSV 앞에 BOM 이라는 표시가 없으면 옛 한글 인코딩(CP949)이라고
넘겨짚습니다. 그래서 UTF-8 로 저장된 한글이 깨져 보입니다.

**`Tools > Arcade > Apply strings.csv` 를 한 번 누르면 고쳐집니다.**
`ShellStringsCsv.RepairEncoding` 이 파일을 UTF-8 + BOM 으로 맞춰 주기 때문입니다.

거꾸로 엑셀에서 그냥 `CSV(쉼표로 분리)` 로 저장하면 CP949 로 쓰여서 이번에는
**Unity 가 한글을 못 읽습니다.** 그 경우도 같은 함수가 알아채고 UTF-8 로 되돌립니다.
**그래서 엑셀에서 어느 쪽으로 저장해도 됩니다.** 굳이 고르신다면
`CSV UTF-8(쉼표로 분리)` 이 안전합니다.

**표가 없거나 어떤 줄이 비어 있어도 앱은 돌아갑니다.** 그 자리는 코드에 적어 둔
기본 문구(전부 영어)로 대신합니다 — `HudController.T()` / `ArcheryHud.T()` 의 두 번째 인자입니다.
그래서 **표를 지워도 화면이 비지 않습니다.**

> **글자를 한글로 바꿔도 됩니다.** 2026-09-08 에 한글이 든 글꼴을 넣었으므로
> 폰에서도 그대로 나옵니다. 다만 **한글은 영어보다 폭이 넓어서 칸을 넘칠 수 있으니**,
> 길게 바꾸신 뒤에는 미리보기 캡처로 한 번 확인하는 편이 좋습니다.

새 미니게임을 만들면 그 게임의 글자도 이 표에 `<게임id>.` 로 시작하는 Key 로 추가하세요.

### 글꼴 (2026-09-08)

**앱 전체가 쓰는 글꼴은 작업 폴더의 `Font/` 폴더 하나로 정해집니다.**

```
D:\00.JumpJump\Font\  에 .ttf 나 .otf 를 넣는다   (파일 이름은 아무거나)
        ↓  Build All Scenes
Assets/Shell/Resources/GameFont.ttf
        ↓  실행 중에
ShellUI.GameFont  ←  타이틀 / 로비 / 점프점프 HUD / 활쏘기 HUD 가 전부 이걸 씁니다
```

- **코드에는 글꼴 이름이 한 글자도 없습니다.** 바꾸려면 폴더의 파일만 갈아 끼우세요.
- 폴더가 비어 있으면 Unity 기본 글꼴(LegacyRuntime)로 돌아갑니다. **글꼴이 없어도 앱은 돌아갑니다.**
- `.ttf` 와 `.otf` 가 둘 다 있으면 **`.ttf` 를 씁니다.** Unity 가 더 잘 다룹니다.
  같은 글꼴을 여러 형식으로 받는 일이 흔해서 그렇게 해 두었습니다
  (`.bdf` / `.woff2` / 압축 파일은 무시합니다).
- **`Resources` 폴더에 넣는 것이 중요합니다.** 실행 중에 `Resources.Load<Font>("GameFont")`
  로 찾기 때문에, 글꼴만 바꿀 때는 **씬을 다시 굽지 않아도 됩니다.**

**임포트 설정은 `ShellArt.ConfigureFont` 가 잡습니다.**

- `fontRenderingMode = HintedRaster` — 글자를 픽셀 격자에 맞춰 또렷하게 굽습니다.
  기본값(Smooth)은 부드럽게 뭉개져서 **픽셀 글꼴과 어울리지 않습니다.**
- `includeFontData = true` — 글꼴을 앱에 같이 담습니다. **끄면 폰에서 한글이 안 나옵니다.**

지금 쓰는 글꼴은 **x10y12pxDenkiChipHangul** (10x12 픽셀, 한글 포함)입니다.
`.ttf` 가 5.1MB 라 APK 가 그만큼 커집니다. 같은 글꼴의 `.otf` 는 489KB 이므로,
용량이 아깝다면 `Font/` 에서 `.ttf` 를 빼면 `.otf` 가 쓰입니다.

> **한글 위험이 사라졌습니다.** 예전에는 화면에 한글이 나오면 OS 글꼴에 기대야 해서
> 안드로이드에서 깨질 수 있었는데, 이제 한글이 들어 있는 글꼴을 앱에 담고 있습니다.

### 그림의 투명 여백을 잘라 내는 이유

원본 버튼 PNG 는 그림 주위에 빈 공간이 넓습니다. 예를 들어 `@Start_Button.png` 는
2400x1519 인데 실제 버튼은 1529x483 뿐입니다. 그대로 쓰면 "버튼을 화면 폭의 46%로"
라고 지정해도 눈에 보이는 버튼은 29% 밖에 안 됩니다.

`ShellArt` 가 가져오면서 여백을 잘라 내므로 **사각형 크기 = 보이는 그림 크기** 가 되고,
배치 숫자가 그대로 맞아떨어집니다. 원본은 작업 폴더에 그대로 남아 있습니다.

> **작업 폴더를 거치지 않고 `Art/Games` · `Art/Names` 에 그림을 바로 넣어도 됩니다.**
> 그 경우에도 다음 빌드에서 여백을 잘라 냅니다(`ShellArt.TrimStrayArt`). 다만 이때는
> **프로젝트 안의 파일을 직접 고치므로 원본이 남지 않습니다.** 원본을 남기고 싶으면
> 작업 폴더에 `@Name_02_이름.png` 형태로 넣는 쪽이 안전합니다.

| 원본 | 프로젝트 안 | 잘린 뒤 |
| --- | --- | --- |
| `@Start_Button.png` 2400x1519 | `Art/Start_Button.png` | 1529x483 |
| `@Play_Button.png` 2400x1309 | `Art/Play_Button.png` | 1724x803 |
| `@Title_Text.png` 2400x1309 | `Art/Title_Text.png` | 2192x534 |
| `@Name_01_점프점프.png` 2400x1308 | `Art/Names/Name_01_점프점프.png` | 1761x821 |
| `@Game_01_Jump.png` 635x569 | `Art/Games/Game_01_Jump.png` | 492x510 |
| `@Title.jpg` / `@Lobby.png` | 그대로 복사 (배경은 자를 여백이 없음) | — |

---

## 랭킹 · 별명 (2026-09-09 뼈대 → 2026-09-11 서버 연결)

**랭킹은 Firebase(익명 로그인 + Firestore)에 올라갑니다.** Firebase SDK 는 넣지 않았습니다 —
Unity 에 들어 있는 `UnityWebRequest` 로 Firebase 의 웹 주소(REST)에 직접 요청합니다.
그래서 APK 가 커지지 않고, 안드로이드 빌드 설정(gradle)을 건드릴 일이 없고,
**배치 모드에서도 게임과 똑같은 코드로 서버를 시험할 수 있습니다.**

| 어디서 | 무엇을 | 무슨 일이 |
| --- | --- | --- |
| 로비 | 게임 칸 번호 자리의 **랭킹 아이콘** | 그 게임의 랭킹 창 ("── 랭킹 ──" 아래 게임 이름 글자) |
| 미니게임 | 판이 끝남 | 별명이 없으면 별명 창 → 점수 올리기 |
| 로비 | 톱니바퀴 → **[별명 바꾸기]** | 별명 창 (횟수 제한 없음) |

- **서버 주소는 작업 폴더의 `google-services.json` 에서 옵니다.** Build All Scenes 때
  `ShellFirebaseConfig` 가 프로젝트 ID · API 키를 `Resources/ArcadeConfig.asset` 에 옮겨 적습니다.
  값이 비어 있거나 `onlineRanking` 을 끄면 **폰 안에만 저장**합니다 (1단계와 같은 동작).
- **보안 규칙은 작업 폴더의 `firestore.rules`** 입니다. Firebase 콘솔 > Firestore > 규칙 에
  붙여 넣어야 적용됩니다 (2026-09-11 에 사용자가 게시함). 규칙 파일은 **영문만** 쓸 것 —
  한글 주석을 넣었더니 콘솔이 `Line 1: Parse error` 로 거절했습니다.
- **별명은 앱 전체에서 하나뿐입니다.** `nicknames/{별명 소문자}` 문서를 "자리표"로 쓰고,
  "없어야만 만든다"는 조건으로 한 번에 잡으므로 동시에 눌러도 한 사람만 잡힙니다.
  영문은 대소문자를 가리지 않습니다 (`Tom` 이 있으면 `tom` 도 못 씀).
- **금지어는 작업 폴더의 `badword.csv`** (사용자가 넣은 17,730개). 별명과 금지어를 둘 다
  소문자로 바꾸고 공백·기호를 뺀 뒤 **금지어가 별명 안 어디에든 있으면** 막습니다.
  짧은 금지어가 멀쩡한 이름을 막을 수 있습니다 (예: `SM` → `SMILE`, `뽕` → `짬뽕`) — 그 줄을 지우면 됩니다.
- **인터넷이 끊겨도 기록은 사라지지 않습니다.** 점수는 먼저 폰에 적고, 서버에 올라간 값을 따로 기억해
  두었다가 다음에 연결될 때(앱을 켤 때 · 다음 판 · 랭킹 창을 열 때) 올립니다.
- 랭킹 창은 **보이는 줄 수(8)만큼만** 서버에서 읽고, 같은 게임을 20초 안에 다시 열면 다시 읽지 않습니다.
  Firestore 무료 한도(하루 읽기 5만 번)를 아끼려는 것입니다.

### 서버 점검 (배치 모드)

```bash
# -quit 을 붙이지 않습니다. 서버 답을 기다리는 동안 에디터가 살아 있어야 해서, 끝나면 스스로 닫습니다.
"$UNITY" -batchmode -projectPath "$PROJ" \
  -executeMethod Arcade.EditorTools.ArcadeOnlineTest.Run -logFile <로그>
```

시험 계정 두 개로 로그인 · 별명 잡기/바꾸기 · 점수 올리기 · 랭킹 가져오기 · 겹치는 별명 거절 ·
**보안 규칙(남의 점수 고치기 / 남의 이름으로 올리기 / 남의 별명 지우기가 `PERMISSION_DENIED` 로 막히는지)** 을
실제 서버로 확인하고, **끝나면 시험 기록과 시험 계정을 전부 지웁니다.** 기록은 `ranks/selftest` 칸에만 씁니다.
**사용자의 Firebase 에 계정을 만들었다 지우는 일이므로 돌리기 전에 사용자에게 물어볼 것.**

---

## 치트 — 모든 게임 데이터 초기화 (2026-09-11)

로비 톱니바퀴 → 설정 창의 **빈 윗부분을 3초 안에 5번** 누르면 확인 창이 뜹니다. [확인] 하면
서버의 내 기록(게임마다 점수 줄 · 별명 자리 · 익명 계정)을 지우고, 폰의 기록(PlayerPrefs)을 전부 지운 뒤 타이틀로 갑니다.
인터넷이 안 되면 폰 기록만 지웁니다.

- 켜기/끄기 · 횟수 · 시간은 `ArcadeConfig.asset` 의 `cheatsEnabled` / `cheatTaps` / `cheatTapSeconds`.
  **스토어 빌드에서는 `cheatsEnabled` 를 끕니다.**
- 에디터에서는 **Tools > Arcade > Reset All Game Data (Editor)** — 에디터 기록만 지웁니다 (서버는 안 지움).
- 부품: `DataResetPopup`(확인 창) · `CheatTapZone`(보이지 않는 누름 영역) · `Editor/ShellCheatUI`(굽기).
  나중에 설정 창에 사운드 조절을 넣다가 누름 영역과 겹치면 `ShellCheatUI.HitCenter` 를 옮기세요.

---

## 광고 (2026-09-11, AdMob 전면 광고)

**2판마다, 사용자가 누른 "길목"에서만** 전면 광고가 뜹니다 — 게임 오버 화면의 **다시하기**와
"로비로 나가시겠습니까?" 의 **[확인]**. 광고가 닫히면 다시하기는 곧바로 판을 시작하지 않고
**"터치하면 시작" 화면으로** 돌아갑니다. 게임 도중에는 절대 뜨지 않습니다.

- 게임 오버 **직후**가 아니라 **누른 뒤**인 이유: 점프점프는 계속 두드리는 게임이라, 죽는 순간 광고가 뜨면
  두드리던 손가락이 광고를 누릅니다. 실수 클릭은 AdMob 정책 위반이고 계정 정지 사유입니다.
- "띄울 때인지"는 `AdGate`(2판 + 최소 90초), "지금 띄워라"는 `AdBreak.TryShow(then)`, 실제 광고는 `AdMobService`.
- **광고가 준비 안 됐으면 건너뜁니다** (인터넷 없음 등). 차례는 남아서 다음 길목에서 다시 봅니다.
  광고가 닫혔다는 소식이 120초 안에 안 오면 닫힌 것으로 칩니다 — 게임이 영영 멈추지 않게.
- **값은 전부 `Resources/ArcadeConfig.asset`** — 앱 ID(`~`) · 광고 단위 ID(`/`) · `useTestAds` · `childDirected` · `maxAdContentRating`.
  앱 ID 는 Build All Scenes 때 `ShellAdsConfig` 가 플러그인 설정(`Assets/GoogleMobileAds/Resources/`)에 옮겨 적습니다.
- **`useTestAds` 가 켜져 있으면 구글 테스트 광고**가 나옵니다. 개발 중에는 반드시 켜 둘 것 — 스토어 빌드에서만 끕니다.
- 플러그인은 `Packages/manifest.json` 의 OpenUPM 레지스트리로 들어옵니다 (`com.google.ads.mobile` 11.5.0).
  안드로이드 라이브러리는 **APK 를 구울 때** 플러그인이 gradle 설정을 만들어 받아 옵니다 (인터넷 필요).
- 동의(UMP): 유럽 등 동의가 필요한 사용자에게만 동의 창이 뜹니다. 그런 사용자에게는 설정 창에
  **"개인정보 설정" 버튼이 필요**한데 아직 없습니다 — 한국에만 배포하면 필요 없습니다.

### 미니게임을 새로 만들 때

각 게임의 `Update` 에서 `Step` 을 부르기 **바로 앞에** 이 한 줄을 넣습니다 (`Step` 안이 아닙니다):

```csharp
if (tap && State == GameState.GameOver && _stateClock >= RetryLockSeconds &&
    Arcade.AdBreak.TryShow(ResetRun)) return;
```

---

## 구조

```
Assets/Shell/
  GameCatalog.asset       미니게임 목록 (여기를 고쳐서 게임을 추가합니다)
  Art/
    Title.jpg             타이틀 배경
    Title_Text.png        타이틀의 게임 이름 (글자가 아니라 그림입니다)
    Lobby.png             로비 배경 (동그란 자리 9개와 자물쇠가 그려져 있음)
    Start_Button.png      타이틀의 START
    Play_Button.png       로비 칸의 PLAY
    NamePlate_Blank.png   이름표 그림이 없는 게임에 깔리는 빈 이름표 (코드가 만듭니다)
    Setting_Button.png    로비 오른쪽 위 톱니바퀴          (원본 Setting.png)
    Back_Button.png       미니게임 오른쪽 위 뒤로가기 화살표 (원본 Undo.png)
    Popup_Panel.png       설정 팝업의 빈 판                (원본 Empty_Pannel.png)
    Popup_Ask_Exit.png    "로비로 나가시겠습니까?"          (원본 Popup_01.png)
    Popup_Ask_Quit.png    "종료하시겠습니까?"              (원본 Popup_02.png)
    Ok_Button.png / Cancel_Button.png / End_Button.png / Close_Button.png
    Rank_Button.png       게임 칸의 랭킹 아이콘 (임시 그림 · 원본 Rank.png 를 넣으면 바뀜)
    Rank_Badge.png        랭킹 아이콘 뒤 동그란 받침 — 칸 번호를 가림 (코드가 만듦)
    Games/                미니게임 아이콘 (Game_01_Jump.png / Game_02_활쏘기.png / Game_03_검강화.png(임시) ...)
    Names/                미니게임 이름표 (Name_01_점프점프.png ...)
  Resources/
    GameFont.ttf          앱 전체가 쓰는 글꼴 (작업 폴더 Font/ 에서 자동으로 들어옴)
    strings.csv           화면에 나오는 글자표 (작업 폴더 strings.csv 에서 들어옴)
    badword.csv           별명 금지어 (작업 폴더 badword.csv 에서 들어옴)
    ArcadeConfig.asset    광고 간격 · 랭킹 · 별명 길이 · 서버 주소
  Scenes/
    TitleScene.unity      앱 시작 지점
    LobbyScene.unity
  Scripts/
    AppFlow.cs            화면 이동의 유일한 통로 (씬 이름이 여기 한 곳에만 있음)
    GameCatalog.cs        미니게임 목록 에셋
    MiniGameEntry.cs      목록 한 줄
    TitleScreen.cs        타이틀 배치 + START
    LobbyScreen.cs        카탈로그를 읽어 칸을 채움
    AspectFitter.cs       배경을 원본 비율로 화면에 맞춤
    ShellUI.cs            비율 기반 배치 도구
    PopupPanel.cs         팝업 한 장 (켜기/끄기 + 열린 팝업 목록)
    ShellMenu.cs          나가기/설정 버튼이 부르는 함수들 + 폰 뒤로 버튼
    StringTable.cs        strings.csv 를 읽어 화면 글자를 꺼내 줌
    PulseScale.cs         버튼 숨쉬기 애니메이션
    PlayerIdentity.cs     PID + 별명 + "서버에 등록됐는지"
    NicknameRules.cs      금지어 거르기 (badword.csv)
    NicknamePopup.cs      별명 창 (처음 정하기 / 바꾸기)
    NicknameLabel.cs      설정 창의 "내 별명 : ..."
    RankingPopup.cs       랭킹 창 (ShowFor(gameId) 로 그 게임의 랭킹을 엶)
    DataResetPopup.cs     치트 — 모든 게임 데이터 초기화 창
    CheatTapZone.cs       치트 — 설정 창의 보이지 않는 누름 영역
    RankingFlow.cs        미니게임에서 판 끝 -> 별명 확인 -> 점수 올리기
    OnlineBoot.cs         앱을 켤 때 랭킹을 서버 구현으로 갈아 끼움
    Online/               서버 통신 (Http / FirebaseAuth / Firestore / MiniJson)
    Ranking/              FirebaseRankingService (서버) · LocalRankingService (폰 안)
    Ads/                  AdGate (띄울 때인지) · AdBreak (길목에서 띄우기) · AdMobService · NoAdService
  Editor/
    ShellArt.cs           작업 폴더 그림 가져오기 + 여백 자르기 + 임포트 설정
    ShellSceneBuilder.cs  씬 자동 생성
    ShellStringsCsv.cs    strings.csv · badword.csv 가져오기 (Tools > Arcade > Apply strings.csv)
    ShellFirebaseConfig.cs google-services.json -> ArcadeConfig 서버 주소
    ShellAdsConfig.cs     ArcadeConfig 의 AdMob 앱 ID -> 광고 플러그인 설정
    ShellPreview.cs       플레이 모드 없이 화면 PNG 캡처
    ShellCheatUI.cs       치트 창 굽기 + 에디터 메뉴 Reset All Game Data
    ArcadeSelfTest.cs     서버 없이 규칙 점검 (51건)
    ArcadeOnlineTest.cs   진짜 서버로 점검 (19건, 끝나면 시험 계정 삭제)
```

## 설계 메모

- **씬을 손으로 편집하지 마세요.** `ShellSceneBuilder` 가 씬을 통째로 굽는 방식이라
  씬에 직접 넣은 변경은 다음 빌드에서 전부 사라집니다.
  화면 구성을 바꾸려면 `ShellSceneBuilder` 를, 배치 숫자만 바꾸려면
  `TitleScreen` / `LobbyScreen` 의 인스펙터 값을 고치세요.
- **로비 칸은 실행할 때 코드로 만듭니다.** 그래야 게임을 추가할 때
  `GameCatalog.asset` 한 줄만 늘리면 되고 씬을 다시 구울 필요가 없습니다.
- **껍데기와 게임은 서로 다른 네임스페이스입니다.** 껍데기는 `Arcade`,
  점프점프는 `JumpJump`, 활쏘기는 `Archery`, 검 강화는 `Enchant`. 게임이 껍데기를 부를 일은
  `AppFlow.GoToLobby()` 하나뿐이고, 게임끼리는 서로를 부르지 않습니다.
- 게임 안에서 "로비로" 버튼을 눌렀을 때 그게 점프로도 세지 않도록,
  `TapInput.OverUI` 가 UI 위의 터치를 걸러 냅니다.

## 아직 없는 것 / 알려진 제약

- **설정 팝업은 아직 [종료] / [닫기] 두 개뿐입니다.** 판 위쪽은 사운드 조절을 넣으려고
  비워 둔 자리입니다. (2026-09-07 에 로비 그림에서 지웠던 톱니바퀴를 2026-09-08 에
  진짜 버튼으로 다시 얹었습니다. 자리는 원래 그림에 그려져 있던 그 자리입니다)
- **한글 글자는 이제 화면에 거의 안 나옵니다.** 타이틀의 게임 이름과 로비의 이름표가
  모두 그림(`Title_Text.png` / `Names/Name_NN_*.png`)으로 바뀌었기 때문입니다.
  글자로 그리는 곳은 미니게임 안의 HUD(전부 영어·숫자)와, 이름표 그림이 없는 게임의 이름뿐입니다.
  **지금은 두 게임 다 이름표 그림이 있어서, 화면에 한글을 글자로 그리는 곳이 없습니다.**
  **2026-09-08 에 한글이 들어 있는 픽셀 글꼴을 앱에 담았으므로 이 위험은 없어졌습니다.**
  (위 "글꼴" 절. 앞으로 한글을 글자로 찍어도 폰에서 그대로 나옵니다)
- 로비에서 폰 뒤로 버튼을 누르면 타이틀로 돌아가지 않고 **바로 종료 확인**이 뜹니다.
  (2026-09-08 에 사용자가 그렇게 정했습니다)
- 로비에서 최고 점수 같은 기록을 보여 주지 않습니다.

## 효과음 (2026-09-14)

- **파일** : 작업 폴더 `Sound/` 에 넣는다. **파일 이름(확장자 빼고) = 소리 이름.** wav / ogg / mp3.
  Build All Scenes 가 `Assets/Shell/Resources/Sounds/` 로 이름 그대로 가져온다 (`ShellArt.SyncSounds`).
  작업 폴더 바로 아래 · `@리소스*` 폴더도 찾지만 `Sound/` 가 맨 뒤라 이긴다. 확장자가 바뀌면 옛 파일을 지운다.
- **부르기** : `Arcade.Sfx.Play(Arcade.Sfx.JumpLand)` — 플레이 모드가 아니면 아무것도 하지 않으므로
  **게임의 `Step` 안에서 불러도 배치 플레이테스트가 그대로 돈다.** 높이(`pitch`) · 늦게(`delay`) 도 줄 수 있다.
- **새 소리** : `Sfx` 에 이름 상수 + `All` 배열에 추가 + 부르는 한 줄. 자체 점검이 `All` 의 파일이 전부 들어왔는지 본다
  (파일 이름을 틀리게 넣으면 거기서 걸린다).
- **새 미니게임** : 버튼 딸깍 소리는 자동(`UiClickSound`). 게임 소리는 게임 이름을 앞에 붙인 파일(`Archery_Hit.wav` 처럼).
- **딸깍 대신 다른 소리** : 버튼의 리스너에서 `Sfx.PlayInsteadOfClick(이름)` (타이틀 START · 로비 게임 칸의 `GameEnter`).
- **크기** : `ArcadeConfig.asset` 의 `sfxVolume`(전체) × `clickVolume`(딸깍만). 설정 창의 음량 조절은 아직 없다.
