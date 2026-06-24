using MAIHealthCoach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MAIHealthCoach.Api.Tests.Food;

/// <summary>
/// A test-only <see cref="IModelCustomizer"/> that maps every <see cref="DateTimeOffset"/> property
/// to a sortable <see cref="long"/> (UTC ticks) when the model is built on SQLite.
/// </summary>
/// <remarks>
/// SQLite has no native <see cref="DateTimeOffset"/> type and cannot translate it in <c>ORDER BY</c>
/// or comparison clauses, so a query such as <c>OrderByDescending(f =&gt; f.LastSyncedAt)</c> —
/// which the production <c>NutritionLookupService</c> issues to pick the freshest cached barcode row
/// — throws <see cref="NotSupportedException"/> under the SQLite provider used for these integration
/// tests. Production runs on PostgreSQL (Npgsql), which orders <see cref="DateTimeOffset"/> natively,
/// so this converter is a pure test-harness shim: it changes only how the value is stored in the test
/// database, never the production mapping or the code under test.
/// </remarks>
internal sealed class SqliteDateTimeOffsetModelCustomizer : RelationalModelCustomizer
{
    private static readonly ValueConverter<DateTimeOffset, long> ToTicks =
        new(v => v.UtcTicks, v => new DateTimeOffset(v, TimeSpan.Zero));

    private static readonly ValueConverter<DateTimeOffset?, long?> ToNullableTicks =
        new(
            v => v.HasValue ? v.Value.UtcTicks : null,
            v => v.HasValue ? new DateTimeOffset(v.Value, TimeSpan.Zero) : null);

    public SqliteDateTimeOffsetModelCustomizer(ModelCustomizerDependencies dependencies)
        : base(dependencies)
    {
    }

    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset))
                {
                    property.SetValueConverter(ToTicks);
                }
                else if (property.ClrType == typeof(DateTimeOffset?))
                {
                    property.SetValueConverter(ToNullableTicks);
                }
            }
        }
    }
}
