from __future__ import annotations

from typing import List, Optional

from pydantic import BaseModel, Field


class Bounds(BaseModel):
    x: float = Field(ge=0.0, le=1.0)
    y: float = Field(ge=0.0, le=1.0)
    width: float = Field(gt=0.0, le=1.0)
    height: float = Field(gt=0.0, le=1.0)


class SceneItem(BaseModel):
    id: str
    label: str
    bounds: Bounds
    confidence: float = 1.0
    cropPath: Optional[str] = None
    maskPath: Optional[str] = None


class InteractionEdge(BaseModel):
    sourceId: str
    targetId: str
    action: str
    instruction: str


class SceneAnalysis(BaseModel):
    sceneId: str
    imagePath: Optional[str] = None
    items: List[SceneItem]
    interactions: List[InteractionEdge] = Field(default_factory=list)


class ReasoningRequest(BaseModel):
    sceneId: str
    items: List[SceneItem]
