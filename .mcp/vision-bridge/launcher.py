from __future__ import annotations

import os
import subprocess
import sys
from pathlib import Path

BRIDGE_DIR = Path(__file__).resolve().parent
SERVER_PATH = BRIDGE_DIR / "server.py"
REQUIREMENTS_PATH = BRIDGE_DIR / "requirements.txt"
LINUX_VENV_PYTHON = BRIDGE_DIR / ".venv" / "bin" / "python"
POSIX_ALT_VENV_DIR = BRIDGE_DIR / ".venv-posix"
POSIX_ALT_VENV_PYTHON = POSIX_ALT_VENV_DIR / "bin" / "python"
WINDOWS_VENV_PYTHON = BRIDGE_DIR / ".venv" / "Scripts" / "python.exe"
WINDOWS_ALT_VENV_DIR = BRIDGE_DIR / ".venv-win"
WINDOWS_ALT_VENV_PYTHON = WINDOWS_ALT_VENV_DIR / "Scripts" / "python.exe"
REQUIRED_IMPORT_CHECK = "import fastmcp,requests,PIL"


def _is_windows() -> bool:
    return os.name == "nt"


def _has_required_packages(python_exe: Path) -> bool:
    if not python_exe.exists():
        return False
    probe = subprocess.run(
        [str(python_exe), "-c", REQUIRED_IMPORT_CHECK],
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
        check=False,
    )
    return probe.returncode == 0


def _ensure_windows_venv(venv_dir: Path) -> Path:
    venv_python = venv_dir / "Scripts" / "python.exe"
    if not venv_python.exists():
        subprocess.check_call([sys.executable, "-m", "venv", str(venv_dir)])

    if not _has_required_packages(venv_python):
        subprocess.check_call(
            [
                str(venv_python),
                "-m",
                "pip",
                "install",
                "--disable-pip-version-check",
                "-r",
                str(REQUIREMENTS_PATH),
            ]
        )

    return venv_python


def _ensure_posix_venv(venv_dir: Path) -> Path:
    venv_python = venv_dir / "bin" / "python"
    if not venv_python.exists():
        subprocess.check_call([sys.executable, "-m", "venv", str(venv_dir)])

    if not _has_required_packages(venv_python):
        subprocess.check_call(
            [
                str(venv_python),
                "-m",
                "pip",
                "install",
                "--disable-pip-version-check",
                "-r",
                str(REQUIREMENTS_PATH),
            ]
        )

    return venv_python


def _select_runtime() -> Path:
    current_python = Path(sys.executable)

    if _is_windows():
        if _has_required_packages(WINDOWS_VENV_PYTHON):
            return WINDOWS_VENV_PYTHON
        if _has_required_packages(WINDOWS_ALT_VENV_PYTHON):
            return WINDOWS_ALT_VENV_PYTHON
        if _has_required_packages(current_python):
            return current_python
        return _ensure_windows_venv(WINDOWS_ALT_VENV_DIR)

    # Keep existing Linux/WSL behavior first.
    if _has_required_packages(LINUX_VENV_PYTHON):
        return LINUX_VENV_PYTHON

    if _has_required_packages(POSIX_ALT_VENV_PYTHON):
        return POSIX_ALT_VENV_PYTHON

    if _has_required_packages(current_python):
        return current_python

    return _ensure_posix_venv(POSIX_ALT_VENV_DIR)


def main() -> int:
    runtime = _select_runtime()
    cmd = [str(runtime), str(SERVER_PATH), *sys.argv[1:]]
    return subprocess.call(cmd)


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as ex:
        print(f"[vision-bridge] launcher failed: {ex}", file=sys.stderr)
        raise
