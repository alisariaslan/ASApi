using Microsoft.EntityFrameworkCore;
using SarsMinimalApi.Models;

namespace SarsMinimalApi.Context;

public class MyDbContext : DbContext
{
	public MyDbContext(DbContextOptions<MyDbContext> options) : base(options) {	}

	public DbSet<UserModel> Users => Set<UserModel>();
	public DbSet<TokenModel> Tokens => Set<TokenModel>();
	public DbSet<VerificationModel> Verifications => Set<VerificationModel>();
    public DbSet<IpModel> Requests => Set<IpModel>();
    public DbSet<LogModel> Logs => Set<LogModel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<TokenModel>()
			   .HasOne<UserModel>()
			   .WithMany()
			   .HasForeignKey(t => t.UserId)
			   .OnDelete(DeleteBehavior.Restrict); // Silme davranışını Restrict olarak belirtiyoruz

		modelBuilder.Entity<VerificationModel>()
		   .HasOne<UserModel>()
		   .WithMany()
		   .HasForeignKey(t => t.UserId)
		   .OnDelete(DeleteBehavior.Restrict); // Silme davranışını Restrict olarak belirtiyoruz
	}

}
