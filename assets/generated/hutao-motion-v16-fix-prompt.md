# 胡桃 V1.6 跑步与攀爬左右交替修正

## 第二轮：前臂垂直穿过脸缘

```text
Correct the anatomy in this 4x2 animation sheet. Same Hu Tao character, proportions, hat, painting style, framing, transparent background. Only edit top row cell 3 and bottom row cells 3 and 4. Other five cells unchanged.
TOP CELL 3: Her frontmost LEFT leg must point BACK toward screen LEFT, with that thigh laid visibly ON TOP of the other thigh at their crossing. Back/left boot shows outer red flower. The obscured RIGHT leg points FORWARD to screen RIGHT, with the forward/right boot showing its plain DARK INNER SIDE and NO RED FLOWER. The near thigh must visibly connect to the BACK/LEFT boot, not to front/right boot.
BOTTOM CELLS 3 AND 4: Draw a noticeably DIFFERENT climbing pose. The near/frontmost arm that currently runs horizontally across her chest must reach UPWARD instead. Bend that near arm up at the elbow so its long decorated black sleeve is VERTICAL IN FRONT OF HER MOUTH AND RIGHT CHEEK. Near hand is raised beside her cheek, just under the RIGHT edge of hat brim. Her face remains visible to LEFT of this raised near sleeve. The near sleeve overlaps/occludes edge of cheek; this makes it unmistakably the front arm reaching HIGH.
The other FAR arm now reaches LOW. Its palm is at belly/waist height on the RIGHT, and its sleeve is partly hidden behind torso. REMOVE the existing high far arm behind the head in these two cells! There are only two hands: near hand HIGH beside cheek at upper head height, far hand LOW beside belly. DO NOT keep original high arm behind head plus add another high arm. Replace the original arms with this opposite leading arm pose.
For legs in those bottom cells: near/frontmost thigh extends down and LEFT and near boot is LOW. Far knee bends HIGH toward screen RIGHT, far thigh partly occluded by near thigh, far foot is higher against wall. In bottom cell 4 bend near knee a little more toward next step while near hand stays HIGH.
No wall, no ladder, no shadows, no labels, no text, no background. Preserve generous cell gutters and entire full bodies inside each cell. Preserve genuine alpha transparency. The visible front sleeve must point UP in last two climbing cells rather than run horizontally across chest.
```

## 验收结果：未采用

- 第一轮没有可确认的左右腿或手臂交换。
- 第二轮攀爬后两格的高举前臂明显，但低位手退到身体后下方，未贴合右侧攀爬面；跑步第三格仍可见近腿前伸、前脚红花，左右腿没有可靠对调。
- 因此保留原 `hutao-motion.png`，没有覆盖生产素材。此项仅是统一胡桃角色后对旧运行/攀爬周期的额外检查；此次用户提出的慢走修正由独立 `hutao-stroll.png` 提供。
- 第二轮候选源路径：`<local-path>`。


方式：内置 image_gen，透明背景，参考初版 `hutao-motion.png`。

```text
Use case: precise-object-edit. Asset type: game animation sprite atlas. Edit this transparent 4-COLUMN by 2-ROW sheet of chibi Hu Tao. Preserve the exact same character, soft painted style, low rounded dark brown hat with gold talisman and red flowers, face, hair, proportions, costume, full figures, figure scale, 4x2 cell layout and empty transparent gutters. No background, no floor, no text, no panels.

Keep TOP row frames 1, 2 and 4 exactly as they are. Keep BOTTOM row frames 1 and 2 exactly as they are. Correct ONLY TOP frame 3 legs, BOTTOM frame 3 arms/legs, and BOTTOM frame 4 arms/legs. This means edit the third column and the bottom-right cell; all other cells stay unchanged.

Near = the limb on the camera side, rendered IN FRONT when crossing. Far = the limb on the hidden side, rendered behind. All sprites continue facing RIGHT, same fixed three-quarter view as source.

TOP ROW THIRD COLUMN: opposite running contact phase from top-left. Move the NEAR leg BACKWARDS to the LEFT in a strong bent-knee backswing, with near thigh clearly connected to near hip and crossing IN FRONT of the other thigh. Near knee behind hip, near shin curling back, sole angled slightly toward viewer. The near boot at the LEFT shows its outer red flower and gold trim. The FAR leg reaches FORWARD to the RIGHT: show the inner side of the far boot, naturally darker, no outer red sock flower visible. Far thigh is behind the near thigh at their crossing. Clear two-leg silhouette. The rightmost shoe is now the FAR foot; the leftmost shoe is NEAR. Keep head, torso, hair and hat of this third frame unchanged.

BOTTOM ROW THIRD COLUMN: opposite CLIMBING grasp from bottom-left. She climbs an invisible vertical wall on her RIGHT. NEAR arm now reaches HIGH from near shoulder, drawn IN FRONT of her upper chest/edge of cheek, with near palm pressed against invisible wall at her eyebrow/hat-brim height. A clear long diagonal near sleeve connects shoulder to high palm. FAR arm bends down with FAR palm at lower CHEST height, behind near arm and partially hidden by torso. This swaps the hands: the hand and sleeve in front of her face/torso are the HIGH one, and the farther hand is LOW. Also swap knees: NEAR knee stays LOW with near leg extended down-left, dark boot low; FAR knee bends HIGH against invisible wall on the RIGHT at lower-chest/waist height, far boot planted below high knee. There are exactly TWO arms/hands and TWO legs/feet.

BOTTOM ROW FOURTH COLUMN: advance from bottom frame 3 into the next upward pull. Near hand still HIGH and taking weight; far hand releases the LOW hold and moves upward to middle chest height (still clearly lower than near high hand). Near leg pushes body up from low foothold and begins bending toward next step; far knee remains the higher knee on RIGHT. Preserve opposite hand layering vs bottom frames 1 and 2, so frames 3 and 4 visibly use the OTHER leading arm. All feet/hands must be attached anatomically. No added wall or ladder.

Important success criteria: top contact frames 1 and 3 visibly trade which leg leads; bottom first half and second half visibly trade which arm reaches high and which knee lifts. Don't simply raise or lower an unchanged pose. Do not shrink or enlarge one cell relative to others. Keep all pixels well inside their original cells, clear row and column gutters, actual alpha transparency.
```
