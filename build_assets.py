from __future__ import annotations

from io import BytesIO
import json
from pathlib import Path
from zipfile import ZIP_STORED, ZipFile

from PIL import Image, ImageDraw, ImageFont
from build_deepseek_v29 import load_frames as load_deepseek_reference_frames
from build_kolo_v29 import load_frames as load_kolo_v29_frames
from build_kolo_v29 import load_hitboxes as load_kolo_v29_hitboxes
from build_gpt6a_v212 import load_frames as load_dragon_reference_frames
from build_gpt6a_v212 import build_preview as build_dragon_preview
from build_kolo_v211 import load_stroll_frames, load_stroll_hitboxes, load_disguise_frames
from build_kolo_v211 import build_preview as build_kolo_v211_preview

OUTPUT = Path(__file__).resolve().parent / "assets"
GENERATED_ROOT = OUTPUT / "generated"

CHARACTERS = {
    "kolo": {"name": "珂洛"},
    "hutao": {"name": "胡桃"},
    "ganyu": {"name": "甘雨"},
    "varesa": {"name": "瓦雷莎"},
    "gpt6a": {"name": "GPT6A · 龙女"},
    "deepseek": {"name": "DeepSeek · 小肥鱼"},
}

AI_CHARACTERS = ("gpt6a", "deepseek")

SPECIAL_CHARACTERS = ("hutao", "ganyu", "varesa")


def load_kolo_reference_frames(generated_root=None):
    root = GENERATED_ROOT if generated_root is None else generated_root
    frames = load_kolo_v29_frames(root)
    frames.update(load_stroll_frames(root))
    frames.update(load_disguise_frames(root))
    return frames


def load_notebook_hitboxes(generated_root=None):
    root = GENERATED_ROOT if generated_root is None else generated_root
    boxes = load_kolo_v29_hitboxes(root)
    boxes.update(load_stroll_hitboxes(root))
    return boxes

POSES = {
    "idle": (0, 0, 0.0, 1.0, 1.0),
    "happy-1": (-2, -4, -1.1, 1.01, 1.01),
    "happy-2": (2, 0, 1.1, 1.01, 0.99),
    "pet-1": (-1, 2, -0.7, 1.02, 0.98),
    "pet-2": (1, 5, 0.7, 1.03, 0.97),
    "rose-1": (-2, -2, -0.8, 1.0, 1.0),
    "rose-2": (2, -4, 0.8, 1.0, 1.01),
    "typing-1": (0, 1, -0.4, 1.0, 0.99),
    "typing-2": (0, -2, 0.4, 1.0, 1.01),
}


def build_special_base(character: str) -> Image.Image:
    # Artwork is generated as a complete chibi sprite. Only normalize the
    # transparent canvas here; never splice an icon onto a full-size body.
    filename = "kolo-chibi-v22.png" if character == "kolo" else f"{character}-chibi.png"
    source = Image.open(GENERATED_ROOT / filename).convert("RGBA")
    alpha = source.getchannel("A")
    if alpha.getextrema()[0] == 255:
        raise ValueError(f"{character} requires a transparent source image")
    # Ignore near-invisible alpha residue when finding the artwork bounds;
    # preserve the original alpha on the cropped image.
    bounds = alpha.point(lambda value: 255 if value > 8 else 0).getbbox()
    if bounds is None:
        raise ValueError(f"{character} source is empty")
    source = source.crop(bounds)
    # Hu Tao's hat adds height above her head; leave room for it without
    # shrinking her face/body below the other characters' visual scale.
    source.thumbnail((400, 450 if character == "hutao" else 410), Image.Resampling.LANCZOS)
    frame = Image.new("RGBA", (512, 512), (0, 0, 0, 0))
    frame.alpha_composite(source, ((512 - source.width) // 2, 486 - source.height))
    return frame


def transform_frame(image: Image.Image, pose: tuple[int, int, float, float, float]) -> Image.Image:
    dx, dy, angle, scale_x, scale_y = pose
    width = max(1, int(round(512 * scale_x)))
    height = max(1, int(round(512 * scale_y)))
    if width > image.width and height < image.height:
        # Preserve the legacy horizontal-then-vertical sampling order. Pillow 13
        # optimizes this mixed-scale case to vertical first, changing old poses.
        # Premultiply once so the two passes retain the original alpha behavior.
        scaled = image.convert("RGBa").resize((width, image.height), Image.Resampling.LANCZOS)
        scaled = scaled.resize((width, height), Image.Resampling.LANCZOS).convert("RGBA")
    else:
        scaled = image.resize((width, height), Image.Resampling.LANCZOS)
    canvas = Image.new("RGBA", (512, 512), (0, 0, 0, 0))
    canvas.alpha_composite(scaled, ((512 - width) // 2, (512 - height) // 2))
    if angle:
        canvas = canvas.rotate(angle, Image.Resampling.BICUBIC, expand=False)
    moved = Image.new("RGBA", (512, 512), (0, 0, 0, 0))
    moved.alpha_composite(canvas, (dx, dy))
    return moved


def add_action_mark(image: Image.Image, frame_name: str) -> Image.Image:
    overlay = Image.new("RGBA", (1024, 1024), (0, 0, 0, 0))
    draw = ImageDraw.Draw(overlay)
    if frame_name.startswith("happy"):
        for x, y in ((120, 155), (392, 132), (416, 220)):
            draw.polygon([(x * 2, (y - 13) * 2), ((x + 6) * 2, (y - 3) * 2), ((x + 16) * 2, y * 2), ((x + 6) * 2, (y + 4) * 2), (x * 2, (y + 15) * 2), ((x - 5) * 2, (y + 4) * 2), ((x - 15) * 2, y * 2), ((x - 5) * 2, (y - 3) * 2)], fill=(181, 232, 255, 230))
    elif frame_name.startswith("pet"):
        for x, y in ((112, 140), (401, 170)):
            draw.ellipse((x * 2, y * 2, (x + 30) * 2, (y + 26) * 2), fill=(244, 123, 156, 225))
            draw.polygon([((x + 1) * 2, (y + 12) * 2), ((x + 29) * 2, (y + 12) * 2), ((x + 15) * 2, (y + 38) * 2)], fill=(244, 123, 156, 225))
    elif frame_name.startswith("rose"):
        x, y = 350, 292
        draw.line((x * 2, y * 2, (x - 18) * 2, (y + 74) * 2), fill=(58, 137, 89, 255), width=8)
        for dx, dy in ((0, 0), (-12, 5), (11, 7), (-5, 15), (7, 17)):
            draw.ellipse(((x + dx - 13) * 2, (y + dy - 12) * 2, (x + dx + 13) * 2, (y + dy + 12) * 2), fill=(190, 48, 74, 245))
    elif frame_name.startswith("typing"):
        draw.rounded_rectangle((156 * 2, 348 * 2, 356 * 2, 438 * 2), radius=14 * 2, fill=(52, 59, 82, 242), outline=(200, 226, 244, 255), width=4 * 2)
        draw.rectangle((142 * 2, 438 * 2, 370 * 2, 456 * 2), fill=(34, 39, 55, 250), outline=(198, 222, 238, 255), width=3 * 2)
        draw.ellipse((247 * 2, 382 * 2, 265 * 2, 400 * 2), fill=(108, 202, 232, 255))
    image = image.copy()
    image.alpha_composite(overlay.resize((512, 512), Image.Resampling.LANCZOS))
    return image


def make_special_frame(character: str, base: Image.Image, frame_name: str) -> Image.Image:
    result = transform_frame(base, POSES[frame_name])
    return add_action_mark(result, frame_name)


def visible_bounds(image: Image.Image) -> tuple[int, int, int, int]:
    bounds = image.getchannel("A").point(lambda value: 255 if value > 8 else 0).getbbox()
    if bounds is None:
        raise ValueError("Empty animation cell")
    return bounds


def load_motion_frames(character: str, idle: Image.Image) -> dict[str, Image.Image]:
    """Slice painted poses, retaining a shared scale and stable contact anchors.

    No image warping, mirrored fake limbs or double-image crossfades are used.
    A fixed row scale preserves the relative size of successive body poses.
    """
    atlas = Image.open(GENERATED_ROOT / f"{character}-motion.png").convert("RGBA")
    if atlas.getchannel("A").getextrema()[0] == 255:
        raise ValueError(f"{character} atlas requires real alpha transparency")
    cell_w = atlas.width / 4
    # Old atlases need their measured gutters; Hu Tao V1.6 has a clear midpoint.
    row_seam = round(atlas.height * (0.5 if character == "hutao" else (450 if character == "ganyu" else 440) / 887))
    row_edges = (0, row_seam, atlas.height)
    idle_bounds = visible_bounds(idle)
    target_height = idle_bounds[3] - idle_bounds[1]
    frames: dict[str, Image.Image] = {}
    for row, action in enumerate(("walk", "climb-up")):
        cells = [atlas.crop((round(col * cell_w), row_edges[row], round((col + 1) * cell_w), row_edges[row + 1])) for col in range(4)]
        boxes = [visible_bounds(cell) for cell in cells]
        top = min(box[1] for box in boxes)
        bottom = max(box[3] for box in boxes)
        widest = max(box[2] - box[0] for box in boxes)
        scale = min(target_height / (bottom - top), (400 if row == 0 else 370) / widest)
        center = sum((box[0] + box[2]) / 2 for box in boxes) / 4
        for index, (cell, box) in enumerate(zip(cells, boxes), 1):
            sprite = cell.crop(box)
            sprite = sprite.resize((round(sprite.width * scale), round(sprite.height * scale)), Image.Resampling.LANCZOS)
            # All climb frames touch the same x=400 wall; run frames share the
            # same horizontal pivot and y=486 floor (passing feet can lift).
            x = round(256 + (box[0] - center) * scale) if row == 0 else 400 - sprite.width
            y = round(486 - (bottom - box[1]) * scale)
            frame = Image.new("RGBA", (512, 512), (0, 0, 0, 0))
            frame.alpha_composite(sprite, (x, y))
            frames[f"{action}-{index}"] = frame
    for index in range(1, 5):
        frames[f"climb-down-{index}"] = frames[f"climb-up-{5 - index}"].copy()
    return frames


def to_png(image: Image.Image) -> bytes:
    stream = BytesIO()
    image.save(stream, format="PNG", optimize=True)
    return stream.getvalue()


def load_kolo_frames() -> dict[str, Image.Image]:
    """Pack the reference redesign using measured crops and shared row scales.

    The artwork is painted in each pose. Packing only crops, uniformly resizes,
    and places it at its measured head/wall pivot; it never recolors or warps it.
    """
    recipe = json.loads((GENERATED_ROOT / "kolo-v22-crops.json").read_text(encoding="utf-8"))
    frames = {}
    for row in recipe["rows"]:
        atlas = Image.open(GENERATED_ROOT / row["source"]).convert("RGBA")
        if atlas.getchannel("A").getextrema()[0] == 255:
            raise ValueError("Kolo artwork requires real alpha transparency")
        scale = row["scale"]
        for index, item in enumerate(row["frames"], 1):
            pose = atlas.crop(tuple(item["crop"]))
            pose = pose.resize((round(pose.width * scale), round(pose.height * scale)), Image.Resampling.LANCZOS)
            frame = Image.new("RGBA", (512, 512), (0, 0, 0, 0))
            frame.alpha_composite(pose, tuple(item["placement"]))
            bounds = visible_bounds(frame)
            if min(bounds[:2]) < 4 or max(bounds[2:]) > 508:
                raise ValueError(f"Clipped Kolo pose: {row['action']}-{index}: {bounds}")
            frames[f"{row['action']}-{index}"] = frame
    for index in range(1, 5):
        frames[f"climb-down-{index}"] = frames[f"climb-up-{5 - index}"].copy()
    return frames


def load_gait_frames(character: str, kind: str, idle: Image.Image) -> dict[str, Image.Image]:
    """Pack a 2x2 sheet at one scale, with feet (or wheel) on y=486."""
    atlas = Image.open(GENERATED_ROOT / f"{character}-{kind}.png").convert("RGBA")
    if atlas.getchannel("A").getextrema()[0] == 255:
        raise ValueError(f"{character}-{kind} requires transparency")
    # Varesa's wide braids extend a little past the nominal center; the actual
    # transparent gutter is farther right. Preserve the full silhouette.
    seam_x = round(atlas.width * (650 / 1254 if character == "varesa" and kind == "stroll" else 0.5))
    xs, ys = (0, seam_x, atlas.width), (0, round(atlas.height / 2), atlas.height)
    cells = [atlas.crop((xs[c], ys[r], xs[c + 1], ys[r + 1])) for r in range(2) for c in range(2)]
    boxes = [cell.getchannel("A").point(lambda a: 255 if a > 32 else 0).getbbox() for cell in cells]
    if any(box is None for box in boxes):
        raise ValueError(f"Empty pose in {character}-{kind}")
    idle_box = visible_bounds(idle)
    scale = min((idle_box[3] - idle_box[1]) / max(b[3] - b[1] for b in boxes), 400 / max(b[2] - b[0] for b in boxes))
    frames = {}
    for index, (cell, box) in enumerate(zip(cells, boxes), 1):
        pose = cell.crop(box)
        pose = pose.resize((round(pose.width * scale), round(pose.height * scale)), Image.Resampling.LANCZOS)
        frame = Image.new("RGBA", (512, 512), (0, 0, 0, 0))
        x = (512 - pose.width) // 2
        if kind == "stroll" and character in ("hutao", "ganyu"):
            # Anchor on the stable upper head, not the changing hair/feet span.
            # Retain one scale and foot baseline across the full walk cycle.
            head_bottom = box[1] + round((box[3] - box[1]) * 0.45)
            head_box = cell.getchannel("A").point(lambda a: 255 if a > 32 else 0).crop((0, box[1], cell.width, head_bottom)).getbbox()
            head_center = (head_box[0] + head_box[2]) / 2
            x = round(256 + (box[0] - head_center) * scale)
        frame.alpha_composite(pose, (x, 486 - pose.height))
        frames[f"{'walk' if kind == 'sprint' else 'stroll'}-{index}"] = frame
    return frames


def load_font(size: int) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
    for path in (Path(r"C:\Windows\Fonts\msyh.ttc"), Path(r"C:\Windows\Fonts\msyhbd.ttc")):
        if path.is_file():
            return ImageFont.truetype(str(path), size)
    return ImageFont.load_default()


def load_hutao_flight_frames() -> dict[str, Image.Image]:
    """Keep butterfly body pivots fixed while wings open and close in the air."""
    atlas = Image.open(GENERATED_ROOT / "hutao-flight.png").convert("RGBA")
    if atlas.size != (1254, 1254) or atlas.getchannel("A").getextrema()[0] == 255:
        raise ValueError("Hu Tao flight requires the validated 1254x1254 RGBA atlas")
    cells = [atlas.crop((c * 627, r * 627, (c + 1) * 627, (r + 1) * 627)) for r in range(2) for c in range(2)]
    boxes = [visible_bounds(cell) for cell in cells]
    # Measured centers of the main butterfly's glowing body (not its wings
    # or trailing motes), in each source cell. One scale avoids wing inflation.
    pivots = ((485, 410), (435, 410), (471, 365), (457, 361))
    scale = min(320 / max(b[2] - b[0] for b in boxes), 260 / max(b[3] - b[1] for b in boxes))
    frames = {}
    for index, (cell, box, pivot) in enumerate(zip(cells, boxes, pivots), 1):
        pose = cell.crop(box)
        pose = pose.resize((round(pose.width * scale), round(pose.height * scale)), Image.Resampling.LANCZOS)
        frame = Image.new("RGBA", (512, 512), (0, 0, 0, 0))
        # Hover above the normal floor y=486, at about half the human silhouette.
        # Center the body on the runtime mirror pivot so changing direction
        # cannot teleport the butterfly sideways or shift it on reappearance.
        frame.alpha_composite(pose, (round(256 + (box[0] - pivot[0]) * scale), round(330 + (box[1] - pivot[1]) * scale)))
        frames[f"walk-{index}"] = frame
    return frames


def load_ganyu_mountain_frames(idle: Image.Image) -> dict[str, Image.Image]:
    """Spring-climb and jump poses share the same character scale."""
    atlas = Image.open(GENERATED_ROOT / "ganyu-mountain.png").convert("RGBA")
    if atlas.size != (1774, 887) or atlas.getchannel("A").getextrema()[0] == 255:
        raise ValueError("Ganyu mountain atlas must be the validated 1774x887 RGBA sheet")
    # Measured transparent seams: the running-style nominal halfway split
    # would cut the climber's foot and include a tuft from the next row.
    xs, ys = (0, 450, 887, 1330, 1774), (0, 473, 887)
    rows = [[atlas.crop((xs[c], ys[r], xs[c + 1], ys[r + 1])) for c in range(4)] for r in range(2)]
    bounds = [[visible_bounds(cell) for cell in row] for row in rows]
    idle_box = visible_bounds(idle)
    tallest = max(b[3] - b[1] for row in bounds for b in row)
    widest = max(b[2] - b[0] for row in bounds for b in row)
    scale = min((idle_box[3] - idle_box[1]) / tallest, 370 / widest)
    frames = {}
    for row, action in enumerate(("climb-up", "jump")):
        bottom = max(b[3] for b in bounds[row])
        for index, (cell, box) in enumerate(zip(rows[row], bounds[row]), 1):
            pose = cell.crop(box)
            pose = pose.resize((round(pose.width * scale), round(pose.height * scale)), Image.Resampling.LANCZOS)
            if row == 0:
                x = 400 - pose.width
            else:
                head_bottom = box[1] + round((box[3] - box[1]) * 0.45)
                head = cell.getchannel("A").point(lambda a: 255 if a > 32 else 0).crop((0, box[1], cell.width, head_bottom)).getbbox()
                x = round(256 + (box[0] - (head[0] + head[2]) / 2) * scale)
            y = round(486 - (bottom - box[1]) * scale)
            frame = Image.new("RGBA", (512, 512), (0, 0, 0, 0))
            frame.alpha_composite(pose, (x, y))
            frames[f"{action}-{index}"] = frame
    for index in range(1, 5):
        frames[f"climb-down-{index}"] = frames[f"climb-up-{5 - index}"].copy()
    return frames


def load_windglider_frames() -> dict[str, Image.Image]:
    """Pack independent back layers without changing any character pixels."""
    atlas = Image.open(GENERATED_ROOT / "windgliders-v19.png").convert("RGBA")
    if atlas.size != (1254, 1254) or atlas.getchannel("A").getextrema()[0] == 255:
        raise ValueError("Windgliders require the validated transparent 1254x1254 atlas")
    seams = (0, 415, 815, 1254)
    pivots = ((627, 278), (627, 676), (627, 1074))
    boxes = []
    cells = []
    for row in range(3):
        cell = atlas.crop((0, seams[row], atlas.width, seams[row + 1]))
        cells.append(cell)
        boxes.append(visible_bounds(cell))
    scale = 480 / max(box[2] - box[0] for box in boxes)
    result = {}
    for row, character in enumerate(SPECIAL_CHARACTERS):
        box = boxes[row]
        wing = cells[row].crop(box)
        wing = wing.resize((round(wing.width * scale), round(wing.height * scale)), Image.Resampling.LANCZOS)
        frame = Image.new("RGBA", (512, 512), (0, 0, 0, 0))
        # All wing roots share the same shoulder pivot as the runtime fold.
        x = round(256 + (box[0] - pivots[row][0]) * scale)
        y = round(340 + (box[1] + seams[row] - pivots[row][1]) * scale)
        frame.alpha_composite(wing, (x, y))
        result[character] = frame
    return result


def build_preview(idle_images: dict[str, Image.Image]) -> None:
    width, height = 22 + len(CHARACTERS) * 264, 330
    canvas = Image.new("RGBA", (width, height), (255, 246, 239, 255))
    draw = ImageDraw.Draw(canvas)
    title_font = load_font(22)
    name_font = load_font(19)
    draw.text((28, 18), "原神启动 V2.12 · AI伙伴统一形象与交互", font=title_font, fill=(76, 48, 45, 255))
    card_width = 250
    for index, key in enumerate(CHARACTERS):
        left = 22 + index * 264
        draw.rounded_rectangle((left, 58, left + card_width, 310), radius=20, fill=(255, 255, 255, 245), outline=(232, 195, 177, 255), width=2)
        image = idle_images[key].copy()
        image.thumbnail((210, 210), Image.Resampling.LANCZOS)
        x = left + (card_width - image.width) // 2
        y = 64 + (206 - image.height) // 2
        canvas.alpha_composite(image, (x, y))
        name = CHARACTERS[key]["name"]
        text_box = draw.textbbox((0, 0), name, font=name_font)
        draw.text((left + (card_width - (text_box[2] - text_box[0])) / 2, 274), name, font=name_font, fill=(78, 50, 49, 255))
    canvas.convert("RGB").save(OUTPUT / "character-preview.png", quality=94)


def build_motion_preview(motions: dict[str, dict[str, Image.Image]]) -> None:
    """QA contact sheet and animated preview using the exact packaged sprites."""
    preview_root = OUTPUT / "previews"
    preview_root.mkdir(exist_ok=True)
    font = load_font(17)
    sheet = Image.new("RGB", (960, len(motions) * 720), (255, 248, 242))
    draw = ImageDraw.Draw(sheet)
    for row, (character, frames) in enumerate(motions.items()):
        for action_row, action in enumerate(("stroll", "walk", "climb-up")):
            y = row * 720 + action_row * 240
            label = " · 金蝶飞行" if character == "hutao" and action == "walk" else (" · 慢走", " · 跑步", " · 攀爬")[action_row]
            draw.text((12, y + 6), CHARACTERS[character]["name"] + label, font=font, fill="#59423D")
            for index in range(4):
                frame = frames[f"{action}-{index + 1}"].resize((210, 210), Image.Resampling.LANCZOS)
                sheet.paste(frame, (index * 240 + 15, y + 28), frame)
    sheet.save(preview_root / "motion-contact-sheet.jpg", quality=94)
    animation = []
    for tick in range(48):
        canvas = Image.new("RGB", (len(motions) * 240, 770), (255, 248, 242))
        draw = ImageDraw.Draw(canvas)
        for column, (character, frames) in enumerate(motions.items()):
            x = column * 240
            draw.text((x + 88, 10), CHARACTERS[character]["name"], font=font, fill="#59423D")
            for row, action in enumerate(("stroll", "walk", "climb-up")):
                hold = 6 if action != "walk" else (2 if character == "varesa" else 4)
                index = (tick // hold) % 4 + 1
                y = 35 + row * 240
                frame = frames[f"{action}-{index}"].resize((220, 220), Image.Resampling.LANCZOS)
                if row < 2:
                    draw.line((x + 18, y + 209, x + 225, y + 209), fill="#DDCBBB", width=2)
                else:
                    draw.line((x + 182, y + 20, x + 182, y + 219), fill="#DDCBBB", width=3)
                canvas.paste(frame, (x + 10, y), frame)
        animation.append(canvas)
    animation[0].save(preview_root / "motion-preview.gif", save_all=True, append_images=animation[1:], duration=40, loop=0, disposal=2)
    build_stroll_preview(motions, preview_root)
    build_flight_contact_sheet(motions["hutao"], preview_root)
    build_ganyu_mountain_preview(motions["ganyu"], preview_root)


def build_ganyu_mountain_preview(frames: dict[str, Image.Image], preview_root: Path) -> None:
    font = load_font(18)
    sheet = Image.new("RGB", (1000, 540), "#EEF6FB")
    draw = ImageDraw.Draw(sheet)
    for row, action in enumerate(("climb-up", "jump")):
        draw.text((16, row * 270 + 6), "甘雨 · " + ("蹬壁攀爬" if row == 0 else "起跳、腾空、伸腿、落地"), font=font, fill="#274B6A")
        for index in range(4):
            pose = frames[f"{action}-{index + 1}"].resize((245, 245), Image.Resampling.LANCZOS)
            sheet.paste(pose, (index * 250, row * 270 + 24), pose)
    sheet.save(preview_root / "ganyu-mountain-contact-sheet.png")


def build_kolo_preview(idle: Image.Image, frames: dict[str, Image.Image]) -> None:
    """Review the exact exported poses at a readable desktop-pet scale."""
    preview_root = OUTPUT / "previews"
    preview_root.mkdir(exist_ok=True)
    actions = (("stroll", "慢走"), ("walk", "跑步"), ("climb-up", "攀爬"), ("ability", "举手能力"))
    font = load_font(20)
    sheet = Image.new("RGB", (1024, 1160), "#FFF7EC")
    draw = ImageDraw.Draw(sheet)
    for row, (action, label) in enumerate(actions):
        draw.text((18, row * 290 + 8), "珂洛 · " + label, font=font, fill="#5E3722")
        for index in range(4):
            pose = frames[f"{action}-{index+1}"].resize((256,256), Image.Resampling.LANCZOS)
            sheet.paste(pose, (index*256, row*290+30), pose)
    sheet.save(preview_root / "kolo-v29-contact.png")
    animation = []
    for tick in range(80):
        elapsed = tick*.04
        canvas = Image.new("RGB", (1000,300), "#FFF7EC")
        draw = ImageDraw.Draw(canvas)
        for column, (action, label) in enumerate(actions):
            draw.text((column*250+18, 10), label, font=font, fill="#5E3722")
            if action == "ability":
                local = elapsed % 2.4
                sequence = ((.3,idle),(.55,frames["ability-1"]),(.8,frames["ability-2"]),(1.7,frames["ability-3"]),(1.95,frames["ability-4"]),(2.4,idle))
                pose = next(image for end,image in sequence if local < end)
            else:
                hold = {"stroll": .33, "walk": .1, "climb-up": .2}[action]
                pose = frames[f"{action}-{int(elapsed/hold)%4+1}"]
            pose = pose.resize((250,250), Image.Resampling.LANCZOS)
            canvas.paste(pose, (column*250,40), pose)
        animation.append(canvas)
    animation[0].save(preview_root/"kolo-v29-actions.gif", save_all=True, append_images=animation[1:], duration=40, loop=0, disposal=2)


def build_flight_contact_sheet(frames: dict[str, Image.Image], preview_root: Path) -> None:
    """Show packaged wings; EffectsTests renders the actual WPF morph preview."""
    font = load_font(19)
    sheet = Image.new("RGB", (1024, 300), "#17222F")
    draw = ImageDraw.Draw(sheet)
    for index in range(4):
        frame = frames[f"walk-{index + 1}"].resize((256, 256), Image.Resampling.LANCZOS)
        sheet.paste(frame, (index * 256, 40), frame)
        draw.text((index * 256 + 14, 12), f"胡桃 · 金蝶 {index + 1}", font=font, fill="#FFE9B2")
    sheet.save(preview_root / "hutao-flight-contact-sheet.png")


def build_stroll_preview(motions: dict[str, dict[str, Image.Image]], preview_root: Path) -> None:
    """Use production frames at the actual 37.5 px/s midpoint walk cadence.

    Display is enlarged 2x: 330 px sprite, 75 px/s movement, 330 ms/pose.
    Ground marks make contact timing and alternating feet easy to inspect.
    """
    font = load_font(20)
    characters = ("hutao", "ganyu")
    contact = Image.new("RGB", (1120, 620), "#FFF8F2")
    cd = ImageDraw.Draw(contact)
    for row, character in enumerate(characters):
        cd.text((16, row * 310 + 8), CHARACTERS[character]["name"] + " · 左右腿交替", font=font, fill="#59423D")
        for index in range(4):
            frame = motions[character][f"stroll-{index + 1}"].resize((280, 280), Image.Resampling.LANCZOS)
            contact.paste(frame, (index * 280, row * 310 + 25), frame)
    contact.save(preview_root / "stroll-contact-sheet.png")
    animation = []
    # Four complete cycles, sampled 30 times per second. GIF timings alternate
    # 30/40 ms to approximate 33 ms without rounding every sample upward.
    for tick in range(160):
        elapsed = tick * 0.033
        index = int((elapsed / 1.32 % 1.0) * 4) + 1
        canvas = Image.new("RGB", (800, 780), "#FFF8F2")
        draw = ImageDraw.Draw(canvas)
        for row, character in enumerate(characters):
            y = 35 + row * 380
            draw.text((20, y - 25), CHARACTERS[character]["name"] + " · 实际慢走节奏（画面放大2倍）", font=font, fill="#59423D")
            ground_y = y + round(486 * 330 / 512)
            draw.line((12, ground_y, 788, ground_y), fill="#BBAA9C", width=2)
            for marker in range(20, 800, 50):
                draw.line((marker, ground_y, marker, ground_y + 8), fill="#BBAA9C", width=2)
            frame = motions[character][f"stroll-{index}"].resize((330, 330), Image.Resampling.LANCZOS)
            canvas.paste(frame, (round(25 + elapsed * 75), y), frame)
        animation.append(canvas)
    durations = [40 if tick % 10 in (2, 5, 8) else 30 for tick in range(160)]
    animation[0].save(preview_root / "stroll-preview.gif", save_all=True, append_images=animation[1:], duration=durations, loop=0, disposal=2)


def load_glide_frames() -> dict[str, dict[str, Image.Image]]:
    """Pack eight original poses per character around a measured shoulder pivot.

    A shared scale preserves body proportions across the sequence. Only the two
    landing poses shift vertically to place their feet on the normal baseline;
    the runtime moves the wing root by the same amount while retracting.
    """
    recipe = json.loads((GENERATED_ROOT / "glide-v20-crops.json").read_text(encoding="utf-8-sig"))
    result = {}
    for character in SPECIAL_CHARACTERS:
        details = recipe[character]
        source = Image.open(GENERATED_ROOT.parent.parent / details["source"]).convert("RGBA")
        scale = details["recommended_common_scale"]
        poses = {}
        for item in details["frames"]:
            crop = source.crop(tuple(item["crop"]))
            resized = crop.resize((round(crop.width * scale), round(crop.height * scale)), Image.Resampling.LANCZOS)
            frame = Image.new("RGBA", (512, 512), (0, 0, 0, 0))
            frame.alpha_composite(resized, tuple(item["placement"]))
            bounds = frame.getchannel("A").point(lambda value: 255 if value > 8 else 0).getbbox()
            if bounds is None or min(bounds[:2]) < 4 or max(bounds[2:]) > 508:
                raise ValueError(f"Clipped glide pose: {character} {item['index']} {bounds}")
            poses[f"glide-{item['index']}"] = frame
        if len(poses) != 8:
            raise ValueError(f"Expected eight glide poses: {character}")
        result[character] = poses
    return result


def load_ai_frames(character: str) -> dict[str, Image.Image]:
    """Extract painted sprites using measured gutters; preserve generated alpha.

    Shared scale per atlas and a shared horizontal pivot per row avoid changing
    body size each frame. Climbing shares x=400 contact, ground poses y=486.
    """
    if character == "deepseek":
        return load_deepseek_reference_frames(GENERATED_ROOT)
    if character == "gpt6a":
        return load_dragon_reference_frames(GENERATED_ROOT)
    raise ValueError(f"Unknown AI character: {character}")


def build_ai_preview(frames_by_character: dict[str, dict[str, Image.Image]]) -> None:
    """Review exact packaged pixels on both light and dark backgrounds."""
    preview_root = OUTPUT / "previews"
    preview_root.mkdir(exist_ok=True)
    actions = ("idle", "stroll-2", "walk-2", "climb-up-3", "ability-3", "fall", "held")
    sheet = Image.new("RGB", (7 * 220, 4 * 260), "#fff8f2")
    draw = ImageDraw.Draw(sheet)
    font = load_font(17)
    for row in range(4):
        character = AI_CHARACTERS[row % 2]
        bg = "#fff8f2" if row < 2 else "#192638"
        fg = "#254D59" if row < 2 else "#E0EDF8"
        draw.rectangle((0, row * 260, sheet.width, (row + 1) * 260), fill=bg)
        for col, action in enumerate(actions):
            frame = frames_by_character[character][action].resize((220, 220), Image.Resampling.LANCZOS)
            sheet.paste(frame, (col * 220, row * 260 + 30), frame)
            draw.text((col * 220 + 8, row * 260 + 6), character + " · " + action, font=font, fill=fg)
    sheet.save(preview_root / "ai-v212-contact-sheet.png")
    gait_sheet = Image.new("RGB", (960, 6 * 260), "#eef7f6")
    gait_draw = ImageDraw.Draw(gait_sheet)
    for character_row, character in enumerate(AI_CHARACTERS):
        for action_row, action in enumerate(("stroll", "walk", "climb-up")):
            row = character_row * 3 + action_row
            for index in range(4):
                pose = frames_by_character[character][f"{action}-{index + 1}"].resize((240, 240), Image.Resampling.LANCZOS)
                gait_sheet.paste(pose, (index * 240, row * 260 + 20), pose)
                gait_draw.text((index * 240 + 8, row * 260 + 2), f"{character} · {action} {index + 1}", font=font, fill="#254D59")
    gait_sheet.save(preview_root / "ai-v212-gait-contact-sheet.png")
    animation = []
    for tick in range(48):
        canvas = Image.new("RGB", (1000, 540), "#eef7f6")
        draw = ImageDraw.Draw(canvas)
        for row, character in enumerate(AI_CHARACTERS):
            for col, action in enumerate(("stroll", "walk", "climb-up", "ability")):
                index = (tick // (6 if action == "stroll" else 3)) % 4 + 1
                pose = frames_by_character[character][f"{action}-{index}"].resize((240, 240), Image.Resampling.LANCZOS)
                canvas.paste(pose, (col * 250 + 5, row * 270 + 22), pose)
                draw.text((col * 250 + 8, row * 270 + 2), character + " · " + action, font=font, fill="#254D59")
        animation.append(canvas)
    animation[0].save(preview_root / "ai-v212-motion.gif", save_all=True, append_images=animation[1:], duration=60, loop=0, disposal=2)


def append_ai_assets() -> None:
    """Add this release's sprites without re-encoding any existing entry."""
    frames_by_character = {character: load_ai_frames(character) for character in AI_CHARACTERS}
    source_path, temporary = OUTPUT / "characters.zip", OUTPUT / "characters-building.zip"
    idle_images = {}
    with ZipFile(source_path, "r") as source, ZipFile(temporary, "w", compression=ZIP_STORED) as destination:
        for entry in source.infolist():
            if any(entry.filename.startswith(character + "-") for character in AI_CHARACTERS):
                continue
            data = source.read(entry.filename)
            destination.writestr(entry, data)
            if entry.filename.endswith("-idle.png"):
                idle_images[entry.filename[:-9]] = Image.open(BytesIO(data)).convert("RGBA")
        for character, frames in frames_by_character.items():
            for name, frame in frames.items():
                destination.writestr(f"{character}-{name}.png", to_png(frame))
            idle_images[character] = frames["idle"]
    temporary.replace(source_path)
    build_preview(idle_images)
    build_ai_preview(frames_by_character)
    print("Added 54 AI character frames; preserved all existing character PNG entries.")


def refresh_reference_assets() -> None:
    """Replace only the two revised characters, preserving the other PNGs."""
    replacements = {"kolo": load_kolo_reference_frames(GENERATED_ROOT),
                    "deepseek": load_deepseek_reference_frames(GENERATED_ROOT)}
    source_path, temporary = OUTPUT / "characters.zip", OUTPUT / "characters-building.zip"
    idle_images = {}
    with ZipFile(source_path, "r") as source, ZipFile(temporary, "w", compression=ZIP_STORED) as destination:
        for entry in source.infolist():
            if any(entry.filename.startswith(character + "-") for character in replacements):
                continue
            data = source.read(entry.filename)
            destination.writestr(entry, data)
            if entry.filename.endswith("-idle.png"):
                idle_images[entry.filename[:-9]] = Image.open(BytesIO(data)).convert("RGBA")
        for character, frames in replacements.items():
            for name, frame in frames.items():
                destination.writestr(f"{character}-{name}.png", to_png(frame))
            idle_images[character] = frames.get("book-idle", frames["idle"])
        destination.writestr("kolo-notebook-hitboxes.json", json.dumps(load_notebook_hitboxes(GENERATED_ROOT), ensure_ascii=False))
    temporary.replace(source_path)
    idle_images["kolo"].save(OUTPUT / "ClaudePet.ico", format="ICO", sizes=[(16, 16), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
    build_preview(idle_images)
    build_kolo_preview(idle_images["kolo"], replacements["kolo"])
    build_ai_preview({"gpt6a": load_ai_frames("gpt6a"), "deepseek": replacements["deepseek"]})
    print("Updated Kolo and DeepSeek; all other character entries preserved.")


def refresh_dragon_assets() -> None:
    """Replace the 27 GPT6A images, retaining unrelated resource bytes."""
    frames = load_dragon_reference_frames(GENERATED_ROOT)
    source_path, temporary = OUTPUT / "characters.zip", OUTPUT / "characters-building.zip"
    idle_images = {}
    held_portrait = None
    with ZipFile(source_path, "r") as source, ZipFile(temporary, "w", compression=ZIP_STORED) as destination:
        replaced = [name for name in source.namelist() if name.startswith("gpt6a-")]
        expected = {f"gpt6a-{name}.png" for name in frames}
        if set(replaced) != expected:
            raise ValueError("GPT6A replacement must retain the existing frame contract")
        for entry in source.infolist():
            if entry.filename.startswith("gpt6a-"):
                continue
            data = source.read(entry.filename)
            if entry.filename == "kolo-book-idle.png":
                held_portrait = Image.open(BytesIO(data)).convert("RGBA")
            destination.writestr(entry, data)
            if entry.filename.endswith("-idle.png") and "-book-" not in entry.filename:
                idle_images[entry.filename[:-9]] = Image.open(BytesIO(data)).convert("RGBA")
        if held_portrait is None:
            raise ValueError("Missing Kolo held-notebook portrait")
        idle_images["kolo"] = held_portrait
        for name, frame in frames.items():
            destination.writestr(f"gpt6a-{name}.png", to_png(frame))
        idle_images["gpt6a"] = frames["idle"]
    temporary.replace(source_path)
    build_preview(idle_images)
    build_ai_preview({"gpt6a": frames, "deepseek": load_ai_frames("deepseek")})
    build_dragon_preview(frames, OUTPUT / "previews")
    print("Updated 27 GPT6A frames; preserved all unrelated resource entries.")


def refresh_kolo_v211_assets() -> None:
    """Refresh eight stroll frames and hitboxes, then add eight costume poses."""
    updated = {"kolo-" + name + ".png": to_png(frame) for name, frame in load_stroll_frames(GENERATED_ROOT).items()}
    updated.update({"kolo-" + name + ".png": to_png(frame) for name, frame in load_disguise_frames(GENERATED_ROOT).items()})
    boxes = load_notebook_hitboxes(GENERATED_ROOT)
    updated["kolo-notebook-hitboxes.json"] = json.dumps(boxes, ensure_ascii=False).encode("utf-8")
    source_path, temporary = OUTPUT / "characters.zip", OUTPUT / "characters-building.zip"
    idle_images, held_portrait = {}, None
    with ZipFile(source_path, "r") as source, ZipFile(temporary, "w", compression=ZIP_STORED) as destination:
        for entry in source.infolist():
            if entry.filename in updated:
                continue
            data = source.read(entry.filename)
            if entry.filename == "kolo-book-idle.png":
                held_portrait = Image.open(BytesIO(data)).convert("RGBA")
            if entry.filename.endswith("-idle.png") and "-book-" not in entry.filename:
                idle_images[entry.filename[:-9]] = Image.open(BytesIO(data)).convert("RGBA")
            destination.writestr(entry, data)
        for name, data in updated.items():
            destination.writestr(name, data)
    if held_portrait is None:
        raise ValueError("Missing Kolo held-notebook portrait")
    temporary.replace(source_path)
    idle_images["kolo"] = held_portrait
    build_preview(idle_images)
    (GENERATED_ROOT / "kolo-notebook-hitboxes-v211.json").write_text(json.dumps(boxes, ensure_ascii=False, indent=2)+"\n", encoding="utf-8")
    build_kolo_v211_preview(OUTPUT / "previews", GENERATED_ROOT)
    print("Updated eight stroll frames and notebook hitboxes; added eight costume poses.")


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    character_bases = {key: build_special_base(key) for key in SPECIAL_CHARACTERS}
    gliders = load_windglider_frames()
    glide_poses = load_glide_frames()
    idle_images: dict[str, Image.Image] = {}
    motion_images: dict[str, dict[str, Image.Image]] = {}
    count = 0
    package = OUTPUT / "characters-building.zip"
    with ZipFile(package, "w", compression=ZIP_STORED) as archive:
        for character in CHARACTERS:
            if character in AI_CHARACTERS or character == "kolo":
                frames = load_kolo_reference_frames(GENERATED_ROOT) if character == "kolo" else load_ai_frames(character)
                idle_images[character] = frames.get("book-idle", frames["idle"])
                motion_images[character] = frames
                for frame_name, output in frames.items():
                    archive.writestr(f"{character}-{frame_name}.png", to_png(output))
                    count += 1
                continue
            for frame_name in POSES:
                output = make_special_frame(character, character_bases[character], frame_name)
                archive.writestr(f"{character}-{frame_name}.png", to_png(output))
                if frame_name == "idle":
                    idle_images[character] = output.copy()
                count += 1
            motions = load_motion_frames(character, idle_images[character])
            motions.update(load_gait_frames(character, "stroll", idle_images[character]))
            if character == "varesa":
                motions.update(load_gait_frames(character, "sprint", idle_images[character]))
            elif character == "hutao":
                # Preserve the original V1.6 humanoid running cycle for the
                # lead-in and moving dissolution; walk remains the gold flight.
                for index in range(1, 5):
                    motions[f"run-human-{index}"] = motions[f"walk-{index}"].copy()
                motions.update(load_hutao_flight_frames())
            elif character == "ganyu":
                motions.update(load_ganyu_mountain_frames(idle_images[character]))
            motion_images[character] = motions
            for frame_name, output in motions.items():
                if frame_name.startswith("climb-down-"):
                    continue  # Runtime reuses the identical up poses in reverse.
                archive.writestr(f"{character}-{frame_name}.png", to_png(output))
                count += 1
            if character in gliders:
                archive.writestr(f"{character}-glider.png", to_png(gliders[character]))
                count += 1
                for frame_name, output in glide_poses[character].items():
                    archive.writestr(f"{character}-{frame_name}.png", to_png(output))
                    count += 1
        archive.writestr("kolo-notebook-hitboxes.json", json.dumps(load_notebook_hitboxes(GENERATED_ROOT), ensure_ascii=False))
        count += 1

    package.replace(OUTPUT / "characters.zip")
    icon = idle_images["kolo"]
    icon.save(OUTPUT / "ClaudePet.ico", format="ICO", sizes=[(16, 16), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
    build_preview(idle_images)
    build_motion_preview(motion_images)
    build_kolo_preview(idle_images["kolo"], motion_images["kolo"])
    build_ai_preview({character: motion_images[character] for character in AI_CHARACTERS})
    build_dragon_preview(motion_images["gpt6a"], OUTPUT / "previews")
    build_kolo_v211_preview(OUTPUT / "previews", GENERATED_ROOT)
    expected = 153 + len(motion_images["kolo"])
    if count != expected:
        raise ValueError(f"Unexpected asset count: {count}")
    print(f"generated {count} assets for {len(CHARACTERS)} characters")
    print(OUTPUT / "characters.zip")
    print(OUTPUT / "character-preview.png")


if __name__ == "__main__":
    import sys
    if "--refresh-v212" in sys.argv:
        refresh_dragon_assets()
    elif "--refresh-v211" in sys.argv:
        refresh_kolo_v211_assets()
    elif "--refresh-v210" in sys.argv:
        refresh_dragon_assets()
    elif "--refresh-v29" in sys.argv:
        refresh_reference_assets()
    elif "--append-ai" in sys.argv:
        append_ai_assets()
    else:
        main()
