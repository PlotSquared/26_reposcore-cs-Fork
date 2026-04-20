#!/usr/bin/env python3
"""
상단 README.md의 Synopsis 섹션을 .NET CLI 도움말 출력으로 자동 갱신합니다.

사용법 (프로젝트 루트에서):
    python tools/update-synopsis.py

Makefile에서 함께 실행하도록 구성할 수 있습니다.
"""

import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
README_PATH = ROOT / "README.md"
PROJECT_FILE = ROOT / "reposcore-cs.csproj"
MARKER_START = "<!-- synopsis:start -->"
MARKER_END = "<!-- synopsis:end -->"


def capture_cli_help() -> str:
    """dotnet run을 이용해 CLI 도움말 출력을 캡처합니다."""
    if not PROJECT_FILE.exists():
        raise FileNotFoundError(f"프로젝트 파일을 찾을 수 없습니다: {PROJECT_FILE}")

    candidates = [
        ["dotnet", "run", "--project", str(PROJECT_FILE), "--no-restore", "--", "--help"],
        ["dotnet", "run", "--project", str(PROJECT_FILE), "--", "--help"],
    ]

    last_error = None
    for command in candidates:
        proc = subprocess.run(
            command,
            cwd=ROOT,
            capture_output=True,
            text=True,
        )
        output = (proc.stdout or "") + (proc.stderr or "")
        output = output.strip()
        if (proc.returncode == 0 or proc.returncode == 129 )and output:
            return output
        last_error = output or f"dotnet returned exit code {proc.returncode}"

    raise RuntimeError(
        "CLI 도움말을 생성하지 못했습니다. dotnet 실행 결과:\n" + last_error
    )


def normalize_cli_help(help_text: str) -> str:
    """출력에서 help 블록의 시작 위치를 찾아 반환합니다."""
    for marker in ["Usage:", "usage:"]:
        index = help_text.find(marker)
        if index != -1:
            return help_text[index:].strip()
    return help_text.strip()


def build_synopsis_block(help_text: str) -> str:
    return "```text\n" + help_text.strip() + "\n```"


def update_readme(readme_path: Path, new_block: str) -> bool:
    """README.md의 Synopsis 마커 구간을 교체하고 변경 여부를 반환합니다."""
    if not readme_path.exists():
        raise FileNotFoundError(f"README.md를 찾을 수 없습니다: {readme_path}")

    original = readme_path.read_text(encoding="utf-8")
    if MARKER_START not in original or MARKER_END not in original:
        heading_match = re.search(r"^##\s+Synopsis\s*$", original, re.MULTILINE)
        if not heading_match:
            raise RuntimeError("README.md에서 '## Synopsis' 제목을 찾을 수 없습니다.")

        insert_pos = heading_match.end()
        updated = (
            original[:insert_pos]
            + "\n\n"
            + MARKER_START
            + "\n\n"
            + new_block
            + "\n\n"
            + MARKER_END
            + original[insert_pos:]
        )
    else:
        pattern = re.compile(
            rf"{re.escape(MARKER_START)}.*?{re.escape(MARKER_END)}",
            re.DOTALL,
        )
        replacement = f"{MARKER_START}\n\n{new_block}\n\n{MARKER_END}"
        updated = pattern.sub(replacement, original)

    if updated == original:
        return False

    readme_path.write_text(updated, encoding="utf-8")
    return True


def main() -> None:
    raw_help = capture_cli_help()
    help_text = normalize_cli_help(raw_help)
    block = build_synopsis_block(help_text)

    print("[생성] CLI 도움말을 수집했습니다.")
    print(help_text)
    print("[업데이트] README.md의 Synopsis 섹션을 갱신합니다...")

    changed = update_readme(README_PATH, block)
    if changed:
        print(f"[완료] {README_PATH}이(가) 갱신되었습니다.")
    else:
        print("[변경 없음] README.md의 Synopsis 섹션이 이미 최신 상태입니다.")


if __name__ == "__main__":
    main()
