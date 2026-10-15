"""캡처를 목표 샷과 수치로 비교한다: HSV 평균 채도·밝기, 밝기 표준편차(대비), 3x3 영역 평균색.
python -I tools/art/stats.py <캡처 폴더> [--grid]
색·노출을 맞출 때는 눈보다 이 수치가 정확했다 (D45~D49).
"""
import os
import sys

from PIL import Image, ImageStat

HERE = os.path.dirname(os.path.abspath(__file__))
TARGETS = os.path.join(HERE, '..', '..', 'docs', 'art', '2026-10-12', 'selected-scenes-v3')
PAIRS = [
    ('01-floating-house-road-shadow.png', 't01_road.png'),
    ('02-inward-leaning-forest.png', 't02_path.png'),
    ('03-empty-forest-room-dolls.png', 't03_room.png'),
    ('04-iron-tree.png', 't04_iron.png'),
    ('05-unsinkable-rocks-submerged-house.png', 't05_lake.png'),
]


def overall(path):
    im = Image.open(path).convert('RGB').resize((320, 180))
    _, s, v = [ImageStat.Stat(c) for c in im.convert('HSV').split()]
    lum = ImageStat.Stat(im.convert('L'))
    return s.mean[0], v.mean[0], lum.stddev[0]


def grid(path):
    im = Image.open(path).convert('RGB').resize((300, 168))
    rows = []
    for r in range(3):
        cells = []
        for c in range(3):
            m = ImageStat.Stat(im.crop((c * 100, r * 56, c * 100 + 100, r * 56 + 56))).mean
            cells.append('%3d,%3d,%3d' % tuple(m))
        rows.append(cells)
    return rows


def main():
    cap = sys.argv[1]
    show_grid = '--grid' in sys.argv
    for t, c in PAIRS:
        cp = os.path.join(cap, c)
        if not os.path.exists(cp):
            continue
        tp = os.path.join(TARGETS, t)
        a, b = overall(tp), overall(cp)
        print('%-14s sat %5.1f vs %5.1f | val %5.1f vs %5.1f | contrast %5.1f vs %5.1f (target vs capture)'
              % (c, a[0], b[0], a[1], b[1], a[2], b[2]))
        if show_grid:
            ga, gb = grid(tp), grid(cp)
            for r in range(3):
                print('  T ' + ' | '.join(ga[r]) + '    O ' + ' | '.join(gb[r]))


if __name__ == '__main__':
    main()
