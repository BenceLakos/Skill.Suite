namespace Skill.Suite.TestLog.Tests.Support;

/// <summary>Contracts and implementations used to exercise <c>ServiceResolver</c>.</summary>
internal interface IResolverLeaf
{
    int Value();
}

internal sealed class AlphaLeaf : IResolverLeaf
{
    public int Value() => 1;
}

/// <summary>Sorts after <see cref="AlphaLeaf"/>, so it must never be the one resolved.</summary>
internal sealed class ZuluLeaf : IResolverLeaf
{
    public int Value() => 26;
}

internal interface IResolverRoot
{
    int LeafValue();
}

internal sealed class ResolverRoot : IResolverRoot
{
    private readonly IResolverLeaf _leaf;

    /// <summary>Widest constructor, but unsatisfiable: <c>name</c> is not a contract interface.</summary>
    public ResolverRoot(IResolverLeaf leaf, string name)
    {
        _leaf = leaf;
        Name = name;
    }

    public ResolverRoot(IResolverLeaf leaf) => _leaf = leaf;

    internal string Name { get; } = "resolved";

    public int LeafValue() => _leaf.Value();
}

internal interface ICycleA
{
    void Go();
}

internal interface ICycleB
{
    void Go();
}

internal sealed class CycleA(ICycleB other) : ICycleA
{
    public void Go() => other.Go();
}

internal sealed class CycleB(ICycleA other) : ICycleB
{
    public void Go() => other.Go();
}

internal interface INoImplementation
{
    void Go();
}
