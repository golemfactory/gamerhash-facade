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
}