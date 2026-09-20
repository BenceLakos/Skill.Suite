namespace Skill.Suite.Application.Tests.Sql;

using Skill.Suite.Infra.Sql;
using Xunit;

/// <summary>
/// Where a seed script is cut, and where it is not.
/// </summary>
/// <remarks>
/// The whole point of the splitter is that an administrator can pick a file exported from Management Studio
/// without editing it first, so the cases here are the ones such an export actually contains: indented
/// separators, a repeat count, mixed case, and a final batch with no separator after it.
/// </remarks>
public sealed class SqlBatchSplitterTests
{
    [Fact]
    public void AScriptWithoutSeparatorsIsOneBatch() =>
        Assert.Equal(
            ["CREATE TABLE dbo.Orders (Id INT);"],
            SqlBatchSplitter.Split("CREATE TABLE dbo.Orders (Id INT);"));

    [Fact]
    public void EachSeparatorEndsABatchAndIsNotSentWithIt() =>
        Assert.Equal(
            ["CREATE TABLE dbo.Orders (Id INT);", "INSERT INTO dbo.Orders VALUES (1);"],
            SqlBatchSplitter.Split(
                """
                CREATE TABLE dbo.Orders (Id INT);
                GO
                INSERT INTO dbo.Orders VALUES (1);
                """));

    [Theory]
    [InlineData("go")]
    [InlineData("Go")]
    [InlineData("gO")]
    [InlineData("   GO")]
    [InlineData("GO   ")]
    [InlineData("\tGO\t")]
    public void CaseAndSurroundingWhitespaceDoNotHideASeparator(string separator) =>
        Assert.Equal(
            ["SELECT 1;", "SELECT 2;"],
            SqlBatchSplitter.Split($"SELECT 1;\n{separator}\nSELECT 2;"));

    [Fact]
    public void ACountRepeatsTheBatchThatManyTimes() =>
        // What GO 3 means to sqlcmd, and what a seed script that inserts a block of sample rows relies on.
        Assert.Equal(
            ["INSERT INTO dbo.Orders DEFAULT VALUES;", "INSERT INTO dbo.Orders DEFAULT VALUES;",
             "INSERT INTO dbo.Orders DEFAULT VALUES;", "SELECT COUNT(*) FROM dbo.Orders;"],
            SqlBatchSplitter.Split(
                """
                INSERT INTO dbo.Orders DEFAULT VALUES;
                GO 3
                SELECT COUNT(*) FROM dbo.Orders;
                """));

    [Theory]
    [InlineData("GO 0")]
    [InlineData("GO 1")]
    public void ACountThatRepeatsNothingStillRunsTheBatchOnce(string separator) =>
        Assert.Equal(["SELECT 1;"], SqlBatchSplitter.Split($"SELECT 1;\n{separator}\n"));

    [Fact]
    public void ATrailingSeparatorDoesNotProduceAnEmptyBatch() =>
        Assert.Equal(["SELECT 1;"], SqlBatchSplitter.Split("SELECT 1;\nGO\n"));

    [Fact]
    public void SeparatorsWithNothingBetweenThemAreDropped() =>
        Assert.Equal(
            ["SELECT 1;", "SELECT 2;"],
            SqlBatchSplitter.Split("GO\nSELECT 1;\nGO\nGO\n\nGO\nSELECT 2;\nGO"));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\n")]
    [InlineData("GO")]
    public void AScriptWithNothingToRunProducesNoBatches(string script) =>
        Assert.Empty(SqlBatchSplitter.Split(script));

    [Fact]
    public void WindowsLineEndingsAreSplitOnAndKept()
    {
        var batches = SqlBatchSplitter.Split("SELECT 1;\r\nSELECT 2;\r\nGO\r\nSELECT 3;\r\n");

        Assert.Equal(["SELECT 1;\r\nSELECT 2;", "SELECT 3;"], batches);
    }

    [Theory]
    [InlineData("GOTO done;")]
    [InlineData("GO TO")]
    [InlineData("GO -- reset")]
    [InlineData("SELECT 1; GO")]
    [InlineData("EXEC dbo.Go;")]
    public void ALineThatOnlyStartsWithGoIsNotASeparator(string line) =>
        // Everything a separator is not stays in the batch verbatim: cutting on any of these would split a
        // statement in half and fail on syntax the author wrote correctly.
        Assert.Equal([$"SELECT 1;\n{line}"], SqlBatchSplitter.Split($"SELECT 1;\n{line}"));

    [Fact]
    public void ASeparatorInsideAStringLiteralIsSplitOnAnyway()
    {
        // Documented, not desirable. Recognising it would mean lexing T-SQL, which is a far larger risk to
        // the scripts that are fine than this case is to the one script in a thousand that hits it —
        // sqlcmd behaves the same way, so an author who has run their script has already met this.
        var batches = SqlBatchSplitter.Split("PRINT '\nGO\n';");

        Assert.Equal(["PRINT '", "';"], batches);
    }

    [Fact]
    public void BatchesKeepTheirOwnContentAndOrder()
    {
        var batches = SqlBatchSplitter.Split(
            """
            CREATE SCHEMA orders;
            GO
            CREATE TABLE orders.Lines (Id INT, Note NVARCHAR(50));
            GO
            INSERT INTO orders.Lines VALUES (1, 'first');
            INSERT INTO orders.Lines VALUES (2, 'second');
            """);

        Assert.Equal(
            ["CREATE SCHEMA orders;",
             "CREATE TABLE orders.Lines (Id INT, Note NVARCHAR(50));",
             "INSERT INTO orders.Lines VALUES (1, 'first');\nINSERT INTO orders.Lines VALUES (2, 'second');"],
            batches);
    }
}
