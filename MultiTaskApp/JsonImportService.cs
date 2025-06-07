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

    public async Task ImportFromJsonFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException($"The folder '{folderPath}' does not exist.");
        }

        var jsonFiles = Directory.GetFiles(folderPath, "*.json");
        var tasks = jsonFiles.Select(filePath => Task.Run(() => ImportFromJsonFile(filePath)));
        await Task.WhenAll(tasks);
    }

    public async Task ImportFromJsonFile(string filePath)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

        var json = await File.ReadAllTextAsync(filePath);
        using var doc = JsonDocument.Parse(json);

        var root = doc.RootElement;

        // Handle `cik` as string or number
        int cik;
        if (root.TryGetProperty("cik", out var cikProperty))
        {
            if (cikProperty.ValueKind == JsonValueKind.String)
            {
                if (!int.TryParse(cikProperty.GetString(), out cik))
                {
                    throw new FormatException($"Invalid CIK format in file: {filePath}");
                }
            }
            else if (cikProperty.ValueKind == JsonValueKind.Number)
            {
                cik = cikProperty.GetInt32();
            }
            else
            {
                throw new FormatException($"Unexpected CIK type in file: {filePath}");
            }
        }
        else
        {
            throw new KeyNotFoundException($"CIK property not found in file: {filePath}");
        }

        var entityName = root.TryGetProperty("entityName", out var nameProperty) && nameProperty.ValueKind != JsonValueKind.Null
            ? nameProperty.GetString()
            : "Unknown";

        // Check if the entity already exists
        var entity = await dbContext.Entities.FirstOrDefaultAsync(e => e.Cik == cik);
        if (entity == null)
        {
            entity = new Entity
            {
                Id = Guid.NewGuid(),
                Cik = cik,
                EntityName = entityName,
                Facts = new List<Fact>()
            };
            dbContext.Entities.Add(entity);
        }

        var facts = root.GetProperty("facts");
        foreach (var namespaceElement in facts.EnumerateObject())
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
                                FiledDate = (DateTime)(valueElement.TryGetProperty("filed", out var filedProp) && filedProp.ValueKind != JsonValueKind.Null
                                    ? DateTime.Parse(filedProp.GetString())
                                    : (DateTime?)null),
                                Frame = valueElement.TryGetProperty("frame", out var frameProp) && frameProp.ValueKind != JsonValueKind.Null
                                    ? frameProp.GetString()
                                    : null
                            };

                            unit.Values.Add(value);
                        }

                        fact.Units.Add(unit);
                    }
                }

                entity.Facts.Add(fact);
            }
        }

        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            throw new Exception($"Error saving data for file: {filePath}. Details: {ex.Message}", ex);
        }
    }
}