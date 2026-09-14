# 활쏘기! — 미니게임 2번

기획서: 작업 폴더의 `@활쏘기 기획서.png` (PRECISION ARCHER) + 손스케치 `Game_02.png`
네임스페이스는 `Archery`. 점프점프(`JumpJump`)와 서로 참조하지 않습니다.

## 규칙 한 장 요약

| 항목 | 규칙 |
| --- | --- |
| 조작 | **터치 / 마우스 좌클릭 / 스페이스바 = 화살 발사** 하나뿐 |
| 활 | 화면 아래 **가운데 고정**. 좌우로 움직이지 않습니다 |
| 화살 | 항상 **수직으로** 날아갑니다 |
| 과녁 | 화면 위에서 **좌우로 왕복**. 벽에 닿으면 방향을 바꿉니다 |
| 점수 | 링 5개. **가운데 5점, 한 링 바깥으로 갈 때마다 1점씩 줄어 가장자리 1점** |
| 화살 수 | 5발. 쏠 때마다 1 줄고, **맞히면 최대치로 가득 찬다** |
| 과녁 크기 | 맞힐 때마다 **1% 씩 작아진다** (하한 35%) |
| 게임 오버 | **화살이 다 떨어지면.** 즉 연속으로 5발을 빗나가면 끝 |
| 최고 점수 | `PlayerPrefs["Archery.BestScore"]` 에 저장 |

게임 오버 판정에 한 가지 단서가 있습니다. 화살이 0발이 되어도 **아직 날아가는 중인 화살이
있으면 기다립니다.** 마지막 한 발이 명중하면 화살이 다시 차기 때문입니다.
(`ArrowPool.PendingCount` 가 그 "아직 결과가 안 난 화살" 수입니다)

## 난이도는 `archery.csv` 가 원본입니다

점프점프가 높이(m)로 구간을 나누듯, 활쏘기는 **누적 명중 수**로 구간을 나눕니다.

- 사용자가 고치는 파일: `D:\00.JumpJump\archery.csv`
- 게임이 읽는 사본: `Assets/Archery/archery.csv` (TextAsset)
- 바깥 파일이 더 새로우면 `ArcheryStageCsv.Sync()` 가 자동으로 복사합니다.
  Build / Playtest / Preview 앞에서 불리므로 **엑셀에서 저장만 하면 반영됩니다.**
- `ArcheryConfig.stageBands` 는 **읽은 결과를 구워 둔 거울**입니다. 원본이 아닙니다.
  난이도 수치를 바꿔 달라는 요청이 오면 **CSV 를 고치세요.**

### 열 설명

| 열 | 뜻 | 없으면 |
| --- | --- | --- |
| `Index` | 사람이 보기 위한 번호. 게임은 읽지 않습니다 | — |
| `Hits_Min` | 구간 시작(참고용). 게임은 읽지 않습니다 | — |
| **`Hits_Max`** | **누적 명중 수가 이 값까지면 이 구간.** 이 열만 필수입니다 | 읽기 실패 |
| `Target_Speed` | 과녁 좌우 이동 속도 (월드 단위/초) | 1.6 |
| `Shrink_Percent` | 명중 1회당 과녁이 작아지는 비율(%) | 1 |
| `Size_Min_Percent` | 과녁 크기 하한(%) | 35 |
| `Ammo_Max` | 화살 최대 수 | 5 |
| `Arrow_Speed` | 화살 속도 (월드 단위/초) | 16 |

**표를 다루는 규칙은 점프점프의 `config.csv` 와 같습니다.**

- 열은 **이름으로** 찾습니다. 순서를 바꾸거나 메모 열을 끼워 넣어도 됩니다.
- 대소문자 / 공백 / 밑줄 / 하이픈은 무시합니다 (`Target_Speed` = `targetSpeed` = `Target Speed`).
- **빈 칸은 바로 위 줄 값을 이어받습니다.** 바뀌는 칸만 채워도 됩니다.
- 엑셀이 붙이는 BOM / CRLF / 따옴표를 모두 처리합니다.
- `Hits_Max` 를 못 읽는 줄(빈 줄, 메모 줄)은 조용히 건너뜁니다.

### 나중에 열을 추가할 때

파서는 `Scripts/StageTable.cs` 입니다. 새 수치를 넣으려면 세 군데만 고치면 됩니다.

1. `StageTable` 위쪽의 별칭 배열에 열 이름을 추가 (`static readonly string[] ...Aliases`)
2. `ArcheryConfig.cs` 의 `StageBand` 클래스에 필드 추가
3. `StageTable.Parse` 안에서 그 값을 읽어 `StageBand` 에 넣는 줄 하나 추가

기존 CSV 는 그대로 열려야 합니다 — 새 열이 없으면 기본값으로 채워지기 때문입니다.

## 만들고 확인하기

| 메뉴 | 하는 일 |
| --- | --- |
| **Tools > Arcade > Build All Scenes** | 타이틀 / 로비 / 점프점프 / 활쏘기 4장을 한 번에. **평소엔 이것만** |
| Tools > Archery > Build Archery Scene | 활쏘기 씬만 다시 굽습니다 |
| Tools > Archery > Apply archery.csv | 지금 CSV 값을 읽어 로그에 표로 찍고 인스펙터에 굽습니다 |
| Tools > Archery > Run Headless Playtest | 플레이 모드 없이 봇 2종으로 돌려 봅니다 |
| Tools > Archery > Capture Preview PNG | 플레이 모드 없이 화면 4장을 PNG 로 뽑습니다 |
| Tools > Archery > Setup Art Assets | 그림의 여백을 자르고 PPU·피벗을 다시 계산합니다 (Build 가 알아서 부릅니다) |
| Tools > Archery > Regenerate Placeholder Art | **임시 그림을 다시 그립니다. 진짜 아트를 덮어쓰므로 주의** |

배치 모드(에디터를 켜지 않고)로도 같습니다.

```bash
UNITY="C:/Program Files/Unity Hub/6000.0.81f1/Editor/Unity.exe"
PROJ="D:/00.JumpJump/JumpJump"

"$UNITY" -batchmode -quit -projectPath "$PROJ" \
  -executeMethod Archery.EditorTools.ArcheryPlaytest.Run -logFile <로그>

"$UNITY" -batchmode -quit -projectPath "$PROJ" \
  -executeMethod Archery.EditorTools.ArcheryPreview.Capture -logFile <로그>
```

### 자동 플레이테스트가 보는 것

봇 두 마리를 돌립니다. `ArcheryGame.Step(dt, tap)` 을 직접 호출하므로 **실제 게임 코드 그대로**입니다.

1. **조준 봇** — 화살이 과녁 높이에 닿는 시점의 과녁 위치를 예측해서 한가운데일 때만 쏩니다.
   "잘 쏘면 판이 계속 이어지는가 / 과녁이 하한까지 작아지는가 / 구간이 올라가는가"를 봅니다.
2. **일부러 빗맞히는 봇** — 착탄 지점이 과녁 밖일 때만 쏩니다.
   "빗나가면 화살이 떨어져 판이 제대로 끝나는가"를 봅니다. 운에 기대지 않습니다.

## 그림 — **그냥 덮어쓰면 됩니다**

`Art/` 의 같은 이름 파일을 덮어쓰고 Build 를 돌리면 끝입니다.
**픽셀 크기가 몇이든, 여백이 얼마나 있든 상관없습니다.**
`Editor/ArcheryArtSetup.cs` 가 여백을 잘라 내고 PPU·피벗을 다시 계산해 주기 때문입니다.

| 파일 | 무엇 | 크기를 무엇에 맞추나 | 피벗 |
| --- | --- | --- | --- |
| `Art/Target.png` | 과녁 | (매 프레임 `targetRadius` 로 다시 잽니다) | 한가운데 |
| `Art/Arrow.png` | 화살 | 세로 길이 = `arrowLength` | 아래끝 가운데 |
| `Art/Bow.png` | 활 | 가로 폭 = `bowWidth` | 아래끝 가운데 (밑동이 `bowY` 에) |
| `Art/Sky.png` | 배경 하늘 | 화면을 덮도록 늘립니다 | 한가운데 |
| `Art/ui_round.png` | HUD 라운드 사각형 | 9-slice 테두리 24px | — |

**과녁 그림만 조건이 하나 있습니다: 원이 그림 한가운데에 오게 그려 주세요.**
과녁의 x 위치가 곧 명중 판정이라, 원이 한쪽으로 치우쳐 있으면
보이는 곳과 맞는 곳이 달라집니다. (주변 여백은 잘라 내므로 신경 쓰지 않아도 됩니다)

> 왜 이런 장치가 필요하냐면 — 스프라이트의 세계 크기는 `픽셀 수 ÷ PPU` 로 정해집니다.
> PPU 를 고정해 두면 320px 짜리 임시 그림 자리에 2400px 짜리 진짜 그림을 넣는 순간
> 활이 화면을 뒤덮습니다. 그래서 **원하는 세계 크기를 먼저 정하고 PPU 를 역산**합니다.
> 점프점프의 `JumpJumpArtSetup` 과 같은 방식입니다.

처음 한 번은 `ArcheryArtGenerator` 가 **임시 그림을 코드로 그려서** 넣어 둡니다.
파일이 이미 있으면 건드리지 않으므로, 진짜 그림을 덮어써 두면 안전합니다.

로비 칸의 아이콘과 이름표는 껍데기 쪽입니다.
`Assets/Shell/Art/Games/Game_02_활쏘기.png` (아이콘) 과
`Assets/Shell/Art/Names/Name_02_활쏘기.png` (이름표) 를 덮어쓰면 됩니다.
작업 폴더에 `@Game_02_활쏘기.png` / `@Name_02_활쏘기.png` 로 넣어도 되고, 그 편이
원본이 남아서 더 안전합니다. **이름표는 가로:세로 약 2.15:1** 로 그려 주세요
(로비의 이름표는 게임마다 크기가 같아야 해서 정해진 칸에 맞춰 그립니다).

## 숫자를 어디서 고치나

| 바꾸고 싶은 것 | 고칠 곳 |
| --- | --- |
| 과녁 속도 / 축소율 / 화살 수 / 화살 속도 | **`D:\00.JumpJump\archery.csv`** |
| 과녁이 오가는 좌우 범위 | `ArcheryConfig.playHalfWidth` |
| 과녁 높이 / 활 높이 | `ArcheryConfig.targetY` / `bowY`(활 밑동) / `arrowStartY` |
| 활 그림의 크기 / 화살 길이 | `ArcheryConfig.bowWidth` / `arrowLength` (그림이 바뀌어도 이 크기가 유지됩니다) |
| 과녁 기본 크기 | `ArcheryConfig.targetRadius` (100% 일 때의 반지름) |
| 링 개수 (= 가운데 점수) | `ArcheryConfig.rings` |
| 과녁이 납작해 보이는 정도 | `ArcheryConfig.targetFlatten` (**판정에는 영향 없음**) |
| 연타 간격 | `ArcheryConfig.fireCooldown` — **한 발당 0.5초** (2026-09-14 사용자 요청, 예전 0.12) |
| 활 반동 / 과녁 움찔 / "+5 PT" 표시 시간 | `ArcheryConfig` 의 "연출" 항목 |

## 구조

```
Assets/Archery/
  archery.csv               난이도 구간표 (원본은 작업 폴더 쪽)
  ArcheryConfig.asset       튜닝 값 + CSV 를 읽은 결과(거울)
  Art/                      과녁 / 화살 / 활 / 하늘 (덮어쓰면 크기는 자동으로 맞춥니다)
  Scenes/ArcheryScene.unity
  Scripts/
    ArcheryGame.cs          규칙 전부. Step(dt, tap) 이 입구입니다
    ArcheryConfig.cs        튜닝 값 + StageBand
    StageTable.cs           archery.csv 파서
    TargetController.cs     과녁 이동 / 크기 / 반지름
    ArrowPool.cs            화살 발사 · 비행 · 명중 판정 · 회수
    BowController.cs        활 (고정) + 발사 반동
    ArcheryHud.cs           점수 / 화살 / 과녁 크기 / 오버레이 / LOBBY 버튼
    TapInput.cs             터치·클릭·스페이스바를 하나로
  Editor/
    ArcherySceneBuilder.cs  씬 자동 생성
    ArcheryArtSetup.cs      그림 여백 자르기 + PPU/피벗 자동 계산 (그림 교체 대응)
    ArcheryArtGenerator.cs  임시 그림 PNG 생성 (파일이 없을 때만)
    ArcheryStageCsv.cs      archery.csv 동기화 + Apply 메뉴
    ArcheryPlaytest.cs      봇 자동 플레이테스트
    ArcheryPreview.cs       플레이 모드 없이 화면 PNG 캡처
```

## 설계 메모

- **씬을 손으로 편집하지 마세요.** `ArcherySceneBuilder` 가 씬을 통째로 굽는 방식이라
  씬에 직접 넣은 변경은 다음 빌드에서 전부 사라집니다.
- **물리 엔진을 쓰지 않습니다.** 점프점프와 같은 원칙입니다.
  화살은 y 를 직접 적분하고, 명중은 "이번 프레임에 촉이 과녁 높이를 통과했는가"를
  수식으로 판정합니다(`ArrowPool.Tick`). 그래서 프레임이 튀어도 과녁을 뚫고 지나가지 않습니다.
- **명중 판정은 x 거리 하나로 끝납니다.** 화살이 수직으로만 날기 때문에,
  통과 순간의 `|화살x - 과녁x|` 를 반지름으로 나누면 몇 번째 링인지 바로 나옵니다
  (`ArcheryConfig.PointsForOffset`). 과녁이 타원으로 납작해 보이는 것은 그림뿐입니다.
- **화살은 풀(pool)로 돌려 씁니다.** 매번 Instantiate 하면 폰에서 끊깁니다.
- **`Step(dt, tap)` 이 게임의 유일한 입구입니다.** 입력과 델타타임을 인자로 받기 때문에
  플레이 모드 없이 배치 모드에서 그대로 재생할 수 있습니다. 점프점프와 같은 구조입니다.
- **`TapInput` 은 점프점프에도 같은 것이 있습니다.** 미니게임끼리 서로 참조하지 않는다는
  규칙 때문에 각자 하나씩 가지고 있습니다. 게임이 더 늘어나면 껍데기(`Arcade`) 쪽으로
  옮겨 공용으로 쓰는 편이 낫습니다.

## 화면 오른쪽 위의 뒤로가기 화살표

게임 중에도 항상 보이고, 누르면 **"로비로 나가시겠습니까?"** 팝업이 뜹니다.
확인을 누르면 로비로 나갑니다 — 기획서대로 **진행 상황은 저장하지 않습니다.**

**이 버튼과 팝업은 껍데기가 통째로 만듭니다** (`ShellSceneBuilder.MakeExitToLobbyUI(root)` 한 줄).
점프점프와 자리·모양이 같고, 고칠 일이 있으면 그 함수 한 곳만 고치면 됩니다.
**활쏘기 쪽에는 관련 코드가 한 줄도 없습니다.**

팝업이 떠 있는 동안에는 `ArcheryGame.Update` 가 `Arcade.PopupPanel.AnyOpen` 을 보고
게임을 멈춥니다. 고민하는 사이에 과녁이 지나가면 안 되니까요.
`Step(dt, tap)` 자체는 팝업을 모르기 때문에 배치 모드 플레이테스트는 그대로 돕니다.

버튼을 누른 것이 발사로 세지 않는 것은 `TapInput.OverUI` 가 막아 줍니다.
자세한 내용은 `Assets/Shell/README.md` 의 "나가기 / 설정 팝업" 절.

## 아직 없는 것

사운드 / 파티클 / 맞은 화살이 과녁에 꽂혀 남는 연출 / 바람 같은 방해 요소 /
과녁 여러 개 / 시간 제한. 지금은 맞은 화살이 그냥 사라집니다.
