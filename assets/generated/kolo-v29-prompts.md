# 珂洛 V2.9 持本与空手素材

2026-10-30。使用内置 imagegen，真实透明背景。参考用户提供的橙发角色与现有Q版珂洛。原图保留，打包只裁切、等比缩放和定位。完整提示词见各段。

原图为 `kolo-free-v29.png`、`kolo-book-v29.png`、`kolo-notebook-v29.png`。打包脚本为 `build_kolo_v29.py`，配方为 `kolo-v29-crops.json`，逐帧本子命中框为 `kolo-notebook-hitboxes-v29.json`。每行/列的图集空隙单独测量，用临时alpha连通分析确定主体外接裁切范围。源图保持原样，提取后仅等比缩放和定位。

2026-11-04核对时，空手跑步第2帧带入了上一行鞋底，持本攀爬第3帧带入了右邻格头发。配方通过两个源图坐标矩形排除邻格区域，分别为 `[448,574,466,578]` 和 `[849,1205,857,1236]`。人物本身的像素未改动，其他45张输出和21个命中框完全一致。验证与对比图在 `artifacts/v29/fragment-check/`。

输出47张：空手25、持本21、独立本子1。除独立紧裁本子外均为512×512，脚底基准486；四张慢行、四张跑步和四张攀爬分别是绘制姿态，表情通用状态复用同一套脸部姿态。没有把纸板贴在空手图上充当手持，持本图中手臂是真实抱住本子的。

## 空手图集

Use case: stylized-concept. Production desktop-pet animation atlas, real transparent PNG, 4 columns x 5 rows, 20 separate full-body sprites, no text or labels, no checkerboard, large transparent gutters. Use image 1 as clothing/character reference and image 2 as exact chibi style/proportions reference. Redesign the same orange-haired girl consistently in every cell: 2.5 heads tall cute chibi, flowing orange hair, amber eyes, gold chrysanthemum hair flower and black bow, elegant ivory blouse with ruffled collar, tiny ivory capelet over shoulders, black waist corset with gold buttons and chain, ivory ruffled skirt, black stockings, black Mary Jane shoes. Keep face/outfit/colors identical in every cell. Polished anime chibi, delicate clean line art and soft shaded highlights matching image 2. THIS ATLAS IS THE EMPTY-HANDED VERSION: NO notebook, NO clipboard, NO book anywhere, no object in hands other than small magical glows in final row.

Row1 four independent front/three-quarter poses: neutral hands relaxed; happy smile arms slightly raised; gentle smiling head tilt; thoughtful eyes-closed chin gesture.
Row2 four successive slow walking poses facing RIGHT with clear alternating LEFT/RIGHT leading leg and passing legs, modest natural steps, upright torso.
Row3 four running poses facing RIGHT with alternating leading leg and opposite arms, forward lean, steady proportions.
Row4 four upward-climbing poses facing RIGHT touching an INVISIBLE wall: alternate reaching hands and bent knees; no wall drawn.
Row5 four magical casting stages: raising right hand; hand fully raised; golden tiny star spark above hand; hand returning down.
Whole character including hair and feet fully inside every cell, no crops or overlapping silhouettes. Transparent background.

## 持本图集

Create a NEW matching variant atlas based on reference image 1 (the exact chibi character design), using reference image 2 ONLY for the notebook/clipboard holding gesture. Real transparent background. Portrait 4 columns x 4 rows grid, EXACTLY 16 full-body sprites, generous EMPTY transparent gutters between ALL cells, no text, no grid lines or floor, no overlaps or cut-off hair. Each sprite must be completely within its cell.
Character matches image 1 exactly: same orange hair, gold flower and black bow, big amber eyes, ivory ruffle dress and tiny shoulder capelet, black corset with gold details, dark stockings and Mary Jane shoes, tiny 2.5-head-tall proportions and detailed soft anime chibi style.
Crucial: in EVERY cell she firmly holds ONE SMALL NOTEBOOK / CLIPBOARD at the front of her torso with her left arm and visible hands touching it. The notebook is a simple vertical rectangle: medium dark brown leather border, cream blank paper, small GOLD clip at the top, NO written text, NO second book. It sits from upper chest to waist/skirt, like reference 2. Both arms/hands anatomically naturally wrap around the board where possible. It must be clearly visible, not hidden behind hair. This book will be an interactive target, keep its front cream rectangle recognizable.
Row1: (1) neutral front-facing holding notebook with both hands, (2) happy smile holding notebook, (3) gentle head tilt cuddling notebook, (4) thoughtful eyes closed holding notebook.
Row2: FOUR successive slow-walking poses facing RIGHT holding notebook stably at front. Explicit sequence: left leg forward, legs passing with right heel raised, right leg forward, legs passing with left heel raised. Visibly distinct step phases.
Row3: FOUR successive running poses facing RIGHT, alternating front leg, hugging notebook securely to chest with both arms, hair trailing, stable identical anatomy and clothes.
Row4: FOUR upward climbing poses facing RIGHT at an invisible wall, notebook held in left arm at chest, right hand reaching up and then pulling, knees alternate. The notebook must remain visible. No actual wall.
All 16 sprites same visual body scale and full body from ahoge to soles. NO magic effects.

## 独立本子

Use case: stylized-concept. Create a single isolated notebook / clipboard prop for a cute anime desktop pet, matching the board held by the orange-haired character in the reference. Only the board, absolutely NO hands, no character, no hair, no surface. Straight-on front view, upright vertical orientation, a slightly rounded rectangular dark brown leather frame around a creamy blank paper face, small warm GOLD clip centered at the top. Thin dark illustrated edges, subtle soft highlights, miniature chibi-game prop style, clean legible silhouette. Real transparent background with generous transparent margin. No text, writing, logos, shadows cast on a floor, bubbles or decorations. Aspect ratio of board about 0.75 wide to 1 tall. Precisely preserve the simple brown + cream + gold design in the reference.
