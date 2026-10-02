using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AlFolk.Api.Data;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AlFolk.Api.Services;

public class FirebaseMessagingService : IFirebaseMessagingService
{
    private readonly AppDbContext _context;
    private readonly ILogger<FirebaseMessagingService> _logger;
    private static readonly object _lock = new object();
    private static bool _isInitialized = false;

    public FirebaseMessagingService(AppDbContext context, ILogger<FirebaseMessagingService> logger)
    {
        _context = context;
        _logger = logger;
        InitializeFirebase();
    }

    private void InitializeFirebase()
    {
        if (_isInitialized) return;

        lock (_lock)
        {
            if (_isInitialized) return;

            try
            {
                if (FirebaseApp.DefaultInstance == null)
                {
                    var credentialsPath = Path.Combine(AppContext.BaseDirectory, "firebase-credentials.json");
                    if (!File.Exists(credentialsPath))
                    {
                        credentialsPath = Path.Combine(Directory.GetCurrentDirectory(), "firebase-credentials.json");
                    }

                    if (File.Exists(credentialsPath))
                    {
                        FirebaseApp.Create(new AppOptions()
                        {
                            Credential = GoogleCredential.FromFile(credentialsPath)
                        });
                        _logger.LogInformation("Firebase Admin SDK initialized successfully from: {Path}", credentialsPath);
                    }
                    else
                    {
                        _logger.LogWarning("Firebase credentials file not found at: {Path}", credentialsPath);
                    }
                }
                else
                {
                    _logger.LogInformation("Firebase Admin SDK already initialized.");
                }
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing Firebase Admin SDK");
            }
        }
    }

    public async Task<(int SuccessCount, int FailureCount, List<string> Errors)> SendNotificationToUsersAsync(List<Guid> userIds, string title, string message)
    {
        var errors = new List<string>();
        if (userIds == null || !userIds.Any()) return (0, 0, errors);

        try
        {
            var tokensFromDeviceTokens = await _context.DeviceTokens
                .Where(dt => userIds.Contains(dt.UserId) && !string.IsNullOrEmpty(dt.Token))
                .Select(dt => dt.Token)
                .ToListAsync();

            var tokensFromUsers = await _context.Users
                .Where(u => userIds.Contains(u.Id) && !string.IsNullOrEmpty(u.FcmToken))
                .Select(u => u.FcmToken)
                .ToListAsync();

            // Null-forgiving operator since we already filtered out nulls, but EF Core might still warn
            var tokens = tokensFromDeviceTokens.Concat(tokensFromUsers!)
                .Where(t => t != "string" && !string.IsNullOrWhiteSpace(t))
                .Distinct()
                .ToList();

            if (!tokens.Any())
            {
                var msg = $"No device tokens found for user list: {string.Join(",", userIds)}";
                _logger.LogInformation(msg);
                errors.Add(msg);
                return (0, 0, errors);
            }

            _logger.LogInformation("Sending push notification to {Count} tokens. Title: {Title}", tokens.Count, title);

            // Using MulticastMessage to send to multiple devices in a single call.
            var multicastMessage = new MulticastMessage()
            {
                Tokens = tokens,
                Notification = new FirebaseAdmin.Messaging.Notification()
                {
                    Title = title,
                    Body = message
                },
                Data = new Dictionary<string, string>()
                {
                    { "title", title },
                    { "body", message }
                },
                Android = new AndroidConfig
                {
                    Priority = Priority.High,
                    Notification = new AndroidNotification
                    {
                        Sound = "default"
                    }
                },
                Apns = new ApnsConfig
                {
                    Aps = new Aps
                    {
                        Sound = "default",
                        ContentAvailable = true
                    }
                }
            };

            if (FirebaseApp.DefaultInstance != null)
            {
                var response = await FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(multicastMessage);
                _logger.LogInformation("Successfully sent Firebase notification. Success: {Success}, Failure: {Failure}", 
                    response.SuccessCount, response.FailureCount);
                    
                if (response.FailureCount > 0)
                {
                    for (int i = 0; i < response.Responses.Count; i++)
                    {
                        if (!response.Responses[i].IsSuccess)
                        {
                            var tokenError = $"Token [{tokens[i]}]: {response.Responses[i].Exception?.Message}";
                            errors.Add(tokenError);
                            _logger.LogError("FCM sending error: {Error}", tokenError);
                        }
                    }
                }
                return (response.SuccessCount, response.FailureCount, errors);
            }
            else
            {
                var msg = "FirebaseApp DefaultInstance is null, could not send notification.";
                _logger.LogError(msg);
                errors.Add(msg);
                return (0, 0, errors);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending Firebase push notifications");
            errors.Add(ex.Message);
            return (0, 0, errors);
        }
    }
}
