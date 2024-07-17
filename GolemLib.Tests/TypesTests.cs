using GolemLib.Types;

namespace GolemLib.Tests;

public class TypesTests
{
    [Fact]
    public void GolemUsage_Reward_HappyPath()
    {
        var startPrice = new GolemPrice
        {
            StartPrice = 0.333m,
            EnvPerSec = 0.123m,
            GpuPerSec = 0.234m,
            NumRequests = 0
        };
        var usage = new GolemUsage(startPrice);

        var price = new GolemPrice
        {
            StartPrice = 0.444m,
            EnvPerSec = 0.321m,
            GpuPerSec = 0.432m,
            NumRequests = 12
        };


        var reward = usage.Reward(price);

        Assert.Equal(0.584571m, reward);
    }

    [Fact]
    public void GolemUsage_Reward_Round()
    {
        var startPrice = new GolemPrice
        {
            StartPrice = 0.333m,
            EnvPerSec = 0.123m,
            GpuPerSec = 0.234m,
            NumRequests = 0
        };
        var usage = new GolemUsage(startPrice);

        var price = new GolemPrice
        {
            StartPrice = 0.444000000000000001m,
            EnvPerSec = 0.321000000000000001m,
            GpuPerSec = 0.432000000000000001m,
            NumRequests = 12
        };


        var reward = usage.Reward(price);

        Assert.Equal(0.584571m, reward);
    }

    [Fact]
    public void GolemUsage_Reward_Round_Precision()
    {
        var usageVec = new GolemPrice
        {
            StartPrice = 1.0m,
            EnvPerSec = 959.0184787m,
            GpuPerSec = 0.4584724m,
            NumRequests = 62.0m
        };
        var usage = new GolemUsage(usageVec);

        var price = new GolemPrice
        {
            StartPrice = 0.0007m,
            EnvPerSec = 0.0003m,
            GpuPerSec = 0.0005m,
            NumRequests = 0.000m
        };

        var reward = usage.Reward(price);
        Assert.Equal(0.288634779809999970m, reward);
    }

    [Fact]
    public void GolemUsage_Reward_Non_Representable_Values()
    {
        var usageVec = new GolemPrice
        {
            StartPrice = 1.0m,
            EnvPerSec = 44.017951m,
            GpuPerSec = 103.002864998m,
            NumRequests = 0.0m
        };
        var usage = new GolemUsage(usageVec);

        var price = new GolemPrice
        {
            StartPrice = 0.0m,
            EnvPerSec = 0.0001m,
            GpuPerSec = 0.00005m,
            NumRequests = 0.0m
        };

        var reward = usage.Reward(price);
        Assert.Equal(0.0095519383499m, reward);
    }

    [Fact]
    public void GolemUsage_Reward_Underflow()
    {
        var usageVec = new GolemPrice
        {
            StartPrice = 1.0m,
            EnvPerSec = 24.141030488m,
            GpuPerSec = 0.0m,
            NumRequests = 0.0m
        };
        var usage = new GolemUsage(usageVec);

        var price = new GolemPrice
        {
            StartPrice = 0.0m,
            EnvPerSec = 0.002m,
            GpuPerSec = 0.008m,
            NumRequests = 0.0m
        };

        var reward = usage.Reward(price);
        Assert.Equal(0.048282060976m, reward);
    }

    [Fact]
    public void GolemUsage_Reward_Overflow()
    {
        var usageVec = new GolemPrice
        {
            StartPrice = 1.0m,
            EnvPerSec = 44.094619588m,
            GpuPerSec = 0.0m,
            NumRequests = 0.0m
        };
        var usage = new GolemUsage(usageVec);

        var price = new GolemPrice
        {
            StartPrice = 0.0m,
            EnvPerSec = 0.002m,
            GpuPerSec = 0.008m,
            NumRequests = 0.0m
        };

        var reward = usage.Reward(price);
        Assert.Equal(0.088189239176m, reward);
    }
}
