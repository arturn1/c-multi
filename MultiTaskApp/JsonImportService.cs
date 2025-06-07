using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MultiTaskApp.Data;
using MultiTaskApp.Models;

public class JsonImportService
{
    private readonly IServiceProvider _serviceProvider;

    public JsonImportService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task ImportFromJsonFiles(IEnumerable<string> filePaths)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

        var entities = new List<Entity>();
        var facts = new List<Fact>();
        var units = new List<Unit>();
        var values = new List<Value>();

        foreach (var filePath in filePaths)
        {
            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                using var doc = JsonDocument.Parse(json);

                var root = doc.RootElement;

                // Handle `cik` as string or number
                int cik = ParseCik(root, filePath);

                var entityName = root.TryGetProperty("entityName", out var nameProperty) && nameProperty.ValueKind != JsonValueKind.Null
                    ? nameProperty.GetString()
                    : "Unknown";

                // Check if the entity already exists
                var entity = entities.FirstOrDefault(e => e.Cik == cik) ?? new Entity
                {
                    Id = Guid.NewGuid(),
                    Cik = cik,
                    EntityName = entityName,
                    Facts = new List<Fact>()
                };

                if (!entities.Contains(entity))
                {
                    entities.Add(entity);
                }

                var factsElement = root.GetProperty("facts");
                foreach (var namespaceElement in factsElement.EnumerateObject())
                {
                    var namespaceName = namespaceElement.Name;
                    foreach (var factElement in namespaceElement.Value.EnumerateObject())
                    {
                        var factName = factElement.Name;
                        var factLabel = factElement.Value.TryGetProperty("label", out var labelProperty) && labelProperty.ValueKind != JsonValueKind.Null
                            ? labelProperty.GetString()
                            : "No Label";
                        var factDescription = factElement.Value.TryGetProperty("description", out var descriptionProperty) && descriptionProperty.ValueKind != JsonValueKind.Null
                            ? descriptionProperty.GetString()
                            : "No Description";

                        var fact = new Fact
                        {
                            Id = Guid.NewGuid(),
                            EntityId = entity.Id,
                            Namespace = namespaceName,
                            Name = factName,
                            Label = factLabel,
                            Description = factDescription,
                            Units = new List<Unit>()
                        };

                        facts.Add(fact);

                        if (factElement.Value.TryGetProperty("units", out var unitsElement))
                        {
                            foreach (var unitElement in unitsElement.EnumerateObject())
                            {
                                var unitType = unitElement.Name;

                                var unit = new Unit
                                {
                                    Id = Guid.NewGuid(),
                                    FactId = fact.Id,
                                    UnitType = unitType,
                                    Values = new List<Value>()
                                };

                                units.Add(unit);

                                foreach (var valueElement in unitElement.Value.EnumerateArray())
                                {
                                    if (!valueElement.TryGetProperty("val", out var valProp) || valProp.ValueKind == JsonValueKind.Null)
                                        continue;

                                    if (!valueElement.TryGetProperty("end", out var endProp) || endProp.ValueKind == JsonValueKind.Null)
                                        continue;

                                    var value = new Value
                                    {
                                        Id = Guid.NewGuid(),
                                        UnitId = unit.Id,
                                        EndDate = DateTime.Parse(endProp.GetString()),
                                        Val = valProp.GetDecimal(),
                                        Accn = valueElement.TryGetProperty("accn", out var accnProp) && accnProp.ValueKind != JsonValueKind.Null
                                            ? accnProp.GetString()
                                            : null,
                                        FiscalYear = valueElement.TryGetProperty("fy", out var fyProp) && fyProp.ValueKind != JsonValueKind.Null
                                            ? fyProp.GetInt32()
                                            : (int?)null,
                                        FiscalPeriod = valueElement.TryGetProperty("fp", out var fpProp) && fpProp.ValueKind != JsonValueKind.Null
                                            ? fpProp.GetString()
                                            : null,
                                        Form = valueElement.TryGetProperty("form", out var formProp) && formProp.ValueKind != JsonValueKind.Null
                                            ? formProp.GetString()
                                            : null,
                                        FiledDate = valueElement.TryGetProperty("filed", out var filedProp) && filedProp.ValueKind != JsonValueKind.Null
                                            ? DateTime.Parse(filedProp.GetString())
                                            : (DateTime?)null,
                                        Frame = valueElement.TryGetProperty("frame", out var frameProp) && frameProp.ValueKind != JsonValueKind.Null
                                            ? frameProp.GetString()
                                            : null
                                    };

                                    values.Add(value);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error processing file: {filePath}. Details: {ex.Message}", ex);
            }
        }

        // Bulk insert in batches of 500
        await BulkInsertAsync(dbContext, entities, 500);
        await BulkInsertAsync(dbContext, facts, 500);
        await BulkInsertAsync(dbContext, units, 500);
        await BulkInsertAsync(dbContext, values, 500);
    }

    private async Task BulkInsertAsync<T>(ApplicationContext dbContext, List<T> items, int batchSize) where T : class
    {
        for (int i = 0; i < items.Count; i += batchSize)
        {
            var batch = items.Skip(i).Take(batchSize).ToList();
            await dbContext.Set<T>().AddRangeAsync(batch);
            await dbContext.SaveChangesAsync();
        }
    }

    private int ParseCik(JsonElement root, string filePath)
    {
        if (root.TryGetProperty("cik", out var cikProperty))
        {
            if (cikProperty.ValueKind == JsonValueKind.String && int.TryParse(cikProperty.GetString(), out var cik))
            {
                return cik;
            }
            else if (cikProperty.ValueKind == JsonValueKind.Number)
            {
                return cikProperty.GetInt32();
            }
        }

        throw new FormatException($"Invalid or missing CIK in file: {filePath}");
    }
}