"""Extract Claude's free/clipboard frames, preserving each subject's artwork."""
from __future__ import annotations

import json
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFont

DEFAULT_ROOT = Path(__file__).resolve().parent / "assets" / "generated"


def _bounds(image: Image.Image) -> tuple[int, int, int, int]:
    bounds = image.getchannel("A").point(lambda alpha: 255 if alpha > 8 else 0).getbbox()
    if bounds is None:
        raise ValueError("Empty Claude source cell")
    return bounds


def _subject_bounds(image: Image.Image) -> tuple[int,int,int,int]:
    """Find the painted body, ignoring neighboring-cell slivers for cropping.

    Run-based connected components inspect a temporary alpha mask only. No
    output alpha or colors are modified; the returned rectangle just crops.
    """
    mask = np.asarray(image.getchannel("A")) > 32
    parents, boxes, sizes = [], [], []
    def root(label):
        while parents[label] != label:
            parents[label] = parents[parents[label]]; label = parents[label]
        return label
    previous = []
    for y,row in enumerate(mask):
        edges = np.flatnonzero(np.diff(np.r_[False,row,False]))
        current = []
        for left,right in zip(edges[::2],edges[1::2]):
            left,right = int(left),int(right)
            label = len(parents); parents.append(label); boxes.append([left,y,right,y+1]); sizes.append(right-left)
            for a,b,prior in previous:
                if a > right or b < left: continue
                aroot,broot = root(label),root(prior)
                if aroot != broot:
                    parents[broot] = aroot; sizes[aroot] += sizes[broot]
                    ba,bb = boxes[aroot],boxes[broot]
                    boxes[aroot] = [min(ba[0],bb[0]),min(ba[1],bb[1]),max(ba[2],bb[2]),max(ba[3],bb[3])]
            current.append((left,right,label))
        previous = current
    if not parents: raise ValueError("Empty Claude body")
    largest = max((i for i in range(len(parents)) if root(i)==i),key=lambda i:sizes[i])
    left,top,right,bottom = boxes[largest]
    return max(0,left-1),max(0,top-1),min(image.width,right+1),min(image.height,bottom+1)


def _pack(generated_root: Path | None = None):
    root = Path(generated_root) if generated_root is not None else DEFAULT_ROOT
    recipe = json.loads((root / "kolo-v29-crops.json").read_text(encoding="utf-8"))
    frames, hitboxes = {}, {}
    for mode in ("free", "book"):
        spec = recipe[mode]
        source = Image.open(root / spec["source"]).convert("RGBA")
        if source.getchannel("A").getextrema()[0] != 0:
            raise ValueError("Claude atlases must have actual alpha transparency")
        rows, rectangles = [], []
        scale = spec["scale"]
        for row in range(5 if mode == "free" else 4):
            cells, origins = [], []
            for col in range(4):
                xs = spec.get("rowXEdges", [recipe["xEdges"]]*5)[row]
                left = xs[col]
                top, bottom = spec["columnYEdges"][col][row:row+2]
                cell = source.crop((left, top, xs[col+1], bottom))
                for exclusion in spec.get("excludeAtlasRects", []):
                    if exclusion["row"] != row or exclusion["column"] != col:
                        continue
                    # Measured pieces of adjacent atlas frames, not subject pixels.
                    # Keep the original cell extent and character alignment.
                    ex0, ey0, ex1, ey1 = exclusion["rect"]
                    if not (left <= ex0 < ex1 <= xs[col+1] and top <= ey0 < ey1 <= bottom):
                        raise ValueError("Neighbor exclusion outside its atlas cell")
                    cell.paste((0, 0, 0, 0), (ex0-left, ey0-top, ex1-left, ey1-top))
                cells.append(cell)
                origins.append((left, top))
            boxes = [_subject_bounds(cell) for cell in cells]
            pivot = sum((box[0] + box[2]) / 2 for box in boxes) / 4
            floor = max(box[3] for box in boxes)
            row_frames, row_rects = [], []
            for col, (cell, box) in enumerate(zip(cells, boxes)):
                painted = cell.crop(box)
                painted = painted.resize((round(painted.width*scale), round(painted.height*scale)), Image.Resampling.LANCZOS)
                x = 400-painted.width if row == 3 else round(256+(box[0]-pivot)*scale)
                y = 486-painted.height if row == 0 else round(486-(floor-box[1])*scale)
                frame = Image.new("RGBA", (512,512))
                frame.alpha_composite(painted,(x,y))
                bounds = _bounds(frame)
                if min(bounds[:2]) < 4 or max(bounds[2:]) > 508:
                    raise ValueError(f"Clipped Claude {mode} row {row} cell {col}: {bounds}")
                row_frames.append(frame)
                if mode == "book":
                    bx,by,bw,bh = spec["bookRects"][row][col]
                    ox,oy = origins[col]
                    rect = [round(x+(bx-ox-box[0])*scale,2),round(y+(by-oy-box[1])*scale,2),round(bw*scale,2),round(bh*scale,2)]
                    if min(rect[:2]) < 0 or rect[0]+rect[2] > 512 or rect[1]+rect[3] > 512:
                        raise ValueError(f"Clipboard hit rectangle outside frame: {rect}")
                    row_rects.append(rect)
            rows.append(row_frames); rectangles.append(row_rects)
        expressions = {"idle":0,"happy-1":1,"happy-2":2,"pet-1":2,"pet-2":3,
                       "rose-1":1,"rose-2":2,"typing-1":3,"typing-2":0}
        prefix = "book-" if mode == "book" else ""
        for name,index in expressions.items():
            frames[prefix+name] = rows[0][index]
            if mode == "book": hitboxes["kolo-"+prefix+name+".png"] = rectangles[0][index]
        for row,action in ((1,"stroll"),(2,"walk"),(3,"climb-up")):
            for index,frame in enumerate(rows[row],1):
                name = prefix+action+"-"+str(index)
                frames[name] = frame
                if mode == "book": hitboxes["kolo-"+name+".png"] = rectangles[row][index-1]
        if mode == "free":
            for index,frame in enumerate(rows[4],1): frames["ability-"+str(index)] = frame
    notebook = Image.open(root / recipe["notebook"]["source"]).convert("RGBA")
    notebook = notebook.crop(_bounds(notebook))
    notebook.thumbnail(tuple(recipe["notebook"]["maximumSize"]),Image.Resampling.LANCZOS)
    frames["notebook"] = notebook
    if len(frames) != 47 or len(hitboxes) != 21: raise ValueError("Claude frame contract mismatch")
    return frames,hitboxes


def load_frames(generated_root: Path | None = None) -> dict[str,Image.Image]:
    return _pack(generated_root)[0]


def load_hitboxes(generated_root: Path | None = None) -> dict[str,list[float]]:
    return _pack(generated_root)[1]


def preview(destination: Path) -> None:
    destination.mkdir(parents=True,exist_ok=True)
    frames,hitboxes = _pack()
    for key,frame in frames.items(): frame.save(destination/("kolo-"+key+".png"))
    (DEFAULT_ROOT/"kolo-notebook-hitboxes-v29.json").write_text(json.dumps(hitboxes,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
    font = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc",18)
    actions = ("idle","stroll-2","walk-2","climb-up-2")
    sheet = Image.new("RGB",(1120,1120),"#FFF8F1")
    draw = ImageDraw.Draw(sheet)
    for row,(mode,bg) in enumerate((("", "#FFF8F1"),("book-","#FFF8F1"),("","#1C2833"),("book-","#1C2833"))):
        draw.rectangle((0,row*280,1120,(row+1)*280),fill=bg)
        for col,action in enumerate(actions):
            frame = frames[mode+action].resize((250,250),Image.Resampling.LANCZOS)
            sheet.paste(frame,(col*280+15,row*280+22),frame)
            draw.text((col*280+12,row*280+4),mode+action,font=font,fill="#6C4433" if row<2 else "#F5E5D0")
    sheet.save(destination/"kolo-v29-contact.png")
    hits = Image.new("RGB",(1120,1120),"#F8F5EE"); hd=ImageDraw.Draw(hits)
    for row,action in enumerate(("idle","stroll","walk","climb-up")):
        for col in range(4):
            name = "book-"+(action if action=="idle" else action+"-"+str(col+1))
            f=frames[name].resize((256,256),Image.Resampling.LANCZOS)
            px,py=col*280+12,row*280+24; hits.paste(f,(px,py),f)
            x,y,w,h=hitboxes["kolo-"+name+".png"]
            hd.rectangle((px+x/2,py+y/2,px+(x+w)/2,py+(y+h)/2),outline="#D82B48",width=2)
            hd.text((col*280+12,row*280+2),name,font=font,fill="#513B32")
    hits.save(destination/"kolo-v29-hitboxes.png")
    print(f"Claude artwork: {len(frames)} PNG frames, {len(hitboxes)} measured notebook hitboxes")


if __name__ == "__main__":
    preview(Path(__file__).resolve().parent/"artifacts"/"v29"/"kolo-art")
