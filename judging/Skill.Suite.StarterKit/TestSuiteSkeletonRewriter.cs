using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Skill.Suite.StarterKit;

/// <summary>
/// Empties a black-box session's reference suite into the wired-but-testless project competitors receive.
/// </summary>
/// <remarks>
/// The rules are the <b>inverse</b> of <see cref="ImplementationStubber"/>, and the inversion is the whole point:
/// <list type="bullet">
/// <item><b>Private fields are kept.</b> In a test suite they are the harness wiring —
/// <c>ServiceResolver.Resolve&lt;T&gt;().WithCallLogging()</c> — not the answer. The stubber removes fields
/// because an implementation's fields <i>are</i> its approach; here removing them would leave a project that
/// cannot resolve the service under test.</item>
/// <item><b>Every method is removed.</b> The test methods are the answer — writing them is the marks. Helpers go
/// with them: once no test remains, nothing references a helper, and its name still hints at the approach.</item>
/// <item><b>Constructors are kept verbatim.</b> <c>: base(output, scope)</c> is the fixture wiring, and without
/// it there is no start/finish-fixture pair and the platform records nothing.</item>
/// <item>The class doc comment is replaced, as in the stubber — the reference suite's summary says it is the
/// calibration suite, which is both wrong and a hint once the competitor is reading it.</item>
/// </list>
/// A commented-out example test is added so the competitor has the required shape to copy. Commented rather than
/// live so the delivered project compiles with zero tests — a starter kit that fails to build costs every
/// competitor the same confused ten minutes.
/// </remarks>
public sealed class TestSuiteSkeletonRewriter : CSharpSyntaxRewriter
{
    /// <inheritdoc/>
    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node) => null;

    /// <inheritdoc/>
    public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        var visited = (ClassDeclarationSyntax)base.VisitClassDeclaration(node)!;

        // Attached to the closing brace rather than appended as a member: it has to survive as trivia, because
        // a real member would make the delivered project fail to compile or ship a passing test for free.
        var withExample = visited.WithCloseBraceToken(
            visited.CloseBraceToken.WithLeadingTrivia(
                visited.CloseBraceToken.LeadingTrivia.Concat(Example)));

        return Reban(withExample);
    }

    /// <inheritdoc/>
    public override SyntaxNode? VisitRecordDeclaration(RecordDeclarationSyntax node) =>
        Reban((RecordDeclarationSyntax)base.VisitRecordDeclaration(node)!);

    /// <summary>Replaces a type's documentation comment with one addressed to the competitor.</summary>
    private static SyntaxNode Reban(MemberDeclarationSyntax member)
    {
        var kept = member.GetLeadingTrivia()
            .Where(t => !t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                        && !t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));

        return member.WithLeadingTrivia(kept.Concat(Banner));
    }

    private static readonly SyntaxTriviaList Banner = SyntaxFactory.ParseLeadingTrivia(
        """
        /// <summary>
        /// Your test suite. In a testing session this is what is graded — coverage of the hidden implementation
        /// and the mutation kill rate, neither of which is the number of tests. In an implementation session
        /// the graded suite is hidden and this project is yours to test your own work with; the same shape
        /// applies.
        /// </summary>
        /// <remarks>
        /// Where this suite is the graded artefact, it is scored on two things, and neither is how many tests
        /// you write:
        /// <list type="number">
        /// <item><b>Coverage</b> — how much of the hidden implementation your suite exercises.</item>
        /// <item><b>Mutation kill rate</b> — how many deliberately injected faults your suite catches. This is
        /// the one that separates running the code from checking it: a test that calls a method and asserts the
        /// result is a number adds coverage and kills nothing.</item>
        /// </list>
        /// Keep the class shape below — derive from <c>LoggedTest&lt;TSelf&gt;</c>, take the fixture, resolve
        /// through <c>ServiceResolver</c>, and assert through <c>Log</c>. A bare <c>Assert</c> still fails the
        /// test but emits no event, so your result arrives with nothing explaining it. Keep this folder's name
        /// and its csproj, and do not add package references — the judge restores offline and a new reference
        /// fails the run.
        /// <para>
        /// Boundaries are where the faults are. For every documented threshold, test the value at it and the
        /// value just below it.
        /// </para>
        /// </remarks>

        """);

    private static readonly SyntaxTriviaList Example = SyntaxFactory.ParseLeadingTrivia(
        """
            // Write your tests here. This example shows the required shape; it is commented out so the project
            // you received compiles with zero tests.
            //
            // [Aspect("C1", CompetitorVisible = true)]
            // [Fact]
            // public void SomeRule_AtItsBoundary_BehavesAsDocumented() =>
            //     Log.AssertEqual(expected, _service.SomeMethod(input));
            //
            // [Aspect("C2", CompetitorVisible = true)]
            // [Theory]
            // [InlineData(9, 180)]
            // [InlineData(10, 190)]
            // public void SomeRule_OnEachSideOfTheThreshold(int input, decimal expected) =>
            //     Log.AssertEqual(expected, _service.SomeMethod(input));

        """);
}
