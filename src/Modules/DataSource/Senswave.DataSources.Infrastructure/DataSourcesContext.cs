using Microsoft.EntityFrameworkCore;
using Senswave.DataSources.Domain.Brokers.Brokers.Entities;
using Senswave.DataSources.Domain.Brokers.Sessions.Entities;
using Senswave.Infrastructure.Extensions;
using Senswave.Infrastructure.Persistence;

namespace Senswave.DataSources.Infrastructure;

public class DataSourcesContext(IOptions<PostgreSqlOptions> options) : DbContext(options.GetPostgresOptions<DataSourcesContext>())
{
    public DbSet<Broker> Brokers { get; set; }

    public DbSet<Subscribtion> Subscribtions { get; set; }

    public DbSet<Session> Sessions { get; set; }

    public DbSet<Log> Logs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("DataSources");

        modelBuilder.Entity<Broker>(entity =>
        {
            entity.ToTable("Brokers");

            entity.HasIndex(x => new { x.OwnerId, x.Name })
                .IsUnique();

            entity.OwnsOne(
                x => x.BrokerInfo,
                owned =>
                {
                    owned.ToTable("BrokerInfo");

                    // Not unique: several users may share one broker (see BrokerOptions.AllowSharedBrokers).
                    owned.HasIndex(x => new { x.Url, x.Port });

                    owned.HasIndex(x => new { x.Url, x.Port, x.ClientName })
                        .IsUnique();
                });
        });

        modelBuilder.Entity<Subscribtion>(entity =>
        {
            entity.ToTable("Subscribtions");

            entity.HasIndex(e => new { e.BrokerId, e.Topic })
                  .IsUnique();
        });

        modelBuilder.Entity<Session>()
            .ToTable("Sessions");

        modelBuilder.Entity<Log>()
            .ToTable("Logs");

        base.OnModelCreating(modelBuilder);
    }
}
