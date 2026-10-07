using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;

namespace AuthMd.AspNetCore;

internal static class ProtocolError
{
    public static IResult Result(string code, string description, int statusCode = StatusCodes.Status400BadRequest) =>
        Results.Json(
            new JsonObject { ["error"] = code, ["error_description"] = description },
            statusCode: statusCode);
}
