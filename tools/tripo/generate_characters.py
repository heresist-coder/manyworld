"""Tripo로 인물 두 명 생성 + Mixamo 뼈대 자동 리깅 (docs/art/2026-10-13-characters/README.md).
API 키는 숨김 입력으로만 받고 파일·로그에 남기지 않는다. 잔액을 보여 준 뒤 GENERATE를 쳐야 제출한다 (유료 크레딧).
작업 ID와 해시는 매니페스트에 남겨 중간에 끊겨도 다시 실행하면 이어서 받는다."""
import getpass, hashlib, json, pathlib, sys, time
from urllib.parse import urlparse
import requests

root = pathlib.Path(__file__).resolve().parents[2]
doc = root / 'docs/art/2026-10-13-characters'
mp = doc / 'tripo-manifest.json'
out = root / 'Assets/ThirdParty/Characters/Tripo20261013'
JOBS = [
    {'key': '01-driver', 'input': 'inputs/01-driver.png', 'height': 1.78},
    {'key': '02-eunju', 'input': 'inputs/02-eunju.png', 'height': 1.66},
]
# 면 8,000(쿼드, 삼각형 약 1만 6천), 기본색 텍스처만 (PBR 끔) — 사양과 이유는 README
GEN = {'type': 'image_to_model', 'model_version': 'v3.1-20260211', 'face_limit': 8000, 'quad': True,
       'texture': True, 'pbr': False, 'texture_quality': 'detailed', 'orientation': 'align_image'}
RIG = {'type': 'animate_rig', 'out_format': 'fbx', 'rig_type': 'biped', 'spec': 'mixamo'}
base = 'https://api.tripo3d.ai/v2/openapi'

m = json.loads(mp.read_text(encoding='utf-8')) if mp.exists() else {'created': '2026-10-13', 'jobs': JOBS}


def save():
    mp.write_text(json.dumps(m, indent=2, ensure_ascii=False), encoding='utf-8')


key = getpass.getpass('Tripo API key (hidden): ')
s = requests.Session()
s.headers['Authorization'] = 'Bearer ' + key


def call(method, path, **kw):
    r = s.request(method, base + path, timeout=120, **kw)
    try:
        d = r.json()
    except Exception:
        raise RuntimeError(f'Non-JSON HTTP {r.status_code}')
    if r.status_code >= 400 or d.get('code', 0) != 0:
        raise RuntimeError(f"Tripo HTTP {r.status_code} code={d.get('code')} message={str(d.get('message', ''))[:300]}")
    return d.get('data', d)


def wait(task_id, label):
    while True:
        d = call('GET', '/task/' + task_id)
        st = d.get('status')
        print('STATUS', label, st, d.get('progress'), flush=True)
        if st == 'success':
            return d
        if st in ('failed', 'cancelled', 'banned', 'expired', 'unknown'):
            raise RuntimeError(f'{label} {st}: {d.get("error")}')
        time.sleep(15)


def download(d, j, stage):
    out.mkdir(parents=True, exist_ok=True)
    j.setdefault('downloads', [])
    for name, url in (d.get('output') or {}).items():
        if isinstance(url, dict):
            url = url.get('url')
        if not isinstance(url, str) or not url.startswith('https://'):
            continue
        ext = pathlib.Path(urlparse(url).path).suffix or '.bin'
        dest = out / f"{j['key']}-{stage}-{name}{ext}"
        r = requests.get(url, timeout=300)
        r.raise_for_status()
        dest.write_bytes(r.content)
        j['downloads'].append({'path': str(dest.relative_to(root)), 'bytes': len(r.content),
                               'sha256': hashlib.sha256(r.content).hexdigest()})
        print('DOWNLOADED', dest.name, len(r.content), flush=True)


try:
    for j in m['jobs']:
        p = doc / j['input']
        if not p.exists():
            raise RuntimeError(f'원화 없음: {p}')
        j['input_sha256'] = hashlib.sha256(p.read_bytes()).hexdigest()
    bal = call('GET', '/user/balance')
    print('BALANCE', json.dumps(bal), flush=True)
    if input('Type GENERATE to submit generation + rigging for 2 characters: ').strip() != 'GENERATE':
        sys.exit(0)
    m['balance_before'] = bal
    save()
    for j in m['jobs']:
        if not j.get('gen_task'):
            with (doc / j['input']).open('rb') as f:
                u = call('POST', '/upload/sts', files={'file': (pathlib.Path(j['input']).name, f, 'image/png')})
            payload = dict(GEN, file={'type': 'png', 'file_token': u['image_token']})
            j['gen_params'] = GEN
            j['gen_task'] = call('POST', '/task', json=payload)['task_id']
            save()
            print('SUBMITTED gen', j['key'], j['gen_task'], flush=True)
    for j in m['jobs']:
        if j.get('status') == 'done':
            continue
        g = wait(j['gen_task'], j['key'] + ' gen')
        if not j.get('gen_downloaded'):
            download(g, j, 'raw')
            j['gen_downloaded'] = True
            save()
        if not j.get('rig_task'):
            j['rig_params'] = RIG
            j['rig_task'] = call('POST', '/task', json=dict(RIG, original_model_task_id=j['gen_task']))['task_id']
            save()
            print('SUBMITTED rig', j['key'], j['rig_task'], flush=True)
        rg = wait(j['rig_task'], j['key'] + ' rig')
        download(rg, j, 'rigged')
        j['status'] = 'done'
        save()
    m['balance_after'] = call('GET', '/user/balance')
    save()
    print('FINISHED', json.dumps(m['balance_after']), flush=True)
except Exception as e:
    print('ERROR', str(e).replace(key, '[REDACTED]'), flush=True)
    save()
    sys.exit(1)
finally:
    s.close()
    key = None
