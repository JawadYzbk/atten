"""On-demand model downloading and verification for Atten."""

import os
from pathlib import Path
import sys
from typing import Callable, Dict, List, Optional
import urllib.request
import json

XTTS_V2_REPO = "coqui/XTTS-v2"
XTTS_V2_FILES = [
    ("config.json", "https://huggingface.co/coqui/XTTS-v2/raw/main/config.json"),
    ("vocab.json", "https://huggingface.co/coqui/XTTS-v2/raw/main/vocab.json"),
    ("speakers_xtts.pth", "https://huggingface.co/coqui/XTTS-v2/resolve/main/speakers_xtts.pth"),
    ("model.pth", "https://huggingface.co/coqui/XTTS-v2/resolve/main/model.pth"),
]


def get_models_directory() -> Path:
    """Returns user-writable models directory in local app data or environment override."""
    env_root = os.environ.get("ATTEN_MODELS_DIR")
    if env_root:
        path = Path(env_root)
    elif sys.platform == "win32":
        local_app_data = os.environ.get("LOCALAPPDATA") or Path.home() / "AppData" / "Local"
        path = Path(local_app_data) / "Atten" / "Models"
    else:
        path = Path.home() / ".local" / "share" / "atten" / "models"
    path.mkdir(parents=True, exist_ok=True)
    return path


def is_xtts_installed() -> bool:
    """Checks whether the XTTS-v2 model files are present and valid."""
    xtts_dir = get_models_directory() / "XTTS-v2"
    if not xtts_dir.is_dir():
        return False
    required = ["config.json", "vocab.json", "model.pth"]
    for filename in required:
        file_path = xtts_dir / filename
        if not file_path.is_file() or file_path.stat().st_size == 0:
            return False
    return True


def download_xtts_model(progress_callback: Optional[Callable[[dict], None]] = None) -> Path:
    """Downloads XTTS-v2 weights with streaming progress."""
    xtts_dir = get_models_directory() / "XTTS-v2"
    xtts_dir.mkdir(parents=True, exist_ok=True)

    total_files = len(XTTS_V2_FILES)
    for idx, (filename, url) in enumerate(XTTS_V2_FILES):
        target = xtts_dir / filename
        if target.is_file() and target.stat().st_size > 0:
            if progress_callback:
                progress_callback({
                    "model": "xtts-v2",
                    "file": filename,
                    "file_index": idx + 1,
                    "total_files": total_files,
                    "percent": int(((idx + 1) / total_files) * 100),
                    "status": f"Verified {filename}",
                })
            continue

        temp_target = xtts_dir / f".{filename}.part"
        req = urllib.request.Request(url, headers={"User-Agent": "Atten/0.2.1"})
        with urllib.request.urlopen(req) as response:
            total_length = response.headers.get("content-length")
            total_bytes = int(total_length) if total_length else 0
            downloaded = 0
            chunk_size = 1024 * 1024  # 1MB chunks

            with open(temp_target, "wb") as out_file:
                while True:
                    chunk = response.read(chunk_size)
                    if not chunk:
                        break
                    out_file.write(chunk)
                    downloaded += len(chunk)
                    file_percent = (downloaded / total_bytes * 100) if total_bytes > 0 else 0
                    overall_percent = int(((idx + (downloaded / total_bytes if total_bytes > 0 else 0)) / total_files) * 100)
                    if progress_callback:
                        progress_callback({
                            "model": "xtts-v2",
                            "file": filename,
                            "file_index": idx + 1,
                            "total_files": total_files,
                            "downloaded_bytes": downloaded,
                            "total_bytes": total_bytes,
                            "file_percent": round(file_percent, 1),
                            "percent": overall_percent,
                            "status": f"Downloading {filename} ({idx + 1}/{total_files})",
                        })

        if temp_target.exists():
            if target.exists():
                target.unlink()
            temp_target.rename(target)

    if progress_callback:
        progress_callback({
            "model": "xtts-v2",
            "percent": 100,
            "status": "XTTS-v2 model download complete!",
            "installed": True,
        })

    return xtts_dir
