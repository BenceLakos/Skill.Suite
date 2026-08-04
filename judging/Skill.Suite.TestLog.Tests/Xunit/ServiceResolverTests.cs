using Skill.Suite.TestLog.Tests.Support;
using Skill.Suite.TestLog.Xunit;
using Xunit;

namespace Skill.Suite.TestLog.Tests.Xunit;

/// <summary>
/// Pins how a competitor's implementation gets found and built. Graded suites resolve services through
/// this, so its selection rules decide which class is under test.
/// </summary>
public sealed class ServiceResolverTests
{
    public ServiceResolverTests() => ServiceResolver.Reset();

    [Fact]
    public void Resolve_PicksTheAlphabeticallyFirstImplementation()
    {
        // Ordinal ordering on the simple type name, so AlphaLeaf beats ZuluLeaf. Competitors ship one
        // implementation per contract; the rule only decides ties deterministically.
        Assert.IsType<AlphaLeaf>(ServiceResolver.Resolve<IResolverLeaf>());
    }

    [Fact]
    public void Resolve_UsesTheWidestConstructorItCanSatisfy()
    {
        // ResolverRoot's widest constructor takes a string, which is not a contract interface, so it is
        // rejected and the single-interface constructor wins.
        var root = ServiceResolver.Resolve<IResolverRoot>();

        Assert.Equal(1, root.LeafValue());
    }

    [Fact]
    public void Resolve_CachesOnePerContract()
    {
        Assert.Same(ServiceResolver.Resolve<IResolverLeaf>(), ServiceResolver.Resolve<IResolverLeaf>());
    }

    [Fact]
    public void Reset_DropsTheCache()
    {
        var first = ServiceResolver.Resolve<IResolverLeaf>();
        ServiceResolver.Reset();

        Assert.NotSame(first, ServiceResolver.Resolve<IResolverLeaf>());
    }

    [Fact]
    public void Resolve_OnACyclicGraph_ReportsTheCycleInTheExceptionChain()
    {
        var ex = Assert.Throws<InvalidOperationException>(ServiceResolver.Resolve<ICycleA>);

        // The top-level message is the generic "no satisfiable constructor": each candidate constructor
        // is rejected because resolving its parameter threw, and that failure is preserved as the inner
        // exception. So the real cause is one level down, not in ex.Message - worth knowing when reading
        // a competitor's failing run.
        Assert.Contains("No public constructor", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Cyclic dependency", Chain(ex), StringComparison.Ordinal);
    }

    private static string Chain(Exception ex)
    {
        var messages = new List<string>();
        for (Exception? current = ex; current is not null; current = current.InnerException)
            messages.Add(current.Message);
        return string.Join(" | ", messages);
    }

    [Fact]
    public void Resolve_WithNoImplementation_SaysWhatWasMissing()
    {
        var ex = Assert.Throws<InvalidOperationException>(ServiceResolver.Resolve<INoImplementation>);

        Assert.Contains(nameof(INoImplementation), ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_OnANonInterface_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => ServiceResolver.Resolve(typeof(AlphaLeaf)));
    }

    [Fact]
    public void BuildWith_UsesTheSuppliedDependencyAndBypassesTheCache()
    {
        var root = ServiceResolver.BuildWith<IResolverRoot>(new ZuluLeaf());

        Assert.Equal(26, root.LeafValue());
        Assert.NotSame(root, ServiceResolver.Resolve<IResolverRoot>());
    }

    [Fact]
    public void BuildWith_WhenNothingSatisfiesTheConstructor_SaysSo()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => ServiceResolver.BuildWith<IResolverRoot>());

        Assert.Contains(nameof(ResolverRoot), ex.Message, StringComparison.Ordinal);
    }
}
