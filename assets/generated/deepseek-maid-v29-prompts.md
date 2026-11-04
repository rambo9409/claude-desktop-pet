# DeepSeek V2.9 参考图重绘记录

日期：2026-10-30。工具：内置 image_gen，真实透明背景；无外部API。用户参考为蓝发鲸尾女仆少女，不再使用V2.8圆鲸鱼形象。

用户提供的参考原图保留在 `assets/source/deepseek-reference-v29.png`，只作为形象参考，不作为运行素材。

原图：`deepseek-maid-atlas-v29.png`（20姿态）、`deepseek-maid-airborne-v29.png`（缓降、拖拽）。裁切配方：`deepseek-maid-v29-crops.json`，脚本：项目根 `build_deepseek_v29.py`。只进行矩形裁切、等比缩放、画布定位，不补画、重染或清除生成alpha；alpha阈值仅用于测量边界。

所有慢行、奔跑、攀爬和施法都是独立绘制姿态；27个运行帧包含复用的表情过渡。只有姿态图像与本地动作，不涉及模型服务接入。

## 主图集完整提示词

Use case: stylized-concept. Asset type: production transparent sprite atlas for a Windows desktop companion. Use the supplied image strictly as CHARACTER IDENTITY reference: this is a human chibi WHALE-TAILED MAID GIRL, NOT a round animal whale. Preserve her navy-blue long hair fading to pale cyan at the ends, big blue eyes, small blue bows by her ear fins, white frilled maid headband, dark navy puff-sleeved dress, white frilled apron with a tiny blue whale motif, white underskirt, dark blue Mary Jane shoes, white socks, HUMAN arms and two HUMAN legs, and her beautiful thick blue whale tail with forked flukes curling compactly behind her. Match this polished anime chibi style, around 2.5 heads tall, clean delicate outlines, gentle shading and readable rounded silhouette. No watermark, no logo badge, no background. Do not include the dark blue rectangular background from the reference.
Create EXACTLY 20 fully painted separate full-body poses in a clear 4 columns by 5 rows regular grid on a genuinely transparent canvas, about 4:5 aspect ratio. Make generous transparent gutters between every cell and margins outside all sprites. Keep all clothing, hair, proportions and colors CONSISTENT. Every pose must fit fully inside its cell, including shoes, fin tips and tail. Compact tail curls behind the body so it does not cross cell boundaries. No cell labels or grid lines. All moving poses face RIGHT, side-three-quarter view.
Row 1: neutral front-three-quarter standing (both blue eyes open); happy arms lifted; affectionate wink hands near cheek; thoughtful hands gently holding a small light-blue bubble.
Row 2, a clearly alternating FOUR-PHASE SLOW WALK: (1) left leg forward RIGHT and right leg behind LEFT, (2) legs passing underneath body, (3) RIGHT leg forward RIGHT and left leg behind LEFT, (4) opposite passing pose. Keep head at constant height, small natural steps, boots fully visible below skirt. Visibly change which leg overlaps: do not repeat the same leg in front four times.
Row 3, FOUR-PHASE RUN to right: left leg reaching forward with opposite arm; passing tucked legs; right leg reaching forward with opposite arm; opposite airborne passing pose. Both legs distinct, modest forward body lean, hair and tail trail behind.
Row 4, FOUR-PHASE UPWARD CLIMB facing right, invisible wall on RIGHT: left hand high and right knee raised; hands exchanging with feet braced; right hand high and left knee raised; opposing passing pose. Draw no actual wall, always humanoid arms and legs, compact curled tail clear of hands and feet.
Row 5, FOUR magic bubble casting phases: hands rise; one little bubble above palms; three little bubbles released upward; relaxed lowered hands returning to idle. Bubbles are modest and remain within the cell. No huge surrounding glow, no floor shadow. Crisp full anatomy and genuine transparent alpha.

## 缓降与拖拽完整提示词

Use case: stylized-concept. Create a transparent production sprite sheet with TWO full-body chibi poses side by side, each inside a generous separate transparent cell. Reference 1 is the exact established character sprite atlas; preserve this navy-to-cyan long-haired WHALE-TAILED HUMAN MAID GIRL exactly. She is NOT an animal whale. Same 2.5-head chibi proportions, blue eyes, white frilled maid headband, blue bows, dark navy puff-sleeved dress, white frilled apron with tiny blue whale motif, white underskirt, white socks, navy Mary Jane shoes, HUMAN arms and HUMAN legs, blue ear fins and big forked whale tail curling compactly behind. Same polished anime shading and crisp outlines. Reference 2 confirms the identity only; no watermark or background.
Left cell: natural gentle descent, head and body upright three-quarter facing RIGHT, arms gently spread sideways for balance, two legs relaxed bent underneath, hair and whale tail floating gently upward behind as she slowly sinks. No glider, no wings, no parachute; tail stays fully visible and helps buoyancy. Friendly surprised expression.
Right cell: being lifted or dragged gently by an unseen hand above, cute surprised open blue eyes, shoulders slightly raised, relaxed dangling HUMAN legs and shoes, arms slightly out with elbows bent, tail curling down and to her LEFT behind. Do not draw a grabbing hand or pointer. Do not make her a fish torso.
Real transparent background, no floor shadow, no text, no box, no UI, no huge bubbles. Both heads the same scale and both bodies fully in view, with clear margin around hair, tail flukes, fingers and shoes. Keep each silhouette fully inside its own cell with a wide clear vertical gutter; do not crop any anatomy.
