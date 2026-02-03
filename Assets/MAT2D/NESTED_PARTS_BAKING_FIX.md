# LArm ve RArm Baking Sorunu - Çözüm

## Sorun
Baked animasyonlarda **LArm ve RArm** parçalarının büyüklüğü ve yerleri doğru görünmüyordu.

## Kök Neden

### Parent Scale Inheritance

**Nested Hierarchy:**
```
Root
└── Body
    ├── LArm
    └── RArm
```

**Önceki Kod:**
```csharp
Matrix4x4 m = root.worldToLocalMatrix * p.localToWorldMatrix;
Vector2 axisX = new Vector2(m.m00, m.m01);
Vector2 axisY = new Vector2(m.m10, m.m11);
float sx = axisX.magnitude;  // World scale (parent scale dahil!)
float sy = axisY.magnitude;
```

**Sorun:**
- `localToWorldMatrix` **parent'ın scale'ini de içerir**
- Eğer Body scale = (1.5, 1.5) ise
- LArm local scale = (1.0, 1.0) olsa bile
- LArm world scale = (1.5, 1.5) olur ❌

**Sonuç:**
- Kollar parent'ın scale'i kadar büyütülüyor
- Mesh builder'da her part bağımsız render edildiği için uyumsuzluk oluşuyor

### Rotation Sorunu

Aynı şekilde rotation da:
- Matrix'ten çıkarılan rotation, parent rotation'ı da içeriyor
- Ama mesh builder'da her part bağımsız, parent rotation'ı yok

## Çözüm

### Local Transform Kullanımı

Her part'ın **kendi local scale ve rotation**'ını kullanmalıyız:

```csharp
// Position: Root space'e dönüştür (doğru)
Matrix4x4 worldToRoot = root.worldToLocalMatrix * p.localToWorldMatrix;
Vector3 pos = worldToRoot.GetColumn(3);
pos -= restPosePositions[part];

// Rotation: Root'a göre world rotation
Quaternion worldRot = p.rotation;
Quaternion rootRot = root.rotation;
Quaternion localToRootRot = Quaternion.Inverse(rootRot) * worldRot;
float angle = localToRootRot.eulerAngles.z * Mathf.Deg2Rad;

// Scale: LOCAL scale (parent scale dahil değil!)
Vector3 localScale = p.localScale;
float sx = Mathf.Abs(localScale.x);
float sy = Mathf.Abs(localScale.y);
```

### Neden Bu Çalışıyor?

**Mesh Builder:**
- Her part bağımsız bir quad olarak render edilir
- Parent-child ilişkisi mesh'te yok
- Her part'ın kendi scale'i var

**Shader:**
```hlsl
pos *= scale;     // Part'ın kendi scale'i
pos = rotate(pos); // Part'ın kendi rotation'ı
pos += translation; // Part'ın pozisyonu
```

**Baking (Yeni):**
- Local scale → Shader'daki scale ile eşleşir ✓
- Root-relative rotation → Shader'daki rotation ile eşleşir ✓
- Root-relative position → Shader'daki translation ile eşleşir ✓

## Karşılaştırma

### Örnek Senaryo

**Rig Hierarchy:**
```
Body (scale: 1.5, 1.5, rotation: 0°)
└── LArm (local scale: 1.0, 1.0, local rotation: 45°)
```

**Önceki Baking:**
```
LArm world scale = (1.5, 1.5)  ← Parent scale dahil!
LArm baked scale = (1.5, 1.5)
Shader: Kol 1.5x büyük render edilir ❌
```

**Yeni Baking:**
```
LArm local scale = (1.0, 1.0)  ← Sadece kendi scale'i!
LArm baked scale = (1.0, 1.0)
Shader: Kol doğru boyutta render edilir ✓
```

## Debug Logging

Baking sırasında ilk frame için console'da:

```
MAT2D Baker: Baking clip 'Walk' - Length: 1.000s, Frames: 31, FPS: 30
  Frame 0/30: t = 0.0000s
  Part[0] 'Body':
    Local Scale: (1.000, 1.000)
    World Scale: (1.000, 1.000)
    Baked Scale: (1.000, 1.000)
    Angle: 0.0°
  Part[3] 'LArm':
    Local Scale: (1.000, 1.000)
    World Scale: (1.500, 1.500)  ← Parent'tan miras!
    Baked Scale: (1.000, 1.000)  ← Ama biz local'i kullanıyoruz ✓
    Angle: 45.0°
```

**Kontrol:**
- Local Scale = Baked Scale olmalı ✓
- World Scale ≠ Baked Scale olabilir (nested parts için normal)

## Teknik Detaylar

### Transform Hierarchy

Unity'de transform hierarchy:
```
Transform.localScale: Parent'a göre scale
Transform.lossyScale: World space scale (parent scale dahil)
Transform.localToWorldMatrix: Local'den world'e dönüşüm (parent dahil)
```

**Mesh Builder için:**
- Her part bağımsız → Local scale kullan
- Parent-child yok → Parent scale'i görmezden gel

### Rotation Hesaplama

```csharp
Quaternion worldRot = p.rotation;           // World space rotation
Quaternion rootRot = root.rotation;         // Rig root rotation
Quaternion localToRootRot = Quaternion.Inverse(rootRot) * worldRot;
float angle = localToRootRot.eulerAngles.z; // Z-axis rotation (2D)
```

Bu formül:
- Part'ın world rotation'ını alır
- Rig root'un rotation'ını çıkarır
- Sonuç: Part'ın root'a göre rotation'ı

### Position Hesaplama

Position için world matrix kullanmak **doğru**:
```csharp
Matrix4x4 worldToRoot = root.worldToLocalMatrix * p.localToWorldMatrix;
Vector3 pos = worldToRoot.GetColumn(3);
```

Çünkü:
- Position parent'tan etkilenmeli (parent hareket ederse child da hareket eder)
- Ama scale ve rotation parent'tan etkilenmemeli (mesh'te bağımsızlar)

## Test

### Doğrulama Adımları

1. **Rig Oluştur:**
   - Body part'ı oluştur
   - LArm ve RArm'ı Body'nin child'ı yap
   - Body'yi scale et (örn. 1.5x)

2. **Mesh Builder Test:**
   - Rig'i mesh builder'a ata
   - Rebuild yap
   - Kolların doğru boyutta olduğunu doğrula ✓

3. **Baking Test:**
   - Aynı rig'i MAT Baker'a ata
   - Animasyon bake et
   - Console'da debug log'ları kontrol et:
     - LArm Local Scale = (1.0, 1.0)
     - LArm World Scale = (1.5, 1.5)
     - LArm Baked Scale = (1.0, 1.0) ✓

4. **Runtime Test:**
   - Baked animasyonu oynat
   - Kolların mesh builder'daki boyutla aynı olduğunu doğrula ✓

## Sonuç

### Önceki Durum
- ❌ LArm ve RArm parent scale'inden etkileniyordu
- ❌ Kollar mesh builder'dan farklı boyutta görünüyordu
- ❌ Nested hierarchy sorunluydu

### Yeni Durum
- ✅ Her part kendi local scale'ini kullanıyor
- ✅ Parent scale etkisi yok
- ✅ Mesh builder ve baked animasyon tutarlı
- ✅ Nested hierarchy doğru çalışıyor

## İlgili Dosyalar

- `/Assets/MAT2D/Editor/Mat2DMatBakerWindow.cs` - Baking kodu (satır 257-305)
- `/Assets/MAT2D/Scripts/Mat2DCharacterMeshBuilder.cs` - Mesh builder
- `/Assets/MAT2D/Shaders/MAT2D_UnlitAtlas_MAT5.shader` - Shader

## Özet

| Özellik | Önceki | Yeni |
|---------|--------|------|
| Scale Kaynağı | World (parent dahil) | **Local** |
| LArm Scale (Body 1.5x) | 1.5, 1.5 ❌ | 1.0, 1.0 ✓ |
| Rotation Kaynağı | Matrix (parent dahil) | **Root-relative** |
| Mesh Uyumu | ❌ Farklı | ✅ Aynı |

Artık LArm, RArm ve diğer nested part'lar doğru büyüklük ve pozisyonda bake ediliyor! 🎉
