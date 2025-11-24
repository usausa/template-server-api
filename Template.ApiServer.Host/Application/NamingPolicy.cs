namespace Template.ApiServer.Host.Application;

using System.Text.Json;

using Microsoft.AspNetCore.Mvc.ApplicationModels;

using Smart.AspNetCore.ApplicationModels;

public static class NamingPolicy
{
    public static IControllerModelConvention PathNaming => new LowercaseControllerModelConvention();

    public static JsonNamingPolicy JsonPropertyNaming => JsonNamingPolicy.SnakeCaseLower;

    public static JsonNamingPolicy JsonDictionaryKeyNaming => JsonNamingPolicy.SnakeCaseLower;
}
