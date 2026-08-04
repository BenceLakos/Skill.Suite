using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Skill.Suite.StarterKit;
using Xunit;

namespace Skill.Suite.StarterKit.Tests;

/// <summary>
/// What a black-box competitor receives, and what must not travel with it.
/// </summary>
/// <remarks>
/// In a black-box session the reference suite IS the answer — writing those tests is the marks. Every assertion
/// here guards a leak, and the field/constructor cases guard the opposite failure: stripping the harness wiring
/// leaves a project that cannot resolve the service under test.
/// </remarks>
public sealed class TestSuiteSkeletonRewriterTests
{
    private static string Rewrite(string source)
    {
        var root = (CompilationUnitSyntax)CSharpSyntaxTree.ParseText(source).GetRoot();
        return ((CompilationUnitSyntax)new TestSuiteSkeletonRewriter().Visit(root)!)
            .NormalizeWhitespace().ToFullString();
    }

    private const string ReferenceSuite = """
        using Skill.Suite.TestLog;
        using Skill.Suite.TestLog.Xunit;

        namespace S.UnitTests;

        /// <summary>The reference suite — calibration only, and the answer key.</summary>
        public sealed class EngineTests : LoggedTest<EngineTests>, IClassFixture<FixtureScope<EngineTests>>
        {
            private readonly IEngine _svc = ServiceResolver.Resolve<IEngine>().WithCallLogging();

            public EngineTests(ITestOutputHelper output, FixtureScope<EngineTests> scope)
                : base(output, scope) { }

            [Aspect("A1", CompetitorVisible = true)]
            [Fact]
            public void Total_AtTheBulkBoundary_TakesFivePercent() =>
                Log.AssertEqual(190m, _svc.Total(10, 20m));

            [Aspect("A2")]
            [Theory]
            [InlineData(99.99, 99.99)]
            [InlineData(100, 80)]
            public void Coupon_HonoursTheMinimum(decimal subtotal, decimal expected) =>
                Log.AssertEqual(expected, _svc.Coupon(subtotal));

            [Fact, Trait("category", "slow")]
            public void MultipleAttributesInOneList() => Log.AssertTrue(true);

            private static decimal Expected(int quantity) => quantity * 19m;
        }
        """;

    [Fact]
    public void EveryTestMethodIsRemoved()
    {
        var skeleton = Rewrite(ReferenceSuite);

        Assert.DoesNotContain("Total_AtTheBulkBoundary", skeleton);
        Assert.DoesNotContain("Coupon_HonoursTheMinimum", skeleton);
        Assert.DoesNotContain("MultipleAttributesInOneList", skeleton);
    }

    [Fact]
    public void ExpectedValuesAndInlineDataDoNotSurvive()
    {
        // The data rows are the answer as much as the assertions are: 99.99 -> 99.99 and 100 -> 80 give away
        // both the rule and its boundary.
        var skeleton = Rewrite(ReferenceSuite);

        Assert.DoesNotContain("190m", skeleton);
        Assert.DoesNotContain("99.99", skeleton);
        Assert.DoesNotContain("InlineData", skeleton.Replace("// [InlineData(9, 180)]", "")
            .Replace("// [InlineData(10, 190)]", ""));
    }

    [Fact]
    public void PrivateHelpersGoWithTheTests() =>
        // Unreferenced once the tests are gone, and the name still hints at the rule.
        Assert.DoesNotContain("Expected", Rewrite(ReferenceSuite).Replace("SomeRule_AtItsBoundary", ""));

    [Fact]
    public void HarnessWiringSurvives()
    {
        // The inverse of ImplementationStubber, which removes private fields. Here the field IS the wiring.
        var skeleton = Rewrite(ReferenceSuite);

        Assert.Contains("private readonly IEngine _svc = ServiceResolver.Resolve<IEngine>().WithCallLogging();", skeleton);
        Assert.Contains("public EngineTests(ITestOutputHelper output, FixtureScope<EngineTests> scope)", skeleton);
        Assert.Contains("base(output, scope)", skeleton);
    }

    [Fact]
    public void BaseListSurvives() =>
        // Without LoggedTest and the class fixture there is no start/finish-fixture pair, and the platform
        // records nothing at all for the run.
        Assert.Contains("LoggedTest<EngineTests>, IClassFixture<FixtureScope<EngineTests>>", Rewrite(ReferenceSuite));

    [Fact]
    public void ClassDocCommentIsReplaced()
    {
        var skeleton = Rewrite(ReferenceSuite);

        Assert.DoesNotContain("answer key", skeleton);
        Assert.DoesNotContain("calibration only", skeleton);
        Assert.Contains("Your test suite.", skeleton);
    }

    [Fact]
    public void AnExampleIsPresentButCommentedOut()
    {
        var skeleton = Rewrite(ReferenceSuite);

        Assert.Contains("// [Fact]", skeleton);
        Assert.Contains("// public void SomeRule_AtItsBoundary_BehavesAsDocumented", skeleton);
        // Live it would either fail to compile or ship the competitor a passing test for free.
        Assert.DoesNotContain("\n    [Fact]", skeleton);
    }

    [Fact]
    public void TheResultParsesWithoutErrors()
    {
        var errors = CSharpSyntaxTree.ParseText(Rewrite(ReferenceSuite))
            .GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        Assert.Empty(errors);
    }

    [Fact]
    public void UsingsAndNamespaceSurvive()
    {
        var skeleton = Rewrite(ReferenceSuite);

        Assert.Contains("using Skill.Suite.TestLog.Xunit;", skeleton);
        Assert.Contains("namespace S.UnitTests;", skeleton);
    }
}
