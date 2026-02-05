from __future__ import annotations

import base64
import os
from io import BytesIO
from pathlib import Path
from typing import Any, Dict, Optional

import requests
from PIL import Image
from fastmcp import FastMCP

mcp = FastMCP("vision-bridge")

# Config via env (set these in mcp.json)
WORKSPACE_ROOT = Path(os.getenv("WORKSPACE_ROOT", ".")).resolve()
OLLAMA_HOST = os.getenv("OLLAMA_HOST", "http://localhost:11434").rstrip("/")
OLLAMA_MODEL = os.getenv("OLLAMA_MODEL", "qwen2.5vl:7b")
MAX_IMAGE_EDGE = int(os.getenv("VISION_MAX_EDGE", "1600"))
TIMEOUT_S = int(os.getenv("VISION_TIMEOUT_S", "120"))


def _resolve_workspace_path(p: str) -> Path:
    """
    Resolve an image path against WORKSPACE_ROOT and block path traversal.
    Accepts relative paths like 'assets/ui/screen.png' or absolute paths inside the workspace.
    """
    raw = Path(p)
    candidate = (WORKSPACE_ROOT / raw).resolve() if not raw.is_absolute() else raw.resolve()

    try:
        candidate.relative_to(WORKSPACE_ROOT)
    except ValueError as ex:
        raise ValueError(f"Path must be within workspace root: {WORKSPACE_ROOT}") from ex

    if not candidate.exists():
        raise FileNotFoundError(f"File not found: {candidate}")

    if candidate.suffix.lower() not in {".png", ".jpg", ".jpeg", ".webp", ".bmp"}:
        raise ValueError(f"Unsupported image type: {candidate.suffix}")

    return candidate


def _load_image_b64(path: Path) -> str:
    """
    Load image, optionally downscale, return base64-encoded PNG bytes
    (Ollama accepts base64 images in the message.images array).
    """
    img = Image.open(path).convert("RGB")

    # Downscale to keep requests fast + predictable
    w, h = img.size
    edge = max(w, h)
    if edge > MAX_IMAGE_EDGE:
        scale = MAX_IMAGE_EDGE / edge
        img = img.resize((int(w * scale), int(h * scale)))

    out = BytesIO()
    img.save(out, format="PNG", optimize=True)
    return base64.b64encode(out.getvalue()).decode("ascii")


def _ollama_chat_with_image(image_b64: str, prompt: str, json_mode: bool = False) -> str:
    payload: Dict[str, Any] = {
        "model": OLLAMA_MODEL,
        "messages": [
            {
                "role": "user",
                "content": prompt,
                "images": [image_b64],
            }
        ],
        "stream": False,
    }

    # Ollama supports `format: "json"` for JSON mode (best effort).
    if json_mode:
        payload["format"] = "json"

    r = requests.post(
        f"{OLLAMA_HOST}/api/chat",
        json=payload,
        timeout=TIMEOUT_S,
    )
    r.raise_for_status()
    data = r.json()
    return (data.get("message", {}) or {}).get("content", "").strip()


@mcp.tool
def vision_ask(image_path: str, question: str) -> Dict[str, Any]:
    """
    Ask a vision-capable model a question about an image file in the workspace.
    Use for: "What’s in this screenshot?", "Which button is selected?", "Describe layout", etc.
    """
    path = _resolve_workspace_path(image_path)
    b64 = _load_image_b64(path)
    answer = _ollama_chat_with_image(b64, question, json_mode=False)
    return {
        "model": OLLAMA_MODEL,
        "image": str(path.relative_to(WORKSPACE_ROOT)),
        "answer": answer,
    }


@mcp.tool
def vision_ocr(image_path: str) -> Dict[str, Any]:
    """
    Extract visible text from an image (OCR-style, best effort).
    """
    path = _resolve_workspace_path(image_path)
    b64 = _load_image_b64(path)
    prompt = (
        "Extract ALL visible text from this image.\n"
        "Rules:\n"
        "- Keep original spelling, casing, punctuation.\n"
        "- Preserve line breaks.\n"
        "- If something is partially unreadable, write [illegible].\n"
        "Return only the extracted text."
    )
    text = _ollama_chat_with_image(b64, prompt, json_mode=False)
    return {
        "model": OLLAMA_MODEL,
        "image": str(path.relative_to(WORKSPACE_ROOT)),
        "text": text,
    }


@mcp.tool
def vision_ui_spec(image_path: str) -> Dict[str, Any]:
    """
    Turn a UI screenshot into a structured JSON spec the agent can implement against.
    (Returns JSON when possible; falls back to raw text if the model outputs something else.)
    """
    path = _resolve_workspace_path(image_path)
    b64 = _load_image_b64(path)

    prompt = (
        "You are extracting a UI spec from a screenshot.\n"
        "Return JSON ONLY with this shape:\n"
        "{\n"
        '  "summary": string,\n'
        '  "elements": [\n'
        "    {\n"
        '      "type": "button"|"text"|"input"|"checkbox"|"toggle"|"image"|"icon"|"panel"|"list"|"table"|"link"|"other",\n'
        '      "label": string|null,\n'
        '      "role": string|null,\n'
        '      "bounds": {"x": number, "y": number, "w": number, "h": number},\n'
        '      "notes": string|null\n'
        "    }\n"
        "  ]\n"
        "}\n"
        "Coordinates are in pixels relative to the top-left of the image.\n"
        "If you can’t estimate bounds reliably, still include the element with approximate bounds and note it in notes."
    )

    raw = _ollama_chat_with_image(b64, prompt, json_mode=True)

    # Best-effort parse: if it isn't valid JSON, return raw.
    import json
    try:
        parsed = json.loads(raw)
        return {
            "model": OLLAMA_MODEL,
            "image": str(path.relative_to(WORKSPACE_ROOT)),
            "ui": parsed,
        }
    except Exception:
        return {
            "model": OLLAMA_MODEL,
            "image": str(path.relative_to(WORKSPACE_ROOT)),
            "ui_raw": raw,
        }


if __name__ == "__main__":
    # stdio transport is what VS Code uses for local MCP servers
    mcp.run()
