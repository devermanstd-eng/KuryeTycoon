using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Dükkan menüleri ve market ürünleri (~1 300 ürün; fiyatlar forint, 2026 Macaristan fiyatlarına yakın).
    /// Her satır "Ürün|fiyat|kategori":
    ///   Restoran / kafe: M menü (ana yemek + yan + içecek), A ana yemek, Y yan ürün / fırın, I içecek, K kahve,
    ///                    C çorba, T tatlı, E ekstra / sos
    ///   Market:          N normal, L sıvı (dökülebilir), F kırılgan (yumurta, ekmek, cam şişe…)
    /// Sipariş gerçek bir sepet gibi oluşur: restoranda kişi başı menü ya da ana yemek + yan + içecek, ara sıra tatlı ve sos;
    /// kafede kahve + fırın ürünü; markette adetli çok ürün. Sepet tutarı ürünlerin toplamıdır.
    /// Hasar / eksik teslimatta iade, sadece etkilenen ürünün fiyatıdır.
    /// </summary>
    public static class Menus
    {
        public struct Item
        {
            public string name;
            public int price;
            public char cat;
            public bool Liquid => cat == 'I' || cat == 'K' || cat == 'C' || cat == 'L';
            public bool Fragile => cat == 'A' || cat == 'T' || cat == 'Y' || cat == 'F' || cat == 'M';
        }

        // ================================================================ menüler

        static readonly string[] BurgerQueen =
        {
            "Whooper Menü|3690|M", "Dupla Whooper Menü|4490|M", "Sajtos Whooper Menü|3890|M", "Crispy Chicken Menü|3490|M",
            "Long Chicken Menü|3390|M", "Big King Menü|3590|M", "Steakhouse Burger Menü|4290|M", "Veggie Whooper Menü|3590|M",
            "Cheeseburger Menü|2590|M", "Nuggets (9) Menü|3190|M", "Whooper Junior Menü|2790|M",
            "Whooper|2390|A", "Dupla Whooper|3190|A", "Whooper Junior|1590|A", "Cheeseburger|890|A", "Dupla Cheeseburger|1390|A",
            "Hamburger|790|A", "Crispy Chicken|2190|A", "Long Chicken|1990|A", "Big King|2290|A", "Steakhouse Burger|2890|A",
            "Veggie Whooper|2290|A", "Chicken Nuggets (6)|1290|A", "Chicken Nuggets (9)|1790|A", "Chicken Wings (6)|1990|A",
            "Patates (orta)|790|Y", "Patates (büyük)|990|Y", "Soğan halkası (9)|890|Y", "Peynirli jalapeño (6)|1190|Y",
            "Coleslaw|690|Y", "Bahçe salatası|1190|Y",
            "Kola 0,4 L|590|I", "Kola 0,5 L|690|I", "Kola Zero 0,5 L|690|I", "Fanta 0,5 L|690|I", "Sprite 0,5 L|690|I",
            "Ice tea şeftali 0,5 L|690|I", "Su 0,5 L|490|I", "Portakal suyu 0,25 L|590|I", "Çilekli milkshake|1190|I",
            "Çikolatalı milkshake|1190|I", "Vanilyalı milkshake|1190|I", "Filtre kahve|590|K",
            "Çikolatalı sundae|790|T", "Karamelli sundae|790|T", "Elmalı turta|690|T", "Brownie|890|T", "Donut|590|T",
            "Ketçap|150|E", "Mayonez|150|E", "Barbekü sos|190|E", "Sarımsaklı sos|190|E", "Ekstra peynir|290|E", "Ekstra bacon|390|E",
            // Ek çeşitler
            "Rodeo Burger Menü|2990|M", "Chili Cheese Menü|3690|M", "Tavuk Royal Menü|3790|M", "Fish King Menü|3390|M",
            "King Box (4 kişilik)|9990|M", "Çocuk menüsü|2190|M", "Rodeo Burger|1290|A", "Chili Cheese Burger|2390|A",
            "Tavuk Royal|2490|A", "Fish King|2090|A", "Plant-based Whooper|2490|A", "Bacon King|3290|A",
            "Chicken Fries (9)|1590|A", "Chicken Nuggets (20)|3490|A", "Onion Rings King Burger|2590|A", "Halloumi Burger|2490|A",
            "Patates (küçük)|590|Y", "Sweet Potato Fries|1190|Y", "Cheese Fries|1290|Y", "Chili Cheese Nuggets (6)|1290|Y",
            "Mısır|690|Y", "Mozzarella çubukları (5)|1390|Y", "Kola 1 L|990|I", "Fanta 0,4 L|590|I",
            "Limonata 0,5 L|790|I", "Ice tea limon 0,5 L|690|I", "Maden suyu 0,5 L|490|I", "Muzlu milkshake|1190|I",
            "Oreo milkshake|1290|I", "Cappuccino|790|K", "Latte|890|K", "Sundae çilekli|790|T",
            "King Fusion (kurabiyeli)|1090|T", "King Fusion (karamelli)|1090|T", "Külah dondurma|290|T", "Çikolatalı kurabiye|490|T",
            "Cheesecake|990|T", "Ranch sos|190|E", "Acı jalapeño|190|E", "Hardal|150|E",
            "Ekstra köfte|790|E", "Ekstra soğan halkası|290|E",
        };

        static readonly string[] DonerKral =
        {
            "Dürüm menü (dürüm + patates + ayran)|3490|M", "Döner tabak menü (+ içecek)|4290|M", "İskender menü (+ ayran)|4690|M",
            "Dürüm döner (tavuk)|2490|A", "Dürüm döner (dana)|2990|A", "Ekmek arası döner (tavuk)|2190|A", "Ekmek arası döner (dana)|2590|A",
            "Porsiyon döner (tavuk)|3290|A", "Porsiyon döner (dana)|3790|A", "Döner tabağı (pilav, salata)|3890|A", "İskender|4290|A",
            "Döner box (patates üstü)|2790|A", "Falafel dürüm|2190|A", "Falafel tabağı|2890|A", "Lahmacun|1390|A", "Lahmacun (2'li)|2590|A",
            "Kaşarlı pide|2690|A", "Kıymalı pide|2890|A", "Karışık pide|3190|A", "Adana kebap|3990|A", "Urfa kebap|3990|A",
            "Tavuk şiş|3490|A", "Köfte porsiyon|3590|A", "Karışık ızgara|5490|A",
            "Mercimek çorbası|1090|C", "Ezogelin çorbası|1090|C",
            "Patates|890|Y", "Pilav|790|Y", "Bulgur pilavı|790|Y", "Çoban salata|990|Y", "Cacık|790|Y", "Humus|990|Y", "Ezme|790|Y",
            "Ayran|490|I", "Şalgam|590|I", "Kola 0,33 L|590|I", "Kola 0,5 L|690|I", "Fanta 0,33 L|590|I", "Su 0,5 L|390|I",
            "Çay|390|I", "Türk kahvesi|690|K", "Limonata|790|I",
            "Baklava (2 dilim)|1290|T", "Baklava (4 dilim)|2290|T", "Künefe|2490|T", "Sütlaç|1190|T", "Kazandibi|1190|T", "Kadayıf|1390|T",
            "Acı sos|190|E", "Sarımsaklı yoğurt|290|E", "Ekstra et|990|E", "Ekstra kaşar|390|E",
            // Ek çeşitler
            "Lahmacun menü (2 lahmacun + ayran)|3190|M", "Pide menü (+ çorba, ayran)|4290|M", "Kebap menü (kebap + çorba + içecek)|5490|M", "Aile döner kovası (4 kişilik)|11990|M",
            "Beyti sarma|4290|A", "Ali Nazik|4490|A", "Kuzu şiş|4590|A", "Ciğer şiş|3690|A",
            "Tavuk kanat (8)|2990|A", "Kanat + pirzola karışık|5990|A", "Çiğ köfte dürüm|1590|A", "Kokoreç (yarım)|2490|A",
            "Kokoreç (tam)|3990|A", "Islak hamburger (2)|1990|A", "Sucuklu pide|2990|A", "Kuşbaşılı pide|3490|A",
            "Yumurtalı pide|2290|A", "Tantuni dürüm|2490|A", "Etli ekmek|2690|A", "Kumpir|2790|A",
            "Mantı|2990|A", "Karnıyarık + pilav|3290|A", "Tavuk pilav|2190|A", "Patlıcan kebap|4290|A",
            "Yayla çorbası|990|C", "İşkembe çorbası|1490|C", "Kelle paça|1690|C", "Tarhana çorbası|990|C",
            "Tavuk suyu çorba|1090|C", "Mevsim salata|1190|Y", "Gavurdağı salata|1390|Y", "Haydari|790|Y",
            "Acılı ezme|790|Y", "Közlenmiş biber|890|Y", "Yaprak sarma (6)|1290|Y", "İçli köfte (2)|1390|Y",
            "Sigara böreği (4)|1190|Y", "Turşu|490|Y", "Ayran (büyük)|690|I", "Açık ayran (köpüklü)|790|I",
            "Şalgam (acılı)|590|I", "Gazoz 0,33 L|590|I", "Soda|390|I", "Meyve suyu 0,2 L|490|I",
            "Nar ekşili limonata|890|I", "Ice tea 0,33 L|590|I", "Fıstıklı baklava (4 dilim)|2790|T", "Şöbiyet (2)|1590|T",
            "Katmer|2690|T", "Fırın sütlaç|1290|T", "Aşure|1190|T", "Kemalpaşa|1190|T",
            "Revani|990|T", "Trileçe|1390|T", "Maraş dondurma|1290|T", "Ekstra lavaş|290|E",
            "Közlenmiş domates|290|E", "Turşu ekstra|290|E", "Ekstra soğan salatası|290|E",
        };

        static readonly string[] Starbocks =
        {
            "Espresso|890|K", "Doppio|1090|K", "Americano (Tall)|1290|K", "Americano (Grande)|1490|K", "Latte (Tall)|1590|K",
            "Latte (Grande)|1790|K", "Latte (Venti)|1990|K", "Cappuccino (Tall)|1590|K", "Cappuccino (Grande)|1790|K", "Flat White|1790|K",
            "Caramel Macchiato (Grande)|2090|K", "Mocha (Grande)|2090|K", "White Mocha (Grande)|2190|K", "Chai Tea Latte|1890|K",
            "Matcha Latte|1990|K", "Sıcak çikolata|1690|K", "Pumpkin Spice Latte|2290|K", "Caramel Frappuccino|2290|K",
            "Mocha Frappuccino|2290|K", "Java Chip Frappuccino|2390|K", "Çilekli Frappuccino|2190|K", "Iced Latte|1890|K",
            "Cold Brew|1790|K", "Iced Shaken Espresso|1990|K", "Mango-ejder meyveli Refresha|1990|I", "Earl Grey çay|990|I",
            "Taze portakal suyu|1290|I", "Su 0,5 L|590|I",
            "Kruvasan|990|Y", "Çikolatalı kruvasan|1190|Y", "Bademli kruvasan|1290|Y", "Tarçınlı çörek|1290|Y", "Yaban mersinli muffin|1190|Y",
            "Çikolatalı muffin|1190|Y", "Krem peynirli bagel|1390|Y", "Muzlu kek|1190|Y",
            "Tavuklu sandviç|2290|A", "Ton balıklı sandviç|2190|A", "Mozzarella-domates panini|2290|A", "Hindi füme sandviç|2390|A",
            "Tavuklu wrap|2190|A", "Yulaf kasesi|1690|A",
            "New York cheesecake|1690|T", "Havuçlu kek|1590|T", "Brownie|1190|T", "Çikolatalı kurabiye|790|T", "Cake pop|690|T", "Tiramisu|1790|T",
            "Ekstra shot|290|E", "Yulaf / badem sütü|290|E", "Karamel şurubu|250|E", "Vanilya şurubu|250|E",
            // Ek çeşitler
            "Latte (Short)|1390|K", "Americano (Venti)|1690|K", "Cappuccino (Venti)|1990|K", "Caffè Misto|1490|K",
            "Cortado|1490|K", "Macchiato (espresso)|1090|K", "Caramel Macchiato (Tall)|1890|K", "Caramel Macchiato (Venti)|2290|K",
            "Toffee Nut Latte|2190|K", "Hazelnut Latte|2090|K", "Vanilla Latte|1990|K", "Cinnamon Dolce Latte|2190|K",
            "Gingerbread Latte|2290|K", "Honey Oat Latte|2090|K", "Coconut Latte|2090|K", "Iced Caramel Macchiato|2190|K",
            "Iced Mocha|2190|K", "Iced Americano|1490|K", "Iced Matcha Latte|2090|K", "Iced Chai Latte|1990|K",
            "Vanilla Cream Cold Brew|1990|K", "Salted Caramel Cold Brew|2090|K", "Nitro Cold Brew|2090|K", "Vanilla Frappuccino|2190|K",
            "White Chocolate Frappuccino|2390|K", "Matcha Frappuccino|2290|K", "Espresso Frappuccino|2090|K", "Double Chocolate Chip Frappuccino|2390|K",
            "Çilekli Refresha|1990|I", "Limonata|1290|I", "Taze elma suyu|1290|I", "Yeşil çay|990|I",
            "Bitki çayı (papatya)|990|I", "Hibiskus çayı|990|I", "Sıcak süt|990|K", "Maden suyu|690|I",
            "Peynirli kruvasan|1290|Y", "Jambonlu-peynirli kruvasan|1590|Y", "Pain au raisin|1290|Y", "Elmalı turta dilimi|1190|Y",
            "Limonlu kek|1090|Y", "Mermer kek|1090|Y", "Scone|990|Y", "Simit|690|Y",
            "Pretzel|890|Y", "Avokadolu tost|2390|A", "Kaşarlı tost|1590|A", "Sucuklu tost|1790|A",
            "Somonlu bagel|2690|A", "Vegan wrap|2190|A", "Yoğurt parfe (granola)|1490|A", "Meyve kasesi|1590|A",
            "Quiche Lorraine|1990|A", "Red velvet kek|1690|T", "Lotus cheesecake|1790|T", "Frambuazlı cheesecake|1790|T",
            "San Sebastian cheesecake|1890|T", "Çikolatalı pasta dilimi|1690|T", "Macaron (3'lü)|1290|T", "Madlen (2)|790|T",
            "Yulaflı kurabiye|790|T", "Fındıklı kurabiye|790|T", "Krema (whipped)|250|E", "Fındık şurubu|250|E",
            "Karamel sos|250|E", "Laktozsuz süt|290|E", "Hindistan cevizi sütü|290|E",
        };

        static readonly string[] SzparExpress =
        {
            // Fırın
            "Beyaz ekmek 1 kg|690|F", "Tam buğday ekmeği 500 g|890|F", "Kifli (adet)|69|F", "Peynirli poğaça|249|F", "Kakaolu çörek|349|F",
            "Tost ekmeği|790|F", "Kaiser zsemle (adet)|89|F",
            // Süt ürünleri
            "Süt 1 L (%2,8)|399|L", "Laktozsuz süt 1 L|549|L", "Kefir 0,5 L|349|L", "Meyveli yoğurt 150 g|199|N", "Sade yoğurt 400 g|449|N",
            "Tereyağı 200 g|999|N", "Trappista peyniri 300 g|1490|N", "Krem peynir 200 g|699|N", "Ekşi krema 330 g|549|L",
            "Túró Rudi|229|N", "Yumurta (10'lu)|1290|F", "Mozzarella 125 g|599|N",
            // Şarküteri ve et
            "Párizsi 200 g|699|N", "Szalámi 100 g|899|N", "Sosis 400 g|1290|N", "Tavuk göğsü 500 g|1890|N", "Kıyma 500 g|1990|N",
            "Hindi füme 100 g|799|N",
            // Meyve sebze
            "Elma 1 kg|599|N", "Muz 1 kg|699|N", "Portakal 1 kg|799|N", "Domates 1 kg|1290|F", "Salatalık|499|N", "Patates 2 kg|899|N",
            "Kuru soğan 1 kg|399|N", "Çarliston biber (3'lü)|799|N", "Limon 500 g|699|N", "Üzüm 500 g|1190|F",
            // Kuru gıda
            "Makarna 500 g|449|N", "Pirinç 1 kg|799|N", "Un 1 kg|349|N", "Şeker 1 kg|499|N", "Ayçiçek yağı 1 L|899|L", "Domates sos 500 g|699|F",
            "Ton balığı konservesi|899|N", "Mısır konservesi|499|N", "Öğütülmüş kahve 250 g|2190|N", "Çay (20 poşet)|899|N", "Akasya balı 500 g|1990|F",
            "Kayısı reçeli|999|F", "Fındık kreması 400 g|1990|F", "Mısır gevreği|1290|N", "Yulaf ezmesi 500 g|699|N",
            // Atıştırmalık
            "Cips 140 g|799|N", "Sütlü çikolata 100 g|549|N", "Kraker|499|N", "Gofret|299|N", "Bisküvi|499|N", "Tuzlu fıstık 150 g|799|N",
            "Sakız|349|N",
            // İçecek
            "Su 1,5 L|199|L", "Su 6×1,5 L|1190|L", "Maden suyu 1,5 L|249|L", "Kola 1,75 L|899|L", "Kola 0,5 L|449|L", "Meyve suyu 1 L|699|L",
            "Enerji içeceği 0,25 L|399|L", "Bira 0,5 L (kutu)|449|L", "Bira (6'lı)|2490|L", "Kırmızı şarap 0,75 L|2490|F", "Soğuk çay 1,5 L|599|L",
            // Ev ve bakım
            "Tuvalet kağıdı (8'li)|2290|N", "Kağıt havlu (2'li)|999|N", "Bulaşık deterjanı|799|L", "Çamaşır deterjanı 1,5 L|3490|L",
            "Çöp poşeti|699|N", "Diş macunu|899|N", "Şampuan|1490|L", "Sıvı sabun|599|L", "Islak mendil|699|N",
            // Dondurulmuş
            "Dondurma 1 L|1990|N", "Dondurulmuş pizza|1490|N", "Dondurulmuş patates 1 kg|1290|N",
            // Ek çeşitler
            "Çavdar ekmeği|790|F", "Baget|399|F", "Kakaolu kalács|899|F", "Haşhaşlı rulo (bejgli dilim)|499|F",
            "Hamburger ekmeği (6'lı)|899|F", "Lavaş (6'lı)|799|N", "Tortilla (6'lı)|899|N", "Süt 1 L (%1,5)|379|L",
            "Süt 1 L (%3,5)|429|L", "Kakaolu süt 0,5 L|399|L", "Krema (pişirme) 200 ml|449|L", "Krem şanti 200 ml|549|L",
            "Labne 200 g|699|N", "Gouda dilim 150 g|899|N", "Parmesan 100 g|1490|N", "Feta peyniri 200 g|1190|N",
            "Lor peyniri (túró) 250 g|699|N", "Puding (çikolatalı)|299|N", "Sütlaç (kap)|349|N", "Margarin 250 g|599|N",
            "Jambon dilim 100 g|799|N", "Pastırma 100 g|1590|N", "Sucuk 200 g|1290|N", "Kabanos 200 g|999|N",
            "Pick szalámi 100 g|1290|N", "Kolbász 300 g|1490|N", "Hindi göğsü 500 g|2290|N", "Bütün tavuk 1,5 kg|2990|N",
            "Domuz pirzola 500 g|2190|N", "Somon fileto 250 g|2990|N", "Dondurulmuş karides 300 g|2490|N", "Balık kroket|1390|N",
            "Armut 1 kg|899|N", "Kiraz 500 g|1990|F", "Çilek 500 g|1490|F", "Kivi (6'lı)|899|N",
            "Mandalina 1 kg|799|N", "Avokado|599|N", "Ananas|1290|N", "Karpuz (dilim)|899|N",
            "Mantar 250 g|699|F", "Brokoli|699|N", "Havuç 1 kg|399|N", "Kabak|399|N",
            "Patlıcan|499|N", "Marul|499|F", "Roka|499|F", "Sarımsak (3'lü)|399|N",
            "Zencefil 100 g|299|N", "Maydanoz|299|N", "Taze soğan|299|N", "Bulgur 1 kg|799|N",
            "Mercimek 1 kg|899|N", "Nohut konservesi|499|N", "Fasulye konservesi|499|N", "Spagetti 500 g|499|N",
            "Penne 500 g|499|N", "Erişte 500 g|699|N", "Kuskus 500 g|799|N", "Ketçap 500 g|699|N",
            "Mayonez 400 g|899|N", "Hardal|499|N", "Soya sosu|799|L", "Sirke 1 L|399|L",
            "Zeytinyağı 0,5 L|2490|L", "Tuz 1 kg|199|N", "Karabiber|599|N", "Toz biber (pirospaprika)|699|N",
            "Kekik|399|N", "Kakao 200 g|999|N", "Hazır çorba|299|N", "Hazır noodle|299|N",
            "Kurabiye karışımı|899|N", "Kabartma tozu|99|N", "Vanilya şekeri|99|N", "Pirinç unu 500 g|599|N",
            "Zeytin 400 g|1290|F", "Turşu kavanoz|899|F", "Közlenmiş biber kavanoz|1190|F", "Lecsó konservesi|899|F",
            "Tahin 300 g|1290|F", "Pekmez|1190|F", "Helva 300 g|1390|N", "Çikolatalı gofret (paket)|699|N",
            "Jelibon|499|N", "Patlamış mısır|599|N", "Kuru üzüm 200 g|699|N", "Fındık 150 g|1490|N",
            "Badem 150 g|1590|N", "Çekirdek 200 g|599|N", "Pirinç patlağı|399|N", "Mısır cipsi (tortilla)|899|N",
            "Protein bar|599|N", "Kahve kapsül (10'lu)|1990|N", "Hazır kahve (3'ü 1 arada, 10'lu)|1290|N", "Filtre kahve 500 g|3490|N",
            "Meyve çayı|799|N", "Portakal suyu 1 L (%100)|899|L", "Elma suyu 1 L|699|L", "Vişne nektarı 1 L|699|L",
            "Şeftali nektarı 1 L|699|L", "Maden suyu 0,5 L|149|L", "Gazoz 1,5 L|599|L", "Sprite 1,75 L|899|L",
            "Fanta 1,75 L|899|L", "Ice tea şeftali 1,5 L|599|L", "Ayran 1 L|599|L", "Kefir 1 L|599|L",
            "Soğuk kahve kutu|499|L", "Spor içeceği 0,5 L|499|L", "Bira 0,5 L (şişe)|499|F", "Alkolsüz bira 0,5 L|449|L",
            "Radler 0,5 L|449|L", "Beyaz şarap 0,75 L|2290|F", "Rosé şarap 0,75 L|2390|F", "Köpüklü şarap|2990|F",
            "Pálinka 0,5 L|4490|F", "Fröccs hazır (kutu)|499|L", "Diş fırçası|699|N", "Deodorant|1290|N",
            "Duş jeli|999|L", "Tıraş bıçağı (4'lü)|1990|N", "Pamuk|399|N", "Kulak çubuğu|399|N",
            "Hijyenik ped|999|N", "El kremi|899|N", "Güneş kremi|2990|L", "Yüzey temizleyici|899|L",
            "Çamaşır suyu|599|L", "Cam sil|799|L", "Sünger (5'li)|499|N", "Alüminyum folyo|899|N",
            "Streç film|699|N", "Pişirme kağıdı|699|N", "Peçete|399|N", "Mum|599|N",
            "Kibrit|99|N", "Dondurulmuş ıspanak|799|N", "Dondurulmuş börek|1490|N", "Dondurulmuş köfte|1990|N",
            "Dondurulmuş balık fileto|1990|N", "Çubuk dondurma|399|N", "Külah dondurma|499|N", "Kedi maması (yaş, 4'lü)|1190|N",
            "Köpek ödülü|899|N", "Bebek bisküvisi|699|N", "Bebek mendili|799|N",
        };

        static readonly string[] Market24Extras =
        {
            "Sigara kağıdı|349|N", "Çakmak|399|N", "Bira 0,5 L (şişe)|549|F", "Pálinka 0,5 L|4990|F", "Vodka 0,5 L|3990|F", "Unicum 0,5 L|5490|F",
            "Mikrodalga sandviç|1290|N", "Hazır noodle|599|N", "Prezervatif (3'lü)|1990|N", "Ağrı kesici|990|N", "Telefon şarj kablosu|2990|N",
            "Buz torbası|699|N", "Mangal kömürü|1990|N",
            // Ek çeşitler
            "Gece sandviçi|990|N", "Hazır salata|1190|N", "Sıcak börek (adet)|599|N", "Sosisli (ısıtmalı)|790|N",
            "Pizza dilim (ısıtmalı)|890|N", "Kahve (makineden)|490|L", "Enerji içeceği 0,5 L|599|L", "Bira 4'lü (kutu)|1990|L",
            "Cin 0,5 L|4290|F", "Viski 0,5 L|5990|F", "Rom 0,5 L|4990|F", "Tonik 0,25 L|399|L",
            "Meyve şarabı|1990|F", "Kumanda pili|899|N", "Sinek ilacı|1490|N", "Diş ipi|699|N",
            "Islak mendil (cep)|299|N", "Şemsiye|2990|N", "Kulaklık|3990|N", "Sakız (paket)|399|N",
            "Nane şekeri|349|N",
        };

        static readonly string[] InterSzparExtras =
        {
            "Su 6×1,5 L (koli)|1090|L", "Bebek bezi (paket)|5490|N", "Bebek maması|2490|N", "Çamaşır deterjanı 3 L|5990|L", "Yumuşatıcı 2 L|2490|L",
            "Kedi maması 2 kg|3990|N", "Köpek maması 4 kg|6990|N", "Kedi kumu 10 L|2990|N", "Tavuk butu 1 kg|1990|N", "Somon fileto 400 g|4490|N",
            "Dana kuşbaşı 500 g|3990|N", "Zeytinyağı 1 L|4290|L", "Çekirdek kahve 1 kg|7990|N", "Kağıt havlu (4'lü)|1990|N", "Peynir 1 kg|4990|N",
            "Patlamış mısır (mikrodalga)|799|N", "Pirinç 5 kg|3490|N", "Un 5 kg|1590|N", "Ayçiçek yağı 3 L|2490|L", "Domates konservesi (4'lü)|1490|N",
            "Kola 6×1,75 L|4990|L", "Bira 24'lü koli|9990|L", "Maden suyu 6×1,5 L|1390|L", "Dondurulmuş sebze 1 kg|1290|N", "Bulaşık makinesi tableti|4490|N",
            "Çöp poşeti büyük|1290|N", "Pil (4'lü)|1990|N", "Ampul|990|F", "Meyve sepeti (karışık)|2990|N", "Dondurma 2 L|2990|N",
            // Ek çeşitler
            "Kahvaltılık kutu (peynir çeşitleri)|3990|N", "Tavuk kanat 1 kg|2490|N", "Kuzu but 1 kg|6990|N", "Hindi bütün|7990|N",
            "Dana bonfile 500 g|7990|N", "Sucuk 1 kg|4990|N", "Meyve suyu 6×1 L|3490|L", "Süt 12×1 L|4290|L",
            "Yumurta (30'lu)|3490|F", "Yoğurt 2 kg|1990|N", "Tereyağı 1 kg|4490|N", "Bal 1 kg|4490|F",
            "Fındık kreması 1 kg|4290|F", "Makarna 5×500 g|1990|N", "Konserve ton (6'lı)|4490|N", "Çay 1 kg (dökme)|2990|N",
            "Kahve kapsül (50'li)|8990|N", "Şeker 5 kg|2290|N", "Mangal kömürü 5 kg|2990|N", "Mangal seti|9990|N",
            "Piknik tabağı (50'li)|1290|N", "Parti bardağı (50'li)|999|N", "Tencere seti|19990|N", "Tava 28 cm|7990|N",
            "Çaydanlık|8990|N", "Bardak seti (6'lı)|3990|F", "Tabak seti (6'lı)|5990|F", "Saklama kabı seti|3990|N",
            "Nevresim takımı|11990|N", "Havlu (2'li)|4990|N", "Yastık|4990|N", "Çamaşır sepeti|2990|N",
            "Kova + paspas|4990|N", "Elektrikli süpürge torbası|2490|N", "Ampul LED (4'lü)|3490|F", "Uzatma kablosu|2990|N",
            "Pil şarj cihazı|5990|N", "Oyuncak araba|2990|N", "Boyama kitabı|1290|N", "Okul defteri (5'li)|1490|N",
            "Kalem seti|1990|N", "A4 kağıt (500)|2490|N", "Şampuan 1 L|2490|L", "Diş macunu (3'lü)|1990|N",
            "Tıraş köpüğü|1290|L", "Bebek bezi (jumbo)|8990|N", "Bebek şampuanı|1490|L", "Kedi maması 10 kg|14990|N",
            "Köpek maması 15 kg|19990|N",
        };

        static readonly string[] McDanolds =
        {
            "Big Mec Menü|3590|M", "Dupla Big Mec Menü|4290|M", "McRoyal Menü|3790|M", "McChicken Menü|3190|M", "McNuggets (9) Menü|3290|M",
            "Filet-o-Fish Menü|3190|M", "Cheeseburger Menü|2490|M", "Tavuk Wrap Menü|3190|M", "McSpicy Menü|3390|M", "Happy Meal|2290|M",
            "Big Mec|2090|A", "Dupla Big Mec|2790|A", "McRoyal|2290|A", "McChicken|1590|A", "McSpicy|1890|A", "Filet-o-Fish|1590|A",
            "Hamburger|690|A", "Cheeseburger|790|A", "Dupla Cheeseburger|1290|A", "McNuggets (6)|1390|A", "McNuggets (9)|1890|A",
            "McNuggets (20)|3690|A", "Tavuk wrap|1890|A", "Veggie burger|1790|A",
            "Patates (küçük)|590|Y", "Patates (orta)|790|Y", "Patates (büyük)|950|Y", "Elma dilimleri|490|Y", "Sezar salata|1590|Y",
            "Mozzarella çubukları (4)|1290|Y",
            "Kola 0,4 L|550|I", "Kola 0,5 L|650|I", "Kola Zero 0,5 L|650|I", "Fanta 0,5 L|650|I", "Sprite 0,5 L|650|I", "Ice tea 0,5 L|650|I",
            "Su 0,5 L|450|I", "Portakal suyu|590|I", "McCafé latte|990|K", "McCafé cappuccino|890|K", "Espresso|590|K",
            "Çilekli milkshake|990|I", "Çikolatalı milkshake|990|I",
            "McFlurry kurabiyeli|990|T", "McFlurry karamelli|990|T", "Sundae|690|T", "Elmalı turta|590|T", "Çikolatalı muffin|790|T", "Donut|590|T",
            "Ketçap|150|E", "Big Mec sos|190|E", "Barbekü sos|190|E", "Tatlı ekşi sos|190|E", "Acı sos|190|E",
            // Ek çeşitler
            "McCrispy Menü|3490|M", "Double Royal Menü|4490|M", "Big Tasty Menü|4290|M", "McWrap Menü|3290|M",
            "Share Box (20 nugget + 2 büyük patates)|6490|M", "Kahvaltı menüsü (McMuffin + kahve + hash brown)|2490|M", "McCrispy|1990|A", "Double Royal|2990|A",
            "Big Tasty|2890|A", "Big Tasty Bacon|3190|A", "Chicken Mythic|2390|A", "Spicy McChicken|1690|A",
            "McMuffin (yumurtalı)|1190|A", "McMuffin (sosisli)|1290|A", "Hash brown|490|Y", "Pancake (3)|1290|A",
            "Kanat (6)|1890|A", "Cheddar melt|1990|A", "Grilled tavuk salata|2190|A", "Fish wrap|1890|A",
            "Patates (paylaşım boyu)|1290|Y", "Peynir soslu patates|1190|Y", "Soğan halkası (6)|990|Y", "Mısır kasesi|690|Y",
            "Havuç çubukları|490|Y", "Kola 0,25 L|450|I", "Kola 1 L|990|I", "Fanta 0,4 L|550|I",
            "Limonata|750|I", "Ice tea limon 0,5 L|650|I", "Meyve suyu (elma)|590|I", "Maden suyu|450|I",
            "Süt (çocuk)|390|I", "McCafé americano|690|K", "McCafé flat white|1090|K", "McCafé karamel latte|1190|K",
            "McCafé mocha|1190|K", "McCafé iced latte|1190|K", "Sıcak çikolata|790|K", "McCafé çay|490|I",
            "Vanilyalı milkshake|990|I", "Muzlu milkshake|990|I", "Frappé karamel|1290|K", "McFlurry çikolatalı|990|T",
            "McFlurry çilekli|990|T", "Külah dondurma|290|T", "Sufle|890|T", "Çikolatalı kurabiye|490|T",
            "Cheesecake|990|T", "Macaron|390|T", "Croissant|690|Y", "Mayonez|150|E",
            "Ranch sos|190|E", "Köri sos|190|E", "Hardal sos|150|E", "Ekstra peynir|250|E",
        };

        static readonly string[] PizzaHatExtras =
        {
            "2 orta pizza + 1,25 L kola menü|8490|M", "Aile menüsü (aile pizza + 6 kanat + 1,25 L kola)|9990|M", "Öğle menüsü (küçük pizza + içecek)|2990|M",
            "Sarımsaklı ekmek|990|Y", "Peynirli sarımsaklı ekmek|1290|Y", "Tavuk kanat (6)|2190|Y", "Tavuk kanat (12)|3890|Y", "Patates|990|Y",
            "Mozzarella çubukları (6)|1490|Y", "Bahçe salatası|1490|Y", "Makarna bolonez|2690|A", "Makarna carbonara|2790|A", "Lazanya|2990|A",
            "Kola 1,25 L|990|I", "Kola 0,5 L|690|I", "Fanta 1,25 L|990|I", "Sprite 1,25 L|990|I", "Su 0,5 L|450|I", "Bira 0,5 L|790|I",
            "Tiramisu|1390|T", "Çikolatalı sufle|1490|T", "Dondurma 0,5 L|1990|T", "Cheesecake|1390|T",
            "Ekstra peynir|490|E", "Sarımsaklı sos|290|E", "Barbekü sos|290|E", "Acı biber yağı|190|E",
            // Ek çeşitler
            "Öğrenci menüsü (orta pizza + içecek)|3490|M", "Parti menüsü (3 büyük pizza + 2 L kola)|14990|M", "Tek kişilik menü (küçük pizza + patates + içecek)|3290|M", "Calzone|3290|A",
            "Fırın tavuk kanat (8)|2690|A", "Penne arrabbiata|2490|A", "Fettuccine Alfredo|2890|A", "Fırında mantarlı makarna|2790|A",
            "Tavuk Sezar salata|2490|A", "Ton balıklı salata|2390|A", "Patates kızartması (büyük)|1290|Y", "Patates dilim (baharatlı)|1190|Y",
            "Sosis rulo (6)|1290|Y", "Soğan halkası (8)|1190|Y", "Peynirli çubuk ekmek|1190|Y", "Tavuk nugget (8)|1590|Y",
            "Jalapeño popper (6)|1490|Y", "Kola 2 L|1290|I", "Kola Zero 1,25 L|990|I", "Ice tea 1,5 L|990|I",
            "Limonata 0,5 L|790|I", "Maden suyu 0,5 L|450|I", "Kırmızı şarap (kadeh)|1190|I", "Alkolsüz bira|690|I",
            "Brownie|990|T", "Profiterol|1390|T", "Panna cotta|1290|T", "Çikolatalı pizza (tatlı)|2490|T",
            "Dondurma (kap)|990|T", "Cookie dough|1190|T", "Ranch sos|290|E", "Pesto sos|390|E",
            "Ekstra sucuk|590|E", "Ekstra mantar|390|E", "Ekstra jalapeño|290|E", "Kenar peynir dolgu|690|E",
        };

        static readonly (string name, int medium)[] Pizzas =
        {
            ("Margarita", 2990), ("Pepperoni", 3490), ("Karışık", 3790), ("Sucuklu", 3590), ("Mantarlı", 3290), ("Barbekü tavuk", 3790),
            ("Ton balıklı", 3590), ("Dört peynirli", 3790), ("Vejetaryen", 3390), ("Hawaii", 3490), ("Acılı Meksika", 3690), ("Prosciutto", 3990),
            ("Kavurmalı", 3990), ("Pastırmalı", 3990), ("Füme hindi", 3590), ("Truffle mantar", 4290), ("Diavola", 3690), ("Quattro stagioni", 3790),
            ("Kebap pizza", 3790), ("Somonlu", 4490), ("Lahmacun pizza", 3290), ("Vegan", 3490),
        };

        static readonly string[] KFG =
        {
            "Kova Menü (8 parça + 2 patates + 1,5 L kola)|7990|M", "Zinger Menü|3290|M", "Twister Menü|3190|M", "Strips (5) Menü|3290|M",
            "Hot Wings (8) Menü|3190|M", "Klasik Burger Menü|2890|M",
            "Kova (6 parça)|4490|A", "Kova (10 parça)|6990|A", "Kova (15 parça)|9490|A", "Zinger burger|1990|A", "Twister wrap|1890|A",
            "Strips (3)|1490|A", "Strips (6)|2590|A", "Hot Wings (6)|1690|A", "Hot Wings (12)|2990|A", "Tavuk but (2)|1590|A",
            "Popcorn tavuk|1390|A", "Klasik burger|1490|A",
            "Patates (orta)|790|Y", "Patates (büyük)|990|Y", "Coleslaw|690|Y", "Haşlanmış mısır|690|Y", "Patates püresi|790|Y",
            "Kola 0,5 L|650|I", "Kola 1,5 L|1190|I", "Fanta 0,5 L|650|I", "Ice tea 0,5 L|650|I", "Su 0,5 L|450|I",
            "Çikolatalı sufle|990|T", "Sundae|690|T", "Brownie|890|T",
            "Barbekü sos|190|E", "Sarımsaklı sos|190|E", "Acı sos|190|E", "Ballı hardal|190|E",
            // Ek çeşitler
            "Kova Menü (12 parça + 3 patates + 2 L kola)|10990|M", "Tavuk But Menü (3 parça)|3490|M", "Popcorn Menü|2790|M", "Wings Box (16 kanat + patates)|5990|M",
            "Kahraman Menü (çocuk)|2190|M", "Kova (21 parça)|12990|A", "Tavuk göğüs (orijinal)|1290|A", "Tavuk but (orijinal, 3)|2290|A",
            "Fillet burger|1790|A", "Tower burger|2490|A", "Double down|2390|A", "BBQ burger|2190|A",
            "Strips (10)|3990|A", "Hot wings (20)|4690|A", "Popcorn tavuk (büyük)|2190|A", "Tavuk wrap (acılı)|1990|A",
            "Tavuk salata|2190|A", "Patates (küçük)|590|Y", "Peynirli patates|1190|Y", "Mısır koçanı|790|Y",
            "Pilav|790|Y", "Soğan halkası|890|Y", "Biscuit (tereyağlı)|490|Y", "Fasulye (BBQ)|690|Y",
            "Kola 0,4 L|550|I", "Kola 1 L|990|I", "Kola Zero 0,5 L|650|I", "Sprite 0,5 L|650|I",
            "Limonata|750|I", "Ice tea şeftali 0,5 L|650|I", "Milkshake çikolatalı|1090|I", "Kahve|590|K",
            "Krispy kreme donut|690|T", "Cheesecake|990|T", "Dondurma (vanilya)|590|T", "Elmalı turta|690|T",
            "Kurabiye|490|T", "Ketçap|150|E", "Mayonez|150|E", "Garlic mayo|190|E",
            "Buffalo sos|190|E", "Ekstra peynir|250|E",
        };

        static readonly string[] SushiSamurai =
        {
            "Sushi seti (16 parça)|5990|M", "Sushi tabağı (24 parça)|8490|M", "Samurai seti (40 parça)|13490|M", "Öğle bento (tavuk teriyaki)|3990|M",
            "California roll (8)|2990|A", "Philadelphia roll (8)|3290|A", "Spicy tuna roll (8)|3390|A", "Dragon roll (8)|3990|A",
            "Rainbow roll (8)|4290|A", "Ebi tempura roll (8)|3490|A", "Avokado maki (6)|1690|A", "Salatalık maki (6)|1490|A",
            "Somon maki (6)|1990|A", "Ton maki (6)|2190|A", "Somon nigiri (2)|1390|A", "Ton nigiri (2)|1590|A", "Karides nigiri (2)|1490|A",
            "Yılan balığı nigiri (2)|1690|A", "Somon sashimi (6)|2890|A", "Ton sashimi (6)|3290|A", "Tavuk teriyaki|3290|A", "Yakisoba|2990|A",
            "Tempura karides (5)|2690|A",
            "Miso çorbası|990|C", "Tonkotsu ramen|3490|C", "Tavuklu ramen|3290|C",
            "Gyoza (5)|1790|Y", "Edamame|1190|Y", "Wakame salata|1290|Y", "Pilav|690|Y",
            "Yeşil çay|590|I", "Japon birası 0,33 L|990|I", "Kola 0,33 L|590|I", "Su 0,5 L|450|I", "Ramune|890|I",
            "Mochi (2)|1290|T", "Matcha dondurma|1190|T", "Dorayaki|990|T",
            "Ekstra wasabi|150|E", "Soya sosu|150|E", "Zencefil turşusu|150|E", "Spicy mayo|190|E",
            // Ek çeşitler
            "Vegan set (16 parça)|5490|M", "Somon severler seti (20 parça)|7990|M", "Aile tabağı (60 parça)|19990|M", "Ramen menü (+ gyoza + içecek)|5290|M",
            "Poke menü (+ miso + içecek)|4690|M", "Futomaki (5)|2490|A", "Crunchy roll (8)|3490|A", "Volcano roll (8)|3790|A",
            "Tempura somon roll (8)|3490|A", "Uramaki karides (8)|3290|A", "Cheese roll (8)|3190|A", "Unagi roll (8)|4290|A",
            "Somon temaki|1890|A", "Ton temaki|1990|A", "Karides temaki|1890|A", "Somon gunkan (2)|1590|A",
            "Somon tataki|3490|A", "Ton tataki|3990|A", "Somon poke kase|3990|A", "Ton poke kase|4290|A",
            "Tavuk poke kase|3490|A", "Tofu poke kase|3290|A", "Chirashi kase|4990|A", "Katsu curry|3490|A",
            "Tavuk katsu don|3290|A", "Teriyaki somon|4490|A", "Udon (tavuklu)|3190|A", "Udon (sebzeli)|2890|A",
            "Pad thai|2990|A", "Kızarmış pilav (tavuklu)|2490|A", "Takoyaki (6)|1990|A", "Shoyu ramen|3290|C",
            "Miso ramen|3390|C", "Spicy ramen|3490|C", "Vegan ramen|3190|C", "Gyoza (sebzeli, 5)|1590|Y",
            "Karaage tavuk|1990|Y", "Spring roll (4)|1490|Y", "Kimchi|990|Y", "Sushi pirinci|690|Y",
            "Tempura sebze|1690|Y", "Edamame (acılı)|1290|Y", "Seaweed salata|1290|Y", "Matcha latte|1490|K",
            "Yuzu limonata|1190|I", "Japon soğuk çayı|790|I", "Sake (küçük şişe)|2490|I", "Plum wine (kadeh)|1490|I",
            "Kola Zero 0,33 L|590|I", "Maden suyu|490|I", "Mochi dondurma (3)|1690|T", "Matcha cheesecake|1490|T",
            "Tempura muz|1290|T", "Taiyaki|990|T", "Susamlı dondurma|1190|T", "Ekstra avokado|390|E",
            "Teriyaki sos|190|E", "Unagi sos|190|E", "Wasabi mayo|190|E", "Ekstra pirinç|490|E",
        };

        static readonly string[] FishAndShips =
        {
            "Fish & chips menü (+ içecek)|3890|M", "Balık burger menü|2990|M",
            "Fish & chips|3290|A", "Büyük fish & chips|4290|A", "Kızarmış kalamar|2790|A", "Karides tava|3490|A", "Izgara somon|4490|A",
            "Izgara levrek|4290|A", "Balık burger|2290|A", "Balık dürüm|2190|A", "Midye tava|2490|A", "Fish taco (2)|2490|A",
            "Ton balıklı salata|2290|A", "Izgara alabalık|3690|A",
            "Halászlé (balık çorbası)|2590|C", "Deniz ürünleri çorbası|1890|C",
            "Patates|890|Y", "Tatlı patates kızartması|1090|Y", "Coleslaw|690|Y", "Pilav|790|Y", "Yeşil salata|990|Y", "Soğan halkası|890|Y",
            "Ev yapımı limonata|890|I", "Kola 0,5 L|690|I", "Su 0,5 L|450|I", "Bira 0,5 L|890|I", "Beyaz şarap (kadeh)|1290|I",
            "Limonlu cheesecake|1390|T", "Dondurma (2 top)|990|T", "Elmalı rétes|990|T",
            "Tartar sos|290|E", "Ekstra limon|100|E", "Sarımsaklı mayonez|290|E",
            // Ek çeşitler
            "Deniz ürünleri tabağı (2 kişilik)|9990|M", "Aile fish & chips kovası|11990|M", "Çocuk balık menüsü|2490|M", "Izgara çipura|4290|A",
            "Izgara hamsi|2990|A", "Hamsi tava|2790|A", "Kalamar ızgara|3490|A", "Ahtapot ızgara|5990|A",
            "Karides güveç|3990|A", "Midye dolma (10)|1990|A", "Somon burger|2790|A", "Karides burger|2690|A",
            "Balık ekmek|1690|A", "Paella|4490|A", "Deniz ürünlü makarna|3990|A", "Karidesli risotto|4290|A",
            "Levrek buğulama|4490|A", "Fish curry|3490|A", "Ton balığı steak|5490|A", "Midye çorbası|1890|C",
            "Karides çorbası|1990|C", "Bouillabaisse|2890|C", "Patates püresi|790|Y", "Haşlanmış patates|690|Y",
            "Izgara sebze|1190|Y", "Bezelye püresi (mushy peas)|690|Y", "Roka salatası|1090|Y", "Akdeniz salatası|1290|Y",
            "Sarımsaklı ekmek|790|Y", "Kalamar halkası (yan)|1390|Y", "Fröccs (şarap + soda)|990|I", "Rakı (tek)|1690|I",
            "Elma suyu|590|I", "Ice tea 0,5 L|690|I", "Soda|390|I", "Alkolsüz bira|790|I",
            "Rosé şarap (kadeh)|1290|I", "Çikolatalı sufle|1390|T", "Somlói galuska|1290|T", "Tiramisu|1290|T",
            "Meyve tabağı|1490|T", "Remoulade sos|290|E", "Acı sos|190|E", "Ekstra patates|590|E",
            "Limonlu mayonez|290|E",
        };

        static readonly string[] OtogarBufe =
        {
            "Lángos (sade)|990|A", "Lángos (ekşi krema, peynir)|1490|A", "Lángos (sarımsaklı)|1190|A", "Hot dog|990|A", "Büyük hot dog|1390|A",
            "Kaşarlı tost|890|A", "Karışık tost|1090|A", "Hamburger|1490|A", "Jambonlu sandviç|1190|A", "Gyros pita|1890|A", "Pizza dilim|790|A",
            "Kroket (6)|990|Y", "Patates|690|Y", "Pogácsa (3'lü)|590|Y", "Simit|490|Y", "Peynirli poğaça|290|Y",
            "Çay|290|I", "Kahve|390|K", "Kola 0,5 L|590|I", "Su 0,5 L|290|I", "Ayran|390|I", "Enerji içeceği|590|I", "Sıcak çikolata|490|K",
            "Kakaolu çörek|390|T", "Elmalı rétes|590|T", "Gofret|290|T", "Túrós batyu|490|T",
            "Ketçap|100|E", "Hardal|100|E",
            // Ek çeşitler
            "Lángos (peynirli)|1290|A", "Lángos (jambonlu)|1590|A", "Lángos (Nutella)|1290|A", "Sosisli sandviç|990|A",
            "Kızarmış tavuk sandviç|1490|A", "Börek (peynirli)|790|A", "Börek (patatesli)|790|A", "Kumru|1490|A",
            "Islak burger|990|A", "Kaşarlı simit|890|A", "Gözleme|1290|A", "Pide dilim|890|A",
            "Falafel sandviç|1290|A", "Zsíros kenyér|590|A", "Vadász sandviç|1190|A", "Patates (büyük)|890|Y",
            "Soğan halkası|790|Y", "Mısır (bardak)|590|Y", "Haşlanmış yumurta (2)|390|Y", "Turşu (bardak)|390|Y",
            "Türk kahvesi|490|K", "Cappuccino|590|K", "Latte|690|K", "Bitki çayı|290|I",
            "Meyve suyu 0,33 L|390|I", "Ice tea 0,5 L|490|I", "Gazoz|490|I", "Soda|290|I",
            "Boza|490|I", "Salep|590|K", "Kakaolu süt|390|I", "Kek dilimi|390|T",
            "Dondurma (kap)|490|T", "Krep (çikolatalı)|990|T", "Waffle|1290|T", "Lokma (6)|690|T",
            "Puding|390|T", "Fánk (krapfen)|390|T", "Mayonez|100|E", "Acı sos|100|E",
            "Ekstra peynir|250|E",
        };

        static readonly string[] VillaGourmet =
        {
            "Akşam menüsü (başlangıç + ana yemek + tatlı)|14990|M", "Şefin tadım menüsü (5 tabak)|24990|M",
            "Burrata salata|3990|A", "Kaz ciğeri|5990|A", "Dana carpaccio|4490|A", "Ton tartar|4290|A",
            "Dana bonfile|8990|A", "Kuzu pirzola|8490|A", "Ördek göğsü|6990|A", "Somon fileto|6490|A", "Trüflü risotto|5990|A",
            "Istakozlu linguine|9990|A", "Mantarlı tagliatelle|4990|A", "Wagyu burger|6990|A", "Levrek fileto|6490|A",
            "Kuşkonmaz kreması|2490|C", "Kestane çorbası|2290|C",
            "Trüflü patates|1990|Y", "Izgara sebze|1790|Y", "Patates püresi|1490|Y", "Roka-parmesan salata|1890|Y",
            "Kırmızı şarap (şişe)|6990|I", "Beyaz şarap (şişe)|6490|I", "Prosecco|5990|I", "Maden suyu 0,75 L|990|I", "Taze limonata|1290|I",
            "Espresso|790|K",
            "Tiramisu|2290|T", "Crème brûlée|2190|T", "Çikolatalı fondan|2390|T", "Panna cotta|1990|T", "Peynir tabağı|3490|T",
            "Ekmek sepeti|990|E", "Trüf yağı|790|E",
            // Ek çeşitler
            "İş yemeği menüsü (2 tabak + içecek)|9990|M", "Sevgililer menüsü (2 kişi, 3 tabak + şarap)|29990|M", "Istiridye (6)|6990|A", "Somon gravlax|3990|A",
            "Bruschetta tabağı|2490|A", "Kuzu kafes|9990|A", "Dana yanağı (ağır pişmiş)|7490|A", "Kaz budu confit|6990|A",
            "Mantı (trüflü)|4990|A", "Ravioli (ıstakozlu)|7990|A", "Gnocchi (adaçayı tereyağlı)|4490|A", "Risotto (deniz ürünlü)|6990|A",
            "Paccheri (dana ragù)|4990|A", "Mezgit fileto|5490|A", "Ton balığı tataki|5990|A", "Tavuk supreme|4990|A",
            "Vegan Wellington|5490|A", "Dana Wellington (2 kişilik)|17990|A", "Tomahawk steak (2 kişilik)|24990|A", "Gulyás (gurme)|2690|C",
            "Soğan çorbası (gratin)|2290|C", "Gazpacho|1990|C", "Kremalı ıspanak|1490|Y", "Trüflü makarna peyniri|2290|Y",
            "Kuşkonmaz (tereyağlı)|2490|Y", "Brüksel lahanası (pastırmalı)|1690|Y", "Polenta kızartması|1490|Y", "Taze ekmek (ekşi maya)|1190|Y",
            "Şampanya (şişe)|19990|I", "Tokaji Aszú (şişe)|12990|I", "Egri Bikavér (şişe)|7990|I", "Villányi kırmızı (şişe)|8990|I",
            "Badacsonyi beyaz (şişe)|6990|I", "Kokteyl (Negroni)|2990|I", "Mocktail|1690|I", "Cappuccino|990|K",
            "Tarte tatin|2290|T", "Dobos pasta (gurme)|2490|T", "Eszterházy pasta|2490|T", "Sorbet tabağı|1790|T",
            "Çikolata tabağı (el yapımı)|2990|T", "Profiterol (gurme)|2290|T", "Mille-feuille|2390|T", "Havyar (30 g)|9990|E",
            "Ekstra trüf rendesi|1990|E", "Peynir seçkisi ekstra|1490|E",
        };

        static readonly string[] BioSzpar =
        {
            "Organik süt 1 L|690|L", "Organik yumurta (10'lu)|1690|F", "Ekşi mayalı ekmek|1490|F", "Çavdar ekmeği|1290|F", "Organik tereyağı 200 g|1590|N",
            "Keçi peyniri 150 g|1890|N", "Organik yoğurt 400 g|799|N", "Badem sütü 1 L|1190|L", "Yulaf sütü 1 L|990|L", "Tofu 200 g|899|N",
            "Avokado (2'li)|1290|N", "Organik muz 1 kg|990|N", "Organik elma 1 kg|990|N", "Cherry domates 250 g|899|F", "Bebek ıspanak 150 g|799|N",
            "Organik havuç 1 kg|699|N", "Tatlı patates 1 kg|1290|N", "Organik limon 500 g|899|N", "Yaban mersini 125 g|1190|F",
            "Kinoa 500 g|1490|N", "Esmer pirinç 1 kg|1290|N", "Tam buğday makarna|899|N", "Granola 400 g|1690|N", "Chia tohumu 200 g|1290|N",
            "Badem 200 g|1990|N", "Ceviz 200 g|1890|N", "Hurma 250 g|1290|N", "Yerel akasya balı|2490|F", "Fıstık ezmesi 350 g|1690|F",
            "Bitter çikolata %85|1190|N", "Kombucha 0,33 L|899|L", "Soğuk sıkım portakal suyu 1 L|1890|L", "Maden suyu (cam) 0,75 L|590|F",
            "Organik kahve 250 g|3490|N", "Yeşil çay (organik)|1490|N", "Zeytinyağı (sızma) 0,5 L|3990|F", "Hindistan cevizi yağı|2490|N",
            "Organik tavuk göğsü 500 g|3290|N", "Somon (ASC) 250 g|3490|N", "Doğal sabun|1290|N", "Bambu diş fırçası|990|N",
            // Ek çeşitler
            "Organik kefir 0,5 L|799|L", "Hindistan cevizi yoğurdu|999|N", "Vegan peynir 200 g|1490|N", "Bio tavuk butu 500 g|2790|N",
            "Organik kıyma 500 g|3290|N", "Tempeh 200 g|1290|N", "Seitan 200 g|1390|N", "Ispanak (organik) 250 g|899|N",
            "Organik domates 500 g|1190|F", "Organik salatalık|599|N", "Organik brokoli|999|N", "Organik patates 2 kg|1390|N",
            "Organik soğan 1 kg|699|N", "Pancar 1 kg|699|N", "Organik çilek 250 g|1590|F", "Organik portakal 1 kg|1290|N",
            "Organik nar|899|N", "Organik mango|1190|N", "Frambuaz 125 g|1290|F", "Böğürtlen 125 g|1290|F",
            "Karabuğday 500 g|1190|N", "Amarant 500 g|1590|N", "Mercimek makarnası|1290|N", "Nohut unu|899|N",
            "Hindistan cevizi şekeri|1490|N", "Akçaağaç şurubu|2990|F", "Agave şurubu|1990|F", "Kaju 200 g|2490|N",
            "Fındık (organik) 200 g|2190|N", "Kabak çekirdeği 200 g|1290|N", "Keten tohumu 250 g|790|N", "Goji berry 150 g|1590|N",
            "Kuru kayısı (organik) 250 g|1390|N", "Yulaf barı|599|N", "Raw bar (hurmalı)|699|N", "Pirinç krakeri|599|N",
            "Kale cipsi|1290|N", "Organik cips|999|N", "Badem içecek (şekersiz)|1190|L", "Fındık içeceği 1 L|1290|L",
            "Kefir suyu|899|L", "Soğuk sıkım yeşil smoothie|1590|L", "Hindistan cevizi suyu|899|L", "Organik limonata|799|L",
            "Zencefil shot|599|L", "Matcha tozu 50 g|3990|N", "Rooibos çay|1290|N", "Organik bitki çayı|1190|N",
            "Kakao nibs|1690|N", "Doğal deodorant|2490|N", "Organik şampuan|2990|L", "Bambu pamuk çubuk|699|N",
            "Ekolojik deterjan|2990|L", "Bez çanta|990|N", "Cam pipet seti|1990|F",
        };

        // ================================================================ yükleme

        static Dictionary<string, Item[]> menus;

        static Item[] Parse(IEnumerable<string> rows, float priceMul = 1f)
        {
            var list = new List<Item>();
            var seen = new HashSet<string>();
            foreach (var r in rows)
            {
                var p = r.Split('|');
                if (p.Length < 3 || !seen.Add(p[0])) continue;   // aynı isimli ürün iki kez eklenmesin
                int price = int.Parse(p[1]);
                if (!Mathf.Approximately(priceMul, 1f)) price = Mathf.Max(50, Mathf.RoundToInt(price * priceMul / 10f) * 10 - 1);
                list.Add(new Item { name = p[0], price = price, cat = p[2][0] });
            }
            return list.ToArray();
        }

        static IEnumerable<string> PizzaRows()
        {
            foreach (var (n, m) in Pizzas)
            {
                yield return $"{n} pizza (orta 28 cm)|{m}|A";
                yield return $"{n} pizza (büyük 32 cm)|{Mathf.RoundToInt(m * 1.3f / 10f) * 10 - 10}|A";
                yield return $"{n} pizza (aile 40 cm)|{Mathf.RoundToInt(m * 1.75f / 10f) * 10 - 10}|A";
            }
            foreach (var r in PizzaHatExtras) yield return r;
        }

        static IEnumerable<string> Concat(string[] a, string[] b)
        {
            foreach (var x in a) yield return x;
            foreach (var x in b) yield return x;
        }

        static void Ensure()
        {
            if (menus != null) return;
            menus = new Dictionary<string, Item[]>
            {
                { "Burger Queen", Parse(BurgerQueen) },
                { "Döner Kral", Parse(DonerKral) },
                { "Starbocks", Parse(Starbocks) },
                { "Szpar Express", Parse(SzparExpress) },
                { "Market 24", Parse(Concat(SzparExpress, Market24Extras), 1.12f) },     // gece açık market: biraz pahalı
                { "McDanold's", Parse(McDanolds) },
                { "Pizza Hat", Parse(PizzaRows()) },
                { "InterSzpar", Parse(Concat(SzparExpress, InterSzparExtras), 0.93f) },  // hipermarket: biraz ucuz, toplu ürün
                { "KFG", Parse(KFG) },
                { "Sushi Samurai", Parse(SushiSamurai) },
                { "Fish & Ships", Parse(FishAndShips) },
                { "Otogar Büfé", Parse(OtogarBufe) },
                { "Villa Gourmet", Parse(VillaGourmet) },
                { "BioSzpar", Parse(BioSzpar) },
            };
        }

        /// <summary>Dükkanın bütün ürünleri (dükkan kartında menü göstermek için).</summary>
        public static Item[] Of(string shopName)
        {
            Ensure();
            return menus.TryGetValue(shopName, out var m) ? m : new Item[0];
        }

        // ================================================================ sepet

        static Item PickCat(Item[] menu, string cats)
        {
            var c = new List<Item>();
            foreach (var it in menu) if (cats.IndexOf(it.cat) >= 0) c.Add(it);
            return c.Count > 0 ? c[Random.Range(0, c.Count)] : menu[Random.Range(0, menu.Length)];
        }

        static bool HasCat(Item[] menu, char cat)
        {
            foreach (var it in menu) if (it.cat == cat) return true;
            return false;
        }

        static void AddItem(List<OrderItem> list, Item it, int qty = 1)
        {
            int idx = list.FindIndex(x => x.name == it.name);
            if (idx >= 0) { var o = list[idx]; o.qty += qty; list[idx] = o; }
            else list.Add(new OrderItem { name = it.name, qty = qty, unitPrice = it.price, cat = it.cat });
        }

        /// <summary>Hedef tutara yakın, gerçekçi bir sepet oluşturur. Sepet tutarı ürünlerin toplamıdır.</summary>
        public static List<OrderItem> Generate(ShopDef shop, int target)
        {
            Ensure();
            var list = new List<OrderItem>();
            if (!menus.TryGetValue(shop.name, out var menu) || menu.Length == 0)
            {
                list.Add(new OrderItem { name = "Sipariş", qty = 1, unitPrice = target, cat = 'N' });
                return list;
            }

            int guard = 0;
            if (shop.isMarket)
            {
                // Market: adetli çok ürün; ucuz ürünlerden daha çok adet
                while (Sum(list) < target * 0.9f && guard++ < 60)
                {
                    var it = menu[Random.Range(0, menu.Length)];
                    int maxQty = it.price < 150 ? 8 : it.price < 600 ? 4 : it.price < 1500 ? 2 : 1;
                    int q = Random.Range(1, maxQty + 1);
                    if (Sum(list) > 0 && Sum(list) + it.price * q > target * 1.3f) { q = 1; if (Sum(list) + it.price > target * 1.3f) continue; }
                    AddItem(list, it, q);
                }
                return list;
            }

            bool cafe = !shop.hotFood && HasCat(menu, 'K') && !HasCat(menu, 'M');   // kahveci
            bool hasMenus = HasCat(menu, 'M');
            // Kişi kişi sepet: her kişi menü ya da ana yemek (+ yan, + içecek) ya da kafede kahve (+ fırın / tatlı)
            while (Sum(list) < target * 0.85f && guard++ < 12)
            {
                if (cafe)
                {
                    AddItem(list, PickCat(menu, "K"));
                    if (Random.value < 0.55f) AddItem(list, PickCat(menu, "YT"));
                    if (Random.value < 0.2f) AddItem(list, PickCat(menu, "A"));
                    continue;
                }
                if (hasMenus && Random.value < 0.45f)
                {
                    AddItem(list, PickCat(menu, "M"));
                    continue;
                }
                AddItem(list, PickCat(menu, HasCat(menu, 'C') && Random.value < 0.15f ? "C" : "A"));
                if (Random.value < 0.55f) AddItem(list, PickCat(menu, "Y"));
                if (Random.value < 0.65f) AddItem(list, PickCat(menu, "IK"));
            }
            // Sipariş başına: ara sıra tatlı ve sos / ekstra
            if (!cafe && Random.value < 0.25f) AddItem(list, PickCat(menu, "T"));
            if (HasCat(menu, 'E') && Random.value < 0.3f) AddItem(list, PickCat(menu, "E"), Random.Range(1, 3));
            return list;
        }

        public static int Sum(List<OrderItem> items)
        {
            int s = 0;
            foreach (var i in items) s += i.Total;
            return s;
        }

        public static string Text(List<OrderItem> items, int maxItems = 6)
        {
            if (items == null || items.Count == 0) return "";
            var sb = new StringBuilder();
            for (int i = 0; i < items.Count && i < maxItems; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(items[i].qty > 1 ? $"{items[i].qty}× {items[i].name}" : items[i].name);
            }
            if (items.Count > maxItems) sb.Append($" +{items.Count - maxItems} ürün");
            return sb.ToString();
        }

        // ================================================================ hasar / eksik teslimat

        /// <summary>
        /// Kurye kaynaklı sorunda etkilenen ürün(ler) ve iade tutarı:
        ///  • döküldü: sıvı bir ürün (içecek, kahve, çorba, sıvı market ürünü); menüdeyse menünün içeceği (~%20'si)
        ///  • ezildi / ıslandı / kırıldı: kırılgan bir ürün (burger, tatlı, yumurta, ekmek…), 1 adet
        ///  • poşet unutuldu: 1–2 ürünün tamamı
        /// İade sadece etkilenen ürünün fiyatıdır, siparişin tamamı değil.
        /// </summary>
        public static int DamagedValue(List<OrderItem> items, string kind, out string what)
        {
            what = "bir ürün";
            if (items == null || items.Count == 0) return 0;

            if (kind == "forgot")
            {
                int n = Mathf.Min(items.Count, Random.value < 0.7f ? 1 : 2);
                var picked = new List<OrderItem>();
                var pool = new List<OrderItem>(items);
                for (int i = 0; i < n; i++) { int k = Random.Range(0, pool.Count); picked.Add(pool[k]); pool.RemoveAt(k); }
                int sum = 0;
                var names = new List<string>();
                foreach (var p in picked) { sum += p.Total; names.Add(p.qty > 1 ? $"{p.qty}× {p.name}" : p.name); }
                what = string.Join(", ", names);
                return sum;
            }

            bool spill = kind == "spill";
            var cands = new List<OrderItem>();
            foreach (var it in items)
            {
                bool liquid = it.cat == 'I' || it.cat == 'K' || it.cat == 'C' || it.cat == 'L' || it.cat == 'M';
                bool fragile = it.cat == 'A' || it.cat == 'T' || it.cat == 'Y' || it.cat == 'F' || it.cat == 'M';
                if (spill ? liquid : fragile) cands.Add(it);
            }
            if (cands.Count == 0) cands.AddRange(items);
            var c = cands[Random.Range(0, cands.Count)];
            if (c.cat == 'M')
            {
                // Menünün tamamı değil, etkilenen kısmı: içecek ya da yan ürün (~%20–30)
                what = spill ? $"{c.name} içindeki içecek" : $"{c.name} içindeki ürün";
                return Mathf.Max(300, Mathf.RoundToInt(c.unitPrice * (spill ? 0.2f : 0.35f)));
            }
            what = c.name;
            return c.unitPrice;
        }
    }
}
