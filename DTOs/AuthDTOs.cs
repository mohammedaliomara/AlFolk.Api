namespace AlFolk.Api.DTOs;

public record LoginRequest(string Email, string Password);

public record LoginResponse(string Token, string RefreshToken, UserDto User);

public record UserDto(Guid Id, string FirstName, string LastName, string? Email, string? Phone, string? Role, string? AvatarUrl);

public record UpdateProfileRequest(string FirstName, string LastName, string Email, string? Password, string? CurrentPassword);

public record TokenRefreshRequest(string RefreshToken);

public record TokenRefreshResponse(string AccessToken, string RefreshToken);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword, string ConfirmNewPassword);
