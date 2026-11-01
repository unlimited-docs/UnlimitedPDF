# JPEG test fixtures

Project-authored test images, released under Apache-2.0 with the rest of the repository (PR-008, PR-011).

## Origin

Every file below was generated on 2026-11-01 by [`Generate-JpegFixtures.ps1`](Generate-JpegFixtures.ps1), which uses the Windows imaging encoder (WPF `JpegBitmapEncoder`, quality 90). Run it again to recreate them:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Generate-JpegFixtures.ps1
```

Each image shows a 96 x 64 test pattern as displayed, with top-left red, top-right green, bottom-left blue, and bottom-right yellow, so rotation, mirroring, or wrong colors are easy to spot. Grayscale uses four gray levels in the same layout (black, dark gray, light gray, white). CMYK uses cyan, magenta, yellow, and black.

| File | Frame | Components | Markers | Used for |
|---|---|---|---|---|
| `rgb-baseline.jpg` | baseline | 3 | JFIF 96 dpi | RGB loading, demo |
| `rgb-300dpi.jpg` | baseline | 3 | JFIF 300 dpi | reported resolution |
| `gray-baseline.jpg` | baseline | 1 | JFIF 96 dpi | grayscale loading |
| `cmyk-baseline.jpg` | baseline | 4 | JFIF, Adobe (transform 2, YCCK) | CMYK with Adobe marker |
| `exif-orientation-1.jpg` to `exif-orientation-8.jpg` | baseline | 3 | JFIF, EXIF (big-endian) orientation 1 to 8 | orientation; each appears upright only when its orientation is applied |

## Not included

The Windows imaging encoder writes only baseline JPEG and always marks CMYK with an Adobe segment, so there are no files for:

- progressive JPEG (grayscale, RGB, CMYK)
- CMYK without an Adobe segment (non-inverted values)

Loading these variants is tested with synthetic marker-structure JPEGs (`Support/JpegBuilder.cs`), and they are not part of the visual viewer check. This gap was accepted on 2026-11-01. If a project tool can produce them later, add them here with their origin recorded in this file.
