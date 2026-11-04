"""Pack the silver/lilac reference dragon using original transparent artwork.

Only atlas extraction, uniform scaling and placement happen here. The command
line writes review artifacts and never changes the production resource ZIP.
"""
from __future__ import annotations

import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont


DEFAULT_ROOT = Path(__file__).resolve().parent / "assets" / "generated"


def _bounds(image: Image.Image) -> tuple[int, int, int, int]:
    box = image.getchannel("A").point(lambda value: 255 if value > 8 else 0).getbbox()
    if box is None:
        raise ValueError("Empty GPT6A dragon sprite")
    return box


def _source(path: Path) -> Image.Image:
    image = Image.open(path).convert("RGBA")
    # An RGBA file can still contain a painted translucent background. These
    # spacious atlases must include genuine empty gutters, not just a few zeros.
    if image.getchannel("A").histogram()[0] < image.width * image.height * .02:
        raise ValueError("GPT6A artwork requires genuinely transparent atlas gutters")
    return image


def _checked(image: Image.Image, label: str) -> Image.Image:
    box = _bounds(image)
    if min(box[:2]) < 4 or max(box[2:]) > 508:
        raise ValueError(f"Clipped GPT6A dragon pose: {label} {box}")
    return image


def _cell(source: Image.Image, rectangle, label: str) -> Image.Image:
    cell = source.crop(tuple(rectangle))
    left, top, right, bottom = _bounds(cell)
    if left < 2 or top < 2 or right > cell.width - 2 or bottom > cell.height - 2:
        raise ValueError(f"GPT6A source crop touches painted content: {label}")
    return cell


def load_frames(generated_root: Path | str | None = None) -> dict[str, Image.Image]:
    """Return the existing 27-key contract as 512x512 straight-alpha RGBA."""
    root = Path(generated_root) if generated_root is not None else DEFAULT_ROOT
    recipe = json.loads((root / "gpt6a-v210-crops.json").read_text(encoding="utf-8"))
    sources = {}
    rows = []
    for row, row_spec in enumerate(recipe["rows"]):
        source_name = row_spec["source"]
        if source_name not in sources:
            sources[source_name] = _source(root / source_name)
        source = sources[source_name]
        crops = row_spec["crops"]
        if len(crops) != 4:
            raise ValueError("GPT6A atlas must have four poses per action")
        cells = [_cell(source, crop, f"row {row} cell {col}") for col, crop in enumerate(crops)]
        boxes = [_bounds(cell) for cell in cells]
        pivot = sum((box[0] + box[2]) / 2 for box in boxes) / 4
        floor = max(box[3] for box in boxes)
        scale = row_spec["scale"]
        packed = []
        for col, (cell, box) in enumerate(zip(cells, boxes)):
            pose = cell.crop(box)
            pose = pose.resize((round(pose.width * scale), round(pose.height * scale)), Image.Resampling.LANCZOS)
            x = 400 - pose.width if row == 3 else round(256 + (box[0] - pivot) * scale)
            y = 486 - pose.height if row == 0 else round(486 - (floor - box[1]) * scale)
            frame = Image.new("RGBA", (512, 512))
            frame.alpha_composite(pose, (x, y))
            packed.append(_checked(frame, f"{row},{col}"))
        rows.append(packed)
    if len(rows) != 5:
        raise ValueError("GPT6A atlas must have five action rows")
    result = {"idle": rows[0][0], "happy-1": rows[0][1], "happy-2": rows[0][0],
              "pet-1": rows[0][2], "pet-2": rows[0][0], "rose-1": rows[0][1],
              "rose-2": rows[0][3], "typing-1": rows[0][3], "typing-2": rows[4][1]}
    for row, action in ((1, "stroll"), (2, "walk"), (3, "climb-up"), (4, "ability")):
        for index, pose in enumerate(rows[row], 1):
            result[f"{action}-{index}"] = pose
    airborne = _source(root / recipe["airborneSource"])
    for item in recipe["airborne"]:
        pose = _cell(airborne, item["crop"], item["name"])
        pose = pose.crop(_bounds(pose))
        scale = item["scale"]
        pose = pose.resize((round(pose.width * scale), round(pose.height * scale)), Image.Resampling.LANCZOS)
        frame = Image.new("RGBA", (512, 512))
        frame.alpha_composite(pose, ((512 - pose.width) // 2, 486 - pose.height))
        result[item["name"]] = _checked(frame, item["name"])
    expected = {"idle", "happy-1", "happy-2", "pet-1", "pet-2", "rose-1", "rose-2", "typing-1", "typing-2", "fall", "held"}
    expected.update(f"{action}-{index}" for action in ("stroll", "walk", "climb-up", "ability") for index in range(1, 5))
    if set(result) != expected:
        raise ValueError("GPT6A dragon frame contract mismatch")
    return result


def build_preview(frames: dict[str, Image.Image], destination: Path) -> None:
    destination.mkdir(parents=True, exist_ok=True)
    font_path = Path("C:/Windows/Fonts/msyh.ttc")
    font = ImageFont.truetype(str(font_path), 18) if font_path.exists() else ImageFont.load_default()
    examples = ("idle", "stroll-1", "walk-2", "climb-up-3", "ability-3", "fall", "held")
    sheet = Image.new("RGB", (1540, 520), "#F8F5FF")
    draw = ImageDraw.Draw(sheet)
    for row, (background, foreground) in enumerate((("#F8F5FF", "#514264"), ("#242031", "#F4EDFF"))):
        draw.rectangle((0, row * 260, sheet.width, (row + 1) * 260), fill=background)
        for col, name in enumerate(examples):
            pose = frames[name].resize((220, 220), Image.Resampling.LANCZOS)
            sheet.paste(pose, (col * 220, row * 260 + 30), pose)
            draw.text((col * 220 + 6, row * 260 + 5), "GPT6A · " + name, font=font, fill=foreground)
    sheet.save(destination / "gpt6a-v210-contact.png")
    gait = Image.new("RGB", (960, 1040), "#F8F5FF")
    draw = ImageDraw.Draw(gait)
    for row, action in enumerate(("stroll", "walk", "climb-up", "ability")):
        for col in range(4):
            pose = frames[f"{action}-{col+1}"].resize((240, 240), Image.Resampling.LANCZOS)
            gait.paste(pose, (col * 240, row * 260 + 20), pose)
            draw.text((col * 240 + 8, row * 260), f"{action} {col+1}", font=font, fill="#514264")
    gait.save(destination / "gpt6a-v210-gait.png")
    animation = []
    for tick in range(48):
        canvas = Image.new("RGB", (1000, 280), "#F8F5FF")
        draw = ImageDraw.Draw(canvas)
        for col, action in enumerate(("stroll", "walk", "climb-up", "ability")):
            index = tick // (6 if action == "stroll" else 3) % 4 + 1
            pose = frames[f"{action}-{index}"].resize((240, 240), Image.Resampling.LANCZOS)
            canvas.paste(pose, (col * 250 + 5, 30), pose)
            draw.text((col * 250 + 8, 4), action, font=font, fill="#514264")
        animation.append(canvas)
    animation[0].save(destination / "gpt6a-v210-motion.gif", save_all=True, append_images=animation[1:], duration=60, loop=0, disposal=2)


if __name__ == "__main__":
    output = Path(__file__).resolve().parent / "artifacts" / "v210"
    frames = load_frames()
    build_preview(frames, output)
    print(f"Packed {len(frames)} reference dragon frames. Preview: {output}")
