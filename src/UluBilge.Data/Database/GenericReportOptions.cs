using System.ComponentModel.DataAnnotations;

namespace UluBilge.Data.Database
{
    public sealed class GenericReportOptions : IValidatableObject
    {
        public const string SectionName = "GenericReports";

        public string[] ExcludedTables { get; set; } = [];

        [Range(1, 10000)]
        public int MaxRows { get; set; } = 1000;

        [Range(1024, 10485760)]
        public int MaxResultBytes { get; set; } = 262144;

        [Range(1, 120)]
        public int TimeoutSeconds { get; set; } = 30;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (ExcludedTables is null || ExcludedTables.Any(name => string.IsNullOrWhiteSpace(name) || name.Split('.').Length != 2 || name.Split('.').Any(string.IsNullOrWhiteSpace)))
                yield return new ValidationResult("GenericReports:ExcludedTables, schema.Tablo biçimindeki adlardan oluşmalıdır.", [nameof(ExcludedTables)]);
        }
    }
}
