Use case: precise-object-edit
Asset type: four-frame transparent PNG sprite atlas for a tiny desktop walking character.
Image 1 is the EDIT TARGET (the current 2x2 Ganyu walking sheet). Image 2 is a supporting identity reference only.
Primary request: replace ONLY the legs and very subtle arm poses of image 1 to make a gentle GROUNDED SHUFFLE walk. The current high lifted knee and airborne feet are wrong. The user explicitly says "不要抬脚" — DO NOT RAISE EITHER FOOT.

Keep the same exact powder-blue-haired chibi Ganyu identity, face, horns, colors, outfit, hair, rendering finish and three-quarter view facing right in ALL FOUR cells. Preserve her soft rounded 2.5D game-chibi proportions. Upper body stays upright, level and quiet. Change the lower-body choreography to tiny relaxed alternating steps with BOTH shoes touching the SAME imaginary horizontal floor in EVERY frame. Both knees are almost straight. No marching, no kicking, no raised heels, no toes above floor, no knee approaching the torso. This is a small quiet foot-slide shuffle, not a run.

Four genuinely different successive poses arranged 2 columns x 2 rows, read left to right, then top to bottom:
A TOP LEFT: near leg slightly forward toward picture right, far leg slightly behind left; BOTH soles flat on the floor, a small stance only.
B TOP RIGHT: both straight legs close to vertical, feet nearly beside one another, BOTH soles flat touching the floor, weight gently shifts toward far leg; nothing lifted.
C BOTTOM LEFT: reverse which leg is front: FAR leg slightly forward to picture right, NEAR leg slightly back to picture left; both soles flat touching the floor. The near thigh overlaps in front of the far thigh to clearly show near leg is now back. Match A's tiny stride length.
D BOTTOM RIGHT: both legs almost vertical again, BOTH shoes flat on the floor, weight gently shifts toward near leg; no lifted knee or raised foot.
Across the cycle the relative near/far leg overlap must swap, without flipping the character or moving her head sideways. Tiny arm counter-swing only. All four bodies same scale, height and camera angle. Shoes must have exactly the same bottom height within their cell. Knee joints stay low and nearly straight.

Layout: square 2x2 grid with very generous entirely transparent horizontal and vertical gutters (at least 12% of whole sheet around center seams), equal sprite scale and clear margin on every side. Each complete sprite fully contained in its own quadrant, no edges or hair crossing the central seams. No text, labels, panels, frame borders, shadows, floor, sparkles, or extra items. Real transparent alpha background.
Highest-priority acceptance criterion: neither foot is raised in any cell. BOTH shoes on the same level ground in ALL FOUR frames.

## Refinement: opposite grounded contact pose

Use case: precise-object-edit.
Edit this transparent 2x2 Ganyu sprite atlas in ONE narrowly targeted place: only the legs in the BOTTOM LEFT quadrant. Keep all other pixels/poses/identity/hair/head/clothes/style/view the same. TOP LEFT, TOP RIGHT, BOTTOM RIGHT stay as they are.

Bottom-left pose should be the OPPOSITE STEP to top-left. Top-left has nearer leg stepping forward to picture right. Therefore bottom-left must have the NEARER LEG angling backward toward picture LEFT, and the FAR LEG extending forward toward picture RIGHT. The nearer leftward stocking/thigh must visibly pass in front of the farther rightward thigh at their overlap. Swap the near/far leg visual layering at the hips; the nearer shoe on picture LEFT should be subtly wider/larger and the farther shoe at picture RIGHT subtly narrower. This must read as the OTHER foot forward, not the exact same stance as top-left.
Keep bottom-left knees nearly straight, both shoes fully FLAT and planted with the same baseline. Both shoes remain level, with no lift at all. Keep the small stride width identical to now, keep full body height/head alignment. No new objects, no text, no floor or shadow, maintain genuinely transparent alpha, equal 2x2 grid and generous empty central gutters.

## Output and verification

- Mode: built-in imagegen, transparent RGBA output.
- Selected output: assets/generated/ganyu-stroll.png (1254 × 1254).
- V1.6 backup: assets/generated/ganyu-stroll-v16.png.
- Initial generation: exec-b5710d11-d5f2-47f5-bcb6-689faa935a0f.png.
- Selected refinement: exec-412a8494-0cb6-4d74-8112-3857b5feb244.png.
- Visual review: all four poses have knees low, both shoes flat and grounded. Bottom-left reverses near/far thigh overlap for the opposite contact step.
- Alpha > 32 shoe-bottom measurements by cell (left/right, local y): 597/598, 601/599, 565/565, 568/567. Within-cell difference at most 2 source pixels; atlas packing aligns each complete pose baseline.
- Center seams ±20 px have alpha at most 1, leaving safe transparent crop gutters.

V2.0 整理注：文中旧 ganyu-stroll-v16.png 已完整归档至 [历史源图](../../archive/source-history/ganyu-stroll/ganyu-stroll-v16.png)，原始内容与 SHA-256 保持一致。
