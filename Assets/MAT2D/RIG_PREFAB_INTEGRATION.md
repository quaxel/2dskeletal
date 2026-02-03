# Mat2DCharacterMeshBuilder - Rig Prefab Integration

## Özet (Summary)

`Mat2DCharacterMeshBuilder.cs` dosyasına rig prefab desteği eklendi. Artık parts build yaparken rig prefab seçilip, bütün partlar doğru konum, doğru büyüklük ve doğru pivot noktaları ile otomatik olarak oluşturulabilir.

## Yapılan Değişiklikler (Changes Made)

### 1. Yeni Alanlar (New Fields)
- **`rigPrefab`**: Rig prefab referansı (GameObject)
- **`autoFillFromRig`**: Rig'den otomatik doldurma aktif/pasif

### 2. Yeni Method: `BuildFromRig()`
Bu method şu işlemleri yapar:
1. **Rig Validation**: Rig prefab'ın `Mat2DRigDefinition` component'ine sahip olduğunu kontrol eder
2. **Part Extraction**: Her bir part için:
   - **Size (Boyut)**: SpriteRenderer'dan sprite'ın `textureRect` bilgisini alır
   - **Pivot**: Sprite'ın pivot noktasını alır
   - **Position (Konum)**: Transform'un rig root'a göre local pozisyonunu alır ve `offsetPixels`'a dönüştürür
   - **Atlas Rect**: Sprite'ın atlas içindeki UV koordinatlarını hesaplar
   - **Name**: Part'ın ismini transform'dan alır
3. **Material Setup**: İlk sprite'ın texture'ını material'ın `_BaseMap`'ine atar (eğer `autoAssignBaseMap` aktifse)

### 3. Güncellenen `Rebuild()` Method
Öncelik sırası:
1. **Rig Prefab** (eğer `autoFillFromRig` aktif ve `rigPrefab` atanmışsa)
2. **Sprite Array** (eğer `autoFillFromSprites` aktifse)
3. **Debug Fallback** (eğer `debugFillIfMissing` aktifse)

## Kullanım (Usage)

### Inspector'da:
1. `Mat2DCharacterMeshBuilder` component'ine sahip bir GameObject seç
2. **Auto Fill (optional)** bölümünde:
   - `rigPrefab` alanına rig prefab'ı sürükle
   - `autoFillFromRig` checkbox'ını işaretle
3. Component otomatik olarak rebuild olacak (veya sağ tık > "Rebuild" seç)

### Rig Prefab Gereksinimleri:
- `Mat2DRigDefinition` component'ine sahip olmalı
- Tam olarak 6 part transform'u tanımlanmış olmalı
- Her part'ın `SpriteRenderer` component'i ve sprite'ı olmalı
- Tüm sprite'lar aynı atlas texture'ından olmalı (GPU instancing için)

## Teknik Detaylar

### Pivot ve Position Hesaplama:
```csharp
// Sprite pivot (sprite'ın kendi local space'inde)
p.pivotPixels = sprite.pivot;

// Part position (rig root'a göre, pixel cinsinden)
Vector3 localPos = rigRoot.InverseTransformPoint(partTransform.position);
p.offsetPixels = new Vector2(localPos.x * pixelsPerUnit, localPos.y * pixelsPerUnit);
```

### Atlas UV Hesaplama:
```csharp
Rect rect = sprite.textureRect; // Pixel coordinates in atlas
float invW = 1f / tex.width;
float invH = 1f / tex.height;
p.atlasRect = new Rect(rect.x * invW, rect.y * invH, rect.width * invW, rect.height * invH);
```

## Avantajlar (Benefits)

1. **Otomatik Konum**: Part pozisyonları rig hierarchy'sinden otomatik alınır
2. **Doğru Pivot**: Her part'ın pivot noktası sprite'dan doğru şekilde alınır
3. **Doğru Boyut**: Sprite boyutları atlas'tan doğru şekilde hesaplanır
4. **Tek Texture**: Tüm partlar aynı atlas texture'ını kullanır (GPU instancing için optimal)
5. **Hata Kontrolü**: Eksik veya hatalı rig yapılandırmaları için uyarılar verir

## Örnek Workflow

1. Unity'de bir rig prefab oluştur (6 part ile)
2. Her part'a SpriteRenderer ekle ve sprite ata
3. `Mat2DRigDefinition` component'i ekle ve part'ları tanımla
4. `Mat2DCharacterMeshBuilder` component'inde `rigPrefab`'ı ata
5. `autoFillFromRig` aktif et
6. Mesh otomatik olarak rig'den build edilir!
