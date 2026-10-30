# V2.8 AI角色原始生成提示词

制作日期：2026-10-30。工具：内置 image_gen，透明背景；不使用外部 API。图集为新创作，裁切脚本只提取、等比缩放及摆放，不重绘角色或去除已有透明度。

## GPT6A 龙女

Use case: stylized-concept. Asset type: production sprite atlas for a Windows desktop pet. Create a large transparent PNG, portrait 4:5 aspect ratio, with EXACTLY 20 separate fully painted sprites in a perfectly aligned 4-column by 5-row grid. Every cell has equal size, generous empty transparent gutters, no overlaps, no labels, no text, no background, no shadows outside the sprite. Character identity: an original adorable chibi dragon girl named GPT6A, 2.5 heads tall, large emerald green eyes, pearl-white long wavy hair fading to mint tips, two small gold dragon horns, tiny emerald dragon wings and a visible curled mint dragon tail; elegant modest short ivory-and-jade fantasy dress with golden edging, dark jade short boots. Match polished anime chibi desktop companions: soft illustrated shading, crisp fine dark outlines, rounded cheeks, subtle detailed hair, warm porcelain highlights; NOT pixel art or a flat icon. Keep precisely identical outfit, face, colors, scale and proportions in all cells. Her body and all limbs fully visible in every cell, facing slightly to the RIGHT throughout except the front-facing first cell. Make the hair and tail compact enough to fit well inside each cell.

Row 1 left to right: neutral standing front-facing; happy delighted arms lifted; gentle eyes-closed affectionate head tilt; thoughtfully holding a small glowing orb in both hands.
Row 2: FOUR successive phases of a gentle slow walking cycle to the RIGHT, visibly alternating left and right legs, full-body side-three-quarter view, modest steps and constant torso height.
Row 3: FOUR successive phases of a dynamic running cycle to the RIGHT, visibly alternating legs and arms, forward body lean, tail following movement; feet separated naturally.
Row 4: FOUR successive upward climbing phases viewed from left side facing RIGHT: alternate left hand/right hand raised and left leg/right leg tucked, palms touching an INVISIBLE vertical wall on the RIGHT. No actual wall drawn.
Row 5: FOUR magic casting phases: raising one hand; holding hand up with small mint orb; both hands making a small golden star ring; relaxed hands returning down. Effects must remain small and contained inside individual cells. No gigantic aura or floor plane.

## DeepSeek 小肥鱼

Use case: stylized-concept. Asset type: production sprite atlas for a Windows desktop pet. Create a large transparent PNG, portrait 4:5 aspect ratio, with EXACTLY 20 separate fully painted sprites in a perfectly aligned 4-column by 5-row grid. Every cell has equal size, generous empty transparent gutters, no overlaps, no labels, no text, no background, no floor shadows. Character identity: an original cute round plump baby whale-fish named DeepSeek, cobalt blue back and head fading into light azure flanks, creamy white belly, big glossy navy eyes, tiny rosy cheeks, smiling small mouth, two short rounded pectoral fins, a broad little upward curled whale tail, and a small dorsal fin. A tiny cyan star-shaped highlight on its forehead identifies this particular mascot, not a hat or accessory. Entire body is a rounded whale, NO human legs, NO human arms, NO clothing. Match polished softly shaded anime chibi desktop companions: fine dark outlines, clean silhouette, pearly highlights, detailed yet simple and lovable, NOT pixel art or a flat logo. Keep precisely identical face, colors, scale, shape and design in all cells. Full body visible throughout. All movement sprites face RIGHT (eyes on right, tail on left). The first neutral sprite is a slight front-three-quarter view facing RIGHT.

Row 1: neutral hovering; happy smile with fins raised; eyes closed contentedly with relaxed fins; thoughtful expression holding one tiny glowing bubble between fins.
Row 2: FOUR successive phases of slow swimming to the RIGHT: tail flukes and fins gently alternate up/down, stable center of body, subtly changing curve without rotating the whole body.
Row 3: FOUR successive phases of fast swimming to the RIGHT: active tail beats, fins swept back then forward, slightly stretched determined pose, no motion streaks outside the body.
Row 4: FOUR successive upward swimming phases: fish body angled nose-up to the RIGHT at about 55 degrees, tail and fins alternate visibly, as if swimming up an invisible wall. Do not draw a wall or water surface.
Row 5: FOUR bubble magic phases: cheeks puffing; releasing one small cyan bubble from mouth; happy little cluster of three bubbles close to its face; returning to relaxed smile. Effects remain inside individual cells.

## 独立掉落与拖拽姿态

参考图：上面两张生成图集。输出 `ai-airborne-v28.png`。

Use these two atlases only as CHARACTER IDENTITY REFERENCES. Create a NEW transparent 2-column by 2-row sprite atlas with exactly FOUR sprites and generous empty margins between cells, no labels/text. Keep the polished anime chibi illustrated style, exact faces, colors, outfits, and proportions from references. Top left: the white-haired mint-and-gold dragon girl in a falling pose, arms spread outward for balance, knees gently bent, hair and dragon tail lifted by the air, small wings extended, facing RIGHT. Top right: same dragon girl being gently picked up by the desktop cursor (no cursor drawn): arms relaxed forward, legs dangling below, playful surprised expression, tail loosely curled, facing RIGHT. Bottom left: blue-white round baby whale-fish in a falling pose, soft surprised round mouth, fins spread outward like tiny parachutes, tail raised, body facing RIGHT. Bottom right: same fat whale-fish being held (no hand/cursor drawn): relaxed half-closed happy eyes, fins hanging down and tail curved down, body still rounded, facing RIGHT. All four full bodies fully visible, no cut-off horns, fins, tails, no water, no hands, no extra characters, no glow outside each cell. Actual alpha transparency.

## 龙女慢行4相位修订

参考图：`gpt6a-atlas-v28.png`。输出 `gpt6a-stroll-v28.png`。原图集慢行步伐差异过细，修订为迈步—并足—反向迈步—并足，保持低抬脚。

Using the supplied atlas as exact character identity and art-style reference, make a NEW 2x2 transparent sprite sheet containing ONLY the mint-white-gold dragon girl, exactly four full-body poses of a WALK CYCLE facing RIGHT, side three-quarter view, very generous empty margins, no labels. Keep same small chibi proportions, eye size, white hair with mint tips, gold horns, jade/ivory dress, emerald wings and tail, shoes, shading, and SAME BODY SIZE across all four frames. This is for actual animation: the legs MUST alternate, not four near-identical steps. Top left: LEFT LEG in front with heel forward touching ground, RIGHT LEG extended behind, arms counter-swing. Top right: PASSING phase, feet close together under hips, right knee slightly forward, left supporting leg straight, arms pass near torso. Bottom left: RIGHT LEG now clearly far in front touching ground, LEFT LEG extended behind, opposite arm swing from top left. Bottom right: opposite PASSING phase, feet close under hips, left knee slightly forward and right supporting leg straight. Keep modest low steps, head and torso nearly level, no hopping, no oversized step, no running. Keep both boots clearly visible, with occlusion correctly changing for alternate legs, all tail/hair/horns fully inside each cell. Actual alpha transparency, no ground lines, no text, no background.

## 打包约定

- 每角色27条PNG资源：idle1、happy2、pet2、rose2、typing2、stroll4、walk4、climb-up4、ability4、fall1、held1。
- 每角色原图集20姿态，加2个独立空中姿态；龙女另补4帧慢行替换旧慢行，原稿仍保留。happy/pet/rose/typing选取相应的已绘姿态交替，保留现有CommonStates接口；没有生成假腿或扭曲鱼身。
- `ai-v28-crops.json` 记录实际行缝和裁切框。512×512输出，只裁切、等比缩放、放置，保留原始alpha；alpha<=8只在测量可见范围时忽略。
- 原图透明边缘有低alpha颜色残值，真实浅色/深色合成预览均检查，不使用删除背景颜色或全局改饱和度的方法处理。
- 龙女walk3与climb3共用的格缝独立设在y=795，避免下一行角尖进入跑步帧，同时保留攀爬帧的完整龙角。
- `build_assets.py --append-ai` 保留旧123个PNG内容原样，追加54个新条目。正常 `build_assets.py` 可从源图完整重建177项。
- 原图、提示词和裁切配方均纳入工程。生成自内置image_gen，未调用外部模型API或上传用户文件到其他服务。
