using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Skill.Suite.StarterKit;

/// <summary>
/// Rewrites a reference implementation into a compiling skeleton with the answer removed.
/// </summary>
/// <remarks>
/// The rule is deliberately blunt, because a subtle one leaks:
/// <list type="bullet">
/// <item>Public members keep their declaration exactly as written — signature, attributes, member-level XML
/// docs — and get a <see cref="NotImplementedException"/> body.</item>
/// <item>Non-public members are <b>removed entirely</b>. A private helper's name and signature give away the
/// approach, and once every public body throws, nothing references them.</item>
/// <item>Fields are removed unless <c>const</c>. An implementation's fields <i>are</i> its approach — a lookup
/// table or a memo cache is the answer in data form. Constants survive because they are usually part of the
/// contract (a documented sentinel, an upper bound) and removing them breaks the doc comments that cite them.</item>
/// <item>A type's own doc comment is replaced. The reference implementation's summary describes the answer key,
/// which is both wrong and a hint once the competitor is the one reading it.</item>
/// </list>
/// Blanking bodies while keeping private members would compile too, but it would hand competitors the
/// decomposition, which for many sessions is most of the marks.
/// <para>
/// Nothing here manages whitespace: <see cref="StarterKitGenerator"/> reformats the rewritten tree in one pass.
/// Preserving the author's layout around members that have been gutted is not worth the trivia surgery — the
/// output is a generated file, and consistent formatting serves the competitor better than a faithful echo of
/// a layout whose bodies are gone.
/// </para>
/// </remarks>
public sealed class ImplementationStubber : CSharpSyntaxRewriter
{
    /// <inheritdoc/>
    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node) =>
        IsPublic(node.Modifiers)
            ? node.WithBody(null).WithExpressionBody(Arrow()).WithSemicolonToken(Semicolon())
            : null;

    /// <inheritdoc/>
    public override SyntaxNode? VisitPropertyDeclaration(PropertyDeclarationSyntax node)
    {
        if (!IsPublic(node.Modifiers))
            return null;

        // An auto-property keeps its accessors — they carry no logic. Anything with real accessor bodies, or an
        // initializer that could hold the answer, collapses to a throwing getter.
        var isAuto = node.AccessorList is { } list
                     && list.Accessors.All(a => a.Body is null && a.ExpressionBody is null);

        return isAuto
            ? node.WithInitializer(null).WithSemicolonToken(default)
            : node.WithAccessorList(null)
                .WithInitializer(null)
                .WithExpressionBody(Arrow())
                .WithSemicolonToken(Semicolon());
    }

    /// <inheritdoc/>
    public override SyntaxNode? VisitConstructorDeclaration(ConstructorDeclarationSyntax node) =>
        // Kept with an empty body rather than removed: a competitor may need to construct the type, and the
        // parameter list is part of the shape they are being asked to fill in.
        IsPublic(node.Modifiers)
            ? node.WithInitializer(null)
                .WithExpressionBody(null)
                .WithSemicolonToken(default)
                .WithBody(SyntaxFactory.Block())
            : null;

    /// <inheritdoc/>
    public override SyntaxNode? VisitFieldDeclaration(FieldDeclarationSyntax node) =>
        node.Modifiers.Any(SyntaxKind.ConstKeyword) ? node : null;

    /// <inheritdoc/>
    public override SyntaxNode? VisitOperatorDeclaration(OperatorDeclarationSyntax node) =>
        IsPublic(node.Modifiers)
            ? node.WithBody(null).WithExpressionBody(Arrow()).WithSemicolonToken(Semicolon())
            : null;

    /// <inheritdoc/>
    public override SyntaxNode? VisitIndexerDeclaration(IndexerDeclarationSyntax node) =>
        IsPublic(node.Modifiers)
            ? node.WithAccessorList(null).WithExpressionBody(Arrow()).WithSemicolonToken(Semicolon())
            : null;

    /// <inheritdoc/>
    public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node) =>
        Reban(base.VisitClassDeclaration(node));

    /// <inheritdoc/>
    public override SyntaxNode? VisitRecordDeclaration(RecordDeclarationSyntax node) =>
        Reban(base.VisitRecordDeclaration(node));

    /// <inheritdoc/>
    public override SyntaxNode? VisitStructDeclaration(StructDeclarationSyntax node) =>
        Reban(base.VisitStructDeclaration(node));

    /// <summary>Replaces a type's documentation comment with one addressed to the competitor.</summary>
    private static SyntaxNode? Reban(SyntaxNode? node)
    {
        if (node is not MemberDeclarationSyntax member)
            return node;

        // Member-level docs survive: those describe the contract, which is what the competitor needs. Only the
        // type's own summary is replaced.
        var kept = member.GetLeadingTrivia()
            .Where(t => !t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                        && !t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));

        return member.WithLeadingTrivia(kept.Concat(Banner));
    }

    private static bool IsPublic(SyntaxTokenList modifiers) => modifiers.Any(SyntaxKind.PublicKeyword);

    private static ArrowExpressionClauseSyntax Arrow() =>
        SyntaxFactory.ArrowExpressionClause(
            SyntaxFactory.ThrowExpression(
                SyntaxFactory.ObjectCreationExpression(
                        SyntaxFactory.ParseTypeName("NotImplementedException"))
                    .WithArgumentList(SyntaxFactory.ArgumentList())));

    private static SyntaxToken Semicolon() => SyntaxFactory.Token(SyntaxKind.SemicolonToken);

    // One line, on purpose: the contract's own XML docs say what each member has to do, and a competitor
    // reads a paragraph of boilerplate once and then scrolls past it every time after.
    private static readonly SyntaxTriviaList Banner = SyntaxFactory.ParseLeadingTrivia(
        """
        /// <summary>Implement the members below. Keep this folder's name and its csproj, and add no package references.</summary>

        """);
}
