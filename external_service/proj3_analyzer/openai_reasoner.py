from __future__ import annotations

import json
import os
from typing import List

from .schemas import InteractionEdge, SceneItem


PROMPT = """You are building an interaction graph for a Unity proxy menu.

Given detected scene items, decide which ordered item pairs can support a meaningful physical interaction.
Return only JSON in this shape:
{
  "interactions": [
    {
      "sourceId": "id of the item the user holds",
      "targetId": "id of the item it interacts with",
      "action": "short verb phrase",
      "instruction": "short imperative instruction"
    }
  ]
}

Rules:
- Use only ids from the provided items.
- Do not invent objects.
- Prefer obvious, actionable interactions.
- Include both directions only when the action would genuinely differ by source item.
- Return an empty interactions array if no pair is meaningful.
"""


class OpenAIReasoner:
    def __init__(self, model: str = "gpt-4.1-mini") -> None:
        self.model = model

    def infer_interactions(self, items: List[SceneItem]) -> List[InteractionEdge]:
        if not os.getenv("OPENAI_API_KEY"):
            return mock_interactions(items)

        from openai import OpenAI

        client = OpenAI()
        item_payload = [
            {
                "id": item.id,
                "label": item.label,
                "bounds": item.bounds.model_dump(),
                "confidence": item.confidence,
            }
            for item in items
        ]

        response = client.chat.completions.create(
            model=self.model,
            messages=[
                {"role": "system", "content": PROMPT},
                {"role": "user", "content": json.dumps({"items": item_payload}, ensure_ascii=False)},
            ],
            response_format={"type": "json_object"},
        )

        text = response.choices[0].message.content or "{}"
        data = json.loads(text)
        raw_edges = data.get("interactions", [])
        valid_ids = {item.id for item in items}
        edges: List[InteractionEdge] = []

        for raw in raw_edges:
            source_id = raw.get("sourceId")
            target_id = raw.get("targetId")
            if source_id not in valid_ids or target_id not in valid_ids or source_id == target_id:
                continue
            edges.append(
                InteractionEdge(
                    sourceId=source_id,
                    targetId=target_id,
                    action=str(raw.get("action", "interact")).strip() or "interact",
                    instruction=str(raw.get("instruction", "")).strip()
                    or f"use {source_id} with {target_id}",
                )
            )

        return edges


def mock_interactions(items: List[SceneItem]) -> List[InteractionEdge]:
    by_label = {item.label.lower(): item for item in items}
    edges: List[InteractionEdge] = []

    def add(source_label: str, target_label: str, action: str, instruction: str) -> None:
        source = by_label.get(source_label)
        target = by_label.get(target_label)
        if source is not None and target is not None:
            edges.append(
                InteractionEdge(
                    sourceId=source.id,
                    targetId=target.id,
                    action=action,
                    instruction=instruction,
                )
            )

    add("knife", "apple", "cut", "cut the apple with the knife")
    add("bottle", "cup", "pour", "pour from the bottle into the cup")
    return edges
