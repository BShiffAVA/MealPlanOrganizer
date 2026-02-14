using System;

namespace MealPlanOrganizer.Functions.Models
{
    /// <summary>
    /// Request model for updating household settings.
    /// </summary>
    public class UpdateHouseholdRequest
    {
        /// <summary>
        /// Optional: New household name. If null, name is not updated.
        /// </summary>
        public string? Name { get; set; }
        
        /// <summary>
        /// Optional: IANA timezone identifier (e.g., "America/New_York").
        /// Used for scheduling notifications at the correct local time.
        /// </summary>
        public string? TimeZoneId { get; set; }
        
        /// <summary>
        /// List of common IANA timezone identifiers for validation.
        /// </summary>
        public static readonly string[] CommonTimeZones = new[]
        {
            "America/New_York",
            "America/Chicago",
            "America/Denver",
            "America/Los_Angeles",
            "America/Anchorage",
            "Pacific/Honolulu",
            "America/Phoenix",
            "America/Toronto",
            "America/Vancouver",
            "Europe/London",
            "Europe/Paris",
            "Europe/Berlin",
            "Europe/Rome",
            "Europe/Madrid",
            "Europe/Amsterdam",
            "Asia/Tokyo",
            "Asia/Shanghai",
            "Asia/Hong_Kong",
            "Asia/Singapore",
            "Asia/Seoul",
            "Asia/Kolkata",
            "Asia/Dubai",
            "Australia/Sydney",
            "Australia/Melbourne",
            "Australia/Perth",
            "Pacific/Auckland",
            "UTC"
        };
        
        /// <summary>
        /// Validates the timezone ID against known IANA identifiers.
        /// </summary>
        public static bool IsValidTimeZone(string timeZoneId)
        {
            try
            {
                // Try to find the timezone using .NET's TimeZoneInfo
                var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                return tz != null;
            }
            catch (TimeZoneNotFoundException)
            {
                // Windows uses different IDs, try converting
                try
                {
                    // Check if it's in our common list
                    return Array.Exists(CommonTimeZones, tz => 
                        tz.Equals(timeZoneId, StringComparison.OrdinalIgnoreCase));
                }
                catch
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
