using Fibonacci.Contracts;
using Skill.Suite.TestLog;
using Skill.Suite.TestLog.Xunit;
using Xunit.Abstractions;

namespace Fibonacci.UnitTests;

/// <summary>
/// The hidden graded suite. Baked into the judge image and never shipped to competitors.
/// </summary>
/// <remarks>
/// Written in the required consumer shape: derive from <c>LoggedTest&lt;TSelf&gt;</c>, take the class fixture,
/// resolve the service through <c>ServiceResolver</c> with call logging, and assert only through <c>Log</c>.
/// An assertion made with a bare <c>Assert</c> still fails the test but emits no event, so the run would show a
/// red row with nothing explaining it.
/// </remarks>
public sealed class FibonacciTests : LoggedTest<FibonacciTests>, IClassFixture<FixtureScope<FibonacciTests>>
{
    private readonly IFibonacci _svc = ServiceResolver.Resolve<IFibonacci>().WithCallLogging();

    public FibonacciTests(ITestOutputHelper output, FixtureScope<FibonacciTests> scope)
        : base(output, scope) { }

    // ---- A1: At() ----

    [Aspect("A1.1", CompetitorVisible = true)]
    [Fact]
    public void At_BaseCases_AreZeroAndOne()
    {
        Log.AssertEqual(0L, _svc.At(0));
        Log.AssertEqual(1L, _svc.At(1));
    }

    [Aspect("A1.2", CompetitorVisible = true)]
    [Theory]
    [InlineData(2, 1L)]
    [InlineData(7, 13L)]
    [InlineData(10, 55L)]
    [InlineData(40, 102334155L)]
    public void At_TypicalIndices_ReturnTheSequenceValue(int index, long expected) =>
        Log.AssertEqual(expected, _svc.At(index));

    [Aspect("A1.3", CompetitorVisible = true)]
    [Fact]
    public void At_NegativeIndex_ReturnsTheSentinel() =>
        Log.AssertEqual(-1L, _svc.At(-1));

    [Aspect("A1.4")]
    [Fact]
    public void At_BeyondLongRange_ReturnsTheSentinel()
    {
        // 92 is the last index that fits; 93 overflows. A naive implementation wraps to a negative number here
        // instead of reporting the sentinel, which is why this is graded separately.
        Log.AssertEqual(7540113804746346429L, _svc.At(92));
        Log.AssertEqual(-1L, _svc.At(93));
    }

    // ---- A2: Sequence() ----

    [Aspect("A2.1", CompetitorVisible = true)]
    [Fact]
    public void Sequence_ReturnsTheFirstNValues() =>
        Log.AssertEqual("0,1,1,2,3,5,8", string.Join(',', _svc.Sequence(7)));

    [Aspect("A2.2", CompetitorVisible = true)]
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Sequence_WithNonPositiveCount_IsEmpty(int count) =>
        Log.AssertEmpty(_svc.Sequence(count));

    [Aspect("A2.3")]
    [Fact]
    public void Sequence_OfOne_IsJustZero() =>
        Log.AssertEqual(0L, Log.AssertSingle(_svc.Sequence(1)));

    // ---- A3: IsFibonacci() ----

    [Aspect("A3.1", CompetitorVisible = true)]
    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(21L)]
    [InlineData(6765L)]
    public void IsFibonacci_ForMembers_IsTrue(long value) =>
        Log.AssertTrue(_svc.IsFibonacci(value));

    [Aspect("A3.2", CompetitorVisible = true)]
    [Theory]
    [InlineData(4L)]
    [InlineData(22L)]
    [InlineData(-3L)]
    public void IsFibonacci_ForNonMembers_IsFalse(long value) =>
        Log.AssertFalse(_svc.IsFibonacci(value));

    // ---- A4: Sum() ----

    [Aspect("A4.1", CompetitorVisible = true)]
    [Fact]
    public void Sum_AddsTheFirstNValues() =>
        Log.AssertEqual(20L, _svc.Sum(7));

    [Aspect("A4.2")]
    [Fact]
    public void Sum_WithNonPositiveCount_IsZero() =>
        Log.AssertEqual(0L, _svc.Sum(0));
}
