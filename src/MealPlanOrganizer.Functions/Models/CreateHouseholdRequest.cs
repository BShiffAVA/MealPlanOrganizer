namespace MealPlanOrganizer.Functions.Models
{
    /// <summary>
    /// Request model for creating a new household
    /// </summary>
    public class CreateHouseholdRequest
    {
        /// <summary>
        /// Name of the household (e.g., "Smith Family")
        /// </summary>
        public string Name { get; set; } = string.Empty;
    }
}
