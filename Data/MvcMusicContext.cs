using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MvcMusic.Models;

namespace MvcMusic.Data
{
    public class MvcMusicContext : IdentityDbContext<ApplicationUser>
    {
        public MvcMusicContext (DbContextOptions<MvcMusicContext> options)
            : base(options)
        {
        }

        public DbSet<MvcMusic.Models.Product> Product { get; set; } = default!;
        public DbSet<MvcMusic.Models.ProductImage> ProductImage { get; set; } = default!;
        public DbSet<MvcMusic.Models.ActivityLog> ActivityLog { get; set; } = default!;
        public DbSet<MvcMusic.Models.Order> Order { get; set; } = default!;
        public DbSet<MvcMusic.Models.OrderItem> OrderItem { get; set; } = default!;
    }
}
