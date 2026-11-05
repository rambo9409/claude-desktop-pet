"""Extract only the two AI companions' corrected four-phase running cycles.

Keep the generated colors and alpha. These operations only crop rectangles,
uniformly resize, and position each figure on the shared runtime canvas.
"""
from __future__ import annotations

import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageOps

DEFAULT_ROOT = Path(__file__).resolve().parent / "assets/generated"


def _bounds(image, threshold=8):
    box = image.getchannel("A").point(lambda value: 255 if value > threshold else 0).getbbox()
    if box is None:
        raise ValueError("Empty running pose")
    return box


def load_run_frames(character, generated_root=None):
    """Return just walk-1..4 (the historical resource name for running)."""
    root = Path(generated_root) if generated_root is not None else DEFAULT_ROOT
    recipe = json.loads((root / "ai-runs-v213-crops.json").read_text(encoding="utf-8"))
    if character not in recipe["characters"]:
        raise ValueError("Only GPT6A and DeepSeek have V2.13 running overrides")
    row = recipe["characters"][character]
    source = Image.open(root / recipe["source"]).convert("RGBA")
    if source.getchannel("A").histogram()[0] < source.width * source.height * .02:
        raise ValueError("Running atlas needs transparent gutters")
    if len(row["cells"]) != 4:
        raise ValueError("A run cycle must have four consecutive phases")
    result = {}
    for index, spec in enumerate(row["cells"], 1):
        cell = source.crop(tuple(spec["crop"]))
        material = _bounds(cell, 32)
        if min(material[:2]) < 2 or material[2] > cell.width - 2 or material[3] > cell.height - 2:
            raise ValueError("Run cell cuts material artwork: " + character + str(index))
        box = _bounds(cell)
        scale = row["scale"]
        pose = cell.crop(box)
        pose = pose.resize((round(pose.width * scale), round(pose.height * scale)), Image.Resampling.LANCZOS)
        x = round(row["target_anchor_x"] + (box[0] - spec["anchor_x"]) * scale)
        # One measured source ground/pelvis relationship per cycle preserves
        # the painted contact/passing motion instead of pinning every sole.
        y = round(row["target_ground_y"] + (box[1] - row["source_ground_y"]) * scale)
        frame = Image.new("RGBA", (512, 512))
        frame.alpha_composite(pose, (x, y))
        extent = _bounds(frame)
        if min(extent[:2]) < 4 or max(extent[2:]) > 508:
            raise ValueError("Run pose clips output: " + character + str(index))
        result["walk-" + str(index)] = frame
    return result


def build_preview(destination, generated_root=None):
    destination = Path(destination)
    destination.mkdir(parents=True, exist_ok=True)
    font = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 18)
    chars = ("gpt6a", "deepseek")
    frames = {ch: load_run_frames(ch, generated_root) for ch in chars}
    sheet = Image.new("RGB", (1120, 600), "#F8F5FF")
    draw = ImageDraw.Draw(sheet)
    for row, character in enumerate(chars):
        for col in range(4):
            pose = frames[character]["walk-" + str(col + 1)].resize((280, 280), Image.Resampling.LANCZOS)
            sheet.paste(pose, (col * 280, row * 300 + 20), pose)
            draw.text((col * 280 + 8, row * 300), character + " run " + str(col + 1), font=font, fill="#514264")
            draw.line((col * 280, row * 300 + 286, col * 280 + 279, row * 300 + 286), fill="#A1A0AD")
    sheet.save(destination / "ai-runs-v213-contact.png")
    cycle = []
    for i in range(4):
        canvas = Image.new("RGB", (660, 380), "#F8F5FF")
        draw = ImageDraw.Draw(canvas)
        for col, character in enumerate(chars):
            pose = frames[character]["walk-" + str(i+1)].resize((330, 330), Image.Resampling.LANCZOS)
            canvas.paste(pose, (col * 330, 35), pose)
            draw.text((col * 330 + 10, 8), character + " · run " + str(i+1), font=font, fill="#514264")
            draw.line((col * 330, 349, col * 330 + 329, 349), fill="#A1A0AD")
        cycle.append(canvas)
    # Runtime: distance driven, stride=width*.42, speed=(95..140)*width/165.
    # At midpoint speed this is .590s/cycle; GIF quantization gives .600s.
    cycle[0].save(destination / "ai-runs-v213-cycle.gif", save_all=True, append_images=cycle[1:], duration=150, loop=0, disposal=2)
    cycle[0].save(destination / "ai-runs-v213-fast-cycle.gif", save_all=True, append_images=cycle[1:], duration=[120,130,120,130], loop=0, disposal=2)
    moving = []
    width, speed, stride, dt = 256, 117.5 * 256 / 165, 256 * .42, .04
    for tick in range(80):
        distance = tick * speed * dt
        half = 40 * speed * dt
        direction = 1 if tick < 40 else -1
        offset = distance if tick < 40 else 2 * half - distance
        phase = int((distance / stride % 1) * 4) + 1
        canvas = Image.new("RGB", (720, 600), "#F8F5FF")
        draw = ImageDraw.Draw(canvas)
        for row, character in enumerate(chars):
            pose = frames[character]["walk-" + str(phase)].resize((width, width), Image.Resampling.LANCZOS)
            if direction < 0:
                pose = ImageOps.mirror(pose)
            canvas.paste(pose, (round(60 + offset), row * 300 + 30), pose)
            draw.text((12, row * 300 + 4), character + " · 距离驱动跑步", font=font, fill="#514264")
            draw.line((0, row * 300 + 273, 720, row * 300 + 273), fill="#A1A0AD")
        moving.append(canvas)
    moving[0].save(destination / "ai-runs-v213-moving.gif", save_all=True, append_images=moving[1:], duration=40, loop=0, disposal=2)
    return frames


if __name__ == "__main__":
    output = Path(__file__).resolve().parent / "artifacts/v213"
    frames = build_preview(output)
    print("Prepared " + str(sum(len(group) for group in frames.values())) + " run frames. " + str(output))
