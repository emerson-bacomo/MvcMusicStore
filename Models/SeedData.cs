using MvcMusic.Data;
using Microsoft.EntityFrameworkCore;

namespace MvcMusic.Models
{
    public static class SeedData
    {
        public static void Initialize(IServiceProvider serviceProvider)
        {
            using var context = new MvcMusicContext(
                serviceProvider.GetRequiredService<DbContextOptions<MvcMusicContext>>());

            if (context == null || context.Product == null)
                throw new ArgumentNullException("Null MvcMusicContext");

            if (context.Product.Any())
                return;

            context.Product.AddRange(
                new Product
                {
                    Name = "Squier Sonic Stratocaster HSS MN Black",
                    Category = "Electric Guitar",
                    Brand = "Squier",
                    Price = 9966.26,
                    Stock = 99,
                    Description = "The Squier Sonic Series Stratocaster MN HSS Black is not only an optimal entry-level model, but also meets the expectations of experienced guitar lovers with its typical sound palette and design. The poplar body with Contour-Body provides absolute wearing comfort and shines in a black finish. The bolt-on maple neck in a modern 'C' shape fits comfortably in every hand and ensures easy playability of the 21-fret maple fingerboard.",
                    Image = "https://images.musicstore.de/images/0960/squier-sonic-stratocaster-hss-mn-black_1_GIT0060106-000.jpg",
                    IsBanner = true,
                    BannerDescription = "Rock the Stage with Squier Sonic Strat!",
                    Rating = 5,
                    SoldAmount = 120
                },
                new Product
                {
                    Name = "Epiphone Les Paul Standard 50s Cardinal Red",
                    Category = "Electric Guitar",
                    Brand = "Epiphone",
                    Price = 28771.66,
                    Stock = 99,
                    Description = "The Epiphone Les Paul Standard 50s from the Inspired by Gibson collection offers a fresh take on the classic Fifties-era Les Paul. This model captures the essence of the iconic original Gibson colors, delivering an authentic look and feel. Crafted with proven tonewoods, classic dimensions, and that signature vintage sound.",
                    Image = "https://images.musicstore.de/images/0960/epiphone-les-paul-standard-50s-cardinal-red_1_GIT0062234-004.jpg",
                    IsBanner = true,
                    BannerDescription = "Classic Les Paul Tone and Style!",
                    Rating = 5,
                    SoldAmount = 85
                },
                new Product
                {
                    Name = "Ortega R221BK 7/8 Black Highgloss",
                    Category = "Acoustic Guitar",
                    Brand = "Ortega",
                    Price = 16135.86,
                    Stock = 99,
                    Description = "The Ortega R221BK 7/8 Classical Guitar is an affordable instrument perfect for beginners and intermediate players. The tone is derived from a combination of a spruce top and Mahogany back and sides. This guitar also comes with Ortega's famous 12-hole bridge which offers a groundbreaking improvement for sound and tuning stability.",
                    Image = "https://images.musicstore.de/images/0960/ortega-r221bk-7-8-black-highgloss-incl-gigbag_1_GIT0028388-000.jpg",
                    IsBanner = true,
                    BannerDescription = "Perfect Sound for Young Guitarists!",
                    Rating = 5,
                    SoldAmount = 140
                },
                new Product
                {
                    Name = "Valencia VC 203 3/4 LH Lefthand",
                    Category = "Acoustic Guitar",
                    Brand = "Valencia",
                    Price = 4033.96,
                    Stock = 99,
                    Description = "A body with Sitka spruce top and Nato back and sides promises a warm, expressive sound. The Jabon neck also has a comfortable profile and a fretboard with a saddle width of 48 mm, on which even small hands can comfortably grip the first chords and notes.",
                    Image = "https://images.musicstore.de/images/0960/valencia-vc-203-3-4-lh-lefthand_1_GIT0041407-000.jpg",
                    IsBanner = false,
                    Rating = 5,
                    SoldAmount = 75
                },
                new Product
                {
                    Name = "Roland FP-10 BK Stage Piano",
                    Category = "Piano",
                    Brand = "Roland",
                    Price = 22839.36,
                    Stock = 99,
                    Description = "The FP-10 BK is an affordable stage piano for beginners, equipped with a PHA-4 standard keyboard that provides an authentic playing feel. The SuperNATURAL tone generation, which is also integrated into the larger models, provides a very natural, full sound. A powerful speaker system projects a dynamic piano sound into any room.",
                    Image = "https://images.musicstore.de/images/0960/roland-fp-10-bk_1_KEY0004982-000.jpg",
                    IsBanner = true,
                    BannerDescription = "Compact Piano with Grand Sound!",
                    Rating = 5,
                    SoldAmount = 40
                },
                new Product
                {
                    Name = "Native Instruments KONTROL S88 MK3 Midi Keyboard",
                    Category = "Piano",
                    Brand = "Native Instruments",
                    Price = 60865.40,
                    Stock = 99,
                    Description = "With the KONTROL S88 MK3 Native Instruments presents the third generation of the popular USB master keyboard controller. The exterior impresses with its elegant design with metal and glass elements. The large, high-resolution colour display with glass surface is framed by new key islands.",
                    Image = "https://images.musicstore.de/images/0960/native-instruments-kontrol-s88-mk3_1_SYN0008776-000.jpg",
                    IsBanner = false,
                    Rating = 5,
                    SoldAmount = 220
                },
                new Product
                {
                    Name = "Monzani MZCL-133B Bb-Clarinet Boehm System",
                    Category = "Clarinet",
                    Brand = "Monzani",
                    Price = 7474.70,
                    Stock = 99,
                    Description = "A clarinet made out of ABS plastic offers a lot of advantages. The ABS plastic body is easy-care and has a low weight. Especially students with low budget get the possibility to buy a good instrument.",
                    Image = "https://images.musicstore.de/images/0960/monzani-mzcl-133b-bb-clarinet-boehm-system-17-keys_1_BLA0003940-000.jpg",
                    IsBanner = false,
                    Rating = 5,
                    SoldAmount = 65
                },
                new Product
                {
                    Name = "Monzani MZMT-500S Bb-Pocket Trumpet Silverplate",
                    Category = "Trumpet",
                    Brand = "Monzani",
                    Price = 8601.84,
                    Stock = 99,
                    Description = "The Monzani MZMT-500S Bb-Pocket Trumpet is small and handy. It is perfect for travel, party, parades or practicing on holidays. The brilliant, warm sound and the quite good intonation make it a special instrument.",
                    Image = "https://images.musicstore.de/images/0640/monzani-mzmt-500s-bb-pocket-trumpet-silverplate_1_BLA0001976-000.jpg",
                    IsBanner = false,
                    Rating = 5,
                    SoldAmount = 110
                },
                new Product
                {
                    Name = "Fame FG20B-RD First Gig Studio Set Red Sparkle",
                    Category = "Drums",
                    Brand = "Fame",
                    Price = 20889.61,
                    Stock = 99,
                    Description = "The Fame FG20B-RD First Gig Studio Set Red Sparkle is a high-quality complete set for drum set beginners with cymbals, hardware and stool. The six-ply poplar drum shells deliver a balanced sound with impressive bass content.",
                    Image = "https://images.musicstore.de/images/0960/fame-fg20b-rd-first-gig-studio-set-red-sparkle_1_DRU0040969-000.jpg",
                    IsBanner = true,
                    BannerDescription = "Complete Drum Set – Ready to Rock Your First Gig!",
                    Rating = 5,
                    SoldAmount = 80
                },
                new Product
                {
                    Name = "Monzani Violinset Capriccio 21 1/8",
                    Category = "Violin",
                    Brand = "Monzani",
                    Price = 6349.97,
                    Stock = 99,
                    Description = "The Monzani Capriccio violin set offers a perfect start for beginners entering the world of violin playing. Featuring a well-straightened maple bridge, the violin provides an excellent foundation for developing first playing techniques. The set comes with a lightweight bow, rosin, and a sturdy case.",
                    Image = "https://images.musicstore.de/images/0960/monzani-violinset-capriccio-21-1-8_1_STR0000369-000.jpg",
                    IsBanner = true,
                    BannerDescription = "Start Your Musical Journey Today!",
                    Rating = 5,
                    SoldAmount = 200
                }
            );
            context.SaveChanges();
        }
    }
}
