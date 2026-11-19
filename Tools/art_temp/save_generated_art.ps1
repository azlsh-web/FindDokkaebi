param([Parameter(Mandatory=$true)][string]$Jobs, [switch]$ReplaceGenerated)
$ErrorActionPreference = 'Stop'
# Only writes new PNG assets inside this repository. Generated originals stay untouched.
Add-Type -AssemblyName System.Drawing
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
foreach ($entry in ($Jobs -split "`n")) {
    if (-not $entry.Trim()) { continue }
    $parts = $entry.Trim() -split '\|'
    $sourcePath, $relativePath, $kind = $parts
    $destination = [IO.Path]::GetFullPath((Join-Path $repo $relativePath))
    if (-not $destination.StartsWith($repo + [IO.Path]::DirectorySeparatorChar)) { throw 'Destination escapes repository' }
    if ([IO.File]::Exists($destination) -and -not $ReplaceGenerated) { throw "Refusing to replace existing file: $relativePath" }
    $source = [Drawing.Bitmap]::FromFile($sourcePath)
    $width, $height = 1024, 1024
    if ($kind -eq 'photo') { $width, $height = 1760, 1286 }
    $target = New-Object Drawing.Bitmap($width, $height, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [Drawing.Graphics]::FromImage($target)
    $graphics.CompositingMode = [Drawing.Drawing2D.CompositingMode]::SourceCopy
    $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.Clear([Drawing.Color]::Transparent)
    if ($kind -eq 'photo') {
        $graphics.Clear([Drawing.Color]::White)
        $graphics.CompositingMode = [Drawing.Drawing2D.CompositingMode]::SourceOver
        $scale = [Math]::Max($width / $source.Width, $height / $source.Height)
    } else {
        $left, $top, $right, $bottom = $source.Width, $source.Height, 0, 0
        for ($y=0; $y -lt $source.Height; $y+=6) {
            for ($x=0; $x -lt $source.Width; $x+=6) {
                if ($source.GetPixel($x,$y).A -gt 16) {
                    $left=[Math]::Min($left,$x); $right=[Math]::Max($right,$x)
                    $top=[Math]::Min($top,$y); $bottom=[Math]::Max($bottom,$y)
                }
            }
        }
        if ($right -le $left -or $bottom -le $top) { throw 'No visible object found' }
        $scale = 820 / [Math]::Max(($right-$left+12),($bottom-$top+12))
    }
    $drawWidth = $source.Width * $scale
    $drawHeight = $source.Height * $scale
    $rect = New-Object Drawing.RectangleF((($width-$drawWidth)/2), (($height-$drawHeight)/2), $drawWidth, $drawHeight)
    if ($kind -ne 'photo') {
        $rect.X = ($width/2) - (($left+$right)/2)*$scale
        $rect.Y = ($height/2) - (($top+$bottom)/2)*$scale
    }
    $graphics.DrawImage($source, $rect)
    $graphics.Dispose()
    $source.Dispose()
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
    $target.Save($destination, [Drawing.Imaging.ImageFormat]::Png)
    $target.Dispose()
    $pngHeader = [IO.File]::ReadAllBytes($destination)
    if ($pngHeader[25] -ne 6) { throw "PNG is not RGBA: $relativePath" }
    $check = [Drawing.Bitmap]::FromFile($destination)
    if ($check.Width -ne $width -or $check.Height -ne $height) { throw "Incorrect size: $relativePath" }
    $bounds = New-Object Drawing.Rectangle(0, 0, $width, $height)
    $locked = $check.LockBits($bounds, [Drawing.Imaging.ImageLockMode]::ReadOnly, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $bytes = New-Object byte[] ([Math]::Abs($locked.Stride) * $height)
    [Runtime.InteropServices.Marshal]::Copy($locked.Scan0, $bytes, 0, $bytes.Length)
    $check.UnlockBits($locked)
    $alphaMin, $alphaMax, $zeroCount = 255, 0, 0
    for ($i=3; $i -lt $bytes.Length; $i+=4) {
        $alpha = $bytes[$i]
        if ($alpha -lt $alphaMin) { $alphaMin = $alpha }
        if ($alpha -gt $alphaMax) { $alphaMax = $alpha }
        if ($alpha -eq 0) { $zeroCount++ }
    }
    if ($kind -eq 'photo') {
        if ($alphaMin -ne 255) { throw "Photo is not opaque: $relativePath" }
    } else {
        if ($alphaMin -ne 0 -or $alphaMax -ne 255 -or $zeroCount -lt ($width*$height*0.1)) { throw "Missing transparent background: $relativePath" }
        foreach ($point in @(@(0,0),@(($width-1),0),@(0,($height-1)),@(($width-1),($height-1)))) {
            if ($check.GetPixel($point[0], $point[1]).A -ne 0) { throw "Nontransparent corner: $relativePath" }
        }
    }
    $check.Dispose()
    Write-Output "$relativePath | ${width}x${height} | RGBA | alpha $alphaMin..$alphaMax | transparent pixels $zeroCount"
}

<#
Built-in image_gen prompt set (illustration-story):
ITEM COMMON PROMPT:
Use case: illustration-story. Create ONE temporary Unity mobile game item sprite for 찾아라 도깨비. 1024x1024 RGBA PNG, TRUE TRANSPARENT background. Center the whole object, front view, about 80% of canvas with empty margins, nothing cropped. Simple clean cute children's picture-book, thick smooth DARK BROWN outline (#50301c), soft flat fills, minimum simple shapes drawable by a child in one minute. No text, no logos, no watermark, no ground plane, no cast shadow, no background, no gradients, no fine detail. 

Chapter2/Ch2_Socks_Original.png
ONE classic L-shaped sock, toe extending right, with broad horizontal warm white and sky blue stripes.

Chapter2/Ch2_Socks_Wrong1.png
ONE classic L-shaped sock, toe extending right, warm white with many large sky blue POLKA DOTS, absolutely no stripes.

Chapter2/Ch2_Socks_Wrong2.png
ONE cozy MITTEN with a clearly protruding thumb on the right and NO sock foot, broad HORIZONTAL warm white and sky blue stripes across the mitten, similar to the striped sock but unmistakably a mitten.

Chapter2/Ch2_Socks_Wrong3.png
ONE funny yellow rubber duck bath toy, plump rounded body, orange beak, small round eye, simple cartoon duck side view. No sock.

Chapter2/Ch2_Umbrella_Original.png
ONE open umbrella SIDE VIEW with a rounded dome canopy, simple scalloped lower edge, straight vertical shaft and clearly visible J-shaped hook handle below. Sky blue canopy covered in large cream POLKA DOTS.

Chapter2/Ch2_Umbrella_Wrong1.png
ONE open umbrella SIDE VIEW with rounded dome canopy, simple scalloped edge, straight shaft and J hook handle. Sky blue canopy with broad cream HORIZONTAL STRIPES, absolutely no polka dots.

Chapter2/Ch2_Umbrella_Wrong2.png
ONE broad-brimmed sky blue SUN HAT, rounded low crown and wide flat brim, covered in large cream POLKA DOTS. Clearly a sun hat with NO umbrella shaft or handle.

Chapter2/Ch2_Umbrella_Wrong3.png
ONE funny simple smiling green FROG, round body, two big eyes on top, wide tiny smile, squat rounded feet, no clothes and no accessories. Child can draw it in one minute.

Chapter2/Ch2_Pillow_Original.png
ONE wide RECTANGULAR pillow with slightly puffy corners and gently curved sides, warm cream and soft peach CHECKERED GRID pattern of large obvious squares.

Chapter2/Ch2_Pillow_Wrong1.png
ONE wide RECTANGULAR pillow with slightly puffy corners, warm cream body with large soft peach POLKA DOTS. No checks, no grid.

Chapter2/Ch2_Pillow_Wrong2.png
ONE ROUND cushion, circular outer shape with NO rectangular corners, warm cream and soft peach CHECKERED GRID pattern of large obvious squares.

Chapter2/Ch2_Pillow_Wrong3.png
ONE funny giant orange CARROT with three simple green leaves, triangular tapered carrot body with just three short brown marks. No pillow.

Chapter3/Ch3_PartyHat_Original.png
ONE simple CONE PARTY HAT with a small red round pompom on top. Three broad DIAGONAL bands on the cone: RED at the TOP, YELLOW in the MIDDLE, BLUE at the BOTTOM. Cone is triangular with gently curved oval base. Use only RED YELLOW BLUE fills plus dark brown outline, no white or other colors.

Chapter3/Ch3_PartyHat_Wrong1.png
ONE simple CONE PARTY HAT with small red round pompom, YELLOW cone covered with big RED and BLUE POLKA DOTS. Absolutely NO bands or stripes. Only red yellow blue fills plus dark brown outline.

Chapter3/Ch3_PartyHat_Wrong2.png
ONE tall cylindrical TOP HAT with flat crown and wide brim, absolutely NO pointed cone, covered in three broad DIAGONAL bands RED at TOP, YELLOW in MIDDLE, BLUE at BOTTOM. Only red yellow blue fills plus dark brown outline.

Chapter3/Ch3_PartyHat_Wrong3.png
ONE funny red and yellow ROCKET TOY pointing upward, very simple rounded red rocket, round blue window, yellow fins and small yellow flame. Only red yellow blue fills plus dark brown outline, no white.

Chapter3/Ch3_Balloons_Original.png
Exactly THREE ROUND circular balloons: RED on upper left, YELLOW on upper right, BLUE in lower middle overlapping them slightly. Each balloon is SOLID color, no stripes or dots. Three visible thin dark brown strings descend and tie TOGETHER in ONE clear knot below, with short loose ends. Only red yellow blue fills and dark brown outline, no white highlights.

Chapter3/Ch3_Balloons_Wrong1.png
Exactly THREE ROUND balloons: RED balloon with BLUE STRIPES on upper left, YELLOW balloon with RED STRIPES on upper right, BLUE balloon with YELLOW STRIPES in lower middle. Three visible strings tied together into ONE knot below. This is the wrong pattern version: very obvious stripes instead of solid balloons. Only red yellow blue fills and dark brown outline.

Chapter3/Ch3_Balloons_Wrong2.png
Exactly THREE HEART-SHAPED balloons, obvious heart lobes and pointed bottoms: one SOLID RED upper left, one SOLID YELLOW upper right, one SOLID BLUE lower middle. Three strings tied together in one knot below. No pattern, no stripes, no dots, no round balloons. Only red yellow blue fills and dark brown outline.

Chapter3/Ch3_Balloons_Wrong3.png
ONE funny rubber CHICKEN squeaky toy, yellow plump chicken with red comb on top and red beak, blue little wings, silly big eyes made with blue dots. Only red yellow blue fills and dark brown outline. Very simple shapes.

Chapter3/Ch3_GiftBox_Original.png
ONE simple square BLUE gift box seen STRAIGHT ON, RED ribbon CROSS very visible across the box front, red BOW on top, several clearly separated large YELLOW FIVE-POINT STARS on the blue box around the ribbon. Only red yellow blue fills and dark brown outline, no white.

Chapter3/Ch3_GiftBox_Wrong1.png
ONE simple square BLUE gift box seen straight on, RED ribbon CROSS very visible across front, red BOW on top. Large YELLOW POLKA DOTS on blue box, absolutely NO stars. Only red yellow blue fills plus dark brown outline.

Chapter3/Ch3_GiftBox_Wrong2.png
ONE ROUND CYLINDRICAL BLUE gift tin seen straight on with obvious rounded sides and elliptical lid, red ribbon CROSS on front and RED BOW on top, large YELLOW FIVE-POINT STARS on blue tin around ribbon. Clearly a round tin rather than a square box. Only red yellow blue fills and dark brown outline.

Chapter3/Ch3_GiftBox_Wrong3.png
ONE funny simple toy ROBOT, square yellow head with two blue circular eyes, red rectangular body, chunky blue arms and yellow short legs, little red antenna. Only red yellow blue fills plus dark brown outline.

Chapter4/Ch4_Tangerine_Original.png
ONE round TANGERINE, orange round body, exactly ONE green LEAF and tiny green stem on top, just a few small darker orange DIMPLE DOTS. Very simple round fruit silhouette.

Chapter4/Ch4_Tangerine_Wrong1.png
ONE round TANGERINE with clearly WRONG COLORS: BLUE round body, exactly ONE PINK leaf and tiny pink stem on top, small darker blue dimple dots. Preserve simple round tangerine shape.

Chapter4/Ch4_Grapes_Original.png
ONE bunch of GRAPES: a triangle-ish cluster of simple ROUND PURPLE grapes, wide at top tapering down to one grape at bottom, thick GREEN bent stem on top. About 9 grapes, simple overlapping circles, no leaf.

Chapter4/Ch4_Grapes_Wrong1.png
ONE bunch of GRAPES with clearly WRONG COLORS: triangle-ish cluster of ROUND ORANGE grapes, wide at top tapering to one at bottom, BLUE bent stem on top. About 9 grapes, simple overlapping circles, no leaf.

Chapter4/Ch4_Candy_Original.png
ONE ROUND wrapped candy, large CIRCULAR middle and simple twist wrapper on BOTH LEFT AND RIGHT sides. Middle covered with broad vertical stripes alternating ORANGE and GREEN; wrappers orange. No other fill colors, very simple shapes.

Chapter4/Ch4_Candy_Wrong1.png
ONE ROUND wrapped candy with WRONG COLORS, large CIRCULAR middle and simple twist wrapper on BOTH LEFT AND RIGHT sides. Broad vertical middle stripes alternating PURPLE and YELLOW; wrappers purple. Same round wrapped candy shape, no orange or green.

SCENE COMMON PROMPT:
Use case: illustration-story. Create ONE opaque landscape illustrated chapter scene card for a Korean children's Unity game 찾아라 도깨비. Desired composition aspect 1760x1286. Full bleed, no border or text. Match cute soft children's picture-book style of Chapter_Picnic: thick smooth dark brown contour lines, flat gentle colors, rounded simple shapes, clean cheerful readable composition, no photorealism, no complex texture. SAME BEDROOM LAYOUT in every chapter: warm cream walls, honey-colored wooden floor, wooden desk at far left with two closed books and a small green chair, large wooden four-pane window centered on back wall, small low wooden bed at right with blue blanket, large pale oval rug in foreground. Keep main story objects large and clearly readable in lower middle. A cute little Korean boy with simple short dark brown hair and large black eyes wears cream long sleeves and blue overalls, seated at left side of rug. NO lettering, no numbers, no banners with writing, no logos, no watermark. 

Chapter2/Chapter_Rain.png
Rainy DAY room cleanup mood. Window shows a gray-blue rainy sky and large simple visible raindrops running down glass. A very TINY FAINT BLUISH WILL-O'-THE-WISP visible OUTSIDE window, a little soft blue flame in lower right pane, subtle yet recognizable. On rug scattered separately: ONE L-shaped sock with white and sky blue HORIZONTAL stripes, ONE small OPEN sky blue umbrella with cream POLKA DOTS and a J handle, and ONE cream-peach CHECKERED rectangular puffy pillow. Boy is tidying, reaching toward the sock. No basket, no balloons. Cozy reassuring rainy afternoon, not frightening.

Chapter3/Chapter_Birthday.png
EVENING, secretly preparing little sibling's birthday party. Peach-purple twilight visible outside window, warm room lighting. A simple short garland of blank red yellow blue triangle pennants across upper wall, no lettering. On rug: a simple CONE PARTY HAT with diagonal bands RED TOP, YELLOW MIDDLE, BLUE BOTTOM and a red pompom; exactly THREE ROUND BALLOONS one red one yellow one blue with strings tied into ONE knot; a square BLUE GIFT BOX with RED ribbon cross and RED bow, YELLOW STAR pattern. Boy quietly prepares the decorations with one finger to his smiling lips, sweet secret surprise mood. Clear simple prop shapes, primary red yellow blue party props. No cake, no candles, no extra people.

Chapter4/Chapter_Snack.png
NIGHT, dark blue sky with crescent moon and a few stars outside window. Warm bedside lamp. In the CENTER of rug is one LARGE EMPTY woven snack basket with curved handle. The basket is unmistakably EMPTY: show unobstructed oval interior opening and empty bottom, nothing inside or obscuring it. THREE snacks sit SEPARATELY OUTSIDE and NEAR the basket: one round ORANGE tangerine with ONE GREEN leaf, one triangular bunch of PURPLE round grapes with GREEN stem, one ROUND wrapped candy with twist ends and alternating ORANGE GREEN stripes. Boy sits nearby looking expectantly at the empty basket. Cozy magical night, no extra food. Keep snack shapes very simple and recognizable.

Targeted precise-object edits: balloon Original cream knot fill -> blue;
balloon Wrong1 orange knot fill -> blue; balloon Wrong3 white eyes -> yellow.
All other generated features and transparency were preserved.
Final processing: RGBA PNG, item alpha bounding box centered at about 80%
of 1024x1024 canvas; opaque scenes centered/cropped to 1760x1286.
#>
