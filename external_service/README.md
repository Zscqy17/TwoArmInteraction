# Proj3 External Scene Analyzer

This service receives a scene image from Unity, runs local YOLO segmentation, then asks OpenAI for the item interaction graph.

## Setup

```powershell
cd external_service
python -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -r requirements.txt
$env:OPENAI_API_KEY="your-api-key"
uvicorn proj3_analyzer.app:app --reload --host 127.0.0.1 --port 8000
```

If `OPENAI_API_KEY` is not set, the service still runs and returns mock interactions for common test items.

## API

```http
POST /analyze-scene
Content-Type: multipart/form-data

image=<png-or-jpg>
sceneId=<optional id>
mock=<true|false>
```

Response:

```json
{
  "sceneId": "scene_001",
  "imagePath": "storage/uploads/scene_001.png",
  "items": [
    {
      "id": "knife_0",
      "label": "knife",
      "bounds": { "x": 0.24, "y": 0.66, "width": 0.5, "height": 0.16 },
      "confidence": 0.92,
      "cropPath": "storage/crops/scene_001/knife_0.png"
    }
  ],
  "interactions": [
    {
      "sourceId": "knife_0",
      "targetId": "apple_1",
      "action": "cut",
      "instruction": "cut the apple with the knife"
    }
  ]
}
```
