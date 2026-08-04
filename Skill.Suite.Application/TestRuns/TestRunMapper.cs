using Riok.Mapperly.Abstractions;
using Skill.Suite.Domain.TestRuns;

namespace Skill.Suite.Application.TestRuns;

[Mapper]
public static partial class TestRunMapper
{
    [MapperIgnoreSource(nameof(TestRun.DomainEvents))]
    public static partial TestRunDto ToDto(TestRun run);

    [MapperIgnoreSource(nameof(TestFixtureResult.TestRunId))]
    [MapperIgnoreSource(nameof(TestFixtureResult.DomainEvents))]
    private static partial TestFixtureResultDto ToDto(TestFixtureResult fixture);

    [MapperIgnoreSource(nameof(UnitTestResult.FixtureId))]
    [MapperIgnoreSource(nameof(UnitTestResult.DomainEvents))]
    private static partial UnitTestResultDto ToDto(UnitTestResult test);

    private static partial TestEventDto ToDto(TestEventRecord record);
}
