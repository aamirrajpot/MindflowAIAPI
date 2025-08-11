using Microsoft.EntityFrameworkCore;
using Mindflow_Web_API.Models;

namespace Mindflow_Web_API.Persistence
{
    public class MindflowDbContext(DbContextOptions<MindflowDbContext> options) : DbContext(options)
    {
        public DbSet<Movie> Movies => Set<Movie>();
        public DbSet<User> Users => Set<User>();
        public DbSet<UserOtp> UserOtps => Set<UserOtp>();
        public DbSet<WellnessCheckIn> WellnessCheckIns => Set<WellnessCheckIn>();
        public DbSet<TaskItem> Tasks => Set<TaskItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema("app");
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(MindflowDbContext).Assembly);
            base.OnModelCreating(modelBuilder);
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder
                .UseAsyncSeeding(async (context, _, cancellationToken) =>
                {
                    var sampleMovie = await context.Set<Movie>().FirstOrDefaultAsync(b => b.Title == "Sonic the Hedgehog 3");
                    if (sampleMovie == null)
                    {
                        sampleMovie = Movie.Create("Sonic the Hedgehog 3", "Fantasy", new DateTimeOffset(new DateTime(2025, 1, 3), TimeSpan.Zero), 7);
                        await context.Set<Movie>().AddAsync(sampleMovie);
                        await context.SaveChangesAsync();
                    }
                })
                .UseSeeding((context, _) =>
                {
                    var sampleMovie = context.Set<Movie>().FirstOrDefault(b => b.Title == "Sonic the Hedgehog 3");
                    if (sampleMovie == null)
                    {
                        sampleMovie = Movie.Create("Sonic the Hedgehog 3", "Fantasy", new DateTimeOffset(new DateTime(2025, 1, 3), TimeSpan.Zero), 7);
                        context.Set<Movie>().Add(sampleMovie);
                        context.SaveChanges();
                    }
                });
        }
    }
}
