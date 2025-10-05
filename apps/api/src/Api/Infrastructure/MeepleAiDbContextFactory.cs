using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Api.Infrastructure;

public class MeepleAiDbContextFactory : IDesignTimeDbContextFactory<MeepleAiDbContext>
{
    public MeepleAiDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<MeepleAiDbContext>();

        var connectionString = Environment.GetEnvironmentVariable("CONNECTIONSTRINGS__POSTGRES")
            ?? Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
            ?? "Host=localhost;Database=meepleai;Username=postgres;Password=postgres";

        optionsBuilder.UseNpgsql(connectionString);

        return new MeepleAiDbContext(optionsBuilder.Options);
    }
}
