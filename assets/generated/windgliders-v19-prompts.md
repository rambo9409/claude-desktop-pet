# V1.9 三色风之翼素材

## 生成方式

使用内置 `image_gen`（透明背景），未使用 CLI 或 API fallback。角色参考为本项目的 `hutao-chibi.png`、`ganyu-chibi.png`、`varesa-chibi.png`；生成前已查看这三张图，提示词中描述其共同柔和精致 Q 版画风。本次新建完整风之翼 atlas，不含人物。

最终文件：`assets/generated/windgliders-v19.png`

项目最终打包：三组翼宽480，放在独立512×512透明图层；共同翼根为(256,340)，经真实WPF叠加检查后从初始建议的y=300下移到肩背。只裁切、等比缩放和定位，没有重绘或改色。运行时绕这个翼根展开、收拢，并在大倾角时适当收翼以防窗口裁边。

原始输出：`<local-path>`

## 最终提示词（原文）

```text
Use case: stylized-concept.
Asset type: one production transparent sprite atlas for a soft, polished chibi anime desktop companion, containing exactly three separate complete pairs of Wind Glider wings, stacked in three rows.
Primary request: paint a coherent set of three original Genshin-inspired wearable wind-glider rigs, no characters. Each rig has left and right wings attached at a small central shoulder-blade harness, very wide and shallow, nearly horizontal, a low fan of overlapping stylized feather-like fabric panels with a fine elegant structural frame. These are wearable horizontal gliders, NOT upright angel wings, not butterfly wings, not bat wings. Wide upper edge, gentle upward sweep to outer tips, scalloped layered lower edge, compact root at the exact horizontal center. A small central V-shaped empty notch above the harness allows the character neck and head in front. All three share the same clean symmetrical design, geometry, size, center alignment and front-facing slightly top-down view.
Layout: square 1536 by 1536 transparent canvas. Exactly one complete left+right wing pair per row, three rows. Each pair is roughly 1330 pixels wide and 330 pixels tall. Top row within y=70..400, middle y=600..930, bottom y=1130..1460. Large genuinely transparent gaps between rows, safe transparent margin at all four outer edges, no clipping. Center harness near x=768 and near the lower middle of each pair. Wings extend broadly to both sides and slightly upwards, no drooping long plumes.
Row 1, Hu Tao: soft charcoal-black and muted crimson-red fabric and feather panels, a very small amount of warm aged gold filigree and plum-blossom detailing near the root, dignified and playful.
Row 2, Ganyu: luminous soft white and icy pale blue panels, slightly deeper powder blue inner panels, subtle silver frame and elegant cloud/frost motif. The pale blues must be gentle, not electric or neon.
Row 3, Varesa: lavender-purple and warm pastel pink panels, deeper plum inner panel accents and small pale-gold frame details, lively but gentle, no neon.
Style/medium: premium cute anime game inventory illustration with delicate hand-painted gradients, soft satin highlights, clean thin warm outlines and crisp alpha edges. Match a softly shaded detailed chibi character, neither photorealistic nor flat vector.
Constraints: transparent background with true alpha. Wings only; NO person, body, head, face, mannequin, ghost body, separate accessories, sparkles, particles, text, letters, numbers, labels, borders, watermark, ground, backdrop, lighting cast outside wings, shadows outside wings, or checkerboard pattern. All visible marks must belong to one of the three wing rigs. Preserve empty transparent row separators.
```

## 实际输出与打包建议

- 实际输出为 **1254 × 1254 RGBA**，alpha 范围 0–255；与提示词请求的 1536 像素不同，须采用下列实测坐标。
- 三组翼形已经目视检查：横向展开，尖端上翘，中央 V 形镂空，风之翼骨架与羽片有层次。胡桃黑红暗金、甘雨蓝白银、瓦雷莎紫粉浅金。无人物、文字、水印。
- 建议行分割：`y = 0, 415, 815, 1254`。分割线及图像边缘只有 0–2 / 255 的透明度噪声；不要对整体色彩、饱和度或 alpha 做二次处理。
- 以 alpha > 8 计算有效包围盒（右下端点为开区间）：

| 角色 | 建议 crop 范围 | 实际有效 bbox | 估计中心翼根 |
|---|---|---|---|
| 胡桃 | `(0,0,1254,415)` | `(7,72,1248,361)` | `(627,278)` |
| 甘雨 | `(0,415,1254,815)` | `(7,467,1248,766)` | `(627,676)` |
| 瓦雷莎 | `(0,815,1254,1254)` | `(7,865,1248,1166)` | `(627,1074)` |

- 三组使用共同 `x=627` 居中。每组有效宽约 1241，高 289–301；如以 480 像素宽放入 512 画布，高约 112–116 像素。保持原始比例，不拉伸。
- 放到角色身后，将中心翼根对齐角色肩胛约 `(256,300)`，翼片由身体两侧向上横展。后置层可由人物本身遮住中心扣件，无需擦改图片。
- 素材只进行了文件复制和只读 alpha 统计，未通过 Python 或绘图程序重绘、改色、补透明。

