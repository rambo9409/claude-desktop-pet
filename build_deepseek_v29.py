"""Pack the reference-matched whale-maid character without modifying artwork.

Imported by build_assets.py. This module only crops source atlas cells,
uniformly scales them, and places the original RGBA pixels on runtime canvases.
The command-line entry emits review files in artifacts/v29, never characters.zip.
"""
from __future__ import annotations

import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont


def _bounds(image: Image.Image) -> tuple[int, int, int, int]:
    box = image.getchannel("A").point(lambda value: 255 if value > 8 else 0).getbbox()
    if box is None:
        raise ValueError("Empty DeepSeek maid sprite")
    return box


def _check(image: Image.Image, label: str) -> Image.Image:
    box = _bounds(image)
    if min(box[:2]) < 4 or max(box[2:]) > 508:
        raise ValueError(f"Clipped DeepSeek maid pose: {label} {box}")
    return image


def load_frames(generated_root: Path | str | None = None) -> dict[str, Image.Image]:
    """Return 27 unprefixed 512x512 RGBA frames, with floor baseline y=486."""
    root = Path(generated_root) if generated_root is not None else Path(__file__).resolve().parent / "assets" / "generated"
    recipe = json.loads((root / "deepseek-maid-v29-crops.json").read_text(encoding="utf-8"))
    source = Image.open(root / recipe["source"]).convert("RGBA")
    if source.getchannel("A").getextrema()[0] != 0:
        raise ValueError("DeepSeek maid atlas requires original alpha transparency")
    scale = recipe["scale"]
    rows: list[list[Image.Image]] = []
    for row, crops in enumerate(recipe["rows"]):
        cells = [source.crop(tuple(crop)) for crop in crops]
        boxes = [_bounds(cell) for cell in cells]
        pivot = sum((box[0] + box[2]) / 2 for box in boxes) / len(boxes)
        baseline = max(box[3] for box in boxes)
        packed: list[Image.Image] = []
        for col, (cell, box) in enumerate(zip(cells, boxes)):
            pose = cell.crop(box)
            pose = pose.resize((round(pose.width * scale), round(pose.height * scale)), Image.Resampling.LANCZOS)
            x = 400 - pose.width if row == 3 else round(256 + (box[0] - pivot) * scale)
            y = 486 - pose.height if row == 0 else round(486 - (baseline - box[1]) * scale)
            frame = Image.new("RGBA", (512, 512))
            frame.alpha_composite(pose, (x, y))
            packed.append(_check(frame, f"{row},{col}"))
        rows.append(packed)
    result = {"idle": rows[0][0], "happy-1": rows[0][1], "happy-2": rows[0][0],
              "pet-1": rows[0][2], "pet-2": rows[0][0], "rose-1": rows[0][1], "rose-2": rows[0][3],
              "typing-1": rows[0][3], "typing-2": rows[4][1]}
    for row, action in ((1, "stroll"), (2, "walk"), (3, "climb-up"), (4, "ability")):
        for index, frame in enumerate(rows[row], 1):
            result[f"{action}-{index}"] = frame
    airborne = Image.open(root / recipe["airborneSource"]).convert("RGBA")
    if airborne.getchannel("A").getextrema()[0] != 0:
        raise ValueError("DeepSeek maid air atlas requires original alpha")
    for item in recipe["airborne"]:
        pose = airborne.crop(tuple(item["crop"]))
        pose = pose.crop(_bounds(pose))
        pose = pose.resize((round(pose.width * item["scale"]), round(pose.height * item["scale"])), Image.Resampling.LANCZOS)
        frame = Image.new("RGBA", (512, 512))
        frame.alpha_composite(pose, ((512 - pose.width) // 2, 486 - pose.height))
        result[item["name"]] = _check(frame, item["name"])
    if len(result) != 27:
        raise ValueError(f"Expected 27 DeepSeek maid frames, got {len(result)}")
    return result


def build_preview(frames: dict[str, Image.Image], destination: Path) -> None:
    destination.mkdir(parents=True, exist_ok=True)
    font_path = Path("C:/Windows/Fonts/msyh.ttc")
    font = ImageFont.truetype(str(font_path), 18) if font_path.exists() else ImageFont.load_default()
    actions = ("idle", "stroll-1", "walk-2", "climb-up-3", "ability-3", "fall", "held")
    sheet = Image.new("RGB", (1540, 520), "#f1f8fc")
    draw = ImageDraw.Draw(sheet)
    for row, (bg, fg) in enumerate((("#f1f8fc", "#23466c"), ("#1c2b3c", "#edf6ff"))):
        draw.rectangle((0, row * 260, sheet.width, (row + 1) * 260), fill=bg)
        for col, name in enumerate(actions):
            pose = frames[name].resize((220, 220), Image.Resampling.LANCZOS)
            sheet.paste(pose, (col * 220, row * 260 + 30), pose)
            draw.text((col * 220 + 6, row * 260 + 5), "DeepSeek · " + name, font=font, fill=fg)
    sheet.save(destination / "deepseek-v29-contact.png")
    gait = Image.new("RGB", (960, 1040), "#f1f8fc")
    draw = ImageDraw.Draw(gait)
    for row, action in enumerate(("stroll", "walk", "climb-up", "ability")):
        for col in range(4):
            pose = frames[f"{action}-{col+1}"].resize((240, 240), Image.Resampling.LANCZOS)
            gait.paste(pose, (col * 240, row * 260 + 20), pose)
            draw.text((col * 240 + 8, row * 260), f"{action} {col+1}", font=font, fill="#23466c")
    gait.save(destination / "deepseek-v29-gait.png")
    images = []
    for tick in range(48):
        image = Image.new("RGB", (1000, 280), "#f1f8fc")
        draw = ImageDraw.Draw(image)
        for col, action in enumerate(("stroll", "walk", "climb-up", "ability")):
            index = tick // (6 if action == "stroll" else 3) % 4 + 1
            pose = frames[f"{action}-{index}"].resize((240, 240), Image.Resampling.LANCZOS)
            image.paste(pose, (col * 250 + 5, 30), pose)
            draw.text((col * 250 + 8, 4), action, font=font, fill="#23466c")
        images.append(image)
    images[0].save(destination / "deepseek-v29-motion.gif", save_all=True, append_images=images[1:], duration=60, loop=0, disposal=2)


if __name__ == "__main__":
    result = load_frames()
    output = Path(__file__).resolve().parent / "artifacts" / "v29"
    build_preview(result, output)
    print(f"Packed {len(result)} DeepSeek whale-maid frames. Preview: {output}")
