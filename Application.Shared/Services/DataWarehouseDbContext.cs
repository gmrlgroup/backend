using Microsoft.EntityFrameworkCore;

namespace Application.Shared.Services
{
    public class DataWarehouseDbContext : DbContext
    {
        public DataWarehouseDbContext(DbContextOptions<DataWarehouseDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // No predefined entities - we'll query dynamically
        }
    }
}
