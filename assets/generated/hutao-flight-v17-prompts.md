# Hu Tao golden butterfly flight V1.7

Generated with built-in image_gen, transparent alpha. Final asset: `hutao-flight.png`. Hu Tao's human idle sprite was a color/style reference only. Flight replaces only the four running frames.

## Initial four wing phases

Use case: stylized-concept.
Asset type: four-frame transparent animation sprite sheet for Hu Tao's butterfly transformation dash in a chibi desktop pet.
The supplied Hu Tao image is ONLY a reference for warm golden/crimson fantasy motifs and the soft polished painted style. The human character TRANSFORMS COMPLETELY into a luminous GOLDEN BUTTERFLY. Do NOT draw any human, chibi head, hat, girl, body, hands, feet, portrait, or character silhouette.
Square 2 columns by 2 rows atlas, exactly FOUR sequential wing-flap poses, read top-left, top-right, bottom-left, bottom-right. One main enchanted gold butterfly plus two tiny golden butterfly motes trailing behind to the LEFT in every cell. The main butterfly flies toward screen RIGHT in a consistent slight three-quarter side view. Its little slender luminous body is diagonal almost horizontal, head/antennae at upper RIGHT, tail at lower LEFT. Delicate curved gold wings are stylized like amber flame petals, with pale gold rim light, honey-gold translucent interiors and very subtle crimson-orange roots. The elegant wing contours evoke Hu Tao's golden butterflies. Soft painted glow contained close to the silhouette, crisp readable gold edges at desktop icon size. No photoreal insect texture, no black outline, no mechanical parts.
CRITICAL animation: Keep main body exactly the SAME size, position and orientation in all four cells, center at 58% cell width, 50% cell height. Only wing opening changes distinctly:
1 top-left: wings FULLY OPEN, four broad luminous petal-shaped wings spread, maximum span.
2 top-right: wings move UPWARD to a clearly narrower HALF-OPEN V.
3 bottom-left: wings FOLDED UP above body, a narrow slim luminous upright pair, minimum span, visibly different from frame1.
4 bottom-right: wings unfold DOWNWARD through a half-open DOWNSTROKE, medium span with lower wings lower than body.
Two small butterfly motes and 4-6 pinhead sparkles drift in a short delicate curved trail to LEFT, faint orange-gold wisps only. No long horizontal streak or lightning. Motifs and body consistent across the four frames, so sequence loops smoothly.
Every cell has at least 16% EMPTY transparent padding on ALL four sides. No glowing pixel may reach a cell border. Clear wide central gutters. Entire sprites inside cells. Genuine transparent alpha including semitransparent glow. No floor, cast shadow, background, text, labels, grid or frame. The golden butterfly is the whole character's flying form.

## Final framing refinement

Use case: precise-object-edit.
Preserve these exact FOUR golden butterfly animation poses, ornate golden wings, narrow folded pose bottom-left, warm petal patterns, two little trailing butterflies, compact warm glow and all artwork. Change only atlas FRAMING and SPACE: every butterfly group currently fills its quadrant too much. Recompose into a square 2x2 transparent atlas with MUCH SMALLER sprites and wide empty gutters.
Treat each quadrant as a 512x512 square cell. Entire butterfly group including ALL glow and particles fits in a box x=80..432 and y=80..432 inside its cell. There must be at least 80 completely TRANSPARENT pixels on EVERY side of EVERY cell. Shrink all four groups by the SAME factor, keep their relative wing sizes. No artwork can cross a midpoint seam or touch outer border. The main butterfly's little circular glowing body pivot must be exactly at local (315,315) within EVERY cell. Same body scale, matching body pivot positions. Wings change around that fixed body pivot. Top-left wide spread, top-right halfway closed, bottom-left narrow folded upward, bottom-right reopening downstroke. Do not redesign the butterflies or add anything. Full transparent alpha background. No text, no squares, no cell borders, no human, no props.
