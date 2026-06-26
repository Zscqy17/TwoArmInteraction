from __future__ import annotations

import shutil
import uuid
from pathlib import Path

from fastapi import FastAPI, File, Form, UploadFile
from fastapi.responses import JSONResponse
from fastapi.staticfiles import StaticFiles

from .openai_reasoner import OpenAIReasoner
from .schemas import SceneAnalysis
from .yolo_segmenter import YoloSegmenter, mock_items


BASE_DIR = Path(__file__).resolve().parent.parent
STORAGE_DIR = BASE_DIR / "storage"
UPLOAD_DIR = STORAGE_DIR / "uploads"
CROP_DIR = STORAGE_DIR / "crops"

STORAGE_DIR.mkdir(parents=True, exist_ok=True)

app = FastAPI(title="Proj3 Scene Analyzer")
app.mount("/storage", StaticFiles(directory=str(STORAGE_DIR)), name="storage")

segmenter = YoloSegmenter()
reasoner = OpenAIReasoner()


@app.get("/health")
def health():
    return {"status": "ok"}


@app.post("/analyze-scene", response_model=SceneAnalysis)
async def analyze_scene(
    image: UploadFile = File(...),
    sceneId: str | None = Form(default=None),
    mock: bool = Form(default=False),
):
    scene_id = sceneId or uuid.uuid4().hex[:12]
    UPLOAD_DIR.mkdir(parents=True, exist_ok=True)
    CROP_DIR.mkdir(parents=True, exist_ok=True)

    suffix = Path(image.filename or "scene.png").suffix or ".png"
    image_path = UPLOAD_DIR / f"{scene_id}{suffix}"
    with image_path.open("wb") as output:
        shutil.copyfileobj(image.file, output)

    if mock:
        items = mock_items()
    else:
        items = segmenter.analyze(image_path, scene_id, CROP_DIR / scene_id)
        if not items:
            items = mock_items()

    interactions = reasoner.infer_interactions(items)
    analysis = SceneAnalysis(
        sceneId=scene_id,
        imagePath=str(image_path.as_posix()),
        items=items,
        interactions=interactions,
    )
    return JSONResponse(content=analysis.model_dump())
