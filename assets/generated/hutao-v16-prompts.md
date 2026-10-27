# Hu Tao V1.6

Generated with built-in image_gen. Final images copied into this folder; V1.5 sources backed up under artifacts/v15-backup.

## hutaoV16Prompt

Use case: style-transfer.
Asset type: transparent full-body desktop pet sprite.
Create Hu Tao from Genshin Impact in EXACTLY the same soft polished rounded chibi rendering style as the two provided Ganyu and Varesa character images. Those inputs define art style and head/body proportions ONLY, not Hu Tao's costume.
One full-body Hu Tao, front-facing calm idle standing pose, giant round adorable face, short compact body, tiny arms relaxed by hips, short visible legs, red eyes with tiny plum-flower highlights, dark chestnut brown long twin hair tails with warm brown tips, black/dark brown traditional long coat with crimson and muted gold details, short black shorts, black knee socks with small red floral details, black shoes. No wings, no frilly maid dress, no white detached sleeves.
Critical hat: Hu Tao's dark brown short flat-topped Chinese hat fits naturally around the top of her head. Soft shaded cloth, subtle rounded cylindrical crown, slender elliptical brim with believable perspective, a pale narrow vertical paper talisman with small decorative gold strokes centered on front, small cluster of red plum blossoms and thin branch at one side. The hat must be part of the painted character, soft lighting and material rendering matching her hair and face. NOT an oversized tall top hat, NOT a flat geometric sticker, NOT a giant pentagonal badge, no thick flat red hat band.
Full character centered with generous 10% transparent margin on all edges including hat and shoes. Same two-head-tall proportions and softly painted linework as references. Clear warm friendly expression. True transparent alpha background, no shadow, floor, words or grid.

## hutaoWalkV16Prompt

Use case: identity-preserve.
Create a production animation sprite atlas of exactly FOUR phases in a 2 column x 2 row square grid of the reference Hu Tao walking to screen RIGHT. Read order top-left, top-right, bottom-left, bottom-right. Preserve this EXACT new character's shaded low brown hat, plum blossoms, long dark brown hair, black/brown/gold coat, shorts, red eyes, socks, shoes and soft rounded chibi proportions. Head and torso same scale, same angle, center and height in all frames. True transparent alpha, each character contained within its own cell with 12 percent empty padding, clear central gutters. No text, grid or floor. Full hat and shoes in every cell.
Critical: this is an actual COMPLETE LEFT-RIGHT WALK CYCLE, not four tiny variations of the same stepping pose. Near-side leg must swing back then forward; far-side leg must take over the forward position in the opposite half. Use a near-PROFILE right-facing view so fore-aft leg motion can be seen clearly. Torso upright, easy arms swing low opposite legs. Mild natural cheek/face visible, same head angle throughout. Show both thighs, knees, socks and shoes unobscured by coat/hair.
FRAME A top left CONTACT: the NEAR leg with its sock's red flower fully visible extends diagonally FORWARD toward screen RIGHT, heel lands; FAR leg stretches diagonally BACK toward screen LEFT, heel rises and toe touches floor. Both feet widely separated.
FRAME B top right PASS: near leg now straight supporting directly BELOW hip, far leg bent slightly and passes UNDER torso, its foot a little raised. Feet close together and visually overlapping, NOT widely separated. Body slightly taller.
FRAME C bottom left OPPOSITE CONTACT: NEAR leg is now diagonally BACK toward screen LEFT, heel lifted; FAR leg extends FORWARD toward screen RIGHT landing on heel. The nearer thigh and sock visibly occlude the rearward side; near sock flower is at LEFT rather than RIGHT of hips. Arms also swap fore-aft. Wide stance with opposite leg leading.
FRAME D bottom right OPPOSITE PASS: FAR leg straight supporting under hip; NEAR leg bent slightly lifting and passing under torso to prepare next forward step. Feet nearly together. No two frames are identical. No raised running kick, no jumping, keep one supporting foot on a common floor baseline. A-C must visibly swap which knee is in front. B-D are narrow passing silhouettes, dramatically different from wide A-C.

## hutaoMotionV16Prompt

Use case: identity-preserve.
Create a wide 4 columns x 2 rows transparent animation sprite sheet of exactly 8 full-body poses of the single supplied new Hu Tao character. Keep exact identity, brown softly shaded low traditional hat with red plum blossoms, brown long hair, red flower eyes, black/brown long coat with gold detail, shorts, socks and shoes. Soft polished chibi shading and proportions identical to reference, giant adorable head and short little limbs. Same scale each cell, each full character comfortably fits equal cell with generous clear transparent gutters and 12% empty margin.
TOP ROW: four sequential energetic rightward RUN keyframes. Side/three-quarter facing RIGHT, compact forward body lean. 1 near leg leads forward far leg back; 2 near leg under hips in support far knee moving forward; 3 far leg forward near leg kicks back; 4 far leg under hips near knee swinging forward. Clear alternating bent knees and opposite arm swing. Both legs change positions, not four identical poses. Hat stable naturally on head. Feet at common baseline.
BOTTOM ROW: four sequential CLIMB UP keyframes on imaginary vertical wall just to the RIGHT of every sprite. Facing right near profile, both hands reach right; 1 near hand above far hand, near knee lifted; 2 near hand pulls body up, far hand reaches; 3 far hand above near hand, far knee lifted; 4 far hand pulls, near hand reaches. Hands end at common rightmost wall x. Limbs bend in alternating hand-and-foot climbing motion. Hair hangs behind body to the LEFT. No wall drawn.
Exactly two arms/two legs per pose. Full hat, branches, hair, hands, feet, no crops. No background, no shadow, no text, no separators, no extra character or props, no weapons. Empty alpha background.

## hutaoWalkFix

Use case: precise-object-edit.
Edit the provided 2x2 Hu Tao walking sprite sheet. Preserve all four heads, hats, faces, hair, coats, size, rendering, alpha and framing. Correct the limb animation ONLY. Frame A top-left and frame B top-right stay as references.
Critical correction to bottom-left C: SWAP THE LEGS. The viewer-facing NEAR LEG (the one clearly drawn IN FRONT at the hip crossing) must extend diagonally BACKWARDS to the LEFT and be the dark stocking/shoe on the LEFT side. The FAR LEG extends forward to screen RIGHT and passes BEHIND the near thigh at the hip. Draw a clear overlapping diagonal at the shorts/thigh junction so the LEFT-going thigh is ON TOP of the RIGHT-going thigh. Shade the FAR forward stocking slightly darker because it is farther from camera. Show the far shoe's INNER side, without the bright red outer sock flower. The backwards NEAR sock has its red flower fully visible at screen LEFT. DO NOT repeat the top-left A near leg forward pose. Top-left A shows bright flower on near leg at RIGHT; bottom-left C must clearly show bright flower on near leg at LEFT.
Correct bottom-right D to the opposite passing pose: FAR leg straight supports directly under hips; near thigh bends forward, near knee leads right, near ankle raised off the floor just in front of far supporting shin, visible red flower on near sock. In B the FAR leg passes; in D the NEAR leg passes. Arms swing opposite legs.
Still natural relaxed WALK with small knee lift, no run/jump. Transparent background, unchanged positions within 2x2, no new markings/labels.

## hutaoMotionFix

Use case: precise-object-edit.
Preserve the supplied 4x2 Hu Tao running/climbing sprite sheet and exact character identity/style, including her soft shaded brown hat, blossoms, face and costume. Fix framing: all eight sprites must fit entirely inside their own equal-sized cell with at least 8% CLEAR EMPTY TRANSPARENT margin on EACH side. Some top hat branches and plume currently touch top border; reconstruct complete tips and shrink all characters equally to create safe gutters. No detached fragments, no overlaps across cells, no cut edges. Keep true transparency. No labels or extra props. Four running poses top row; four climbing poses bottom row.
Also ensure alternate left/right limbs: running first and third poses should have opposite leg leading; climbing first and third should have opposite hand high and opposite knee lifted. Keep torso/head stable in each row, right-facing. Clear 4 columns 2 rows, exactly8characters, generous empty center rowgutter.

