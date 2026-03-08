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
                    Description = "The Squier Sonic Series Stratocaster MN HSS Black is not only an optimal entry-level model, but also meets the expectations of experienced guitar lovers with its typical sound palette and design.",
                    IsBanner = true,
                    BannerDescription = "Rock the Stage with Squier Sonic Strat!",
                    Rating = 5,
                    SoldAmount = 120,
                    ProductImages = new List<ProductImage> 
                    { 
                        new ProductImage { Url = "https://images.musicstore.de/images/0960/squier-sonic-stratocaster-hss-mn-black_1_GIT0060106-000.jpg" },
                        new ProductImage { Url = "https://images.musicstore.de/images/0960/squier-sonic-stratocaster-hss-mn-black_4_GIT0060106-000.jpg" }
                    }
                },
                new Product
                {
                    Name = "Epiphone Les Paul Standard 50s Cardinal Red",
                    Category = "Electric Guitar",
                    Brand = "Epiphone",
                    Price = 28771.66,
                    Stock = 99,
                    Description = "The Epiphone Les Paul Standard 50s from the Inspired by Gibson collection offers a fresh take on the classic Fifties-era Les Paul.",
                    IsBanner = true,
                    BannerDescription = "Classic Les Paul Tone and Style!",
                    Rating = 5,
                    SoldAmount = 85,
                    ProductImages = new List<ProductImage> 
                    { 
                        new ProductImage { Url = "https://images.musicstore.de/images/0960/epiphone-les-paul-standard-50s-cardinal-red_1_GIT0062234-004.jpg" },
                        new ProductImage { Url = "https://images.musicstore.de/images/0960/epiphone-les-paul-standard-50s-cardinal-red_2_GIT0062234-004.jpg" }
                    }
                },
                new Product
                {
                    Name = "Ortega R221BK 7/8 Black Highgloss",
                    Category = "Acoustic Guitar",
                    Brand = "Ortega",
                    Price = 16135.86,
                    Stock = 99,
                    Description = "The Ortega R221BK 7/8 Classical Guitar is an affordable instrument perfect for beginners and intermediate players.",
                    IsBanner = true,
                    BannerDescription = "Perfect Sound for Young Guitarists!",
                    Rating = 5,
                    SoldAmount = 140,
                    ProductImages = new List<ProductImage> 
                    { 
                        new ProductImage { Url = "https://images.musicstore.de/images/0960/ortega-r221bk-7-8-black-highgloss-incl-gigbag_1_GIT0028388-000.jpg" }
                    }
                },
                new Product
                {
                    Name = "Roland FP-10 BK Stage Piano",
                    Category = "Piano",
                    Brand = "Roland",
                    Price = 22839.36,
                    Stock = 99,
                    Description = "The FP-10 BK is an affordable stage piano for beginners, equipped with a PHA-4 standard keyboard that provides an authentic playing feel.",
                    IsBanner = true,
                    BannerDescription = "Compact Piano with Grand Sound!",
                    Rating = 5,
                    SoldAmount = 40,
                    ProductImages = new List<ProductImage> 
                    { 
                        new ProductImage { Url = "https://images.musicstore.de/images/0960/roland-fp-10-bk_1_KEY0004982-000.jpg" }
                    }
                },
                new Product
                {
                    Name = "Native Instruments KONTROL S88 MK3 Midi Keyboard",
                    Category = "Piano",
                    Brand = "Native Instruments",
                    Price = 60865.40,
                    Stock = 99,
                    Description = "With the KONTROL S88 MK3 Native Instruments presents the third generation of the popular USB master keyboard controller. The exterior impresses with its elegant design with metal and glass elements. The large, high-resolution colour display with glass surface is framed by new key islands.",
                    IsBanner = false,
                    Rating = 5,
                    SoldAmount = 220,
                    ProductImages = new List<ProductImage> 
                    { 
                        new ProductImage { Url = "https://images.musicstore.de/images/0960/native-instruments-kontrol-s88-mk3_1_SYN0008776-000.jpg" }
                    }
                },
                new Product
                {
                    Name = "Monzani MZCL-133B Bb-Clarinet Boehm System",
                    Category = "Clarinet",
                    Brand = "Monzani",
                    Price = 7474.70,
                    Stock = 99,
                    Description = "A clarinet made out of ABS plastic offers a lot of advantages. The ABS plastic body is easy-care and has a low weight. Especially students with low budget get the possibility to buy a good instrument.",
                    IsBanner = false,
                    Rating = 5,
                    SoldAmount = 65,
                    ProductImages = new List<ProductImage> 
                    { 
                        new ProductImage { Url = "https://images.musicstore.de/images/0960/monzani-mzcl-133b-bb-clarinet-boehm-system-17-keys_1_BLA0003940-000.jpg" }
                    }
                },
                new Product
                {
                    Name = "Monzani MZMT-500S Bb-Pocket Trumpet Silverplate",
                    Category = "Trumpet",
                    Brand = "Monzani",
                    Price = 8601.84,
                    Stock = 99,
                    Description = "The Monzani MZMT-500S Bb-Pocket Trumpet is small and handy. It is perfect for travel, party, parades or practicing on holidays. The brilliant, warm sound and the quite good intonation make it a special instrument.",
                    IsBanner = false,
                    Rating = 5,
                    SoldAmount = 110,
                    ProductImages = new List<ProductImage> 
                    { 
                        new ProductImage { Url = "https://images.musicstore.de/images/0640/monzani-mzmt-500s-bb-pocket-trumpet-silverplate_1_BLA0001976-000.jpg" }
                    }
                },
                new Product
                {
                    Name = "Fame FG20B-RD First Gig Studio Set Red Sparkle",
                    Category = "Drums",
                    Brand = "Fame",
                    Price = 20889.61,
                    Stock = 99,
                    Description = "The Fame FG20B-RD First Gig Studio Set Red Sparkle is a high-quality complete set for drum set beginners with cymbals, hardware and stool. The six-ply poplar drum shells deliver a balanced sound with impressive bass content.",
                    IsBanner = true,
                    BannerDescription = "Complete Drum Set – Ready to Rock Your First Gig!",
                    Rating = 5,
                    SoldAmount = 80,
                    ProductImages = new List<ProductImage> 
                    { 
                        new ProductImage { Url = "https://images.musicstore.de/images/0960/fame-fg20b-rd-first-gig-studio-set-red-sparkle_1_DRU0040969-000.jpg" }
                    }
                },
                new Product
                {
                    Name = "Monzani Violinset Capriccio 21 1/8",
                    Category = "Violin",
                    Brand = "Monzani",
                    Price = 6349.97,
                    Stock = 99,
                    Description = "The Monzani Capriccio violin set offers a perfect start for beginners entering the world of violin playing. Featuring a well-straightened maple bridge, the violin provides an excellent foundation for developing first playing techniques. The set comes with a lightweight bow, rosin, and a sturdy case.",
                    IsBanner = true,
                    BannerDescription = "Start Your Musical Journey Today!",
                    Rating = 5,
                    SoldAmount = 200,
                    ProductImages = new List<ProductImage> 
                    { 
                        new ProductImage { Url = "https://images.musicstore.de/images/0960/monzani-violinset-capriccio-21-1-8_1_STR0000369-000.jpg" }
                    }
                }
            );
            context.SaveChanges();
        }
    }
}
