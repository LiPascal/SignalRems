using Samples.Model;
using SignalRems.Core.Interfaces;

namespace Samples.Server;

public class UserInfoQueryHandler :
    IRpcHandler<GetUserNameRequest, GetUserNameResponse>,
    IRpcHandler<GetUserAgeRequest, GetUserAgeResponse>
{
    private readonly ILogger<UserInfoQueryHandler> _logger;

    public UserInfoQueryHandler(ILogger<UserInfoQueryHandler> logger)
    {
        _logger = logger;
    }

    public Task<GetUserNameResponse> HandleRequest(GetUserNameRequest request)
    {
        _logger.LogInformation("Process GetUserNameRequest, id = {id}, userId = {userId}", request.RequestId,
            request.UserId);
        return Task.FromResult(new GetUserNameResponse() { UserName = request.UserId + "_Name" });
    }

    public Task<GetUserAgeResponse> HandleRequest(GetUserAgeRequest request)
    {
        _logger.LogInformation("Process GetUserAgeRequest, id = {id}, userId = {userId}", request.RequestId, request.UserId);
        return Task.FromResult(new GetUserAgeResponse() { UserAge = 18 });
    }
}