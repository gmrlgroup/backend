using Application.Shared.Models;
using Application.Shared.Models.Data;
using Application.Shared.Models.User;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace Application.Shared.Data
{
    //public delegate ApplicationDbContext DbContextFactory(string companyId);

    public class SchedulerDbContext(DbContextOptions<SchedulerDbContext> options) : DbContext(options)
    {


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
        }



    }
}
