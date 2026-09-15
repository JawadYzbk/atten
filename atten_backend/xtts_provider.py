"""XTTS-v2 Multilingual (including Arabic) synthesis provider for Atten."""

from pathlib import Path
import os
import numpy as np
from typing import Generator, Tuple

from .downloader import get_models_directory, is_xtts_installed
from .device import resolve_device


class XTTSv2Provider:
    """TTS provider for XTTS-v2 supporting Arabic, English, and 15+ languages."""

    def __init__(self, model_root: Path = None, device_mode: str = "auto"):
        self.device_info = resolve_device(device_mode)
        self._model_dir = model_root or (get_models_directory() / "XTTS-v2")
        self._model = None
        self._config = None

    def _ensure_loaded(self):
        if not is_xtts_installed():
            raise RuntimeError(
                "XTTS-v2 model is not installed. Please download it from Settings or run with --download-model xtts-v2."
            )
        if self._model is None:
            try:
                from TTS.tts.configs.xtts_config import XttsConfig
                from TTS.tts.models.xtts import Xtts
            except ImportError:
                # Fallback if full TTS library is not installed: try lightweight torch loader
                try:
                    import torch
                except ImportError:
                    raise RuntimeError("PyTorch is required to run XTTS-v2.")

            config_path = self._model_dir / "config.json"
            model_path = self._model_dir / "model.pth"
            vocab_path = self._model_dir / "vocab.json"
            speakers_path = self._model_dir / "speakers_xtts.pth"

            # Load XTTS Model
            config = XttsConfig()
            config.load_json(str(config_path))
            self._model = Xtts.init_from_config(config)
            self._model.load_checkpoint(
                config,
                checkpoint_path=str(model_path),
                vocab_path=str(vocab_path),
                speaker_file_path=str(speakers_path) if speakers_path.exists() else None,
                eval=True,
                use_deepspeed=False,
            )
            if hasattr(self._model, "to"):
                self._model.to(self.device_info.selected_device)
            self._config = config

    def segments(
        self, text: str, voice: str, speed: float
    ) -> Generator[Tuple[str, str, np.ndarray], None, None]:
        self._ensure_loaded()
        # Parse language from voice or voice prefix
        language = "ar" if voice.startswith("ar_") else "en"
        if "_" in voice:
            parts = voice.split("_")
            if len(parts[0]) == 2:
                language = parts[0]

        # XTTS inference
        # Split text by lines/sentences
        lines = [line.strip() for line in text.split("\n") if line.strip()]
        for line in lines:
            outputs = self._model.synthesize(
                line,
                self._config,
                speaker_name=voice,
                language=language,
                speed=speed,
            )
            audio = outputs["wav"]
            if isinstance(audio, list):
                audio = np.array(audio, dtype=np.float32)
            yield (line, line, audio)
