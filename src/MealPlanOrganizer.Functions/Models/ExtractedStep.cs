namespace MealPlanOrganizer.Functions.Models
{
    /// <summary>
    /// A cooking step extracted from a recipe by GenAI.
    /// </summary>
    public class ExtractedStep
    {
        /// <summary>
        /// Step number (1-based sequential order)
        /// </summary>
        public int StepNumber { get; set; }

        /// <summary>
        /// Step instruction text
        /// </summary>
        public string Instruction { get; set; } = string.Empty;
    }
}
