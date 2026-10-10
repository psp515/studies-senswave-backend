using Grpc.Core;
using Senswave.Abstractions.Resulting;
using Senswave.Infrastructure.Grpc.Errors;

namespace Senswave.ArchitectureTests.Shared;

/// <summary>
/// Guard: adding a new <see cref="ErrorType"/> without a gRPC mapping fails here instead of throwing at runtime.
/// </summary>
[Trait("Collection", "ArchitectureTests")]
public class GrpcErrorMappingTests
{
    public static TheoryData<ErrorType> MappedErrorTypes()
    {
        var data = new TheoryData<ErrorType>();

        foreach (var errorType in Enum.GetValues<ErrorType>().Where(x => x != ErrorType.None))
            data.Add(errorType);

        return data;
    }

    [Theory]
    [MemberData(nameof(MappedErrorTypes))]
    public void EveryErrorTypeHasGrpcMapping(ErrorType errorType)
    {
        //Act
        var statusCode = errorType.GetGrpcStatusCode();
        var title = errorType.GetGrpcTitle();
        var contract = errorType.ToContract();

        //Assert
        Assert.NotEqual(StatusCode.OK, statusCode);
        Assert.False(string.IsNullOrWhiteSpace(title));
        Assert.NotEqual(Senswave.Contracts.Common.V1.ErrorType.Unspecified, contract);
    }

    [Theory]
    [InlineData(ErrorType.Validation, StatusCode.InvalidArgument)]
    [InlineData(ErrorType.NotFound, StatusCode.NotFound)]
    [InlineData(ErrorType.Conflict, StatusCode.AlreadyExists)]
    [InlineData(ErrorType.Failure, StatusCode.FailedPrecondition)]
    [InlineData(ErrorType.ServerFail, StatusCode.Internal)]
    public void ErrorTypeMapsToSpecifiedStatusCode(ErrorType errorType, StatusCode expected)
    {
        Assert.Equal(expected, errorType.GetGrpcStatusCode());
    }
}
