"""On-demand model downloading with resume, live speed, ETA, and metrics."""

import os
from pathlib import Path
import sys
import time
from typing import Callable, Dict, List, Optional
import urllib.request
import json

XTTS_V2_REPO = "coqui/XTTS-v2"
XTTS_V2_FILES = [
    ("config.json", "https://huggingface.co/coqui/XTTS-v2/raw/main/config.json", 3_000),
    ("vocab.json", "https://huggingface.co/coqui/XTTS-v2/raw/main/vocab.json", 160_000),
    ("speakers_xtts.pth", "https://huggingface.co/coqui/XTTS-v2/resolve/main/speakers_xtts.pth", 250_000),
    ("model.pth", "https://huggingface.co/coqui/XTTS-v2/resolve/main/model.pth", 1_870_000_000),
]


def format_bytes(size: float) -> str:
    """Formats bytes to a human-readable string (KB, MB, GB)."""
    if size >= 1024 * 1024 * 1024:
        return f"{size / (1024 * 1024 * 1024):.2f} GB"
    elif size >= 1024 * 1024:
        return f"{size / (1024 * 1024):.1f} MB"
    elif size >= 1024:
        return f"{size / 1024:.0f} KB"
    return f"{size:.0f} B"


def format_time(seconds: float) -> str:
    """Formats seconds into readable remaining time."""
    if seconds <= 0 or seconds > 86400:
        return "--"
    mins, secs = divmod(int(seconds), 60)
    hours, mins = divmod(mins, 60)
    if hours > 0:
        return f"{hours}h {mins}m"
    if mins > 0:
        return f"{mins}m {secs}s"
    return f"{secs}s"


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
    """Downloads XTTS-v2 weights with resumable downloads, live speed, ETA, and progress."""
    xtts_dir = get_models_directory() / "XTTS-v2"
    xtts_dir.mkdir(parents=True, exist_ok=True)

    # Calculate total expected size
    total_model_bytes = sum(expected for _, _, expected in XTTS_V2_FILES)
    total_files = len(XTTS_V2_FILES)

    for idx, (filename, url, expected_size) in enumerate(XTTS_V2_FILES):
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
                    "speed": "0 MB/s",
                    "eta": "",
                    "size_text": f"{format_bytes(target.stat().st_size)} / {format_bytes(expected_size)}",
                })
            continue

        temp_target = xtts_dir / f".{filename}.part"
        existing_bytes = temp_target.stat().st_size if temp_target.exists() else 0

        headers = {"User-Agent": "Atten/0.2.1"}
        if existing_bytes > 0:
            headers["Range"] = f"bytes={existing_bytes}-"

        req = urllib.request.Request(url, headers=headers)
        
        try:
            response = urllib.request.urlopen(req)
        except urllib.error.HTTPError as e:
            # If 416 Range Not Satisfiable, restart file
            if e.code == 416:
                existing_bytes = 0
                headers.pop("Range", None)
                req = urllib.request.Request(url, headers=headers)
                response = urllib.request.urlopen(req)
            else:
                raise

        content_length = response.headers.get("content-length")
        if response.status == 206:
            # Partial content
            file_total = existing_bytes + (int(content_length) if content_length else expected_size)
            mode = "ab"
        else:
            file_total = int(content_length) if content_length else expected_size
            existing_bytes = 0
            mode = "wb"

        downloaded_in_file = existing_bytes
        chunk_size = 1024 * 512  # 512 KB chunks

        # Speed and ETA tracking
        start_time = time.time()
        session_downloaded = 0
        last_update_time = start_time
        speed_bps = 0.0

        with open(temp_target, mode) as out_file:
            while True:
                chunk = response.read(chunk_size)
                if not chunk:
                    break
                out_file.write(chunk)
                downloaded_in_file += len(chunk)
                session_downloaded += len(chunk)

                now = time.time()
                elapsed = now - last_update_time
                if elapsed >= 0.25:  # Update progress 4 times per second
                    total_elapsed = now - start_time
                    if total_elapsed > 0:
                        speed_bps = session_downloaded / total_elapsed

                    remaining_bytes = max(0, file_total - downloaded_in_file)
                    eta_seconds = (remaining_bytes / speed_bps) if speed_bps > 0 else 0

                    file_fraction = downloaded_in_file / file_total if file_total > 0 else 0
                    overall_percent = int(((idx + file_fraction) / total_files) * 100)

                    if progress_callback:
                        progress_callback({
                            "model": "xtts-v2",
                            "file": filename,
                            "file_index": idx + 1,
                            "total_files": total_files,
                            "downloaded_bytes": downloaded_in_file,
                            "total_bytes": file_total,
                            "percent": overall_percent,
                            "speed": f"{format_bytes(speed_bps)}/s",
                            "eta": format_time(eta_seconds),
                            "size_text": f"{format_bytes(downloaded_in_file)} / {format_bytes(file_total)}",
                            "status": f"Downloading {filename} ({idx + 1}/{total_files})",
                        })
                    last_update_time = now

        response.close()

        if temp_target.exists():
            if target.exists():
                target.unlink()
            temp_target.rename(target)

    if progress_callback:
        progress_callback({
            "model": "xtts-v2",
            "percent": 100,
            "speed": "",
            "eta": "",
            "size_text": f"{format_bytes(total_model_bytes)}",
            "status": "XTTS-v2 model download complete!",
            "installed": True,
        })

    return xtts_dir


def download_hf_model(model_id: str, progress_callback: Optional[Callable[[dict], None]] = None) -> Path:
    """Downloads any model from Hugging Face with progress callbacks."""
    clean_id = model_id.strip()
    if clean_id.lower() in ("xtts-v2", "coqui/xtts-v2"):
        return download_xtts_model(progress_callback)

    from huggingface_hub import snapshot_download

    if progress_callback:
        progress_callback({
            "model": clean_id,
            "percent": 30,
            "status": f"Downloading {clean_id} weights from Hugging Face...",
            "speed": "",
            "eta": "",
            "size_text": "",
        })

    path = Path(snapshot_download(repo_id=clean_id))

    if progress_callback:
        progress_callback({
            "model": clean_id,
            "percent": 100,
            "status": f"{clean_id} downloaded successfully!",
            "speed": "",
            "eta": "",
            "size_text": "",
            "installed": True,
        })

    return path
