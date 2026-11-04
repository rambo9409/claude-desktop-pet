"""Pack the V2.12 dragon in the Claude/DeepSeek illustration style.

Source pixels keep their generated alpha and color. Only rectangular crop,
uniform scaling, and canvas placement are used; no alpha threshold is applied
to the artwork. Thresholds below are measurements, never output edits.
"""
from __future__ import annotations

import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

DEFAULT_ROOT = Path(__file__).resolve().parent / "assets" / "generated"


def _bounds(image, threshold=8):
    box = image.getchannel("A").point(lambda value: 255 if value > threshold else 0).getbbox()
    if box is None:
        raise ValueError("Empty V2.12 dragon artwork")
    return box


def _source(path):
    image = Image.open(path).convert("RGBA")
    if image.getchannel("A").histogram()[0] < image.width * image.height * .02:
        raise ValueError("Dragon source must have transparent gutters")
    return image


def _pack_row(source, row):
    cells = [source.crop(tuple(cell["crop"])) for cell in row["cells"]]
    boxes = [_bounds(cell) for cell in cells]
    floor = max(box[3] for box in boxes)
    result = []
    for index, (cell, spec, box) in enumerate(zip(cells, row["cells"], boxes)):
        visible = _bounds(cell, 32)
        # Very faint generated RGB/alpha noise is permitted at the canvas edge;
        # material artwork may not be cut. Keep that low-alpha data unchanged.
        if min(visible[:2]) < 2 or visible[2] > cell.width - 2 or visible[3] > cell.height - 2:
            raise ValueError("Dragon crop cuts visible artwork: " + row["action"] + str(index))
        scale = row["scale"]
        pose = cell.crop(box)
        pose = pose.resize((round(pose.width * scale), round(pose.height * scale)), Image.Resampling.LANCZOS)
        if row.get("align") == "wall":
            x = 400 - pose.width
        else:
            anchor = spec.get("anchor", (box[0] + box[2]) / 2)
            x = round(256 + (box[0] - anchor) * scale)
        y = round(486 - (floor - box[1]) * scale) if row.get("align") in ("wall", "phase") else 486 - pose.height
        frame = Image.new("RGBA", (512, 512))
        frame.alpha_composite(pose, (x, y))
        extent = _bounds(frame)
        if min(extent[:2]) < 4 or max(extent[2:]) > 508:
            raise ValueError("Dragon pose clips output: " + row["action"] + str(index))
        result.append(frame)
    return result


def load_frames(generated_root=None):
    """Return the unchanged 27-key 512x512 RGBA runtime contract."""
    root = Path(generated_root) if generated_root is not None else DEFAULT_ROOT
    recipe = json.loads((root / "gpt6a-v212-crops.json").read_text(encoding="utf-8"))
    sources, rows = {}, {}
    for row in recipe["rows"]:
        if row["source"] not in sources:
            sources[row["source"]] = _source(root / row["source"])
        rows[row["action"]] = _pack_row(sources[row["source"]], row)
    expressions = rows["expressions"]
    result = {"idle": expressions[0], "happy-1": expressions[1], "happy-2": expressions[0],
              "pet-1": expressions[2], "pet-2": expressions[0], "rose-1": expressions[1],
              "rose-2": expressions[3], "typing-1": expressions[3], "typing-2": rows["ability"][1],
              "fall": rows["airborne"][0], "held": rows["airborne"][1]}
    for action in ("stroll", "walk", "climb-up", "ability"):
        if len(rows[action]) != 4:
            raise ValueError("Dragon action requires four phases: " + action)
        result.update({f"{action}-{i}": frame for i, frame in enumerate(rows[action], 1)})
    if len(result) != 27:
        raise ValueError("Dragon 27-key contract mismatch")
    return result


def build_preview(frames, destination):
    destination = Path(destination)
    destination.mkdir(parents=True, exist_ok=True)
    font = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 18)
    examples = ("idle", "stroll-1", "walk-2", "climb-up-3", "ability-3", "fall", "held")
    sheet = Image.new("RGB", (1540, 520), "#F8F5FF")
    draw = ImageDraw.Draw(sheet)
    for row, (background, foreground) in enumerate((("#F8F5FF", "#514264"), ("#24202B", "#F4EDFF"))):
        draw.rectangle((0, row * 260, sheet.width, (row + 1) * 260), fill=background)
        for col, name in enumerate(examples):
            pose = frames[name].resize((220, 220), Image.Resampling.LANCZOS)
            sheet.paste(pose, (col * 220, row * 260 + 30), pose)
            draw.text((col * 220 + 6, row * 260 + 5), "GPT6A · " + name, font=font, fill=foreground)
    sheet.save(destination / "gpt6a-v212-contact.png")
    gait = Image.new("RGB", (960, 1040), "#F8F5FF")
    draw = ImageDraw.Draw(gait)
    for row, action in enumerate(("stroll", "walk", "climb-up", "ability")):
        for col in range(4):
            pose = frames[f"{action}-{col+1}"].resize((240, 240), Image.Resampling.LANCZOS)
            gait.paste(pose, (col * 240, row * 260 + 20), pose)
            draw.text((col * 240 + 8, row * 260), f"{action} {col+1}", font=font, fill="#514264")
    gait.save(destination / "gpt6a-v212-gait.png")
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
    animation[0].save(destination / "gpt6a-v212-motion.gif", save_all=True, append_images=animation[1:], duration=60, loop=0, disposal=2)


if __name__ == "__main__":
    output = Path(__file__).resolve().parent / "artifacts" / "v212"
    frames = load_frames()
    build_preview(frames, output)
    print(f"Packed {len(frames)} matching-style dragon frames. Preview: {output}")
