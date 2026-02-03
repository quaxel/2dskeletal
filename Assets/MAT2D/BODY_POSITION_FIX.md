# Body Parçası Y Ekseni Pozisyon Sorunu - Çözüm Kılavuzu

## Sorun
Body parçası rig prefab'dan build edildiğinde Y ekseninde olması gerekenden daha aşağıda oluşuyor.

## Olası Nedenler ve Çözümler

### 1. Sprite Pivot Konumu
**Neden:** Unity'de sprite'ların pivot noktaları farklı yerlerde olabilir (bottom, center, top, custom).

**Kontrol:**
- Rig prefab'daki body sprite'ının pivot noktasını kontrol edin
- Eğer pivot **top-center** veya **top** ise, transform.position pivot noktasının pozisyonudur
- Bu durumda sprite'ın alt kısmı pivot'tan aşağıda olacaktır

**Çözüm:**
```
Option A: Sprite pivot'unu değiştirin
  - Body sprite'ını seçin
  - Sprite Editor'da pivot'u "Bottom" veya "Center" yapın
  - Apply

Option B: Rig'deki transform pozisyonlarını ayarlayın
  - Body transform'unun Y pozisyonunu pivot'a göre ayarlayın
  - Örnek: Pivot top'taysa, Y pozisyonunu sprite height kadar yukarı taşıyın
```

### 2. PixelsPerUnit Uyumsuzluğu
**Neden:** Mesh builder'daki `pixelsPerUnit` değeri rig'deki sprite'ların PPU değeriyle eşleşmiyor.

**Kontrol:**
- Body sprite'ının "Pixels Per Unit" değerini kontrol edin (genellikle 100)
- Mat2DCharacterMeshBuilder component'indeki `pixelsPerUnit` değerini kontrol edin

**Çözüm:**
```
Her iki değeri de aynı yapın (genellikle 100)
```

### 3. Rig Root Pozisyonu veya Scale
**Neden:** Rig root'un pozisyonu (0,0,0)'da değil veya scale'i (1,1,1) değil.

**Kontrol:**
- Rig prefab'ı açın
- Root GameObject'in Transform değerlerini kontrol edin
- Scale özellikle önemli!

**Çözüm:**
```
Rig root'u:
  Position: (0, 0, 0)
  Rotation: (0, 0, 0)
  Scale: (1, 1, 1)
yapın
```

### 4. Nested Hierarchy
**Neden:** Part'lar rig root'un direkt child'ı değil, nested hierarchy içinde.

**Kontrol:**
- Part transform'ların parent'ını kontrol edin
- Eğer part'lar rig root'un direkt child'ı değilse, pozisyon hesaplaması farklı olabilir

**Çözüm:**
```
Part'ları rig root'un direkt child'ı yapın
veya
Mat2DRigDefinition'da root'u doğru atayın
```

## Debug Adımları

### 1. Console Log'larını Kontrol Edin
Rebuild yaptığınızda console'da şu bilgiler görünecek:
```
MAT2D Part[X] 'PartName':
  World Pos: (x, y, z)
  Local Pos: (x, y, z)
  Rig Root: RootName at (x, y, z)
  Pivot (pixels): (x, y)
  Size (pixels): (x, y)
  Offset (pixels): (x, y)
  Expected Bottom-Left: (x, y)
```

### 2. Kritik Değerleri Karşılaştırın

**Body için beklenen:**
- Eğer body sprite'ı 64x128 piksel ve pivot bottom-center (32, 0) ise:
  - Pivot (pixels): (32, 0)
  - Size (pixels): (64, 128)
  
- Eğer rig'de body transform.localPosition = (0, 64, 0) ise:
  - Local Pos: (0, 64, 0)
  - Offset (pixels): (0, 6400) [eğer PPU=100]
  - Expected Bottom-Left: (-32, 6400)

**Sorun tespiti:**
- Eğer pivot.y ≈ size.y ise (örn. pivot top'taysa):
  - Expected Bottom-Left.y negatif olacaktır
  - Bu body'nin aşağıda görünmesine neden olur

### 3. Sprite Pivot'unu Görselleştirin

Unity Editor'da:
1. Rig prefab'ı Scene'e yerleştirin
2. Body part'ı seçin
3. Scene view'da pivot noktasını göreceksiniz (transform gizmo'nun merkezi)
4. Sprite'ın bu pivot'a göre nasıl konumlandığını gözlemleyin

## Hızlı Çözüm

Eğer body parçası Y ekseninde çok aşağıdaysa:

```csharp
// Mat2DCharacterMeshBuilder Inspector'da:
// 1. rigPrefab'ı atayın
// 2. autoFillFromRig = true yapın
// 3. logWarnings = true yapın (zaten default)
// 4. Rebuild (sağ tık > "Build From Rig Prefab")
// 5. Console'daki log'ları inceleyin
```

**En yaygın çözüm:**
- Body sprite'ının pivot'unu "Bottom" veya "Center" yapın
- Rig'deki body transform pozisyonunu buna göre ayarlayın
- Rebuild edin

## Örnek Düzeltme

Eğer body şu anda 128 piksel aşağıdaysa ve pivot top'taysa:

```
1. Body sprite'ını seçin
2. Sprite Editor > Pivot > Bottom
3. Apply
4. Rig prefab'da body transform.localPosition.y += 128 (sprite height)
5. Mat2DCharacterMeshBuilder'da Rebuild
```

Ya da kod tarafında offset'i manuel düzeltmek isterseniz:

```csharp
// BuildFromRig() sonrasında, parts array'ini manuel düzeltebilirsiniz:
// Örnek: Body part'ı (index 1 diyelim) 128 piksel yukarı taşı
parts[1].offsetPixels.y += 128;
```
