# -*- coding: utf-8 -*-
"""
BG_01~03 (896x400 가로형) -> 세로로 무한 반복 가능한 896x1400 타일.
2차 시도: 구름 변형 늘리기 / 산 봉우리 끝 복원 / 행성 자국 메우기.
원본은 건드리지 않습니다.
"""
from PIL import Image
from collections import Counter, deque
import os, random

SRC = r"D:\00.JumpJump\JumpJump\Assets\JumpJump\Art\Background"
OUT = r"D:\00.JumpJump\BG_시험"
W, H = 896, 1400
random.seed(20260907)
os.makedirs(OUT, exist_ok=True)
log = []


def load(n):
    return Image.open(os.path.join(SRC, n + ".png")).convert("RGB")


def components(mask, w, y0, y1):
    seen = [[False] * w for _ in range(y1)]
    out = []
    for sy in range(y0, y1):
        for sx in range(w):
            if not mask[sy][sx] or seen[sy][sx]:
                continue
            q = deque([(sx, sy)])
            seen[sy][sx] = True
            pts = []
            while q:
                x, y = q.popleft()
                pts.append((x, y))
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = x + dx, y + dy
                    if y0 <= ny < y1 and 0 <= nx < w and mask[ny][nx] and not seen[ny][nx]:
                        seen[ny][nx] = True
                        q.append((nx, ny))
            out.append(pts)
    return out


def sprite_from(im, pts):
    px = im.load()
    xs = [p[0] for p in pts]
    ys = [p[1] for p in pts]
    x0, x1, y0, y1 = min(xs), max(xs), min(ys), max(ys)
    sp = Image.new("RGBA", (x1 - x0 + 1, y1 - y0 + 1), (0, 0, 0, 0))
    sq = sp.load()
    for x, y in pts:
        sq[x - x0, y - y0] = px[x, y] + (255,)
    return sp


def wrap_paste(canvas, sprite, x, y):
    cw, ch = canvas.size
    for ox in (0, -cw, cw):
        for oy in (0, -ch, ch):
            canvas.paste(sprite, (x + ox, y + oy), sprite)


def scatter(canvas, sprites, want, min_gap=8, tries=120):
    placed, guard = [], 0
    while len(placed) < want and guard < 20000:
        guard += 1
        sp = random.choice(sprites)
        if random.random() < 0.5:
            sp = sp.transpose(Image.FLIP_LEFT_RIGHT)
        sw, sh = sp.size
        spot = None
        for _ in range(tries):
            x, y = random.randrange(W), random.randrange(H)
            box = (x - min_gap, y - min_gap, x + sw + min_gap, y + sh + min_gap)
            hit = False
            for b in placed:
                for ox in (0, -W, W):
                    for oy in (0, -H, H):
                        if not (box[2] + ox < b[0] or box[0] + ox > b[2] or
                                box[3] + oy < b[1] or box[1] + oy > b[3]):
                            hit = True
                            break
                    if hit:
                        break
                if hit:
                    break
            if not hit:
                spot = (x, y)
                break
        if spot is None:
            continue
        wrap_paste(canvas, sp, *spot)
        placed.append((spot[0], spot[1], spot[0] + sw, spot[1] + sh))
    return len(placed)


def feather(band, top_f, bot_f):
    band = band.convert("RGBA")
    bw, bh = band.size
    px = band.load()
    for y in range(bh):
        k = 1.0
        if y < top_f:
            k = min(k, y / top_f)
        if y >= bh - bot_f:
            k = min(k, (bh - 1 - y) / bot_f)
        k = k * k * (3 - 2 * k)                  # 부드럽게
        for x in range(bw):
            r, g, b, a = px[x, y]
            px[x, y] = (r, g, b, int(a * k))
    return band


def seam(img):
    """(맨 아랫줄->맨 윗줄 차이) 를 (그림 안쪽 이웃 줄끼리의 평균 차이) 와 비교합니다.
    비율이 1 근처면 이음매가 다른 곳과 구별되지 않는다 = 안 보인다는 뜻입니다."""
    px = img.convert("RGB").load()
    w, h = img.size
    wrap = sum(abs(px[x, 0][i] - px[x, h - 1][i]) for x in range(w) for i in range(3)) / w / 3
    tot = 0.0
    for y in range(0, h - 1, 7):
        tot += sum(abs(px[x, y][i] - px[x, y + 1][i]) for x in range(0, w, 3) for i in range(3))
    inner = tot / (len(range(0, h - 1, 7)) * len(range(0, w, 3)) * 3)
    return wrap, inner


# ==================================================================
# BG_01  평야 하늘
# ==================================================================
im = load("BG_01")
px = im.load()
w, h = im.size
SKY = 238

cnt = Counter()
mask = [[False] * w for _ in range(h)]
for y in range(SKY):
    for x in range(w):
        p = px[x, y]
        if p[0] > 140:
            mask[y][x] = True
        else:
            cnt[p] += 1
sky1 = cnt.most_common(1)[0][0]

allc = components(mask, w, 0, SKY)
whole = [c for c in allc if len(c) >= 25
         and min(p[1] for p in c) > 0 and max(p[1] for p in c) < SKY - 1]
log.append("BG_01 구름 덩어리 %d개 중 온전한 것 %d개" % (len(allc), len(whole)))

base = [sprite_from(im, c) for c in whole]
if not base:                                     # 안전망
    base = [sprite_from(im, max(allc, key=len))]

# 온전한 구름이 적으므로 조각내고 겹쳐서 변형을 만듭니다
variants = []
for sp in base:
    sw, sh = sp.size
    variants.append(sp)
    variants.append(sp.resize((sw * 2, sh * 2), Image.NEAREST))          # 큰 구름
    variants.append(sp.resize((max(6, sw // 2), max(4, sh // 2)), Image.NEAREST))  # 멀리 있는 작은 구름
    cluster = Image.new("RGBA", (int(sw * 1.7), sh + 4), (0, 0, 0, 0))   # 뭉친 구름
    cluster.paste(sp, (0, 4), sp)
    cluster.paste(sp.transpose(Image.FLIP_LEFT_RIGHT), (int(sw * 0.7), 0),
                  sp.transpose(Image.FLIP_LEFT_RIGHT))
    variants.append(cluster)
variants = [v for v in variants if v.size[0] > 3 and v.size[1] > 3]

c1 = Image.new("RGBA", (W, H), sky1 + (255,))
n1 = scatter(c1, variants, 26, min_gap=12)
c1 = c1.convert("RGB")
log.append("BG_01 하늘색 %s / 구름 변형 %d종 / %d개 배치" % (sky1, len(variants), n1))

# ==================================================================
# BG_02  산  (잘린 봉우리 끝을 복원)
# ==================================================================
im = load("BG_02")
px = im.load()
BOT = 311

def is_rock(p):
    r, g, b = p
    return b < r * 1.22 and not (min(p) > 170)

def is_cloud(p):
    return min(p) > 170

cnt = Counter()
feat = [[False] * w for _ in range(h)]
for y in range(BOT):
    for x in range(w):
        p = px[x, y]
        if is_rock(p) or is_cloud(p):
            feat[y][x] = True
        elif y < 140:
            cnt[p] += 1
sky2 = cnt.most_common(1)[0][0]

# 위쪽 끝에 실제 '바위'로 닿아 있는 열 구간을 찾습니다
rock0 = [is_rock(px[x, 0]) for x in range(w)]
runs, s = [], None
for x in range(w):
    if rock0[x]:
        if s is None:
            s = x
    elif s is not None:
        runs.append((s, x - 1))
        s = None
if s is not None:
    runs.append((s, w - 1))
runs = [r for r in runs if r[1] - r[0] >= 4]
TIP = min(110, max(int((b - a + 1) * 0.8) for a, b in runs)) if runs else 0
log.append("BG_02 위쪽 끝에 잘린 봉우리 %d개 %s -> 끝을 %dpx 세워 복원" %
           (len(runs), runs, TIP))

drop = 0
for c in components(feat, w, 0, BOT):
    if len(c) < 60:                              # 하늘에 떠 있는 자잘한 조각
        drop += 1
        for x, y in c:
            feat[y][x] = False
log.append("BG_02 하늘에 떠 있던 조각 %d개 제거" % drop)

band = Image.new("RGBA", (w, BOT + TIP), (0, 0, 0, 0))
bq = band.load()
for y in range(BOT):
    for x in range(w):
        if feat[y][x]:
            bq[x, y + TIP] = px[x, y] + (255,)

# 잘린 자리마다 삼각 봉우리를 세웁니다 (색은 원본 맨 윗줄에서 가져옴)
for a, b in runs:
    width = b - a + 1
    th = min(TIP, max(8, int(width * 0.8)))
    cx = (a + b) / 2.0
    for i in range(1, th + 1):
        frac = 1.0 - i / float(th)
        half = width * frac / 2.0 + random.uniform(-0.8, 0.8)
        x0, x1 = int(round(cx - half)), int(round(cx + half))
        if x1 < x0:
            continue
        yy = TIP - i
        span = max(1, x1 - x0)
        for x in range(max(0, x0), min(w, x1 + 1)):
            sx = a + int((x - x0) / span * (b - a))
            bq[x, yy] = px[max(a, min(b, sx)), 0] + (255,)

# 원본 아래쪽 흰 구름
cm = [[False] * w for _ in range(h)]
for y in range(315, h):
    for x in range(w):
        if is_cloud(px[x, y]):
            cm[y][x] = True
cc = [c for c in components(cm, w, 315, h)
      if len(c) >= 40 and max(p[1] for p in c) < h - 1]
clouds2 = [sprite_from(im, c) for c in cc]

BAND_Y = 1000 - (BOT + TIP)
c2 = Image.new("RGBA", (W, H), sky2 + (255,))
n2 = scatter(c2, clouds2, 9, min_gap=14) if clouds2 else 0
wrap_paste(c2, band, 0, BAND_Y)
c2 = c2.convert("RGB")
log.append("BG_02 하늘색 %s / 산맥 띠 %dx%d / 구름 %d개" % (sky2, w, BOT + TIP, n2))

# ==================================================================
# BG_03  우주
# ==================================================================
im = load("BG_03")
px = im.load()
T, B = 26, 192
EDGE = 30                                        # 원본 사방의 장식 테두리 폭

cnt = Counter()
for y in range(28, 62):
    for x in range(w):
        cnt[px[x, y]] += 1
space = cnt.most_common(1)[0][0]
if sum(space) < 12:
    space = (5, 4, 22)                           # 완전 검정은 피합니다

neb = im.crop((EDGE, T, w - EDGE, B)).convert("RGB")
nq = neb.load()
nh = B - T
nw = neb.size[0]

# 행성 찾기: 성운은 파랑이 가장 센 색인데, 행성(초록/갈색)은 그렇지 않습니다.
# 별도 조건에 걸리지만 별은 1~6px 라 크기로 걸러집니다.
pm = [[False] * nw for _ in range(nh)]
for y in range(nh):
    for x in range(nw):
        r, g, bb = nq[x, y]
        if max(r, g) >= bb and (r + g + bb) > 40:
            pm[y][x] = True
plan = [c for c in components(pm, nw, 0, nh) if len(c) >= 15]

# 지울 영역 = 행성을 감싸는 네모 + 둘레 14px
# (행성 가장자리의 옅은 띠까지 지워야 메운 자리가 갈색으로 번지지 않습니다)
erase = [[False] * nw for _ in range(nh)]
for c in plan:
    xs = [q[0] for q in c]
    ys = [q[1] for q in c]
    for x in range(max(0, min(xs) - 14), min(nw, max(xs) + 15)):
        for y in range(max(0, min(ys) - 14), min(nh, max(ys) + 15)):
            erase[y][x] = True

# 바깥쪽부터 이웃 색 평균으로 안쪽을 메웁니다 (성운처럼 뿌연 그림에 잘 맞습니다)
todo = [(x, y) for y in range(nh) for x in range(nw) if erase[y][x]]
for _ in range(60):
    if not todo:
        break
    done = []
    for x, y in todo:
        acc = [0, 0, 0]
        n = 0
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                nx, ny = x + dx, y + dy
                if 0 <= nx < nw and 0 <= ny < nh and not erase[ny][nx]:
                    c = nq[nx, ny]
                    acc[0] += c[0]; acc[1] += c[1]; acc[2] += c[2]
                    n += 1
        if n:
            nq[x, y] = (acc[0] // n, acc[1] // n, acc[2] // n)
            done.append((x, y))
    for x, y in done:
        erase[y][x] = False
    todo = [p for p in todo if p not in set(done)]
log.append("BG_03 우주색 %s / 지운 행성 %d개 (주변 성운색으로 메움)" % (space, len(plan)))

bm = [[False] * w for _ in range(h)]
for y in range(T, B):
    for x in range(EDGE, w - EDGE):
        if sum(px[x, y]) / 3 > 95:
            bm[y][x] = True
stars = [sprite_from(im, c) for c in components(bm, w, T, B) if 1 <= len(c) <= 6]

c3 = Image.new("RGBA", (W, H), space + (255,))
n3 = scatter(c3, stars, 900, min_gap=2) if stars else 0

big = neb.resize((nw * 2, nh * 2), Image.NEAREST)
lay = Image.new("RGBA", (W, H), (0, 0, 0, 0))
for xoff, ypos, flip in ((300, 90, False), (max(0, nw * 2 - W - 80), 790, True)):
    nb = big.crop((xoff, 0, xoff + W, big.height))
    if flip:
        nb = nb.transpose(Image.FLIP_LEFT_RIGHT)
    fb = feather(nb, 150, 150)
    for oy in (0, -H, H):
        lay.paste(fb, (0, ypos + oy), fb)
c3.alpha_composite(lay)
c3 = c3.convert("RGB")
log.append("BG_03 별 %d종 %d개 / 성운 띠 2줄" % (len(stars), n3))

# ==================================================================
res = []
for name, img in (("BG_01", c1), ("BG_02", c2), ("BG_03", c3)):
    img.save(os.path.join(OUT, name + ".png"))
    rolled = Image.new("RGB", (W, H))
    rolled.paste(img.crop((0, H // 2, W, H)), (0, 0))
    rolled.paste(img.crop((0, 0, W, H // 2)), (0, H // 2))
    rolled.crop((60, 0, 836, H)).save(os.path.join(OUT, name + "_이음매확인.png"))
    two = Image.new("RGB", (W, H * 2))
    two.paste(img, (0, 0))
    two.paste(img, (0, H))
    two.crop((60, 0, 836, H * 2)).resize((388, H), Image.LANCZOS).save(
        os.path.join(OUT, name + "_2장반복.png"))
    res.append((name,) + seam(img))

for line in log:
    print(line)
print()
print("%-7s %10s %10s %8s" % ("파일", "이음매차", "보통 줄간차", "비율"))
for n, wr, inr in res:
    print("%-7s %10.2f %10.2f %8.2f" % (n, wr, inr, wr / max(0.01, inr)))
print("\n저장 위치: " + OUT)
