"""Extract Claude's corrected walk and wardrobe transition from source artwork.

Only rectangle cropping, uniform scaling, and canvas placement are performed.
The original source RGBA is retained, including antialiased and translucent edges.
"""
from __future__ import annotations

import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
from build_deepseek_v29 import load_frames as load_deepseek_reference_frames

DEFAULT_ROOT = Path(__file__).resolve().parent / "assets" / "generated"


def _recipe(generated_root=None):
    root = Path(generated_root) if generated_root is not None else DEFAULT_ROOT
    return root, json.loads((root / "kolo-v211-crops.json").read_text(encoding="utf-8"))


def _source(path):
    image = Image.open(path).convert("RGBA")
    if image.getchannel("A").histogram()[0] < image.width * image.height * .02:
        raise ValueError("Claude V2.11 source requires transparent gutters")
    return image


def _bounds(image):
    bounds = image.getchannel("A").point(lambda value: 255 if value > 8 else 0).getbbox()
    if bounds is None:
        raise ValueError("Empty Claude V2.11 pose")
    return bounds


def _extract(source, cell, scale, label):
    rectangle = cell["crop"]
    cut = source.crop(tuple(rectangle))
    box = _bounds(cut)
    if box[0] < 2 or box[1] < 2 or box[2] > cut.width-2 or box[3] > cut.height-2:
        raise ValueError("Claude source crop crosses artwork: " + label)
    pose = cut.crop(box)
    pose = pose.resize((round(pose.width*scale), round(pose.height*scale)), Image.Resampling.LANCZOS)
    # Anchor is a measured source-space body center, not a hair/tail silhouette
    # center. Matching ground contact removes atlas placement drift in the gait.
    anchor = cell.get("anchorX", rectangle[0] + (box[0]+box[2])/2)
    x = round(cell.get("targetX",256) + (rectangle[0] + box[0] - anchor)*scale)
    y = 486 - pose.height
    frame = Image.new("RGBA", (512,512))
    frame.alpha_composite(pose, (x,y))
    extent = _bounds(frame)
    if min(extent[:2]) < 4 or max(extent[2:]) > 508:
        raise ValueError("Clipped Claude V2.11 pose: " + label)
    return frame, (x, y, rectangle[0]+box[0], rectangle[1]+box[1])


def _stroll(generated_root=None):
    root, recipe = _recipe(generated_root)
    spec = recipe["stroll"]
    source = _source(root/spec["source"])
    frames, hitboxes = {}, {}
    for row in spec["rows"]:
        prefix, scale = row["prefix"], row["scale"]
        if len(row["cells"]) != 4:
            raise ValueError("Claude walk cycle requires four poses")
        for index, cell in enumerate(row["cells"], 1):
            name = prefix + "stroll-" + str(index)
            frame, placement = _extract(source, cell, scale, name)
            frames[name] = frame
            if prefix == "book-":
                bx,by,bw,bh = cell["book"]
                x,y,ox,oy = placement
                rect = [round(x+(bx-ox)*scale,2), round(y+(by-oy)*scale,2), round(bw*scale,2), round(bh*scale,2)]
                if min(rect[:2]) < 0 or rect[0]+rect[2] > 512 or rect[1]+rect[3] > 512:
                    raise ValueError("Claude notebook hit rectangle outside frame")
                hitboxes["kolo-"+name+".png"] = rect
    if len(frames) != 8 or len(hitboxes) != 4:
        raise ValueError("Claude V2.11 stroll contract mismatch")
    return frames,hitboxes


def load_stroll_frames(generated_root=None):
    return _stroll(generated_root)[0]


def load_stroll_hitboxes(generated_root=None):
    return _stroll(generated_root)[1]


def load_disguise_frames(generated_root=None):
    root, recipe = _recipe(generated_root)
    spec = recipe["disguise"]
    source = _source(root/spec["source"])
    if len(spec["cells"]) != 8:
        raise ValueError("Claude wardrobe transition requires eight poses")
    frames = {"disguise-"+str(index): _extract(source, cell, spec["scale"], "disguise-"+str(index))[0]
              for index,cell in enumerate(spec["cells"][:7],1)}
    # End on the exact production idle so the actual character switch cannot
    # introduce a new face, silhouette, scale or placement discontinuity.
    frames["disguise-8"] = load_deepseek_reference_frames(root)["idle"]
    return frames


def build_preview(destination, generated_root=None):
    destination = Path(destination)
    destination.mkdir(parents=True,exist_ok=True)
    frames,hitboxes = _stroll(generated_root)
    frames.update(load_disguise_frames(generated_root))
    font = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc",18)
    sheet = Image.new("RGB",(1120,1120),"#FFF8F1")
    draw = ImageDraw.Draw(sheet)
    names = [["stroll-"+str(i) for i in range(1,5)], ["book-stroll-"+str(i) for i in range(1,5)],
             ["disguise-"+str(i) for i in range(1,5)], ["disguise-"+str(i) for i in range(5,9)]]
    for row,group in enumerate(names):
        for col,name in enumerate(group):
            pose = frames[name].resize((256,256),Image.Resampling.LANCZOS)
            sheet.paste(pose,(col*280+12,row*280+24),pose)
            draw.text((col*280+10,row*280+2),name,font=font,fill="#5E4438")
    sheet.save(destination/"kolo-v211-contact.png")
    hit_sheet = Image.new("RGB",(1120,300),"#FFF8F1")
    hd = ImageDraw.Draw(hit_sheet)
    for col in range(4):
        name = "book-stroll-"+str(col+1)
        pose = frames[name].resize((256,256),Image.Resampling.LANCZOS)
        x,y = col*280+12,24
        hit_sheet.paste(pose,(x,y),pose)
        bx,by,bw,bh = hitboxes["kolo-"+name+".png"]
        hd.rectangle((x+bx/2,y+by/2,x+(bx+bw)/2,y+(by+bh)/2),outline="#D32651",width=2)
        hd.text((col*280+10,2),name,font=font,fill="#5E4438")
    hit_sheet.save(destination/"kolo-v211-stroll-hitboxes.png")
    gait = []
    for tick in range(40):
        canvas = Image.new("RGB",(680,380),"#FFF8F1")
        draw = ImageDraw.Draw(canvas)
        for col,prefix in enumerate(("","book-")):
            pose = frames[prefix+"stroll-"+str(tick//10%4+1)].resize((330,330),Image.Resampling.LANCZOS)
            canvas.paste(pose,(col*340+5,32),pose)
            draw.text((col*340+12,6),"空手慢走" if col==0 else "持本慢走",font=font,fill="#5E4438")
            draw.line((col*340+5,345,col*340+335,345),fill="#BB947D",width=1)
        gait.append(canvas)
    gait[0].save(destination/"kolo-v211-stroll.gif",save_all=True,append_images=gait[1:],duration=[40 if i%10 in (2,5,8) else 30 for i in range(40)],loop=0,disposal=2)
    disguise = []
    for index in range(1,9):
        canvas = Image.new("RGB",(360,390),"#24202B")
        pose = frames["disguise-"+str(index)].resize((350,350),Image.Resampling.LANCZOS)
        canvas.paste(pose,(5,30),pose)
        ImageDraw.Draw(canvas).text((12,6),"待机换装 · "+str(index)+" / 8",font=font,fill="#F7F0E9")
        disguise.append(canvas)
    disguise[0].save(destination/"kolo-v211-disguise.gif",save_all=True,append_images=disguise[1:],duration=[650,550,550,550,550,550,550,1100],loop=0,disposal=2)
    for key,frame in frames.items():
        frame.save(destination/("kolo-"+key+".png"))
    (destination/"kolo-v211-stroll-hitboxes.json").write_text(json.dumps(hitboxes,indent=2)+"\n",encoding="utf-8")
    return frames,hitboxes


if __name__ == "__main__":
    destination = Path(__file__).resolve().parent / "artifacts" / "v211"
    frames,hitboxes = build_preview(destination)
    print(f"Extracted {len(frames)} V2.11 frames and {len(hitboxes)} notebook hitboxes")
