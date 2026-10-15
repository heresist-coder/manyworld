"""목표 샷(왼쪽)과 캡처(오른쪽)를 장면마다 한 줄로 놓은 비교표.
python -I tools/art/compare.py <캡처 폴더> <출력 png>
"""
import os
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
TARGETS = os.path.join(HERE, '..', '..', 'docs', 'art', '2026-10-12', 'selected-scenes-v3')
PAIRS = [
    ('01-floating-house-road-shadow.png', 't01_road.png'),
    ('03-empty-forest-room-dolls.png', 't03_room.png'),
    ('02-inward-leaning-forest.png', 't02_path.png'),
    ('04-iron-tree.png', 't04_iron.png'),
    ('05-unsinkable-rocks-submerged-house.png', 't05_lake.png'),
]
W, H = 800, 450


def main():
    cap, out = sys.argv[1], sys.argv[2]
    rows = [(t, c) for t, c in PAIRS if os.path.exists(os.path.join(cap, c))]
    sheet = Image.new('RGB', (W * 2, H * len(rows)), 'black')
    for i, (t, c) in enumerate(rows):
        sheet.paste(Image.open(os.path.join(TARGETS, t)).convert('RGB').resize((W, H)), (0, H * i))
        sheet.paste(Image.open(os.path.join(cap, c)).convert('RGB').resize((W, H)), (W, H * i))
    sheet.save(out)
    print('saved', out, len(rows))


if __name__ == '__main__':
    main()
