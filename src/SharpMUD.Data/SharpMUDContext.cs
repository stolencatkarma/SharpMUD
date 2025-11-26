using Microsoft.EntityFrameworkCore;
using SharpMUD.Data.Models;

namespace SharpMUD.Data
{
    public class SharpMUDContext : DbContext
    {
        public DbSet<PlayerAccount> Players { get; set; }
        public DbSet<PlayerItem> PlayerItems { get; set; }

        public SharpMUDContext(DbContextOptions<SharpMUDContext> options) : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlite("Data Source=sharpmud.db");
            }
        }
    }
}
