// Kurye Tycoon — çevre animasyonu (köşe gölgelendiricisinde, CPU maliyeti yok): Bina Atölyesi modellerinin rüzgârda
// salınan ağaçları, titreşen yaprakları, dalgalanan bayrakları, akan çeşme suyu, dönen atlıkarıncası.
//  • Pastel.shader (yüzey; bütün geçişler) ve PastelCizgi.shader (kalem çizgisi) AYNI işlevi çağırır: çizgi şeridinin iki
//    ucu da (konum ve TEXCOORD1'deki öbür uç) aynı kaymayı alır, kontur yüzeyden ayrılmaz.
//  • Kayma yalnız köşenin MODEL uzayındaki konumuna, köşenin moduna ve parça yerleşiminin iki sabitine bağlıdır (sürekli
//    bir alan): aynı yerdeki yüzey köşesi, çizgi köşesi ve ters kabuk köşesi birlikte oynar.
//  • Mod köşe renginin ALFASINDA: alfa = 255 − 24 × mod (255: animasyon yok; atölye dışındaki mesh'ler hep 255).
//    Sabitler TEXCOORD0.zw'de (AtolyeKurucu.Topla yazar; mesh'te animasyon yoksa uv iki bileşenlidir, okunmaz):
//      1 Salın  : gövde / dal — rüzgâr yönünde eğilir, yükseklik² ile          zw = (taban y, faz)
//      2 Hışır  : yaprak — 1 + dal kıpırtısı + yaprak titreşimi                 zw = (taban y, faz)
//      3 Titrek : saksı çiçeği, asılı sepet — yalnız titreşim                   zw = (0, faz)
//      4 Saz    : saz, uzun ot — kısa boyda eğilir, rüzgâr yönünde dalga geçer  zw = (taban y, faz)
//      5 Dalga  : bayrak — direğin (çapa) çevresinde yana dalgalanır            zw = çapa (x, z)
//      6 Akış   : su — aşağı akan dalga, yüzeyde hafif çalkantı                 zw = —
//      7 Döner  : atlıkarınca — düşey eksen çevresinde döner (normal de döner)  zw = eksen (x, z)
//      8 Sark   : asılı bez (tente eteği, flama ipi) — üst kenarı sabit         zw = (üst y, 0)
//  • Model uzayı: metre, Y yukarı (AtolyeKurucu.Unity). Rüzgârın gücü ve yönü Diorama'dan (hava durumuna göre):
//    _KuryeSalinim (tepe salınımı, dünya birimi), _KuryeRuzgarYon (dünya xy), _KuryeRuzgar (0 sakin … 1 fırtına).
//  • Uzakta söner (kameraya dünya uzaklığı; kare): titreşim ve su 16 → 34, eğilme ve bayrak 40 → 80. Dönme sönmez.
//  • Kışın (_PastelKis = 1; bu dosyayı içeren gölgelendirici bildirir) rüzgâr hareketleri beşte bire iner, atlıkarınca
//    durur: şehrin kar kabuğu (City3D.KarMesh) köşe animasyonunu izlemeyen ayrı, durağan bir mesh'tir; salınan dalın ya
//    da dönen atın üstündeki kar yerinde kalıp ayrılmasın.
//  • Zaman hiçbir yerde değişken bir katsayıyla çarpılmaz (rüzgâr değişince faz sıçramasın): rüzgâr yalnız genliği büyütür.
#ifndef KURYE_CEVRE_ANIMASYON_INCLUDED
#define KURYE_CEVRE_ANIMASYON_INCLUDED

float _KuryeSalinim;
float4 _KuryeRuzgarYon;
float _KuryeRuzgar;
// _PastelKis: bu dosyayı içeren gölgelendiricide (Pastel.shader, PastelCizgi.shader) #include'dan ÖNCE bildirilir

void CA_Uygula(inout float3 po, inout float3 no, float alfa, float2 ab)
{
    float mf = (1.0 - alfa) * 10.625;              // 255 / 24
    if (mf < 0.5) return;

    float t = _Time.y;
    float3 p0 = po;
    float3 pw = TransformObjectToWorld(po);
    float uzaklik = distance(pw, _WorldSpaceCameraPos.xyz);
    float yakin = 1.0 - smoothstep(16.0, 34.0, uzaklik);
    float orta = 1.0 - smoothstep(40.0, 80.0, uzaklik);
    float ruzgar = saturate(_KuryeRuzgar);
    float kis = saturate(_PastelKis);
    float yaz = 1.0 - 0.8 * kis;                   // kışın rüzgâr hareketi azalır (kar kabuğu durağan)

    // nesnenin ölçeği ve rüzgâr yönü (model uzayında, yatay düzlemde)
    float olcek = max(length(mul((float3x3)GetObjectToWorldMatrix(), float3(0.0, 1.0, 0.0))), 1e-5);
    float3 ro = mul((float3x3)GetWorldToObjectMatrix(), float3(_KuryeRuzgarYon.xy, 0.0));
    ro.y = 0.0;
    float rl = length(ro);
    ro = rl > 1e-6 ? ro / rl : float3(1.0, 0.0, 0.0);
    float2 yanRuzgar = float2(-ro.z, ro.x);
    float guc = _KuryeSalinim / olcek * yaz;       // tepe salınımı, metre

    if (mf > 6.5 && mf < 7.5)
    {
        // ---- 7 Döner
        float sn, cs;
        sincos(t * 0.5 * (1.0 - kis), sn, cs);     // kışın kapalı (durur)
        float2 d = po.xz - ab;
        po.xz = ab + float2(d.x * cs - d.y * sn, d.x * sn + d.y * cs);
        no.xz = float2(no.x * cs - no.z * sn, no.x * sn + no.z * cs);
        return;
    }
    if (mf > 4.5 && mf < 5.5)
    {
        // ---- 5 Dalga (bayrak)
        float2 dv = po.xz - ab;
        float s = length(dv);
        float w = saturate(s / 0.9);
        float2 yan = s > 1e-4 ? float2(-dv.y, dv.x) / s : float2(0.0, 0.0);
        float g5 = (0.05 + 0.11 * ruzgar) * w * orta;
        float dal = sin(s * 3.6 - t * 5.2 + po.y * 1.3) + 0.45 * sin(s * 7.1 - t * 8.3 + po.y * 2.9);
        po.xz += yan * (dal * g5);
        po.y += sin(s * 2.9 - t * 4.1) * g5 * 0.35 - w * w * 0.04 * (1.0 - ruzgar) * orta;
        return;
    }
    if (mf > 5.5 && mf < 6.5)
    {
        // ---- 6 Akış (su)
        float a6 = 0.02 * yakin;
        po.x += sin(p0.y * 9.0 - t * 7.0 + p0.z * 4.0) * a6;
        po.z += cos(p0.y * 8.0 - t * 6.3 + p0.x * 4.0) * a6;
        po.y += sin(p0.x * 3.1 + p0.z * 2.7 + t * 2.4) * a6 * 0.6;
        return;
    }
    if (mf > 7.5)
    {
        // ---- 8 Sark (asılı bez)
        float w8 = saturate((ab.x - po.y) / 0.22);
        float a8 = (0.012 + 0.05 * ruzgar) * w8 * yakin * yaz;
        float dal8 = sin(p0.x * 4.1 + p0.z * 3.3 - t * 3.9) + 0.5 * sin(p0.x * 8.3 - p0.z * 6.1 + t * 6.7);
        po.xz += ro.xz * (dal8 * a8);
        po.y += abs(dal8) * a8 * 0.35;
        return;
    }

    float3 kok = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
    float faz = ab.y + dot(kok.xy, float2(1.7, 2.3));
    if (mf > 3.5)
    {
        // ---- 4 Saz
        float e4 = min(max(p0.y - ab.x, 0.0) / 1.2, 2.0);
        e4 *= e4;
        float fz = dot(p0.xz, ro.xz) * 0.9;
        float d4 = sin(t * 2.1 - fz + faz) * 0.6 + sin(t * 3.7 - fz * 1.7 + faz * 0.7) * 0.3 + 0.35;
        po.xz += ro.xz * ((guc * 0.3 + 0.01 * yaz) * e4 * d4 * orta);
        po.xz += yanRuzgar * ((guc * 0.1 + 0.004 * yaz) * e4 * sin(t * 1.3 + fz * 2.3 + faz) * orta);
        return;
    }
    if (mf < 2.5)
    {
        // ---- 1 Salın, 2 Hışır: gövde eğilir (yükseklik²), ağaçtan ağaca faz farkı
        float hh = min(max(p0.y - ab.x, 0.0) / 6.0, 1.6);
        float e1 = hh * hh;
        float d1 = sin(t * 1.1 + faz) * 0.55 + sin(t * 2.3 + faz * 1.7) * 0.3 + sin(t * 0.37 + faz * 0.6) * 0.35 + 0.45;
        po.xz += ro.xz * (guc * e1 * d1 * orta);
        po.xz += yanRuzgar * (guc * 0.3 * e1 * sin(t * 0.9 + faz * 1.3) * orta);
    }
    if (mf > 1.5 && yakin > 0.0)
    {
        // ---- 2 Hışır, 3 Titrek: dal kıpırtısı (yavaş, iri) + yaprak titreşimi (hızlı, ufak)
        float af = (0.012 + 0.05 * ruzgar + min(guc, 0.6) * 0.06) * yakin * yaz * (mf > 2.5 ? 0.6 : 1.0);
        float3 f = p0 * 2.3;
        float3 dal = float3(sin(t * 1.7 + f.x * 0.6 + f.y * 0.5 + faz), sin(t * 1.3 + f.y * 0.7 + f.z * 0.4 + faz), sin(t * 1.9 + f.z * 0.6 + f.x * 0.5 + faz));
        float3 tit = float3(sin(t * 5.3 + f.x * 1.9 + f.y * 2.3), sin(t * 6.1 + f.y * 2.1 + f.z * 1.7), sin(t * 4.7 + f.z * 2.2 + f.x * 1.3));
        po += (dal * 1.6 + tit) * af * float3(1.0, 0.55, 1.0);
    }
}

#endif
