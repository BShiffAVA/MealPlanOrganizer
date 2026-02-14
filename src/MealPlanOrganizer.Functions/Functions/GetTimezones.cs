using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using MealPlanOrganizer.Functions.Models;

namespace MealPlanOrganizer.Functions.Functions;

/// <summary>
/// Returns the list of supported IANA timezone identifiers.
/// </summary>
public class GetTimezones
{
    [Function(nameof(GetTimezones))]
    public HttpResponseData Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "timezones")] HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.WriteAsJsonAsync(UpdateHouseholdRequest.CommonTimeZones);
        return response;
    }
}
