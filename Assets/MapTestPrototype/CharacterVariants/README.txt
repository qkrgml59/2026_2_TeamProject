char2 임시 캐릭터 전환

범위
MapTest 플레이어 한 개의 Animator 모션과 Visual 크기만 전환합니다. 원래 캐릭터의 PNG/import/controller/clips는 변경하지 않습니다.
기본은 char1. Tab → char2 → Enter로 새 아트, char1 → Enter로 원래 아트. 구역 번호와 M 지도는 계속 사용합니다.
실행 중 선택은 사망 후 MapTest 재로드까지 유지하며, 메인 메뉴로 돌아가면 초기화합니다. 선택은 저장 데이터에 기록하지 않습니다.

파생 자산
Resources/MapTestChar2/Sprites/Char2_*.png 8개는 Assets/Art/Character/Sprite/Anim_Character_*.png와 바이트가 같은 사본입니다.
사본의 meta만 PPU100, Point, 압축 없음, 전체 프레임 영역과 발/몸통/벽 손 접점에 맞춘 pivot으로 정렬했습니다.
Idle/Run 원본 슬라이스의 픽셀 누락은 원본 설정을 건드리지 않고 사본 프레임 범위를 늘려 포함했습니다.
Visual 배율은 기존 배율 ×33/64(현재 .78125)입니다. 부모 Collider, 이동 수치, 공격 판정은 보존합니다.

애니메이션 연결
기존 Warrior.controller를 부모로 하는 Char2.overrideController와 모션별 사본 21개를 사용합니다.
상태명, 파라미터, 전이, 클립 길이/루프와 기존 코드 기반 재생 속도를 보존합니다.
Jump은 상승/정점/하강 상태로 분배합니다. Dash .28초, 공격 .45초 및 코드 타격 구간은 변경하지 않습니다.
사다리/웅크리기는 Idle 첫 프레임 한 장입니다. 정상 진입이 없는 매달리기는 새로 구현하지 않습니다.
미사용 공용 모션도 대기 한 장으로 대체하여 원래 외형이 섞이지 않게 했습니다.

별도 게임 동작 변경
BasicPlayerMovement2D의 슬라이딩 허용값은 기본 true입니다.
MapPrototypeSession에서만 false로 설정해 MapTest의 char1/char2 모두 아래+수평 입력이 Crouch로 들어갑니다.
다른 씬에서 MapPrototypeSession을 사용하지 않으면 기존 슬라이딩을 유지합니다.

제거
1. MapRegionTeleportUI.Submit의 char1/char2 분기와 안내 문구를 제거합니다.
2. CharacterVariants 폴더 및 CharacterVariants.meta를 제거합니다. 다른 자산은 이 폴더를 참조하지 않습니다.
3. 슬라이딩 차단은 독립 변경입니다. 유지하려면 그대로 두고, 복원하려면 MapPrototypeSession의 SetSlideEnabled(false) 호출과 BasicPlayerMovement2D의 관련 값/조건을 원래대로 되돌립니다.
팀원이 검토한 뒤 직접 커밋합니다. 이 작업은 commit/push를 하지 않습니다.


사망 그림은 기존 GameOver timeScale .2와 실제 .8초 복귀를 유지하면서 마지막 자세가 보이도록 char2의 앞 7키를 게임 시간 0~.15초에 배치합니다. 기존 클립 길이 1.1초와 복귀/저장 코드는 그대로입니다.
검증: 격리 Windows 실행 156개 통과/실패0/예외0. 실제 렌더 13장으로 Idle/Run/Attack/Jump/Dash/Hurt/WallSlide/Death/Respawn 및 임시 자세를 확인했습니다. 전경 창에서 직접 키보드로 조작하는 검사는 수행하지 않았습니다.
사다리/웅크리기는 서 있는 그림 한 장이므로 동작을 표현하는 자세는 임시입니다. Collider는 기존 크기와 offset을 유지합니다. 매달리기 기능은 추가하지 않았습니다.
기존 최초 메인 메뉴 상태 None 및 이전 빌드 경고 2개는 이번에 수정하지 않았습니다.

새 대시 시트에 먼지 그림이 포함되어 있으므로, 기존 Dash_Dust의 머티리얼이 비어 있는 경우에만 char2에서 해당 Renderer 표시를 숨깁니다. char1 복귀 시 원래 enabled 상태를 복원합니다. 잔상은 유지하며 원본 씬/머티리얼과 입자 방출·대시 로직은 변경하지 않습니다.
