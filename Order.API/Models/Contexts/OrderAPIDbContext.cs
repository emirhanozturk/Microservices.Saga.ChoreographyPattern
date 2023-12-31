using Microsoft.EntityFrameworkCore;
using Order.API.Models;

namespace Order.API.Models.Contexts
{
    public class OrderAPIDbContext : DbContext
    {
        public OrderAPIDbContext(DbContextOptions options) : base(options)
        {
        }

        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<Order> Orders { get; set; }


    }
}
