using MvcMusic.Models;

namespace MvcMusic.Data
{
    public static class ProductData
    {
        public static List<Product> GetBaseProducts()
        {
            return new List<Product>
            {
                new Product
                {
                    Name = "Squier Sonic Stratocaster HSS MN Black",
                    Category = "Electric Guitar",
                    Brand = "Squier",
                    Price = 9966.26,
                    Stock = 99,
                    Description = "The Squier Sonic Series Stratocaster MN HSS Black according to Fender specifications is not only an optimal entry-level model, but also meets the expectations of experienced guitar lovers:inside with its typical sound palette and design. The poplar body with \"Contour-Body\" provides absolute wearing comfort and shines in a black finish. The bolt-on maple neck in a modern 'C' shape fits comfortably in every hand and ensures easy playability of the 21-fret maple fingerboard. Two Squier single coil pickups sit in neck and middle position and are supported by a humbucker at the bridge, which delivers rocking overdrive tones. With a 5-way switch and Master Volume, Tone 1 and Tone 2 controls, the familiar Stratocaster sound spectrum can still be easily adjusted from bright-bellish to deep-muted tones. Completing the Squier Sonic Strat is a vintage bridge with tremolo arm, the Squier neckplate, skunk stripe and sealed-cast tuning machines.",
                    ProductImages = new List<ProductImage> 
                    { 
                        new ProductImage { Url = "https://images.musicstore.de/images/0960/squier-sonic-stratocaster-hss-mn-black_1_GIT0060106-000.jpg" },
                        new ProductImage { Url = "https://images.musicstore.de/images/0960/squier-sonic-stratocaster-hss-mn-black_4_GIT0060106-000.jpg" }
                    }
                    // https://www.musicstore.com/en_US/USD/Squier-Sonic-Stratocaster-HSS-MN-Black/art-GIT0060106-000
                },
                new Product
                {
                    Name = "Epiphone Les Paul Standard 50s Cardinal Red",
                    Category = "Electric Guitar",
                    Brand = "Epiphone",
                    Price = 28771.66,
                    Stock = 99,
                    Description = "The Epiphone Les Paul Standard '50s from the Inspired by Gibson collection offers a fresh take on the classic Fifties-era Les Paul. This model captures the essence of the iconic original Gibson colors, delivering an authentic look and feel. Crafted with proven tonewoods, classic dimensions, and that signature vintage sound, the Les Paul Standard '50s stays true to the golden era of the '50s. However, it also features some innovative enhancements to meet the needs of today’s musicians, ensuring a playing experience that’s both nostalgic and perfectly suited for modern demands. As a bonus, it comes with an Epiphone Premium Gigbag for safe and stylish transportation!",
                    IsBanner = true,
                    BannerImageUrl = "https://www.rockin.co.jp/shop//files/product_images/res_be58d19200e4df35300a932b23f56f50295de319.jpg",
                    BannerDescription = "Classic Les Paul Tone & Style!",
                    Rating = 5,
                    SoldAmount = 85,
                    ProductImages = new List<ProductImage> 
                    { 
                        new ProductImage { Url = "https://images.musicstore.de/images/0960/epiphone-les-paul-standard-50s-cardinal-red_1_GIT0062234-004.jpg" },
                        new ProductImage { Url = "https://images.musicstore.de/images/0960/epiphone-les-paul-standard-50s-cardinal-red_2_GIT0062234-004.jpg" }
                    }
                    // https://www.musicstore.com/en_US/USD/Epiphone-Les-Paul-Standard-50s-Cardinal-Red/art-GIT0062234-004
                },
                new Product
                {
                    Name = "Ortega R221BK 7/8 Black Highgloss",
                    Category = "Acoustic Guitar",
                    Brand = "Ortega",
                    Price = 16135.86,
                    Stock = 99,
                    Description = "The Ortega R221BK 7/8 Classical Guitar is an affordable instrument perfect for beginners and intermediate players. The tone is derived from a combination of a spruce top and Mahogany back and sides. This guitar also comes with Ortega’s famous 12-hole bridge which offers a groundbreaking improvement for sound; tuning stability and faster response of the guitar top.",
                    IsBanner = true,
                    BannerImageUrl = "/images/banner-images/ortega-r221bk-7-8-black-highgloss.jpg",
                    BannerDescription = "Perfect Sound for Young Guitarists!",
                    Rating = 5,
                    SoldAmount = 140,
                    ProductImages = new List<ProductImage> 
                    { 
                        new ProductImage { Url = "https://images.musicstore.de/images/0960/ortega-r221bk-7-8-black-highgloss-incl-gigbag_1_GIT0028388-000.jpg" }
                    }
                    // https://www.musicstore.com/en_US/USD/Ortega-R221BK-7-8-Black-Highgloss-incl-Gigbag/art-GIT0028388-000
                },
                new Product
                {
                    Name = "Valencia VC 203 3/4 LH Lefthand",
                    Category = "Acoustic Guitar",
                    Brand = "Valencia",
                    Price = 4033.96,
                    Stock = 99,
                    Description = "A body with Sitka spruce top and Nato back and sides promises a warm, expressive sound. The Jabon neck also has a comfortable profile and a fretboard with a saddle width of 48 mm, on which even small hands can comfortably grip the first chords and notes.",
                    IsBanner = false,
                    Rating = 5,
                    SoldAmount = 75,
                    ProductImages = new List<ProductImage> 
                    { 
                        new ProductImage { Url = "https://images.musicstore.de/images/0960/valencia-vc-203-3-4-lh-lefthand_1_GIT0041407-000.jpg" }
                    }
                    // https://www.musicstore.com/en_US/USD/Valencia-VC-203-3-4-LH-Lefthand/art-GIT0041407-000
                },
                new Product
                {
                    Name = "Roland FP-10 BK Stage Piano",
                    Category = "Piano",
                    Brand = "Roland",
                    Price = 22839.36,
                    Stock = 99,
                    Description = "The FP-10 BK is an affordable stage piano for beginners, equipped with a PHA-4 standard keyboard that provides an authentic playing feel. The SuperNATURAL tone generation, which is also integrated into the larger models, provides a very natural, full sound. A powerful speaker system projects a dynamic piano sound into any room. For beginners in piano playing, the Stage Piano offers numerous digital learning functions. A Bluetooth and MIDI interface provides access to the wide world of DAWs (digital audio workstations) and apps. Measuring 128.4 x 25.8 cm, the Roland FP-10 is an optimal solution for anyone looking for a good, but compact stage piano.",
                    IsBanner = true,
                    BannerImageUrl = "https://www.amazona.de/wp-content/uploads/2024/01/roland-fp-10.jpg",
                    BannerDescription = "Compact Piano with Grand Sound!",
                    Rating = 5,
                    SoldAmount = 40,
                    ProductImages = new List<ProductImage> 
                    { 
                        new ProductImage { Url = "https://images.musicstore.de/images/0960/roland-fp-10-bk_1_KEY0004982-000.jpg" }
                    }
                    // https://www.musicstore.com/en_US/USD/Roland-FP-10-BK/art-KEY0004982-000
                },
                new Product
                {
                    Name = "Native Instruments KONTROL S88 MK3 Midi Keyboard",
                    Category = "Piano",
                    Brand = "Native Instruments",
                    Price = 60865.4,
                    Stock = 99,
                    Description = "With the KONTROL S88 MK3 Native Instruments presents the third generation of the popular USB master keyboard controller. The exterior of version 3 impresses with its elegant design with metal and glass elements. The large, high-resolution colour display with glass surface is framed by new key islands that form a continuous, dust-proof user interface and are grouped according to function, as well as eight encoders and a 2-axis encoder. To the left of the keyboard are the two handwheels for pitch band and modulation made of anodized aluminium with RGB lighting. The tried-and-tested Light Guide above the keyboard displays sounds, split zones, switches, scales and other information.",
                    IsBanner = false,
                    Rating = 5,
                    SoldAmount = 220,
                    ProductImages = new List<ProductImage> 
                    { 
                        new ProductImage { Url = "https://images.musicstore.de/images/0960/native-instruments-kontrol-s88-mk3_1_SYN0008776-000.jpg" }
                    }
                    // https://www.musicstore.com/en_US/USD/Native-Instruments-KONTROL-S88-MK3/art-SYN0008776-000
                },
                new Product
                {
                    Name = "Monzani MZCL-133B Bb-Clarinet Boehm System",
                    Category = "Clarinet",
                    Brand = "Monzani",
                    Price = 7474.7,
                    Stock = 99,
                    Description = "A clarinet made out of ABS plastic (like the MZCL-133B) offers a lot of advantages. The ABS plastic body is easy-care and has a low weight. Especially students with low budget get the possibility to buy a good instrument.",
                    IsBanner = false,
                    Rating = 5,
                    SoldAmount = 65,
                    ProductImages = new List<ProductImage> 
                    { 
                        new ProductImage { Url = "https://images.musicstore.de/images/0960/monzani-mzcl-133b-bb-clarinet-boehm-system-17-keys_1_BLA0003940-000.jpg" }
                    }
                    // https://www.musicstore.com/en_US/USD/Monzani-MZCL-133B-Bb-Clarinet-Boehm-System-17-Keys/art-BLA0003940-000
                },
                new Product
                {
                    Name = "Monzani MZMT-500S Bb-Pocket Trumpet Silverplate",
                    Category = "Trumpet",
                    Brand = "Monzani",
                    Price = 8601.84,
                    Stock = 99,
                    Description = "The Monzani MZMT-500S Bb-Pocket Trumpet is small and handy. It is perfect usable for being on a travel, party, parades or practicing on holidays. The brilliant, warm sound and the quite good intonation get the MZMT-500S to a special instrument. The easy attack point helps over the whole tone range and is exemplary for an instrument of this price range.",
                    IsBanner = false,
                    Rating = 5,
                    SoldAmount = 110,
                    ProductImages = new List<ProductImage> 
                    { 
                        new ProductImage { Url = "https://images.musicstore.de/images/0640/monzani-mzmt-500s-bb-pocket-trumpet-silverplate_1_BLA0001976-000.jpg" }
                    }
                    // https://www.musicstore.com/en_US/USD/Monzani-MZMT-500S-Bb-Pocket-Trumpet-Silverplate/art-BLA0001976-000
                },
                new Product
                {
                    Name = "Fame FG20B-RD First Gig Studio Set Red Sparkle",
                    Category = "Drums",
                    Brand = "Fame",
                    Price = 20889.61,
                    Stock = 99,
                    Description = "The Fame FG20B-RD First Gig Studio Set Red Sparkle is a high-quality complete set for drum set beginners with cymbals, Hardware and stool. The six-ply poplar drum shells deliver a balanced sound with impressive bass content. The cleanly milled 45° burr delivers perfect tunability and clear sound character. The heads lie cleanly on the ridges and are easy to tune. A robust hardware package consisting of two cymbal stands, hi-hat stand, snare stand, drum stool and bass drum pedal ensures a firm stand and individual adjustment options because all stives are infinitely adjustable in height. The bass Drum head is pre-damped, so the sound can be controlled excellently. Good sounding drum cymbals in the form of a 14\" Hi Hat and a 16\" Crash cymbal and the 20\" Ride promise excellent feel and assertive cymbal sound. A matching snare drum in set colour with snare stand is also included.",
                    Rating = 5,
                    SoldAmount = 80,
                    ProductImages = new List<ProductImage> 
                    { 
                        new ProductImage { Url = "https://images.musicstore.de/images/0960/fame-fg20b-rd-first-gig-studio-set-red-sparkle_1_DRU0040969-000.jpg" }
                    }
                    // https://www.musicstore.com/en_US/USD/Fame-FG20B-RD-First-Gig-Studio-Set-Red-Sparkle/art-DRU0040969-000
                },
                new Product
                {
                    Name = "Monzani Violinset Capriccio 21 1/8",
                    Category = "Violin",
                    Brand = "Monzani",
                    Price = 6349.97,
                    Stock = 99,
                    Description = "The Monzani Capriccio violin set offers a perfect start for beginners entering the world of violin playing. Featuring a well-straightened maple bridge, the violin provides an excellent foundation for developing first playing techniques. Its fully solid construction with a maple body and solid spruce top delivers a surprisingly full and sweet sound. The instrument is equipped with an ebony tailpiece that includes a fine tuner for the high E string, and a Guarneri-style ebony chinrest. The set also comes with a lightweight roundwood bow made from brazilwood, matching rosin, and a sturdy case for easy transport to lessons.",
                    Rating = 5,
                    SoldAmount = 200,
                    ProductImages = new List<ProductImage> 
                    { 
                        new ProductImage { Url = "https://images.musicstore.de/images/0960/monzani-violinset-capriccio-21-1-8_1_STR0000369-000.jpg" }
                    }
                    // https://www.musicstore.com/en_US/USD/Monzani-Violinset-Capriccio-21-1-8/art-STR0000369-000
                },
                new Product
                {
                    Name = "Gibson 1968 Les Paul Custom Reissue Ebony",
                    Category = "Electric Guitar",
                    Brand = "Gibson",
                    Price = 350768.69,
                    Stock = 99,
                    Description = "A reissue of the 1968 Les Paul Custom featuring a solid body electric guitar design, ebony finish, dual humbucker pickups, and traditional Les Paul hardware and construction.",
                    Rating = 5,
                    IsBanner = true,
                    BannerImageUrl = "/images/banner-images/gibson-bocchi.png",
                    BannerDescription = "Les Paul Custom style guitar similar to the one featured in Bocchi the Rock! A classic design known for its bold look, powerful humbuckers, and thick rock tones favored by many guitarists.",
                    SoldAmount = 200,
                    ProductImages = new List<ProductImage>
                    {
                        new ProductImage { Url = "https://images.musicstore.de/images/0960/gibson-1968-les-paul-custom-reissue-ebony-507798_1_GIT0059327-000.jpg" },
                        new ProductImage { Url = "https://images.musicstore.de/images/0960/gibson-1968-les-paul-custom-reissue-ebony-507798_15_GIT0059327-000.jpg" },

                    }
                    // https://www.musicstore.com/en_OT/EUR/Gibson-1968-Les-Paul-Custom-Reissue-Ebony-507798/art-GIT0059327-000
                }
            };
        }
    }
}
