using System.Text.Json;

namespace AuthMd.Tests;

internal static class JsonElementExtensions
{
    public static string? String(this JsonElement element, string property) =>
        element.GetProperty(property).GetString();

    public static string[] Strings(this JsonElement element, string property) =>
        [.. element.GetProperty(property).EnumerateArray().Select(item => item.GetString()!)];
}
