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
    }
}
