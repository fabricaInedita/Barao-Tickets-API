﻿using BaraoFeedback.Application.DTOs.Shared;
using BaraoFeedback.Application.DTOs.User;

namespace BaraoFeedback.Application.Services.User;

public interface IIdentityService
{
    Task<DefaultResponse> ForgotPassword(ForgotPasswordDto model);
    Task<DefaultResponse> UpdateNameAsync(PatchUserRequest model);
    Task<DefaultResponse> DeleteUser(string id);
    Task<DefaultResponse> UpdateUserAsync(string userId, UpdateUserRequest model);
    Task<bool> UnlockUser(string userId, string token);
    Task<DefaultResponse> GetUsers();
    Task<DefaultResponse> UpdatePasswordAsync(UpdatePassword dto);
    Task<UserLoginResponse> LoginAsync(UserLoginRequest userLogin);
    Task<UserRegisterResponse> RegisterAdminAsync(string type, AdminRegisterRequest request);
    Task<UserRegisterResponse> RegisterStudentAsync(string type, StudentRegisterRequest userRegister);
}
