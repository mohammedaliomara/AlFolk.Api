using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AlFolk.Api.Services;

public interface IFirebaseMessagingService
{
    Task<(int SuccessCount, int FailureCount, List<string> Errors)> SendNotificationToUsersAsync(List<Guid> userIds, string title, string message);
}
