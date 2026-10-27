# V1.4 character sprites

Created with the built-in imagegen tool. Original transparent PNGs are kept here; build_assets.py normalizes their canvas and packages the animation frames.

Style references: ../references/kolo-idle.png and ../references/hutao-idle.png.
Identity references: ../references/ganyu-gacha.png and ../references/varesa-gacha.png.

## Shared prompt

Use case: style-transfer. Asset type: a single full-body transparent desktop pet sprite. The first two input images (Kolo and Hutao) are the STRICT visual style reference: rounded approximately two-head-tall chibi, very oversized round head, tiny short torso and stubby limbs, large shiny anime eyes, soft painterly 3D-like shading, finely painted hair locks, warm soft lighting, cute neutral smile. Match their rendering style and proportions closely. The third image is CHARACTER IDENTITY REFERENCE ONLY; do not copy its realistic body proportions or effects. Output one character only standing front-facing, arms relaxed a little away from torso, both feet at same ground line, complete silhouette visible, centered on transparent background with generous 10% padding. No text, no watermark, no floor or shadow, no scenery, no particles, no extra characters, no bounding box. Keep eyes open.

## Ganyu

Subject: Ganyu from Genshin Impact. Light powder-blue hair with soft long locks and curled bangs; two small dark red-and-black curved qilin horns; gentle violet eyes; blue-black-white Chinese-inspired outfit, white flared fabric and detached sleeves, fine gold trim, dark tights and tiny shoes, red neck tie and small golden bell. Translate recognizable costume to adorable modest rounded chibi proportions like input 1 and 2. NOT a pasted portrait on a tall body. Real alpha transparency.

## Varesa

Subject: Varesa from Genshin Impact. Pink hair in two thick braided pigtails with soft pale mint ends, turquoise ribbons; two small curved ivory cowlike horns with dark geometric bands, cute cowlike ears, large violet-blue eyes; black sporty top and shorts with red and teal accents, cream-yellow detached sleeves, chunky white boots with heart accents. Adapt recognizable outfit into cute rounded modest chibi costume; no eye mask or combat effects. Match reference1 and2 small body proportions closely, not tall slender anime proportions. Real alpha transparency.
