using Senswave.Contracts.Common.V1;
using Senswave.Infrastructure.Grpc;

namespace Senswave.ArchitectureTests.Shared;

[Trait("Collection", "ArchitectureTests")]
public class SharedTests
{
    [Fact]
    public void ContractsHaveNoDependencyOnOtherSenswaveProjects()
    {
        //Arrange
        var contracts = typeof(IdResponse).Assembly;

        //Act
        var senswaveReferences = contracts
            .GetReferencedAssemblies()
            .Where(x => x.FullName.Contains("Senswave"))
            .ToList();

        //Assert
        Assert.Empty(senswaveReferences);
    }

    [Fact]
    public void GrpcInfrastructureCanHaveDependencyOnAbstractionsAndContractsOnly()
    {
        //Arrange
        var grpcInfrastructure = typeof(GrpcInfrastructureExtensions).Assembly;
        string[] allowed =
        [
            typeof(Senswave.Abstractions.Resulting.Result).Assembly.GetName().Name!,
            typeof(IdResponse).Assembly.GetName().Name!,
        ];

        //Act
        var invalidReferences = grpcInfrastructure
            .GetReferencedAssemblies()
            .Where(x => x.FullName.Contains("Senswave"))
            .Where(x => !allowed.Contains(x.Name))
            .ToList();

        //Assert
        Assert.Empty(invalidReferences);
    }
}
