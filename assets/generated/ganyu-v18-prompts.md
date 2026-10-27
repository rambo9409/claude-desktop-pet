# Ganyu V1.8 prompts — built-in imagegen

## Slow walk color correction

Use case: lighting-weather.
Asset type: transparent 2x2 four-frame desktop sprite sheet.
Input image 1 is the EDIT TARGET, the current Ganyu four-frame slow walk. Input image 2 is the AUTHORITATIVE COLOR REFERENCE, her idle character. Input image 3 is another authoritative color/reference for her established animation.
Primary request: MATCH ONLY THE COLOR GRADING AND MATERIAL PALETTE of image 1 to image 2 and image 3. The current walking sheet is too saturated, bright and cobalt blue, while all other animations have soft pale powder blue / periwinkle hair and muted red horns and red ribbons. Reduce saturation of blue hair and soften highlights to match idle and run exactly. Her skin should be soft pale peach, not vivid orange. Her gold should be soft antique gold, not bright yellow. The cool white cloth and muted dark violet stockings must match image 2.
CRITICAL INVARIANTS: Preserve the exact FOUR existing full-body poses and their coordinates, all silhouettes, head size, hair strands, facial expression, costume, horn shapes, near/far leg order, and the 2x2 sheet layout from image 1. Do not redraw poses. Both feet remain perfectly grounded, BOTH flat shoes on the SAME floor baseline in EVERY frame. No foot lifting or raised heels, no changes of any legs, knees, arms, hair shape, facial shape, framing, camera or anatomy. Upper body stays quiet and upright exactly as now. There is no floor drawn.
Output remains an exactly 2 columns by 2 rows square atlas with same four quadrants, large transparent empty gutters, genuinely transparent alpha, no labels, no borders, no marks, no glow, no shadow. The only requested change is color grading to the softer reference palette.

## Mountain climb and jump

Use case: stylized-concept.
Asset type: transparent production animation sprite atlas, exactly FOUR COLUMNS and TWO ROWS, 8 distinct full-body frames, landscape 2:1.
Input image 1 is AUTHORITATIVE character identity, proportions, colors, shading, costume and art style. Input image 2 is continuity reference for the same Ganyu's running/climbing perspective. Make a new sprite sheet in the SAME soft rounded detailed chibi illustration style as image 1, not oversaturated. Her powder-blue/periwinkle hair, black/red horns, purple eyes, muted red accessories, antique gold details, white clothes, dark violet stocking-covered short legs, and tiny shoes must stay identical. Each sprite is approximately two heads tall, big round head tiny body, consistent scale throughout. Keep all hair pale powder blue just like image1, NOT vivid cobalt.
Primary request: Ganyu's special agile mountain-goat-like CLIMB and WINDOW-TO-WINDOW JUMP, retaining humanoid chibi Ganyu identity and clothing. No actual goat, hooves, extra horns or body transformation.
All 8 characters face RIGHT in the same three-quarter side view, with a subtly visible face. Sideways motion is rightward. Every frame entirely contained in its equal grid cell. Each cell has at least 12% fully empty transparent margin on every side. Huge central gutters, no touching, no neighboring fragments, stable proportions and gentle coherent hair movement.
TOP ROW: four successive CLIMB poses beside an INVISIBLE wall on picture RIGHT. Hands extend to the right side, with alternating compact leg bracing and springy pushing. Frame1 leftmost: crouched compact coil, front tiny knee drawn near torso, one hand reaches high to right, other holds lower. Frame2: forceful little leg extends down while opposite knee tucks; upper hand pulls body upward. Frame3: alternate hands and support leg; other hand now high and other knee braces. Frame4: second springy push with hands exchanged, returning to first coil. Distinct alternating hands/legs, soft rounded limb anatomy, no long human legs or stretched body. Both hands remain on RIGHT side at different vertical levels. No wall drawn.
BOTTOM ROW: four phases of a RIGHTWARD leap between windows. Frame1 bottomleft: compact preparatory squat, BOTH knees gently bent, body leans slightly right, hands balance in front. Frame2: airborne compact tuck, two knees bent underneath, arms spread slightly sideways for balance, hair trails softly LEFT. Frame3: airborne approach, tiny legs reach gently forward/right for landing, torso leans slightly right, arms forward, hair trailing left. Frame4 bottomright: soft landing with tiny knees flexed and feet together, arms relaxed outward for balance, upright head. These are clearly a jump, not running legs. Stable head model/face/horns/costume across every frame.
Output: actual transparent alpha, no wall, no floor, no scenery, no frame labels, text or gridlines, no effects, no motion trails, no glow. Soft pale source palette MUST match image1.

## Output and verification

Mode: built-in imagegen, RGBA transparency. All outputs copied into the project; no raster recoloring was performed in Python.

- Slow-walk output: `assets/generated/ganyu-stroll.png`, 1254 × 1254, 2 × 2 cells.
- Previous slow-walk backup: `assets/generated/ganyu-stroll-v17.png`.
- Mountain output: `assets/generated/ganyu-mountain.png`, 1774 × 887, four climb cells above four jump cells.
- Generation outputs: `exec-bfc9d9b7-0e30-4b8c-9b4a-b84b3287ecf7.png` and `exec-78cfb1e9-43af-4f3a-ab37-e627ced906ae.png`.
- Visual review: walking preserves the four small grounded step poses with no knee lift. Pale periwinkle hair, muted red horns/accessories and pale peach skin now resemble the established idle/running palette. Mountain poses show compact alternating leg braces/pushes above, and crouch/tuck/approach/landing below. All face right with short chibi limbs.
- Read-only color analysis: bright opaque blue pixels selected with alpha > 240, Pillow HSV H in [140,172], V > 120, S > 25. Median HSV saturation: idle 79, established motion 99, previous stroll 147, new stroll 90, mountain 109 (0–255 scale). New stroll is within the existing idle/motion range; mountain is near the established motion palette. These region statistics are a useful consistency check, not a claim of identical per-pixel color.
- Slow-walk alpha > 32 silhouette overlap (intersection/union) with V1.7: overall 98.60%; individual cells 98.80%, 98.30%, 98.51%, 98.78%. Remaining changes are small edge variations from image generation.
- IMPORTANT atlas crop: mountain row split must be y=473, not image half-height. Opaque top-row shoes reach y=470; lower-row hair starts y=475. Suggested column boundaries 0,450,887,1330,1774. All split lines are empty at alpha > 32. Use one common scale for both rows so crouched jump frames do not enlarge her head.

V2.0 整理注：文中旧 ganyu-stroll-v17.png 已完整归档至 [历史源图](../../archive/source-history/ganyu-stroll/ganyu-stroll-v17.png)，原始内容与 SHA-256 保持一致。
