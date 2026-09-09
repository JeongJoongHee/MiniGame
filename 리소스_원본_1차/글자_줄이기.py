# -*- coding: utf-8 -*-
"""@리소스 추가_1차 의 그림 안에 그려진 글자를 작게 줄인다 (기본 0.7배).
   글자만 떼어내고(배경과의 색 차이로 판정), 그 자리를 주변 배경으로 메운 뒤,
   지정한 배율로 줄인 글자를 같은 중심에 다시 얹는다. 판/테두리 그림은 건드리지 않는다."""
from PIL import Image
import numpy as np, os, sys

SRC = r"D:/00.JumpJump/@리소스 추가_1차"
SCALE = 0.7   # 글자 배율. 명령줄 두 번째 인자로도 줄 수 있다

# 그림마다 "판 안쪽"(테두리·장식을 뺀 영역)과 배경 표본으로 쓸 깨끗한 세로 띠
JOBS = {
    'Ok_Button.png':     dict(interior=(430, 360, 2010, 960), strips=[(430, 720), (1730, 2010)]),
    'Cancel_Button.png': dict(interior=(430, 360, 2010, 960), strips=[(430, 720), (1730, 2010)]),
    'Close_Button.png':  dict(interior=(430, 360, 2010, 960), strips=[(430, 720), (1730, 2010)]),
    'End_Button.png':    dict(interior=(430, 360, 2010, 960), strips=[(430, 720), (1730, 2010)]),
    'Popup_01.png':      dict(interior=(680, 330, 1720, 850),  strips=[(682, 712), (1706, 1718)], fill='flat'),
    'Popup_02.png':      dict(interior=(680, 330, 1720, 850),  strips=[(682, 712), (1706, 1718)], fill='flat'),
}

T0, T1 = 22.0, 55.0   # 배경과의 색 차이: 이 사이에서 부드럽게 글자로 친다

def row_bg(a, strips, y0, y1):
    cols = np.concatenate([a[y0:y1+1, s0:s1, :3] for s0, s1 in strips], axis=1)
    return np.median(cols, axis=1)            # (rows,3) 줄마다의 배경색

def analyze(f, job):
    a = np.array(Image.open(os.path.join(SRC, f)).convert('RGBA')).astype(np.float64)
    x0, y0, x1, y1 = job['interior']
    bg = row_bg(a, job['strips'], y0, y1)
    region = a[y0:y1+1, x0:x1+1, :3]
    diff = np.abs(region - bg[:, None, :]).max(axis=2)
    alpha = np.clip((diff - T0) / (T1 - T0), 0, 1)
    solid = alpha > 0.6
    bx0, bx1 = span(solid.mean(axis=0))
    by0, by1 = span(solid.mean(axis=1))
    bb = (x0 + bx0, y0 + by0, x0 + bx1, y0 + by1)
    return a, alpha, bb

def span(prof, pad=8, margin=0.03):
    """글자가 실제로 들어찬 구간만 잘라낸다.
       판 가장자리(테두리 빛·장식)는 밀도가 낮으므로 문턱값으로 걸러진다."""
    n = len(prof)
    m = int(n * margin)
    idx = np.where(prof > max(0.04, 0.20 * prof[m:n-m].max()))[0]
    idx = idx[(idx >= m) & (idx < n - m)]
    return max(0, idx.min() - pad), min(n - 1, idx.max() + pad)

def tile_fill(a, bb, job, alpha):
    """글자 자리를 주변 배경으로 메운다.
       버튼(strip): 줄마다 옆의 깨끗한 배경을 좌우로 반복해 무늬(점선·명암)를 살린다.
       팝업(flat) : 판 안이 고른 색이라 줄마다의 배경색으로 평평하게 채운다."""
    ax0, ay0, ax1, ay1 = job['interior']
    flat = job.get('fill') == 'flat'
    # 메운 자리는 원래 글자보다 넉넉히 넓어야 한다 (가장자리를 섞는 폭 + 여유)
    px, py = (44, 44) if flat else (70, 44)
    fx, fy = (10, 10) if flat else (40, 20)
    x0, y0 = max(ax0, bb[0]-px), max(ay0, bb[1]-py)
    x1, y1 = min(ax1, bb[2]+px), min(ay1, bb[3]+py)
    if flat:
        rows = a[y0:y1+1, ax0:ax1+1]
        clean = alpha[y0-ay0:y1-ay0+1, :] < 0.1
        plate = np.empty((y1-y0+1, x1-x0+1, 4))
        for i in range(y1-y0+1):
            sel = rows[i][clean[i]]
            plate[i, :] = np.median(sel if len(sel) > 50 else rows[i], axis=0)
    else:
        s0, s1 = job['strips'][0]
        src = a[y0:y1+1, s0:s1].copy()
        reps = int(np.ceil((x1-x0+1) / src.shape[1])) + 1
        parts = [src if i % 2 == 0 else src[:, ::-1] for i in range(reps)]
        plate = np.concatenate(parts, axis=1)[:, :x1-x0+1]
        # 왼쪽 띠를 반복해 채우므로 오른쪽 끝의 밝기가 미세하게 어긋난다.
        # 왼쪽->오른쪽으로 밝기 차이를 서서히 더해 이음매가 선으로 보이지 않게 한다.
        t0, t1 = job['strips'][1]
        ml = a[y0:y1+1, s0:s1, :3].mean(axis=1)
        mr = a[y0:y1+1, t0:t1, :3].mean(axis=1)
        ramp = np.linspace(0, 1, x1-x0+1)[None, :, None]
        plate[..., :3] += (mr - ml)[:, None, :] * ramp
    out = a.copy()
    # 메운 자리의 가장자리는 원래 그림과 부드럽게 섞는다 (이음매가 선으로 보이지 않도록)
    h, w = plate.shape[:2]
    wx = np.clip(np.minimum(np.arange(w), w - 1 - np.arange(w)) / fx, 0, 1)
    wy = np.clip(np.minimum(np.arange(h), h - 1 - np.arange(h)) / fy, 0, 1)
    wt = (wx[None, :] * wy[:, None])[..., None]
    out[y0:y1+1, x0:x1+1] = plate * wt + out[y0:y1+1, x0:x1+1] * (1 - wt)
    return out, (x0, y0, x1, y1)

def run(f, job, outdir):
    a, alpha, bb = analyze(f, job)
    ix0, iy0 = job['interior'][0], job['interior'][1]
    # 글자 레이어 (bbox 만큼 잘라서 premultiplied 로 축소)
    x0, y0, x1, y1 = bb
    crop = a[y0:y1+1, x0:x1+1, :3]
    al = alpha[y0-iy0:y1-iy0+1, x0-ix0:x1-ix0+1]
    lay = np.dstack([crop * al[..., None], al * 255.0]).astype(np.uint8)
    li = Image.fromarray(lay, 'RGBA')
    nw, nh = max(1, round(li.width*SCALE)), max(1, round(li.height*SCALE))
    li = li.resize((nw, nh), Image.LANCZOS)
    small = np.array(li).astype(np.float64)
    sa = small[..., 3:4] / 255.0
    srgb = np.divide(small[..., :3], np.where(sa > 0, sa, 1))   # unpremultiply
    # 배경 메우기
    plate, filled = tile_fill(a, bb, job, alpha)
    # 같은 중심에 다시 얹기
    cx, cy = (x0+x1)/2.0, (y0+y1)/2.0
    px, py = int(round(cx - nw/2.0)), int(round(cy - nh/2.0))
    dst = plate[py:py+nh, px:px+nw]
    dst[..., :3] = srgb * sa + dst[..., :3] * (1 - sa)
    dst[..., 3] = np.maximum(dst[..., 3], small[..., 3])
    plate[py:py+nh, px:px+nw] = dst
    res = Image.fromarray(np.clip(plate, 0, 255).astype(np.uint8), 'RGBA')
    res.save(os.path.join(outdir, f))
    print(f'{f:20s} 글자 {x1-x0+1}x{y1-y0+1} -> {nw}x{nh}  (자리 {filled})')

if __name__ == '__main__':
    outdir = sys.argv[1]
    if len(sys.argv) > 2:
        SCALE = float(sys.argv[2])
    os.makedirs(outdir, exist_ok=True)
    for f, job in JOBS.items():
        run(f, job, outdir)
