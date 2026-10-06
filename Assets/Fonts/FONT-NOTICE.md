# Cooking HUD font

`NotoSansSC-CookingSubset.ttf` is a reduced character subset of Noto Sans SC,
used by the TextMeshPro cooking HUD. Noto fonts are distributed under the
SIL Open Font License 1.1: https://openfontlicense.org

The subset contains ASCII, the check mark, common CJK punctuation, and the
3,500 characters in the first level of the 2013 Table of General Standard
Chinese Characters. The character list was downloaded from:

https://gist.github.com/Elypha/641901465d639292e18670a5b159c3d8/raw/408fde7ba623b691b6113b7b4d29643d2679eccd/1.txt

The full Noto Sans SC source is available from Google Fonts:

https://github.com/google/fonts/tree/main/ofl/notosanssc

The TextMeshPro atlas uses dynamic population, so new UI copy using these
characters does not require regenerating the TTF. Only characters outside
this basic set require expanding the subset or a fallback font.

The production UI also includes U+2192 (right arrow) and U+7827 (砧).
The subset was expanded with these two characters on 2026-10-06 using the
same Google Fonts source, instantiated at weight 400.
