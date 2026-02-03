# Build From Rig Scale Sorunu - Çözüm

## Sorun
**Build From Rig Prefab** yaparken kolların (LArm, RArm) scale'i **olduğundan çok daha büyük** oluşuyordu.

## Kök Neden

### Transform Scale Göz Ardı Ediliyordu

**Önceki Kod:**
```csharp
Sprite sprite = sr.sprite;
Rect rect = sprite.textureRect;
p.sizePixels = new Vector2(rect.width, rect.height);  // Sadece texture size!
p.pivotPixels = sprite.pivot;  // Sadece sprite pivot!
```

**Sorun:**
- Sprite'ın **texture size**'ı alınıyordu (örn. 64x64 pixels)
- Ama transform'un **scale**'i dikkate alınmıyordu
- Eğer transform scale = (2.0, 2.0) ise:
  - Gerçek render size: 128x128 pixels
  - Mesh builder size: 64x64 pixels ❌
  - **Sonuç:** Part çok küçük render ediliyordu!

### Örnek Senaryo

**Rig Hierarchy:**
```
Body (scale: 1.0, 1.0)
└── LArm (scale: 2.0, 2.0)  ← Scale 2x!
```

**LArm Sprite:**
- Texture size: 32x64 pixels
- Pivot: (16, 32) pixels

**Önceki Hesaplama:**
```csharp
sizePixels = (32, 64)  // Texture size
pivotPixels = (16, 32)  // Sprite pivot
// Mesh'te: 32x64 pixel boyutunda render edilir
```

**Gerçek Render (Unity'de):**
```
Unity render size = texture size × scale
                  = (32, 64) × (2.0, 2.0)
                  = (64, 128) pixels
```

**Sonuç:**
- Mesh builder: 32x64 pixels ❌
- Unity render: 64x128 pixels ✓
- **Fark:** 2x daha küçük!

## Çözüm

### Transform Scale'i Dahil Et

```csharp
Sprite sprite = sr.sprite;
Rect rect = sprite.textureRect;
Vector3 localScale = partTransform.localScale;

// CRITICAL FIX: Multiply by transform scale!
p.sizePixels = new Vector2(
    rect.width * Mathf.Abs(localScale.x), 
    rect.height * Mathf.Abs(localScale.y)
);

// CRITICAL FIX: Scale pivot as well!
p.pivotPixels = new Vector2(
    sprite.pivot.x * Mathf.Abs(localScale.x),
    sprite.pivot.y * Mathf.Abs(localScale.y)
);
```

### Neden Pivot de Scale Edilmeli?

Pivot, sprite'ın **local space**'inde bir nokta. Eğer sprite scale ediliyorsa, pivot noktası da scale edilmelidir.

**Örnek:**
- Sprite size: 64x64, pivot: (32, 32) [center]
- Scale: (2.0, 2.0)
- Scaled size: 128x128
- **Scaled pivot:** (64, 64) [hala center] ✓

Eğer pivot scale edilmezse:
- Scaled size: 128x128
- Pivot: (32, 32) [artık center değil, sol-üst çeyrek!] ❌

## Karşılaştırma

### LArm (Scale 2.0x)

| Özellik | Texture | Önceki (Yanlış) | Yeni (Doğru) |
|---------|---------|-----------------|--------------|
| Sprite Size | 32x64 | 32x64 | **64x128** ✓ |
| Sprite Pivot | 16, 32 | 16, 32 | **32, 64** ✓ |
| Transform Scale | 2.0, 2.0 | Göz ardı | **Dahil** ✓ |
| Render Size | - | 32x64 ❌ | 64x128 ✓ |

### Body (Scale 1.0x)

| Özellik | Texture | Önceki | Yeni |
|---------|---------|--------|------|
| Sprite Size | 64x128 | 64x128 | 64x128 ✓ |
| Sprite Pivot | 32, 64 | 32, 64 | 32, 64 ✓ |
| Transform Scale | 1.0, 1.0 | Göz ardı | Dahil ✓ |
| Render Size | - | 64x128 ✓ | 64x128 ✓ |

## Debug Logging

Build From Rig yapıldığında console'da:

```
MAT2D Part[3] 'LArm':
  Transform Scale: (2.000, 2.000)  ← Scale bilgisi
  Sprite Size (texture): (32.0, 64.0)  ← Texture size
  Size (pixels): (64.0, 128.0)  ← Scaled size (texture × scale) ✓
  Pivot (pixels): (32.0, 64.0)  ← Scaled pivot ✓
```

**Kontrol:**
- Size (pixels) = Sprite Size × Transform Scale ✓
- Pivot (pixels) = Sprite Pivot × Transform Scale ✓

## Neden Bu Sorun Oluştu?

### Unity'de SpriteRenderer

Unity'de bir SpriteRenderer:
```csharp
// Render size hesaplama
float renderWidth = sprite.textureRect.width / sprite.pixelsPerUnit * transform.localScale.x;
float renderHeight = sprite.textureRect.height / sprite.pixelsPerUnit * transform.localScale.y;
```

Unity otomatik olarak scale'i uygular. Ama biz mesh builder'da manuel hesaplama yapıyoruz, bu yüzden scale'i kendimiz dahil etmeliyiz.

### Mesh Builder vs Unity Render

**Unity SpriteRenderer:**
- Otomatik scale uygular ✓
- Transform.localScale dikkate alınır ✓

**Mesh Builder (Önceki):**
- Manuel mesh oluşturur
- Transform.localScale göz ardı ediliyordu ❌

**Mesh Builder (Yeni):**
- Manuel mesh oluşturur
- Transform.localScale dahil edilir ✓

## Baking İle İlişki

Bu düzeltme **sadece mesh builder**'ı etkiler. Baking'de zaten `localScale` kullanıyorduk:

```csharp
// Baking (zaten doğru)
Vector3 localScale = p.localScale;
float sx = Mathf.Abs(localScale.x);
float sy = Mathf.Abs(localScale.y);
```

**Sorun:**
- Mesh builder: Scale dahil değildi → Küçük mesh
- Baking: Scale dahildi → Doğru scale
- **Sonuç:** Mesh ve baking uyumsuzdu!

**Çözüm Sonrası:**
- Mesh builder: Scale dahil → Doğru mesh ✓
- Baking: Scale dahil → Doğru scale ✓
- **Sonuç:** Mesh ve baking uyumlu! ✓

## Pozisyon Sorunu

Bu scale düzeltmesi **pozisyon sorununu da çözebilir**!

**Neden?**
- Pivot yanlış scale edildiğinde, vertex pozisyonları da yanlış hesaplanır
- `bl = (-pivot) + offset` formülünde pivot yanlışsa, bl de yanlış!

**Örnek:**
```
Önceki (yanlış pivot):
  pivot = (16, 32)
  offset = (100, 200)
  bl = (-16, -32) + (100, 200) = (84, 168)

Yeni (doğru pivot):
  pivot = (32, 64)  // Scaled!
  offset = (100, 200)
  bl = (-32, -64) + (100, 200) = (68, 136)

Fark: (-16, -32) → Part 16 pixel sağda, 32 pixel yukarıda!
```

## Test

1. **Rig Hazırla:**
   - LArm transform scale = (2.0, 2.0) yap
   - Sprite texture size = 32x64

2. **Build From Rig:**
   - Mesh builder'da "Build From Rig Prefab"
   - Console'da log'u kontrol et:
     ```
     Transform Scale: (2.000, 2.000)
     Sprite Size (texture): (32.0, 64.0)
     Size (pixels): (64.0, 128.0)  ← 32 × 2 = 64 ✓
     ```

3. **Görsel Kontrol:**
   - Mesh'te LArm'ın boyutu Unity'deki SpriteRenderer ile aynı olmalı ✓

## Özet

| Özellik | Önceki | Yeni |
|---------|--------|------|
| **Size Calculation** | `texture.size` | `texture.size × scale` ✓ |
| **Pivot Calculation** | `sprite.pivot` | `sprite.pivot × scale` ✓ |
| **Transform Scale** | ❌ Göz ardı | ✅ Dahil |
| **Mesh Size** | ❌ Yanlış | ✅ Doğru |
| **Pivot Position** | ❌ Yanlış | ✅ Doğru |
| **Baking Uyumu** | ❌ Uyumsuz | ✅ Uyumlu |

Artık scaled transform'lar doğru şekilde mesh'e dönüştürülüyor! 🎉
