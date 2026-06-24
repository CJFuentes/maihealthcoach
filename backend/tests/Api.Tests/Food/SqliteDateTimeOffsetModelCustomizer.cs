using System.Globalization;
using MAIHealthCoach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MAIHealthCoach.Api.Tests.Food;

/// <summary>
/// A test-only <see cref="IModelCustomizer"/> that, when the model is built on SQLite, maps every
/// <see cref="DateTimeOffset"/> property to a sortable <see cref="long"/> (UTC ticks) and every
/// <see cref="DateOnly"/> property to an ISO-8601 (<c>yyyy-MM-dd</c>) string.
/// </summary>
/// <remarks>
/// SQLite has no native <see cref="DateTimeOffset"/> type and cannot translate it in <c>ORDER BY</c>
/// or comparison clauses, so a query such as <c>OrderByDescending(f =&gt; f.LastSyncedAt)</c> —
/// which the production <c>NutritionLookupService</c> issues to pick the freshest cached barcode row
/// — throws <see cref="NotSupportedException"/> under the SQLite provider used for these integration
/// tests. Likewise the SQLite provider does not translate equality/range predicates over
/// <see cref="DateOnly"/> columns (e.g. the food diary's <c>WHERE e.Date == date</c> day lookup,
/// issue #22) without an explicit converter. Production runs on PostgreSQL (Npgsql), which handles
/// both types natively, so this customizer is a pure test-harness shim: it changes only how values
/// are stored in the test database, never the production mapping or the code under test.
/// </remarks>
internal sealed class SqliteDateTimeOffsetModelCustomizer : RelationalModelCustomizer
{
    private static readonly ValueConverter<DateTimeOffset, long> ToTicks =
        new(v => v.UtcTicks, v => new DateTimeOffset(v, TimeSpan.Zero));

    private static readonly ValueConverter<DateTimeOffset?, long?> ToNullableTicks =
        new(
            v => v.HasValue ? v.Value.UtcTicks : null,
            v => v.HasValue ? new DateTimeOffset(v.Value, TimeSpan.Zero) : null);

    private static readonly ValueConverter<DateOnly, string> DateOnlyToString =
        new(
            v => v.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            v => DateOnly.ParseExact(v, "yyyy-MM-dd", CultureInfo.InvariantCulture));

    private static readonly ValueConverter<DateOnly?, string?> NullableDateOnlyToString =
        new(
            v => v.HasValue ? v.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null,
            v => v == null ? null : DateOnly.ParseExact(v, "yyyy-MM-dd", CultureInfo.InvariantCulture));

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
                else if (property.ClrType == typeof(DateOnly))
                {
                    property.SetValueConverter(DateOnlyToString);
                }
                else if (property.ClrType == typeof(DateOnly?))
                {
                    property.SetValueConverter(NullableDateOnlyToString);
                }
            }
        }
    }
}
