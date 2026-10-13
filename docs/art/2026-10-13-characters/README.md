# 인물 Tripo 제작 (2026-10-13)

목표 샷 02·03의 두 사람을 Tripo로 만든다.
- 기사: 어두운 재킷, 올리브 바지
- 은주: 흰 티, 청바지

지금의 신티 인물은 하얀 얼굴의 각진 마네킹처럼 보여 숲 장면에서 가장 튄다.

## 사양

| 항목 | 값 | 이유 |
|---|---|---|
| 방식 | 원화 1장 → Tripo image_to_model → Tripo 자동 리깅(Mixamo 뼈대) | everret 동료 12명과 같은 방식이다. Mixamo 소총 클립과 총 들기(gun-pose-unity)를 그대로 쓴다 |
| 면 수 | **face_limit 8,000 (쿼드)**, 삼각형으로 약 1만 6천 | 목표 샷의 인물은 단순한 로우폴리 덩어리라 1만 면을 넘는 디테일은 보이지 않는다. 화면에 두 명뿐이라 신티 인물(약 3~5천)보다 많아도 부담이 작다(풀 5만 포기가 훨씬 무겁다). everret 동료는 약 1만 면 |
| 텍스처 | 생성은 detailed, **PBR 끔(기본색만)**, Unity 임포트 **2048** | 세계가 평면 색 위주라 노멀·거칠기 맵이 필요 없다. 3인칭 근접(화면 높이의 1/3)에서는 2048이면 충분하다. 메모리는 인물당 약 5MB(BC7) |
| 뼈대 | Mixamo 65본 (손가락 포함) | 방아쇠 검지와 왼손 받침 IK가 손가락 뼈를 쓴다 |
| 키 | 기사 1.78m, 은주 1.66m | `CharacterRig.Attach`가 맞춘다 |
| 자세 | 원화는 정면 A 포즈 전신, 무기 없음 | 총은 따로 손에 붙인다. A 포즈가 자동 리깅에서 가장 안정적이다 |

## 원화 프롬프트 (정면 A 포즈, 밝은 회색 배경)

공통 부분:

> Full-body front view of a single character, neutral A-pose (arms 30 degrees from body, palms in), standing straight, feet shoulder width, plain light grey background, even soft studio light. Stylized low-poly 3D game character matching Synty POLYGON style but with slightly more realistic proportions (head-to-body 1:7), clean large shapes, flat matte colors, simple readable face. No text, no weapon, no props.

인물별로 덧붙일 부분:

- 기사 (`inputs/01-driver.png`): Korean man in his mid 30s, short black hair, tired calm expression, plain dark charcoal work jacket half zipped, dark grey t-shirt, olive cargo trousers, brown leather work boots.
- 은주 (`inputs/02-eunju.png`): Korean woman in her late 20s, long dark brown hair in a low ponytail, quiet polite expression, plain white cropped t-shirt, fitted mid-blue jeans, white sneakers, small wristwatch.

## 실행

1. 원화 두 장을 `inputs/`에 둔다.
2. `python tools/tripo/generate_characters.py`를 실행한다.
   - Tripo API 키는 숨김 입력으로 받는다.
   - 잔액을 보여 준 뒤 `GENERATE`를 쳐야 제출한다.
   - 생성, 리깅, 내려받기를 차례로 한다.
   - 결과는 `Assets/ThirdParty/Characters/Tripo20261013/`(깃 제외)에, 기록은 `tripo-manifest.json`에 남는다.
3. Manyworld > Art > Setup All을 실행해 임포트하고 카탈로그를 만든다.

## 결과 (2026-10-13)

- 원화: Codex CLI 내장 ImageGen, 목표 샷 03을 그림체 참고 이미지로 붙였다 (`inputs/`, 각 1024×1536).
- 모델: 사용자가 Tripo 웹에서 생성·리깅해 zip으로 받았다 (API 스크립트는 쓰지 않음). Mixamo 뼈대(손가락 포함), 기본색 2048.
  기사 삼각형 9,970개, 은주 9,424개. 출처와 해시는 `tripo-manifest.json`.
- Unity: `Assets/ThirdParty/Characters/Tripo20261013/`(깃 제외). `ArtSetup.SetupTripoCharacters`가 휴머노이드 임포트, 기본색만 쓰는 무광 URP Lit(`Tripo_{key}.mat`)로 바꿔 끼우기를 한다. zip의 노멀·금속도·거칠기 맵은 쓰지 않는다.
- 카탈로그가 신티 인물 대신 이 둘을 쓴다. 총 들기 QA(`CaptureGunPose`): 서 있기·조준 모두 왼손 받침 오차 0.03cm 이하.
