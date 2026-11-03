namespace UluBilge.Core.Models
{
    public sealed class Food
    {
        public int TotalCalorie { get; init; }
        public DateOnly Date { get; init; }
        public string? Soup { get; init; }
        public string? MainCourse { get; init; }
        public IReadOnlyList<string>? SaladsAndAppetizers { get; init; }
        public IReadOnlyList<string>? DessertsAndFruits { get; init; }
    }

}
