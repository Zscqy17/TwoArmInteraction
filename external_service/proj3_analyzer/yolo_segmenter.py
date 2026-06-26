from __future__ import annotations

import re
from pathlib import Path
from typing import List, Tuple

from PIL import Image

from .schemas import Bounds, SceneItem


def _safe_id(label: str, index: int) -> str:
    slug = re.sub(r"[^a-z0-9]+", "_", label.lower()).strip("_")
    if not slug:
        slug = "item"
    return f"{slug}_{index}"


def _normalize_box(box: Tuple[float, float, float, float], image_size: Tuple[int, int]) -> Bounds:
    width, height = image_size
    x1, y1, x2, y2 = box
    x1 = max(0.0, min(x1, width - 1))
    y1 = max(0.0, min(y1, height - 1))
    x2 = max(x1 + 1.0, min(x2, width))
    y2 = max(y1 + 1.0, min(y2, height))
    return Bounds(x=x1 / width, y=y1 / height, width=(x2 - x1) / width, height=(y2 - y1) / height)


class YoloSegmenter:
    def __init__(self, model_name: str = "yolo11n-seg.pt", confidence: float = 0.35) -> None:
        self.model_name = model_name
        self.confidence = confidence
        self._model = None

    def _load_model(self):
        if self._model is None:
            from ultralytics import YOLO

            self._model = YOLO(self.model_name)
        return self._model

    def analyze(self, image_path: Path, scene_id: str, crops_dir: Path) -> List[SceneItem]:
        image = Image.open(image_path).convert("RGB")
        model = self._load_model()
        results = model.predict(str(image_path), conf=self.confidence, verbose=False)
        if not results:
            return []

        result = results[0]
        boxes = result.boxes
        if boxes is None:
            return []

        crops_dir.mkdir(parents=True, exist_ok=True)
        items: List[SceneItem] = []
        names = result.names

        for index, box in enumerate(boxes):
            cls_id = int(box.cls[0].item())
            label = str(names.get(cls_id, f"class_{cls_id}"))
            score = float(box.conf[0].item())
            xyxy = tuple(float(value) for value in box.xyxy[0].tolist())
            bounds = _normalize_box(xyxy, image.size)
            item_id = _safe_id(label, index)

            x1, y1, x2, y2 = [int(round(value)) for value in xyxy]
            crop = image.crop((x1, y1, x2, y2))
            crop_path = crops_dir / f"{item_id}.png"
            crop.save(crop_path)

            items.append(
                SceneItem(
                    id=item_id,
                    label=label,
                    bounds=bounds,
                    confidence=score,
                    cropPath=str(crop_path.as_posix()),
                )
            )

        return items


def mock_items() -> List[SceneItem]:
    return [
        SceneItem(id="knife", label="knife", bounds=Bounds(x=0.24, y=0.66, width=0.5, height=0.16)),
        SceneItem(id="apple", label="apple", bounds=Bounds(x=0.08, y=0.18, width=0.28, height=0.34)),
        SceneItem(id="bottle", label="bottle", bounds=Bounds(x=0.42, y=0.14, width=0.22, height=0.42)),
        SceneItem(id="cup", label="cup", bounds=Bounds(x=0.68, y=0.32, width=0.22, height=0.28)),
    ]
