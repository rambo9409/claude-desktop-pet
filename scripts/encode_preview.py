"""Encode already-rendered WPF PNG frames into a reviewable GIF."""
from argparse import ArgumentParser
from pathlib import Path

from PIL import Image

parser = ArgumentParser(description=__doc__)
parser.add_argument("source", type=Path)
parser.add_argument("destination", type=Path)
parser.add_argument("--frames", type=int, required=True)
parser.add_argument("--cadence", default="40")
options = parser.parse_args()
cadence = [int(value) for value in options.cadence.split(",")]
if options.frames <= 0 or any(value <= 0 for value in cadence):
    parser.error("Frame count and durations must be positive.")
frames = []
for index in range(options.frames):
    with Image.open(options.source / f"{index:03}.png") as source:
        frames.append(source.convert("RGB"))
options.destination.parent.mkdir(parents=True, exist_ok=True)
frames[0].save(
    options.destination,
    save_all=True,
    append_images=frames[1:],
    duration=[cadence[index % len(cadence)] for index in range(options.frames)],
    loop=0,
    disposal=2,
)
print(f"Encoded {len(frames)} frames: {options.destination}")
