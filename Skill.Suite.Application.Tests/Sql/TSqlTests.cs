namespace Skill.Suite.Application.Tests.Sql;

using Skill.Suite.Infra.Sql;
using Xunit;

/// <summary>
/// The escaping that keeps a competitor-supplied name or password from becoming T-SQL.
/// </summary>
/// <remarks>
/// <c>CREATE LOGIN</c>, <c>CREATE DATABASE</c> and <c>DROP</c> take no parameters, so this is the only defence
/// those statements have. Everything else in the application reaches the database through EF Core.
/// </remarks>
public sealed class TSqlTests
{
    [Fact]
    public void AnOrdinaryNameIsBracketed() =>
        Assert.Equal("[c01]", TSql.QuoteName("c01"));

    [Fact]
    public void AClosingBracketIsDoubledSoTheIdentifierStaysOneIdentifier()
    {
        // Without the doubling the first ] ends the identifier and everything after it is parsed as SQL.
        var quoted = TSql.QuoteName("a]; DROP DATABASE x --");

        Assert.Equal("[a]]; DROP DATABASE x --]", quoted);
        Assert.StartsWith("[", quoted, StringComparison.Ordinal);
        Assert.EndsWith("]", quoted, StringComparison.Ordinal);
    }

    [Fact]
    public void ASingleQuoteInsideAnIdentifierIsNotSpecial() =>
        // Only ] terminates a bracketed identifier, so a quote needs no treatment here.
        Assert.Equal("[o'brien]", TSql.QuoteName("o'brien"));

    [Fact]
    public void AnEmptyIdentifierIsRejected() =>
        Assert.Throws<ArgumentException>(() => TSql.QuoteName(string.Empty));

    [Fact]
    public void AWhitespaceIdentifierIsRejected() =>
        Assert.Throws<ArgumentException>(() => TSql.QuoteName("   "));

    [Fact]
    public void AnIdentifierAtTheServerLimitIsAccepted() =>
        Assert.Equal(
            TSql.MaxIdentifierLength + 2,
            TSql.QuoteName(new string('a', TSql.MaxIdentifierLength)).Length);

    [Fact]
    public void AnIdentifierOverTheServerLimitIsRejected() =>
        // The server would reject it anyway, but with an error the admin has to decode.
        Assert.Throws<ArgumentException>(
            () => TSql.QuoteName(new string('a', TSql.MaxIdentifierLength + 1)));

    [Fact]
    public void AnOrdinaryValueIsQuoted() =>
        Assert.Equal("'secret'", TSql.Literal("secret"));

    [Fact]
    public void ASingleQuoteIsDoubledSoTheLiteralStaysOneLiteral()
    {
        // A passphrase with an apostrophe is ordinary. Untreated, it closes the string mid-statement.
        var literal = TSql.Literal("it's'; DROP LOGIN c01 --");

        Assert.Equal("'it''s''; DROP LOGIN c01 --'", literal);
    }

    [Fact]
    public void AnEmptyValueIsAValidLiteral() =>
        // Not this application's job to reject it — the server refuses an empty password on its own terms.
        Assert.Equal("''", TSql.Literal(string.Empty));
}
