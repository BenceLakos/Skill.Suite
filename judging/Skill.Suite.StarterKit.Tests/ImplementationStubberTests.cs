using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Skill.Suite.StarterKit;
using Xunit;

namespace Skill.Suite.StarterKit.Tests;

/// <summary>
/// What survives into a competitor's starting point, and what must not.
/// </summary>
/// <remarks>
/// Every one of these is a leak if it regresses: a private helper's name gives away the decomposition, a
/// surviving field can be the answer in data form, and the reference implementation's own doc comment tells the
/// competitor they are reading the answer key.
/// </remarks>
public sealed class ImplementationStubberTests
{
    private static string Stub(string source)
    {
        var root = (CompilationUnitSyntax)CSharpSyntaxTree.ParseText(source).GetRoot();
        return ((CompilationUnitSyntax)new ImplementationStubber().Visit(root)!)
            .NormalizeWhitespace().ToFullString();
    }

    private const string Reference = """
        namespace S;

        /// <summary>The reference implementation — the answer key.</summary>
        public sealed class Calc : ICalc
        {
            private const int Limit = 92;
            private static readonly int[] Table = [1, 1, 2, 3, 5];
            private int _calls;

            public int Add(int a, int b) => a + b;

            public int Divide(int a, int b)
            {
                if (b == 0) return 0;
                return a / b;
            }

            public string Name { get; set; } = "reference";

            public int Computed => Table[Limit % Table.Length];

            public Calc(int seed) { _calls = seed; }

            private int Helper(int x) => x * 2;

            internal void AlsoHidden() { }
        }
        """;

    [Fact]
    public void PublicMethodBodiesAreReplacedButSignaturesAreKept()
    {
        var stubbed = Stub(Reference);

        Assert.Contains("public int Add(int a, int b) => throw new NotImplementedException();", stubbed);
        Assert.Contains("public int Divide(int a, int b) => throw new NotImplementedException();", stubbed);
        Assert.DoesNotContain("a + b", stubbed);
        Assert.DoesNotContain("a / b", stubbed);
    }

    [Fact]
    public void NonPublicMembersAreRemovedEntirely()
    {
        var stubbed = Stub(Reference);

        // The name alone is the leak: "Helper(int)" tells a competitor the shape of the intended solution.
        Assert.DoesNotContain("Helper", stubbed);
        Assert.DoesNotContain("AlsoHidden", stubbed);
    }

    [Fact]
    public void NonConstFieldsAreRemovedAndConstantsSurvive()
    {
        var stubbed = Stub(Reference);

        // A lookup table is the answer in data form.
        Assert.DoesNotContain("Table", stubbed);
        Assert.DoesNotContain("_calls", stubbed);
        // A documented bound is usually part of the contract, and member docs may cite it.
        Assert.Contains("private const int Limit = 92;", stubbed);
    }

    [Fact]
    public void AutoPropertyKeepsItsAccessorsButLosesItsInitializer()
    {
        var stubbed = Stub(Reference);

        Assert.Contains("public string Name { get; set; }", stubbed);
        Assert.DoesNotContain("\"reference\"", stubbed);
    }

    [Fact]
    public void ComputedPropertyBecomesAThrowingMember()
    {
        var stubbed = Stub(Reference);

        Assert.Contains("public int Computed => throw new NotImplementedException();", stubbed);
    }

    [Fact]
    public void PublicConstructorIsKeptWithAnEmptyBody()
    {
        var stubbed = Stub(Reference);

        // Kept because a competitor may need to construct the type; emptied because the body assigned a field
        // that no longer exists.
        Assert.Contains("public Calc(int seed)", stubbed);
        Assert.DoesNotContain("_calls = seed", stubbed);
    }

    [Fact]
    public void TypeDocCommentIsReplacedWithOneAddressedToTheCompetitor()
    {
        var stubbed = Stub(Reference);

        Assert.DoesNotContain("answer key", stubbed);
        Assert.Contains("Implement the members below.", stubbed);
    }

    [Fact]
    public void TheTypeBannerIsASingleLine()
    {
        // Member-level docs are the contract and stay; the type's own banner is boilerplate every competitor
        // scrolls past, so it is one line naming the two rules the harness enforces and nothing else.
        var stubbed = Stub("""
            /// <summary>Reference.</summary>
            public sealed class Calc
            {
                public int Add(int a, int b) => a + b;
            }
            """);
        var docLines = stubbed.Split('\n').Count(line => line.TrimStart().StartsWith("///"));

        Assert.Equal(1, docLines);
    }

    [Fact]
    public void TheResultParsesWithoutErrors()
    {
        // The whole point is a skeleton that compiles. Syntax errors are the failure mode that would cost every
        // competitor the same confused ten minutes.
        var diagnostics = CSharpSyntaxTree.ParseText(Stub(Reference))
            .GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void ExpressionBodiedAndBlockBodiedMembersProduceTheSameShape()
    {
        var stubbed = Stub("""
            public class C
            {
                public int A() => 1;
                public int B() { return 2; }
            }
            """);

        Assert.Contains("public int A() => throw new NotImplementedException();", stubbed);
        Assert.Contains("public int B() => throw new NotImplementedException();", stubbed);
    }

    [Fact]
    public void RecordsAndStructsAreRebannedToo()
    {
        var stubbed = Stub("""
            /// <summary>Secret notes.</summary>
            public record struct Point(int X, int Y);
            """);

        Assert.DoesNotContain("Secret notes", stubbed);
        Assert.Contains("Implement the members below.", stubbed);
    }
}
