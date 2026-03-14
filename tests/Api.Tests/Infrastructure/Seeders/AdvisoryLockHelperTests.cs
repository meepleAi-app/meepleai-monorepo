using Api.Infrastructure.Seeders;
using Xunit;

namespace Api.Tests.Infrastructure.Seeders;

public sealed class AdvisoryLockHelperTests
{
    [Fact]
    public void LockKey_IsConsistentAcrossCalls()
    {
        var key1 = AdvisoryLockHelper.SeedingLockKey;
        var key2 = AdvisoryLockHelper.SeedingLockKey;

        Assert.Equal(key1, key2);
        Assert.NotEqual(0, key1);
    }

    [Fact]
    public void AcquireSql_ContainsPgAdvisoryLock()
    {
        var sql = AdvisoryLockHelper.AcquireLockSql;
        Assert.Contains("pg_advisory_lock", sql);
        Assert.Contains(AdvisoryLockHelper.SeedingLockKey.ToString(), sql);
    }

    [Fact]
    public void ReleaseSql_ContainsPgAdvisoryUnlock()
    {
        var sql = AdvisoryLockHelper.ReleaseLockSql;
        Assert.Contains("pg_advisory_unlock", sql);
        Assert.Contains(AdvisoryLockHelper.SeedingLockKey.ToString(), sql);
    }
}
