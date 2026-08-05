namespace DatabaseProfiler.App.Models;

public sealed class UserDefinedTypeModel
{
    public string Name { get; set; } = string.Empty;

    public string SchemaName { get; set; } = string.Empty;

    public string SelectionValue => string.IsNullOrWhiteSpace(SchemaName) ? Name : $"{SchemaName}|{Name}";

    public string DisplayName => string.IsNullOrWhiteSpace(SchemaName) ? Name : $"{SchemaName}.{Name}";

    public string BaseTypeName { get; set; } = string.Empty;

    public int? MaxLength { get; set; }

    public int? Precision { get; set; }

    public int? Scale { get; set; }

    public string BaseTypeDisplay
    {
        get
        {
            if (string.IsNullOrWhiteSpace(BaseTypeName))
            {
                return string.Empty;
            }

            // For types with max length (varchar, nvarchar, char, nchar, binary, varbinary)
            if (MaxLength.HasValue && MaxLength.Value > 0)
            {
                // nvarchar/nchar use half the byte length
                if (BaseTypeName.StartsWith("nvar", StringComparison.OrdinalIgnoreCase) ||
                    BaseTypeName.StartsWith("ncha", StringComparison.OrdinalIgnoreCase))
                {
                    var displayLength = MaxLength.Value == -1 ? "max" : (MaxLength.Value / 2).ToString();
                    return $"{BaseTypeName}({displayLength})";
                }
                // varchar, char, binary, varbinary
                else if (BaseTypeName.Contains("var", StringComparison.OrdinalIgnoreCase) ||
                         BaseTypeName.Contains("char", StringComparison.OrdinalIgnoreCase) ||
                         BaseTypeName.Contains("binary", StringComparison.OrdinalIgnoreCase))
                {
                    var displayLength = MaxLength.Value == -1 ? "max" : MaxLength.Value.ToString();
                    return $"{BaseTypeName}({displayLength})";
                }
            }

            // For decimal/numeric with precision and scale
            if (Precision.HasValue && Scale.HasValue &&
                (BaseTypeName.Equals("decimal", StringComparison.OrdinalIgnoreCase) ||
                 BaseTypeName.Equals("numeric", StringComparison.OrdinalIgnoreCase)))
            {
                return $"{BaseTypeName}({Precision},{Scale})";
            }

            // Simple types (int, bit, datetime, etc.)
            return BaseTypeName;
        }
    }
}
