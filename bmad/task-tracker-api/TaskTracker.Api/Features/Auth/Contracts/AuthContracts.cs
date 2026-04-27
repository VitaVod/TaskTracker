namespace TaskTracker.Api.Features.Auth.Contracts;

public record RegisterRequest(string Email, string Password);

public record RegisterResponse(Guid UserId, string Email, string Message);

public record LoginRequest(string Email, string Password);

public record LoginResponse(string AccessToken, string RefreshToken, int ExpiresIn);

public record RefreshRequest(string RefreshToken);

public record RefreshResponse(string AccessToken, string RefreshToken, int ExpiresIn);

public record LogoutRequest(string RefreshToken);

public record LogoutResponse(string Message);

public record PasswordRecoveryRequest(string Email);

public record PasswordRecoveryRequestResponse(string Message);

public record PasswordRecoveryConfirmRequest(string Token, string NewPassword);

public record PasswordRecoveryConfirmResponse(string Message);