# Bake Animasyon Pozisyon Düzeltmesi

## Sorun
Mesh builder'da part pozisyonları doğru görünüyordu, ancak bake edilen animasyonda body parçası Y ekseninde daha aşağıda görünüyordu.

## Kök Neden

### Koordinat Sistemi Farkı

**Mesh Builder:**
- `offsetPixels` = Pivot noktasının karakter space'indeki pozisyonu
- Rig prefab'ın rest pose'undaki transform pozisyonlarını kullanır
- Vertex pozisyonu = `(-pivot + offset)` formülüyle hesaplanır

**Baking (Önceki Hali):**
- Animasyon clip'indeki **absolute pozisyonları** kaydediyordu
- Rest pose offset'ini dikkate almıyordu
- Sonuç: Baked animasyon, mesh'in rest pose'undan farklı bir başlangıç noktasına sahipti

### Örnek Senaryo

Diyelim ki:
- Rest pose'da: Body transform.localPosition.y = 0
- Animasyonun ilk frame'inde: Body transform.localPosition.y = 0 (aynı)
- Mesh builder: offsetPixels.y = 0 (rest pose'dan alındı)
- Baking (eski): pos.y = 0 (animasyondan alındı)

Görünüşte aynı, ama eğer:
- Rest pose'da: Body transform.localPosition.y = 2.0
- Animasyonun ilk frame'inde: Body transform.localPosition.y = 2.5
- Mesh builder: offsetPixels.y = 200 pixels (rest pose: 2.0 * 100 PPU)
- Baking (eski): pos.y = 2.5 (absolute)
- **Sonuç:** Shader'da: `pos = vertex + baked_pos = (-pivot + 200) + 2.5`
  - Ama olması gereken: `pos = vertex + delta = (-pivot + 200) + 0.5`

## Çözüm

### Rest Pose Offset Çıkarma

Baking sırasında, **rest pose pozisyonunu** animasyon pozisyonundan çıkarıyoruz:

```csharp
// 1. Animasyon başlamadan önce rest pose'u kaydet
Vector3[] restPosePositions = new Vector3[6];
for (int part = 0; part < 6; part++)
{
    var p = rig.parts[part];
    Matrix4x4 m = root.worldToLocalMatrix * p.localToWorldMatrix;
    restPosePositions[part] = m.GetColumn(3);
}

// 2. Animasyon sample'larken, rest pose'u çıkar
for each frame:
    Vector3 pos = m.GetColumn(3);
    pos -= restPosePositions[part];  // DELTA hesapla
    // pos artık rest pose'dan olan offset'i temsil ediyor
```

### Shader'da Kullanım

Shader'da vertex transform işlemi:

```hlsl
// input.positionOS.xy = Mesh vertex (zaten -pivot + offset içeriyor)
// t = Baked position (artık rest pose'dan delta)

pos = input.positionOS.xy;  // Örnek: (-pivot + restOffset)
pos *= scale;                // Scale uygula
pos = rotate(pos);           // Rotate uygula
pos = pos + t;               // Delta ekle: (-pivot + restOffset) + delta
                             // = (-pivot + restOffset + delta)
                             // = (-pivot + animatedOffset)
```

## Sonuç

Artık baked animasyon, mesh builder'ın koordinat sistemiyle **tamamen uyumlu**:

1. **Mesh Builder:** Rest pose pozisyonlarını `offsetPixels` olarak kullanır
2. **Baking:** Rest pose'dan delta'ları kaydeder
3. **Shader:** Mesh vertex (rest pose) + delta (animasyon) = Animated position

### Avantajlar

✅ Mesh builder ve baked animasyon aynı koordinat sistemini kullanır
✅ Rest pose'daki part pozisyonları doğru şekilde korunur
✅ Animasyon delta'ları doğru şekilde uygulanır
✅ Body ve diğer partlar artık doğru yükseklikte görünür

## Test

1. Rig prefab'ı mesh builder'a ata
2. `autoFillFromRig = true` yap
3. Rebuild yap - part'ların doğru pozisyonda olduğunu doğrula
4. Aynı rig'i MAT Baker'a ata
5. Animasyon clip'lerini ekle ve bake yap
6. Baked animasyonu oynat
7. **Sonuç:** Part'lar mesh builder'daki pozisyonlardan başlayıp animasyon delta'larını uygular

## Teknik Detaylar

### Rest Pose Nedir?

Rest pose (veya T-pose, bind pose), rig'in **animasyon uygulanmadan önceki** default durumudur. Bu:
- Rig prefab'ın Inspector'daki transform değerleri
- Mesh builder'ın part pozisyonlarını aldığı referans
- Baking'in delta hesaplaması için baseline

### Neden Delta Kullanıyoruz?

Absolute pozisyon yerine delta kullanmak:
- Mesh ve animasyonun aynı koordinat sistemini paylaşmasını sağlar
- Rest pose değişikliklerinin her ikisine de yansımasını sağlar
- Shader'da daha basit ve tutarlı hesaplama sağlar

### Matrix Hesaplama

```csharp
Matrix4x4 m = root.worldToLocalMatrix * p.localToWorldMatrix;
Vector3 pos = m.GetColumn(3);
```

Bu formül:
- `p.localToWorldMatrix`: Part'ın world space transform'u
- `root.worldToLocalMatrix`: World'den rig root'a dönüşüm
- Sonuç: Part'ın rig root space'indeki pozisyonu
- `GetColumn(3)`: 4x4 matrix'in translation component'i

## İlgili Dosyalar

- `/Assets/MAT2D/Editor/Mat2DMatBakerWindow.cs` - Baking kodu (satır 212-260)
- `/Assets/MAT2D/Scripts/Mat2DCharacterMeshBuilder.cs` - Mesh builder (BuildFromRig method)
- `/Assets/MAT2D/Shaders/MAT2D_UnlitAtlas_MAT5.shader` - Shader (vertex transform)
