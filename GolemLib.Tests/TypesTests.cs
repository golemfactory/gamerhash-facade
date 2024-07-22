using GolemLib.Types;

namespace GolemLib.Tests;

// The following tests ensure that C# code's behavior matches Rust's one.
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

    public static GolemUsage UsageFromAgreement(decimal[] array)
    {
        return new GolemUsage(new GolemPrice
        {
            StartPrice = 1.0m,
            EnvPerSec = array[1],
            GpuPerSec = array[2],
            NumRequests = array[0]
        });
    }

    public static GolemPrice PriceFromAgreement(decimal[] array)
    {
        return new GolemPrice
        {
            StartPrice = array[3],
            EnvPerSec = array[1],
            GpuPerSec = array[2],
            NumRequests = array[0]
        };
    }


    [Fact]
    public void GolemUsage_Reward_Case1()
    {
        var usage = UsageFromAgreement(new decimal[] { 27.0m, 538.8576082m, 8.7264478m });
        var price = PriceFromAgreement(new decimal[] { 0.0m, 0.0004m, 0.0003m, 0.0002m });

        var reward = usage.Reward(price);
        Assert.Equal(0.21836097762m, reward);
    }

    [Fact]
    public void GolemUsage_Reward_Case2()
    {
        var usage = UsageFromAgreement(new decimal[] { 0.0m, 9.119924900000001m, 0.0m });
        var price = PriceFromAgreement(new decimal[] { 0.0m, 0.0004m, 0.0003m, 0.0002m });

        var reward = usage.Reward(price);
        Assert.Equal(0.00384796996m, reward);
    }

    [Fact]
    public void GolemUsage_Reward_Case3()
    {
        var usage = UsageFromAgreement(new decimal[] { 0.0m, 9.1072277m, 0.0m });
        var price = PriceFromAgreement(new decimal[] { 0.0m, 0.0004m, 0.0003m, 0.0002m });

        var reward = usage.Reward(price);
        Assert.Equal(0.00384289108m, reward);
    }

    [Fact]
    public void GolemUsage_Reward_Case4()
    {
        var usage = UsageFromAgreement(new decimal[] { 31.0m, 474.6873272m, 9.761029m });
        var price = PriceFromAgreement(new decimal[] { 0.0m, 0.0003m, 0.0005m, 0.0m });

        var reward = usage.Reward(price);
        Assert.Equal(0.147286712660000001m, reward);
    }

    [Fact]
    public void GolemUsage_Reward_Case5()
    {
        var usage = UsageFromAgreement(new decimal[] { 0.0m, 66.5695986m, 0.0m });
        var price = PriceFromAgreement(new decimal[] { 0.0m, 0.00025m, 0.00025m, 0.0m });

        var reward = usage.Reward(price);
        Assert.Equal(0.016642399650000003m, reward);
    }

    [Fact]
    public void GolemUsage_Reward_Case6()
    {
        var usage = UsageFromAgreement(new decimal[] { 1.0m, 41.4281323m, 9.8315089m });
        var price = PriceFromAgreement(new decimal[] { 0.0m, 0.00025m, 0.00025m, 0.0m });

        var reward = usage.Reward(price);
        Assert.Equal(0.0128149103m, reward);
    }

    [Fact]
    public void GolemUsage_Reward_Case7()
    {
        var usage = UsageFromAgreement(new decimal[] { 0.0m, 9.132793m, 0.0m });
        var price = PriceFromAgreement(new decimal[] { 0.0m, 0.00025m, 0.00025m, 0.0m });

        var reward = usage.Reward(price);
        Assert.Equal(0.00228319825m, reward);
    }

    [Fact]
    public void GolemUsage_Reward_Case8()
    {
        var usage = UsageFromAgreement(new decimal[] { 0.0m, 88.9031127m, 0.0m });
        var price = PriceFromAgreement(new decimal[] { 0.0m, 0.00025m, 0.00025m, 0.0m });

        var reward = usage.Reward(price);
        Assert.Equal(0.022225778174999998m, reward);
    }
}
