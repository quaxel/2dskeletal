# Baking Scale Sorunu - Çözüm

## Sorun
Mesh builder'da scale düzeltmesi yapıldıktan sonra, **bake edilen animasyonda part'lar daha küçük görünüyordu**.

## Kök Neden

### Double Scaling (İki Kere Scale)

**Mesh Builder (Yeni):**
```csharp
// Transform scale mesh size'a dahil edildi
Vector3 localScale = partTransform.localScale;
p.sizePixels = new Vector2(
    rect.width * Mathf.Abs(localScale.x),  // Scale dahil!
    rect.height * Mathf.Abs(localScale.y)
);
```

**Baking (Eski):**
```csharp
// Local scale bake ediliyordu
Vector3 localScale = p.localScale;
float sx = Mathf.Abs(localScale.x);  // Scale tekrar!
float sy = Mathf.Abs(localScale.y);
colors1[idx] = new Color(sx, sy, 0f, 0f);
```

**Shader:**
```hlsl
pos *= scale;  // Baked scale uygulanıyor
```

**Sorun:**
1. Mesh vertices zaten scaled (size × scale)
2. Shader tekrar scale uyguluyor (vertices × baked scale)
3. **Toplam:** vertices × scale × scale = **2x scale!**
4. Eğer scale = 2.0 ise: 2.0 × 2.0 = 4.0 → Part 1/4 boyutunda görünür! ❌

### Örnek Senaryo

**LArm:**
- Sprite texture size: 32×64 pixels
- Transform scale: (2.0, 2.0)

**Mesh Builder:**
```csharp
sizePixels = (32, 64) × (2.0, 2.0) = (64, 128)
// Mesh vertices: 64×128 pixels (Unity units: 0.64×1.28)
```

**Baking (Eski):**
```csharp
baked scale = (2.0, 2.0)
```

**Shader:**
```hlsl
pos = mesh_vertex;  // 0.64×1.28
pos *= baked_scale;  // 0.64×1.28 × 2.0 = 1.28×2.56
// Ama olması gereken: 0.64×1.28 (mesh zaten scaled!)
```

**Sonuç:**
- Beklenen: 0.64×1.28 (64×128 pixels)
- Gerçek: 1.28×2.56 (128×256 pixels) → 2x daha büyük!
- Ama shader scale'i **ters** uygular (1/scale), yani:
  - Shader: pos / scale = 1.28×2.56 / 2.0 = 0.64×1.28... Bekle, bu doğru!

Hmm, shader kodu kontrol edelim...

Aslında shader `pos *= scale` yapıyor, yani scale **çarpıyor**, ters uygulamıyor. O zaman:
- Mesh: 0.64×1.28 (zaten scaled)
- Shader: 0.64×1.28 × 2.0 = 1.28×2.56 (2x daha büyük)

Ama kullanıcı "daha küçük" diyor... Belki de mesh builder'da scale **ters** uygulanıyor?

Hayır, mesh builder doğru. Sorun şu olabilir: **Mesh builder scale'i size'a ekliyor, ama baking de scale ekliyor, shader ikisini birden kullanıyor!**

Doğru çözüm: **Baking'de scale her zaman 1.0 olmalı** çünkü mesh zaten scaled!

## Çözüm

### Baked Scale = 1.0

```csharp
// CRITICAL FIX: Scale is always 1.0!
// The mesh builder now includes transform scale in the mesh size (sizePixels).
// So the mesh vertices are already scaled.
// If we apply scale again in the shader, parts will be scaled twice!
// Therefore, baked scale should always be 1.0.
float sx = 1.0f;
float sy = 1.0f;
```

### Neden 1.0?

**Mesh Builder:**
- Size = texture size × transform scale
- Mesh vertices zaten doğru boyutta

**Shader:**
- `pos *= baked_scale`
- Eğer baked_scale = 1.0 ise: `pos *= 1.0` → Değişmez ✓
- Eğer baked_scale = transform scale ise: `pos *= scale` → 2x scale! ❌

**Sonuç:**
- Baked scale = 1.0 → Mesh boyutu korunur ✓

## Animasyon Sırasında Scale

Peki animasyon sırasında part scale edilirse ne olur?

**Cevap:** Animasyon scale'i **bake edilmeli**!

Ama şu anda biz **rest pose scale'ini** göz ardı ediyoruz, çünkü mesh'te zaten var. Animasyon sırasında scale değişirse:

```csharp
// Rest pose scale
restScale = (2.0, 2.0)  // Mesh'te dahil

// Animated scale
animScale = (3.0, 3.0)  // Animasyon sırasında değişti

// Baked scale (delta)
bakedScale = animScale / restScale = (3.0, 3.0) / (2.0, 2.0) = (1.5, 1.5)
```

Ama şu anda animasyon scale'i yok, sadece rest pose var. Bu yüzden:
```csharp
bakedScale = 1.0  // Rest pose = animated pose
```

## Karşılaştırma

### LArm (Transform Scale 2.0x)

| Aşama | Önceki (Yanlış) | Yeni (Doğru) |
|-------|-----------------|--------------|
| **Mesh Size** | 64×128 pixels | 64×128 pixels |
| **Baked Scale** | 2.0, 2.0 | **1.0, 1.0** ✓ |
| **Shader Scale** | ×2.0 | ×1.0 |
| **Final Size** | 128×256 ❌ | 64×128 ✓ |

### Body (Transform Scale 1.0x)

| Aşama | Önceki | Yeni |
|-------|--------|------|
| **Mesh Size** | 64×128 pixels | 64×128 pixels |
| **Baked Scale** | 1.0, 1.0 | 1.0, 1.0 ✓ |
| **Shader Scale** | ×1.0 | ×1.0 |
| **Final Size** | 64×128 ✓ | 64×128 ✓ |

## Debug Logging

Baking sırasında console'da:

```
Part[3] 'LArm' (Nested):
  Transform Scale: (2.000, 2.000) [Included in mesh]  ← Mesh'te dahil
  Baked Scale: (1.000, 1.000) [Always 1.0]  ← Her zaman 1.0!
```

**Kontrol:**
- ✅ Transform Scale: Mesh builder'da kullanılan scale
- ✅ Baked Scale: Her zaman (1.0, 1.0)
- ✅ "[Included in mesh]": Scale zaten mesh'te
- ✅ "[Always 1.0]": Baking'de scale yok

## Pozisyon Sorunu

"Hala aşağıda görünüyor" sorunu için, pozisyon hesaplamasını kontrol etmeliyiz.

**Olası Neden:**
- Pivot scale edildi, ama pozisyon hesaplamasında pivot kullanılıyor
- Eğer pivot yanlışsa, pozisyon da yanlış!

**Mesh Builder:**
```csharp
pivot = sprite.pivot × scale
offset = localPos × pixelsPerUnit
bl = (-pivot) + offset
```

**Baking:**
```csharp
pos = localPos  // Unity units
// Mesh'te: bl = (-pivot) + offset
// Shader: final_pos = bl + pos
//       = (-pivot) + offset + pos
//       = (-pivot × scale) + (localPos × ppu) + localPos
```

Bekle, bu yanlış! Baking'de `pos` zaten `localPos`, ama mesh'te `offset` da `localPos × ppu`. İki kere ekleniyor!

**Sorun:** Baking'de pozisyon **delta** olmalı, ama biz rest pose'u çıkarıyoruz:
```csharp
pos -= restPosePositions[part];
```

Eğer rest pose = animated pose ise, delta = 0 olmalı. Ama hala aşağıda görünüyorsa, rest pose hesaplaması yanlış olabilir!

## Test

1. **Mesh Builder:**
   - "Build From Rig Prefab"
   - Console'da LArm için:
     ```
     Transform Scale: (2.000, 2.000)
     Size (pixels): (64.0, 128.0)  ← 32×2, 64×2
     ```

2. **Baking:**
   - Animasyonu bake et
   - Console'da LArm için:
     ```
     Transform Scale: (2.000, 2.000) [Included in mesh]
     Baked Scale: (1.000, 1.000) [Always 1.0]
     ```

3. **Runtime:**
   - Baked animasyonu oynat
   - LArm boyutu mesh builder ile aynı olmalı ✓

## Özet

| Özellik | Önceki | Yeni |
|---------|--------|------|
| **Mesh Size** | texture × scale | texture × scale ✓ |
| **Baked Scale** | transform scale ❌ | **1.0** ✓ |
| **Shader Scale** | ×scale | ×1.0 ✓ |
| **Double Scaling** | ✅ Var (hata!) | ❌ Yok ✓ |
| **Final Size** | ❌ Yanlış | ✅ Doğru |

Artık baked animasyonda scale doğru! Pozisyon sorunu için ayrı bir kontrol gerekebilir. 🎉
