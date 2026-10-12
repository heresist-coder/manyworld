# Manyworld 아트 방향 핸드오프 — 2026-10-12

## 이어받을 위치와 현재 상태

- 정본 프로젝트: `C:\VC3\manyworld`
- Unity: `6000.3.22f1`, URP, Input System. 시작 씬: `Assets/_Project/Scenes/Main.unity`.
- 이번 작업은 프로젝트 조사, 신티 자산 확인, 목표 이미지 생성과 문서화까지 완료했다. **목표 샷은 ImageGen 콘셉트이며 Unity 적용 화면이 아니다.**
- 게임 코드, 씬, 프리팹, 프로젝트 설정, 차량 물리, 입력, 저장, 오디오를 변경하지 않았다. 새 신티 패키지도 가져오지 않았다.
- 다음 작업은 이 문서와 [최신 다섯 장면 안내](art/2026-10-12/selected-scenes-v3/README.md)를 먼저 읽는다. 이전 위성 이미지가 최신안으로 복원되지 않도록 주의한다.

## 사용자 결정

배경은 한국 동네 중심에서 자연물 중심의 넓은 월드로 바꾼다. 신티 자산을 최대한 활용한다. 차를 타고 이동하고, 내려서 현장 미션을 수행한 뒤 다시 차에 타서 콜을 기다리는 흐름이 목표다. 한국 동네 배경을 포기한 결정만으로 한국어 UI, 인물 이름, 전체 시대 설정을 함께 제거하지 않는다.

**위성은 제외한다.** 최신 선택은 다음 여덟 가지다.

1. 떠 있는 집
2. 모두 같은 방향으로 기운 숲
3. 숲속의 빈 방
4. 철로 된 나무 한 그루
5. 가라앉지 않는 바위
6. 수면 아래의 불 켜진 집
7. 길을 건너는 그림자
8. 숲에 있는 인형들

이 여덟 가지를 다섯 개의 독립된 장면으로 나눴다. 사용자는 아직 특정 장면의 런타임 설치나 여덟 현상의 전체 구현을 지시하지 않았다. 핸드오프 요청을 구현 승인으로 확대하지 않는다.

## 최신 목표 샷

모든 PNG는 `1672 × 941`이다. 아래 링크가 현재 검토용 결과다.

| 장면 | 목표 이미지 | 포함 현상과 구성 |
|---|---|---|
| 01 | [떠 있는 집과 도로 그림자](art/2026-10-12/selected-scenes-v3/01-floating-house-road-shadow.png) | 초원 도로의 작은 노란 차, 공중에 떠 있는 목조 주택, 도로를 가로지르는 다른 형태의 그림자 |
| 02 | [기울어진 숲](art/2026-10-12/selected-scenes-v3/02-inward-leaning-forest.png) | 입구에 주차한 차, 안으로 걸어가는 두 사람, 공터를 향해 기운 나무들 |
| 03 | [숲속의 빈 방과 인형](art/2026-10-12/selected-scenes-v3/03-empty-forest-room-dolls.png) | 나무가 만든 직선 벽 세 면, 빈 사각 바닥, 의자 하나, 가장자리의 마네킹 여섯 개 |
| 04 | [철로 된 나무](art/2026-10-12/selected-scenes-v3/04-iron-tree.png) | 소나무 사이에 금속 기둥·가로대·안테나·늘어진 케이블로 구성한 한 그루 |
| 05 | [뜨는 바위와 수중 주택](art/2026-10-12/selected-scenes-v3/05-unsinkable-rocks-submerged-house.png) | 해 질 무렵 호수, 수면의 바위 다섯 개, 지붕까지 잠긴 집의 따뜻한 창문 빛 |

**남은 방향 차이:** 사용자 표현은 “모두 같은 방향으로 기운 숲”이다. 현재 02 샷은 양쪽 나무가 공터 중심으로 기우는 변형이다. 다음 수정·구현에서 사용자의 원문대로 나무 전체가 한 방향으로 기울게 할지 반영해야 한다. 이 샷을 원문과 정확히 일치한다고 주장하지 않는다.

작은 노란 소형차와 자연 월드의 로우폴리 스타일을 공통으로 유지했다. 정지 이미지이므로 그림자의 이동, 바위의 부력, 미션 진행을 입증하지 않는다.

## 신티 원본과 설치 상태

| 용도 | 확인한 원본 | 상태와 적용 방법 |
|---|---|---|
| 공중·수중 주택 | `Assets/Synty/PolygonFarm/Prefabs/Buildings/SM_Bld_Farmhouse_01.prefab`, `_02.prefab` | Manyworld에 설치됨. 기존 주택을 배치·조명으로 변형하는 후보 |
| 숲 | `Assets/Synty/PolygonNature/Prefabs/Trees/SM_Tree_Pine_01.prefab` 등 | 설치됨. 회전과 배열로 이상현상을 만들 수 있으나 충돌·통행 검증 필요 |
| 바위 | `Assets/Synty/PolygonNature/Prefabs/Rocks/SM_Rock_Boulder_01.prefab` 등 | 설치됨. 실제 부력이나 수면 효과는 구현하지 않음 |
| 빈 방의 의자 | `Assets/Synty/PolygonTown/Prefabs/Props/SM_Prop_Chair_01.prefab` 등 | 설치됨. 나무 배열·빈 바닥과 조합 |
| 인형 | `SM_Prop_Mannequin_Male_01.fbx` | 아래 백업 경로에서 확인. 이번 조사에서 Manyworld 내 해당 모델·프리팹은 찾지 못함. 천 인형이 아닌 얼굴 없는 마네킹 형태 |
| 철 나무 | `SM_Prop_Powerline_Antenna_01.fbx`, `SM_Prop_Antenna_01.fbx` | 다른 프로젝트의 SciFi Worlds 원본 확인. Manyworld에 설치되지 않음. 완성된 철 나무 프리팹이 아니라 부품 조합·확대 후보 |
| 길을 건너는 그림자 | 전용 프리팹 확인 없음 | 그림자만 보이는 캐스터/마스크와 이동 처리가 필요함. 현재는 콘셉트 |

읽기 전용으로 확인한 외부 원본 경로:

```text
D:\Codex\cube125-unity_asset_backups\20260830_172201_city_cute_transition\Assets\Synty\PolygonApocalypse\Models\SM_Prop_Mannequin_Male_01.fbx
D:\Codex\cube125-unity\Assets\Synty\PolygonSciFiWorlds\Models\SM_Prop_Powerline_Antenna_01.fbx
D:\Codex\cube125-unity\Assets\Synty\PolygonSciFiWorlds\Models\SM_Prop_Antenna_01.fbx
```

실제 FBX 세 개를 Blender 5.2의 중립 회색 WORKBENCH 렌더로 확인했다. [원본 프리뷰와 기록](art/2026-10-12/selected-scenes-v3/native-references/)은 **형상 증거**다. Unity 재질·텍스처·셰이더·최종 스케일 검증은 아니다. 생성 이미지의 모든 형태가 단일 신티 프리팹과 일치하는 것도 아니다.

패키지를 다시 찾을 때는 드라이브 전체 검색보다 기존 카탈로그를 먼저 쓴다.

```text
라이브러리: F:\synty&happy\SyntySpacePacks
카탈로그: catalog\packages.json
아카이브 목록: catalog\manifests\
조회 스크립트: C:\Users\heres\.codex\skills\synty-asset-access\scripts\synty_catalog.py
SciFi Worlds: POLYGON_SciFi_Worlds_Unity_2022_3_v1_12_2.unitypackage
```

새 자산을 적용할 때 필요한 프리팹과 종속성만 가져온다. 원본 라이브러리와 다른 프로젝트는 이동·수정하지 않는다. 유료 원본 자산을 공개 저장소에 올리지 않는다.

## 현재 게임과 목표 사이의 차이

- `Assets/_Project/Scripts/World/WorldTerrainBuilder.cs`: 현재 `Size=1024f`인 약 1km 분지, 최대 높이 110m. 월드 번호에 따라 숲·폐허·홍수·색이 바뀌지만 산·강·흙길 다섯 경로·마을 다섯 곳의 큰 지형은 공통이다. 광대한 오픈월드가 구현됐다고 볼 수 없다.
- `TownBuilder.cs`, `KoreanBuildings.cs`: 현재 한국 동네 생성 코드가 남아 있다. 방향 결정만 문서에 반영했으며 배경 코드는 교체하지 않았다.
- `Assets/_Project/Scripts/Core/GameManager.cs`: Hub/Drive/Mission/Bureau/Settlement 흐름. 현장 미션 종료 후 허브·정산으로 돌아가는 기존 구조가 있다.
- `Assets/_Project/Scripts/Drive/DriveSession.cs`: 콜과 운전 세션의 기존 기능이 있지만 “같은 자연 월드에서 미션 후 재탑승하고 다음 콜 대기” 흐름은 아직 구현하지 않았다.
- `TicoCar.cs`, `MissionController.cs`: 승하차·동행자·차량 손상·미션·정산 관련 계약을 보존해야 한다.
- `Assets/_Project/Resources/ArtCatalog.asset`: 조사한 참조는 트리 15, 고사목 4, 덤불 3, 바위 9, 풀 4, 농장 건물 9, 울타리 3으로 로컬 메타에 연결됨. [참조 증거](art/2026-10-12/installed-art-evidence.json).
- 현재 인물은 Tripo `01-dohyun-rigged.fbx`, `02-luna-rigged.fbx`; 차량 본체는 ithappy `Car_06`이다. 신티 `SM_Veh_Car_Small_01`은 별도 후보이며 생성 샷의 노란 차가 현재 차량 교체를 뜻하지 않는다.

## 확인한 결과와 미검증 범위

확인 완료:

- 최신 PNG 다섯 개를 프로젝트에 저장하고 파일 존재, 해상도, SHA256, 생성 원본과 복사본 일치를 확인했다.
- 여덟 현상과 다섯 샷의 대응을 기록했다. 위성 제외는 이미지의 수동 육안 검토이며 객체 검출기 결과가 아니다.
- 마네킹·안테나·전력선 원본의 실제 메시 형상을 렌더로 확인했다.
- [정확한 생성 프롬프트](art/2026-10-12/selected-scenes-v3/imagegen-prompts.txt), [검증 기록](art/2026-10-12/selected-scenes-v3/verification.json), 재검증 스크립트를 저장했다.

런타임 확인 실패 및 미검증:

- 기존 `Builds/Windows/Manyworld.exe`는 2026-10-08 11:37:35의 오래된 빌드다. 최신 소스로 다시 빌드하지 않았다.
- 기존 빌드를 숨김 실행하고 `-mwshots`로 받은 14장 캡처는 전부 검은 화면이었다. 픽셀 extrema로 확인했으며 **유효한 네이티브 화면 증거가 없다**. 출력은 `docs/art/2026-10-12/baseline-build/`에 있다.
- 해당 로그에는 월드 47/200 생성 기록, Nature/Soft Occlusion 나무 빌보드 경고, RenderTexture 경고가 있다. 원인을 해결하지 않았다.
- Unity 적용, 실제 입력을 통한 운전·승하차·미션 복귀, 콜 대기, 셰이더·수면·그림자 효과, 충돌, FPS/LOD, 장시간 플레이, 기기별 동작, 오디오는 이번 아트 결과로 검증하지 않았다.

## 다음 구현 지시가 오면

1. 최신 다섯 샷과 D17–D20을 기준으로 작은 범위를 정한다. 권장 시작은 150–250m 도로와 숲속 빈 방·인형 또는 기울어진 숲 하나다. 전체 여덟 현상을 한 번에 설치하지 않는다.
2. 먼저 현재 Unity 작업 상태를 확인하고 유효한 네이티브 캡처 경로를 확보한다. 오래된 플레이어의 검은 캡처를 성공 증거로 재사용하지 않는다. 사용자 작업을 덮거나 열려 있는 에디터를 임의로 종료하지 않는다.
3. 원본 프리팹의 형태·색·재질·스케일을 목표 샷과 비교한다. 필요한 외부 자산만 가져오고, 생성 이미지와 원본의 남은 차이를 기록한다.
4. 기존 지형, 도로 통행, 미션 앵커·게이트·중간 지점·목표, 차량 물리, 동행자, 저장, 입력, 시드 규칙을 보존한다. 외형 배치로 충돌에 영향을 주면 실제 운전·보행으로 검사한다.
5. 전후 네이티브 화면과 실제 입력 증거를 남긴다. 형상·배치·색·재질·시인성을 목표와 비교하고 누락을 고친다. 물 아래 주택은 투명도/굴절/창문 발광, 도로 그림자는 별도 효과 구현과 검증이 필요하다.
6. 같은 월드의 미션 후 콜 대기는 별도 게임 흐름 변경으로 다룬다. 아트 적용만으로 해당 플레이 루프가 완료됐다고 보고하지 않는다.

모든 ImageGen 후속 생성·편집 전에는 `C:\Users\heres\.codex\instructions\image-quality.md`를 읽는다. 저노이즈, 큰 형태와 명암 우선 원칙을 유지한다.

## 재현과 변경 목록

PowerShell, 프로젝트 루트에서 최신 아트 파일을 검사한다.

```powershell
Set-Location C:\VC3\manyworld
python docs\art\2026-10-12\selected-scenes-v3\verify-artifacts.py
git diff --check
git status --short
```

이번 작업의 문서·결과:

- [전체 아트 조사와 단계별 방향](art-direction-nature-world.md)
- [결정 기록 D17–D20](decisions.md)
- [최신 다섯 장면과 원본 대응](art/2026-10-12/selected-scenes-v3/README.md)
- [초기 이상현상 탐색](art/2026-10-12/anomaly-asset-selection.md): 위성·머리 유물·링은 과거 탐색 기록이며 현재 선택안이 아니다.
- `docs/art/2026-10-12/`: 최초 자연 샷, 초기 탐색 샷, 원본 형상 프리뷰, 실패한 baseline 캡처를 포함한다.

핸드오프 작성 전 Git 상태는 `docs/decisions.md` 수정, `docs/art-direction-nature-world.md`와 `docs/art/` 미추적이었다. 이 핸드오프도 새 문서다. 이번 작업은 커밋하지 않았다. 이어받을 때 사용자 변경과 섞어 전체 파일을 일괄 되돌리거나 자동 커밋하지 않는다.

핸드오프 저장 후 재확인: `verify-artifacts.py`가 다섯 파일·여덟 현상·동일 해상도·생성 원본 해시 일치를 통과했다. `git diff --check`도 통과했으며 `decisions.md`의 CRLF→LF 안내만 있었다.
