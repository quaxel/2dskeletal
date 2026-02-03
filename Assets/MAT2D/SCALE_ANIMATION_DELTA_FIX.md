# Scale Animation Delta Fix

## Sorun
Baked animasyonlar ve scale'ler **daha minimal (küçük)** görünüyordu.

## Kök Neden

### Scale Delta Eksikti

**Önceki Kod:**
```csharp
// Scale her zaman 1.0
float sx = 1.0f;
float sy = 1.0f;
```

**Sorun:**
- Rest pose scale mesh'te dahil edilmişti ✓
- Ama **animasyon sırasında scale değişiklikleri** göz ardı ediliyordu ❌
- Eğer animasyon part'ı scale ediyorsa, bu değişiklik bake edilmiyordu!

### Örnek Senaryo

**LArm:**
- Rest pose scale: (2.0, 2.0) → Mesh'te dahil
- Frame 0: scale = (2.0, 2.0) → Değişiklik yok
- Frame 10: scale = (3.0, 3.0) → **Animasyon scale'i değiştirdi!**
- Frame 20: scale = (1.5, 1.5) → **Küçülttü!**

**Önceki Baking:**
```csharp
baked_scale = 1.0  // Her frame için!
```

**Sonuç:**
- Frame 0: 1.0 → Doğru (değişiklik yok)
- Frame 10: 1.0 → **Yanlış!** (1.5x olmalıydı)
- Frame 20: 1.0 → **Yanlış!** (0.75x olmalıydı)
- **Animasyon scale değişiklikleri kayboldu!**

## Çözüm

### Scale Delta Hesaplama

```csharp
// Capture rest pose scales
Vector3[] restPoseScales = new Vector3[6];
for (int part = 0; part < 6; part++)
{
    restPoseScales[part] = rig.parts[part].localScale;
}

// During baking, calculate scale delta
Vector3 animatedScale = p.localScale;
Vector3 restScale = restPoseScales[part];

float sx = Mathf.Abs(restScale.x) > 1e-6f ? 
    Mathf.Abs(animatedScale.x) / Mathf.Abs(restScale.x) : 1.0f;
float sy = Mathf.Abs(restScale.y) > 1e-6f ? 
    Mathf.Abs(animatedScale.y) / Mathf.Abs(restScale.y) : 1.0f;
```

### Formula

```
baked_scale = animated_scale / rest_pose_scale
```

**Örnekler:**
- Rest: (2.0, 2.0), Anim: (2.0, 2.0) → Baked: 2.0/2.0 = **1.0** (değişiklik yok)
- Rest: (2.0, 2.0), Anim: (3.0, 3.0) → Baked: 3.0/2.0 = **1.5** (1.5x büyüme)
- Rest: (2.0, 2.0), Anim: (1.0, 1.0) → Baked: 1.0/2.0 = **0.5** (yarı boyut)

### Shader'da Uygulama

```hlsl
// Mesh vertices zaten rest pose scale içeriyor
pos = mesh_vertex;  // Rest pose size

// Baked scale delta'sını uygula
pos *= baked_scale;  // animated_size = rest_size × delta

// Örnek:
//   Rest size: 64×128 (texture 32×64, scale 2.0)
//   Baked scale: 1.5
//   Final size: 64×128 × 1.5 = 96×192 ✓
```

## Karşılaştırma

### LArm Animation

| Frame | Rest Scale | Anim Scale | Önceki Baked | Yeni Baked | Final Size |
|-------|------------|------------|--------------|------------|------------|
| 0 | 2.0, 2.0 | 2.0, 2.0 | 1.0 ✓ | **1.0** ✓ | 64×128 ✓ |
| 10 | 2.0, 2.0 | 3.0, 3.0 | 1.0 ❌ | **1.5** ✓ | 96×192 ✓ |
| 20 | 2.0, 2.0 | 1.5, 1.5 | 1.0 ❌ | **0.75** ✓ | 48×96 ✓ |

**Önceki:**
- Tüm frame'lerde aynı boyut (64×128) ❌
- Scale animasyonu yok ❌

**Yeni:**
- Her frame'de doğru boyut ✓
- Scale animasyonu çalışıyor ✓

## Debug Logging

Baking sırasında console'da:

```
Part[3] 'LArm' (Nested):
  Rest Scale: (2.000, 2.000) [In mesh]  ← Mesh'te dahil
  Anim Scale: (2.000, 2.000)  ← İlk frame, değişiklik yok
  Baked Scale: (1.000, 1.000) [Delta: anim/rest]  ← 2.0/2.0 = 1.0 ✓
```

**Animasyon sırasında (frame 10):**
```
Part[3] 'LArm' (Nested):
  Rest Scale: (2.000, 2.000) [In mesh]
  Anim Scale: (3.000, 3.000)  ← Animasyon değiştirdi!
  Baked Scale: (1.500, 1.500) [Delta: anim/rest]  ← 3.0/2.0 = 1.5 ✓
```

**Kontrol:**
- ✅ Baked Scale = Anim Scale / Rest Scale
- ✅ Eğer animasyon scale değiştirmiyorsa: Baked = 1.0
- ✅ Eğer animasyon scale değiştiriyorsa: Baked = delta

## Neden "Minimal" Görünüyordu?

**Olası Senaryo 1: Squash & Stretch**
```
Animasyon squash & stretch kullanıyorsa:
  Frame 5: scale = (1.2, 0.8)  // Genişledi, kısaldı
  Frame 10: scale = (0.8, 1.2)  // Daraldı, uzadı

Önceki baking: Her frame scale = 1.0
  → Squash & stretch kayboldu
  → Animasyon "minimal" görünüyor (dinamizm yok)

Yeni baking: Scale delta'ları bake ediliyor
  → Squash & stretch korunuyor
  → Animasyon dinamik ✓
```

**Olası Senaryo 2: Scale Animasyonu**
```
Animasyon part'ı küçültüyorsa:
  Frame 0: scale = (2.0, 2.0)
  Frame 20: scale = (1.0, 1.0)  // Yarı boyut

Önceki baking: Her frame scale = 1.0
  → Part hep aynı boyutta
  → Küçülme animasyonu yok
  → "Minimal" görünmüyor, ama animasyon eksik

Yeni baking: Scale delta bake ediliyor
  → Frame 20: baked = 1.0/2.0 = 0.5
  → Part küçülüyor ✓
```

## Pozisyon ve Rotation ile İlişki

Scale delta hesaplaması, pozisyon ve rotation delta'larıyla tutarlı:

| Transform | Rest Pose | Animated | Baked |
|-----------|-----------|----------|-------|
| **Position** | restPos | animPos | animPos - restPos (delta) ✓ |
| **Rotation** | restRot | animRot | animRot (absolute) |
| **Scale** | restScale | animScale | **animScale / restScale (delta)** ✓ |

**Not:** Rotation için delta yok çünkü rotation zaten mesh'te dahil değil (shader'da uygulanıyor).

## Özet

| Özellik | Önceki | Yeni |
|---------|--------|------|
| **Rest Pose Scale** | Mesh'te dahil ✓ | Mesh'te dahil ✓ |
| **Animated Scale** | Göz ardı ❌ | **Delta olarak bake** ✓ |
| **Baked Scale** | Her zaman 1.0 | **anim / rest** ✓ |
| **Scale Animation** | ❌ Çalışmıyor | ✅ Çalışıyor |
| **Squash & Stretch** | ❌ Kayboluy or | ✅ Korunuyor |

Artık scale animasyonları ve squash & stretch efektleri doğru şekilde bake ediliyor! 🎉
