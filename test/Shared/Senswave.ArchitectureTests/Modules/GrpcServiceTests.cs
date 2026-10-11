using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using System.Reflection;

namespace Senswave.ArchitectureTests.Modules;

[Trait("Collection", "ArchitectureTests")]
public class GrpcServiceTests : BaseModuleTest
{
    private static Type[] GetGrpcServices() => GetSenswaveAssemblies()
        .Where(a => a.GetName().Name!.EndsWith(".Api"))
        .SelectMany(a => a.GetTypes())
        .Where(t => t is { IsClass: true, IsAbstract: false } && IsGeneratedServiceBase(t.BaseType))
        .ToArray();

    private static bool IsGeneratedServiceBase(Type? type) =>
        type?.GetCustomAttribute<BindServiceMethodAttribute>() is not null;

    [Fact]
    public void GrpcServicesFollowConventions()
    {
        //Arrange
        var services = GetGrpcServices();

        //Act
        var violations = services
            .SelectMany(Violations)
            .ToList();

        //Assert
        Assert.NotEmpty(services);
        Assert.Empty(violations);
    }

    private static IEnumerable<string> Violations(Type service)
    {
        if (service.Namespace is null || !service.Namespace.EndsWith(".Grpc"))
            yield return $"{service.FullName} must live in a 'Grpc' namespace.";

        if (!service.Name.EndsWith("GrpcService"))
            yield return $"{service.FullName} must be named '*GrpcService'.";

        if (!service.IsSealed || service.IsPublic)
            yield return $"{service.FullName} must be internal sealed.";

        var isAuthorized = service.GetCustomAttribute<AuthorizeAttribute>() is not null;
        var isAnonymous = service.GetCustomAttribute<AllowAnonymousAttribute>() is not null;

        if (isAuthorized == isAnonymous)
            yield return $"{service.FullName} must carry exactly one of [Authorize] or [AllowAnonymous].";
    }
}
