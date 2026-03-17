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

        public DbSet<Product> Product { get; set; } = default!;
        public DbSet<Category> Category { get; set; } = default!;
        public DbSet<Brand> Brand { get; set; } = default!;
        public DbSet<ProductImage> ProductImage { get; set; } = default!;
        public DbSet<ActivityLog> ActivityLog { get; set; } = default!;
        public DbSet<Order> Order { get; set; } = default!;
        public DbSet<OrderItem> OrderItem { get; set; } = default!;
        public DbSet<ChartPoint> ChartPoint { get; set; } = default!;
        public DbSet<ChatRoom> ChatRoom { get; set; } = default!;
        public DbSet<ChatMessage> ChatMessage { get; set; } = default!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ChartPoint>().HasNoKey();
            modelBuilder.Entity<ChartPoint>()
                .Property(c => c.Value)
                .HasColumnType("decimal(18,2)");

            // Resolve circular cascade path for ChatMessage
            modelBuilder.Entity<ChatMessage>()
                .HasOne(m => m.Room)
                .WithMany(r => r.Messages)
                .HasForeignKey(m => m.RoomId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ChatMessage>()
                .HasOne(m => m.Sender)
                .WithMany()
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}
