namespace KuryeTycoon
{
    /// <summary>
    /// Sadeleştirme anahtarları: oyunu kalabalıklaştıran yan sistemler buradan kapatılır.
    /// Kod silinmedi (eski kayıtlar bozulmasın, gerekirse geri açılsın); "true" yapınca sistem eskisi gibi çalışır.
    /// </summary>
    public static class Sade
    {
        /// <summary>Çanta hijyeni: kirlenme, şikayet, "temizlik için uyar". Kapalıyken çanta hep temiz; yıpranma (yeni çanta) sürer.</summary>
        public static readonly bool Hijyen = false;
        /// <summary>Nakit borcu: kuryenin üstünde nakit birikmesi, keep cash / offload, hesap kilidi. Kapalıyken nakit doğrudan kasaya girer.</summary>
        public static readonly bool NakitBorcu = false;
        /// <summary>Ekipman depozitosu: bedelin kuryenin kazancından kesilmesi ve iadede geri ödenmesi. Kapalıyken ekipmanı şirket verir, kurye ayrılırken geri getirir.</summary>
        public static readonly bool Depozito = false;
        /// <summary>Yıl içindeki habersiz vergi ve harçlar. Kapalıyken tek vergi: yıl sonu kademeli kâr vergisi.</summary>
        public static readonly bool SurprizVergi = false;
    }
}
