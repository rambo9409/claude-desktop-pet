# 甘雨 V1.6 慢走序列

## 定向修正：对侧腿接替

```text
Use case: precise-object-edit. Edit the provided 2x2 Ganyu walking sprite sheet to fix ONLY the leg-cycle mechanics, maintaining the painted chibi style, four faces, hair, outfit, and same scale. Keep genuine transparent background.
The top-left and bottom-left contact poses currently look almost identical; fix that failure. The two legs must TRADE front/back positions.

Use consistent lighting to make anatomy obvious: the NEAR leg and its shoe are lighter with clearly visible ornament, while the FAR leg and far shoe are darkened by natural body occlusion. This is shading only, same outfit. NEAR leg is the leg toward the viewer and is always drawn over the FAR leg where they overlap.

TOP-LEFT frame: keep its NEAR leg thrust forward to the RIGHT, bright near shoe at far right with heel down; FAR leg stretched back to the LEFT, dark shaded far shoe at far left, toe down and heel lifted. Keep the nearby hand toward back.
TOP-RIGHT frame: NEAR leg straight vertical below hip and bright near foot flat on ground; FAR leg bent with dark far shoe passing behind near shin. Feet nearly together in x.
BOTTOM-LEFT frame: REVERSE the two legs by drawing the BRIGHT NEAR LEG as one obvious continuous limb from the left/near hip down diagonally BACKWARDS to the LEFT, with near knee behind torso and bright near shoe at far LEFT touching ground only at its toe, heel high. This backward bright near thigh is IN FRONT of and obscures the other thigh at their crossing. The DARK FAR LEG stretches FORWARDS diagonally to the RIGHT behind the near thigh and dark far heel is planted at far RIGHT, toe raised. The front/right shoe must be visibly DARK FAR shoe, and back/left shoe visibly BRIGHT NEAR shoe. Same torso view as top-left, not a different body turn. Near arm forward. It should be obviously the opposite leg leading.
BOTTOM-RIGHT frame: DARK FAR leg vertical and flat foot supporting on ground; BRIGHT NEAR leg with near knee bent forward, near foot lifted slightly and moving in front of far shin to the RIGHT. This is a relaxed passing pose, no high kick.

Do not generate identical front-leg silhouettes in frames 1 and 3. Two legs only, same leg lengths. Show clear heel/toe articulation and opposite arm swings. Keep a clean transparent gap separating rows and columns, keep all four tufts and feet within cell. No floor, no shadow, no text, no symbols.
```

## 定向修正：帧间留白

```text
Use case: precise-object-edit. Keep all FOUR walking poses in this input image exactly as drawn, including their distinct near/far leg positions. Fix only the sprite-sheet LAYOUT. Currently the figures touch the quadrant borders and their hair overlaps the row seam. This is unacceptable for game sprite extraction.

Create a square 2x2 atlas with a LOT OF EMPTY TRANSPARENT PADDING. Treat it as four equal 512x512 cells in a 1024x1024 conceptual canvas. EACH character must be no taller than 370 pixels and no wider than 350 pixels within its 512x512 cell. Each is a SMALL figure centered in its cell. In EACH cell, the tuft must begin at y=65 and feet end at y=440. This leaves 65+ pixels of empty transparent top padding and 72+ pixels of empty bottom padding. The horizontal center seam must have at least 130 pixels of totally empty transparent gutter. Left/right central gutter likewise large. The result must have noticeably smaller figures surrounded by empty space, NOT full-bleed sprites.

Absolutely preserve the correct four pose drawings: top-left light NEAR leg forward RIGHT/dark FAR leg back LEFT; top-right light NEAR leg straight support/far leg bends behind; bottom-left light NEAR leg back LEFT crossing in front of dark FAR leg forward RIGHT; bottom-right NEAR knee bent forward and NEAR foot elevated to RIGHT while FAR leg supports vertically. Same character, illustration style, outfits, proportions, horn and hair shape, no redrawing pose mechanics, no extra feet. Whole figure fits each cell with generous transparent space. Genuine transparent background, no texture, no ground, no shadow, no labels, no gridlines, no border.
```

## 验收

- 读序：左上、右上、左下、右下。
- 第 1 帧近腿向右伸，远腿向左后蹬；第 3 帧近腿向左后蹬且大腿从远腿前方穿过，远腿向右落脚。
- 第 2 帧近腿支撑、远脚提起；第 4 帧远腿支撑、近脚在前提起。
- 同一朝向的两组接触与经过姿态；每帧完整全身，行列间透明空隙。
- 最终文件：`assets/generated/ganyu-stroll.png`；旧版保留：`ganyu-stroll-v15.png`。


生成方式：内置 image_gen，透明背景。角色参考：`ganyu-chibi.png`。

```text
Use case: stylized-concept. Asset type: production 4-frame 2D chibi walking sprite atlas for a desktop pet.
The attached image is the CHARACTER IDENTITY AND PAINTED STYLE reference. Re-pose this exact Ganyu chibi in a correct COMPLETE walking cycle. Same blue hair, red-black horns, purple eyes, bell, white blue gold costume, black stockings, decorated black white gold shoes, soft polished anime illustration, round chibi proportions.

Output exactly FOUR full-body sprites in a SQUARE 2 by 2 grid on a genuinely transparent background. Equal cells. Read order top-left, top-right, bottom-left, bottom-right. Every sprite centered in its cell. 12% transparent margin on every side INSIDE EACH CELL; especially empty central horizontal and vertical gutters. Nothing touches the image or cell borders. No labels, no floor, no shadows, no arrows, no frames, no text.

CRITICAL: this must show actual OPPOSITE LEG ALTERNATION, not the same leg always leading and not a translated standing pose. Show the character facing RIGHT in a fixed near SIDE VIEW (75 degrees turned from front), with torso/hips turned right, her face just slightly toward viewer so one violet eye is prominent and the far eye is partially visible. Both legs below her short skirt unobscured, hair mostly behind her back. SAME view and scale in all four poses. Keep head upright, torso nearly vertical, relaxed arms swing opposite legs. Slow gentle walking with at least one foot grounded, never running or jumping.

Define NEAR leg = leg closer to viewer (drawn on top when legs cross); FAR leg = the other leg (behind). One full cycle:
Frame 1 top-left: CONTACT A. Near leg extended FORWARD to screen-right, near heel on the baseline, near toe gently lifted. Far leg extended BACKWARD to screen-left, far heel lifted and toe on baseline. Feet separated horizontally by about 35% of head width. Near arm behind torso, far arm forward.
Frame 2 top-right: PASSING A. Near leg now vertical beneath hip, its whole foot firmly on baseline supporting her weight. Far knee bent as far foot swings forward directly past the near ankle, lifted slightly off baseline. Feet close horizontally. Near arm midway swinging forward.
Frame 3 bottom-left: CONTACT B, exact opposite of Frame 1. Near leg extended BACKWARD to screen-left with heel raised and toes touching baseline, far leg extended FORWARD to screen-right with heel contacting baseline and toe gently up. Feet again separated by about 35% of head width. The NEAR BACKWARD LEG must visibly cross IN FRONT of the far forward leg at upper thigh; the near/frontmost black stocking and shoe are now at LEFT, not RIGHT. Near arm forward, far arm behind. This is NOT a duplicate of Frame 1.
Frame 4 bottom-right: PASSING B, exact opposite of Frame 2. Far leg vertical beneath hip with flat foot on baseline supporting weight. Near knee bent forward, near foot passing in FRONT OF far shin, raised a little off baseline. Near arm midway swinging backward.

Main success criterion: a looping playback of frames 1,2,3,4 must clearly display left-right footfalls with the NEAR leg traveling front, under, back, forward on alternate steps. Keep subtle 2-3% vertical head bob. Maintain stable head and hip position in each cell so no jitter. Ground baseline same relative y in each cell. Draw only two anatomically connected legs and two feet. The legs must have clearly different shapes and overlap order in the two contact poses. Prioritize a readable mechanically correct walking cycle over decorative flourishes.
```

V2.0 整理注：文中旧 ganyu-stroll-v15.png 已完整归档至 [历史源图](../../archive/source-history/ganyu-stroll/ganyu-stroll-v15.png)，原始内容与 SHA-256 保持一致。
