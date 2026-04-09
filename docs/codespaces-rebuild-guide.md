# Codespaces에서 변경된 devcontainer 설정 적용 방법 (Rebuild)

## 개요

devcontainer 설정이 변경된 경우, 기존에 생성해둔 Codespace에는
변경 사항이 자동으로 반영되지 않습니다.
이 문서는 변경된 설정을 적용하는 Rebuild 방법을 안내합니다.

## Rebuild가 필요한 경우

아래와 같은 변경이 있었을 때 Rebuild가 필요합니다.

- `.devcontainer/devcontainer.json` 파일이 수정된 경우
- 새로운 VSCode 확장이 devcontainer에 추가된 경우
- .NET SDK 버전이 변경된 경우

## Rebuild 방법

### 1. 명령 팔레트 열기
`Ctrl+Shift+P` (macOS: `Cmd+Shift+P`)

### 2. Rebuild 명령어 입력
검색창에 아래 중 하나를 입력합니다.

- **일반 Rebuild**: `Codespaces: Rebuild Container`
- **Full Rebuild**: `Codespaces: Full Rebuild Container`

### 3. 선택 후 실행
해당 항목을 선택하면 Codespace가 재시작되며
변경된 devcontainer 설정이 적용됩니다.

## 일반 Rebuild vs Full Rebuild 차이

| 구분 | 일반 Rebuild | Full Rebuild |
|---|---|---|
| 캐시 사용 | 사용 | 사용 안 함 |
| 소요 시간 | 빠름 | 느림 |
| 권장 상황 | 일반적인 설정 변경 시 | 캐시 문제로 설정이 제대로 안 적용될 때 |

## 웹 브라우저 기반 Codespaces에서도 동일하게 적용 가능

웹 브라우저로 Codespaces를 사용하는 경우에도
동일하게 `Ctrl+Shift+P` → `Codespaces: Rebuild Container`
순서로 진행하면 됩니다.

## 주의사항

Rebuild 시 Codespace 컨테이너 내부에서
GitHub에 push하지 않은 변경 사항은 유실될 수 있으니
반드시 미리 push해두세요.
