using FluentAssertions;
using FluentValidation.Results;
using IdentityService.Application.DTOs;
using IdentityService.Application.Validators;
using Xunit;

namespace IdentityService.Tests.Validators;

public class UserValidatorTests
{
    private readonly CreateUserRequestValidator _createValidator = new();
    private readonly UpdateUserRequestValidator _updateValidator = new();
    private readonly UpdateProfileRequestValidator _profileValidator = new();
    private readonly ChangePasswordRequestValidator _changePasswordValidator = new();

    // ---------- CreateUserRequestValidator ----------

    [Fact]
    public void CreateUser_ValidInput_ReturnsValid()
    {
        var request = new CreateUserRequest("test@example.com", "Pass123!", "Test User", 3);
        var result = _createValidator.Validate(request);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void CreateUser_InvalidEmail_ReturnsInvalid(string email)
    {
        var request = new CreateUserRequest(email, "Pass123!", "Test User", 3);
        var result = _createValidator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("nouppercase1!")]
    [InlineData("NOLOWERCASE1!")]
    [InlineData("NoDigit!")]
    [InlineData("NoSpecial1")]
    public void CreateUser_WeakPassword_ReturnsInvalid(string password)
    {
        var request = new CreateUserRequest("test@example.com", password, "Test User", 3);
        var result = _createValidator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Password");
    }

    [Fact]
    public void CreateUser_EmptyFullName_ReturnsInvalid()
    {
        var request = new CreateUserRequest("test@example.com", "Pass123!", "", 3);
        var result = _createValidator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FullName");
    }

    [Fact]
    public void CreateUser_FullNameExceeds200Chars_ReturnsInvalid()
    {
        var longName = new string('A', 201);
        var request = new CreateUserRequest("test@example.com", "Pass123!", longName, 3);
        var result = _createValidator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FullName");
    }

    [Fact]
    public void CreateUser_FullNameAt200Chars_ReturnsValid()
    {
        var name = new string('A', 200);
        var request = new CreateUserRequest("test@example.com", "Pass123!", name, 3);
        var result = _createValidator.Validate(request);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void CreateUser_InvalidRoleId_ReturnsInvalid(int roleId)
    {
        var request = new CreateUserRequest("test@example.com", "Pass123!", "Test User", roleId);
        var result = _createValidator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "RoleId");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void CreateUser_ValidRoleId_ReturnsValid(int roleId)
    {
        var request = new CreateUserRequest("test@example.com", "Pass123!", "Test User", roleId);
        var result = _createValidator.Validate(request);
        result.IsValid.Should().BeTrue();
    }

    // ---------- UpdateUserRequestValidator ----------

    [Fact]
    public void UpdateUser_ValidInput_ReturnsValid()
    {
        var request = new UpdateUserRequest(FullName: "New Name", Email: "new@example.com", RoleId: 2, IsActive: true);
        var result = _updateValidator.Validate(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void UpdateUser_NullFields_ReturnsValid()
    {
        var request = new UpdateUserRequest(null, null, null, null);
        var result = _updateValidator.Validate(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void UpdateUser_EmptyStringFields_SkipsValidation()
    {
        var request = new UpdateUserRequest(FullName: "", Email: "", RoleId: null, IsActive: null);
        var result = _updateValidator.Validate(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void UpdateUser_InvalidEmail_ReturnsInvalid()
    {
        var request = new UpdateUserRequest(FullName: null, Email: "not-an-email", RoleId: null, IsActive: null);
        var result = _updateValidator.Validate(request);
        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void UpdateUser_InvalidRoleId_ReturnsInvalid(int roleId)
    {
        var request = new UpdateUserRequest(FullName: null, Email: null, RoleId: roleId, IsActive: null);
        var result = _updateValidator.Validate(request);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void UpdateUser_FullNameExceeds200Chars_ReturnsInvalid()
    {
        var longName = new string('A', 201);
        var request = new UpdateUserRequest(FullName: longName, Email: null, RoleId: null, IsActive: null);
        var result = _updateValidator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FullName");
    }

    [Fact]
    public void UpdateUser_FullNameAt200Chars_ReturnsValid()
    {
        var name = new string('A', 200);
        var request = new UpdateUserRequest(FullName: name, Email: null, RoleId: null, IsActive: null);
        var result = _updateValidator.Validate(request);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public void UpdateUser_ValidRoleId_ReturnsValid(int roleId)
    {
        var request = new UpdateUserRequest(FullName: null, Email: null, RoleId: roleId, IsActive: null);
        var result = _updateValidator.Validate(request);
        result.IsValid.Should().BeTrue();
    }

    // ---------- UpdateProfileRequestValidator ----------

    [Fact]
    public void UpdateProfile_ValidInput_ReturnsValid()
    {
        var request = new UpdateProfileRequest("Test User", "test@example.com");
        var result = _profileValidator.Validate(request);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void UpdateProfile_InvalidEmail_ReturnsInvalid(string email)
    {
        var request = new UpdateProfileRequest("Test User", email);
        var result = _profileValidator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Fact]
    public void UpdateProfile_EmptyFullName_ReturnsInvalid()
    {
        var request = new UpdateProfileRequest("", "test@example.com");
        var result = _profileValidator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FullName");
    }

    [Fact]
    public void UpdateProfile_FullNameExceeds200Chars_ReturnsInvalid()
    {
        var longName = new string('A', 201);
        var request = new UpdateProfileRequest(longName, "test@example.com");
        var result = _profileValidator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FullName");
    }

    [Fact]
    public void UpdateProfile_EmptyEmail_ReturnsInvalid()
    {
        var request = new UpdateProfileRequest("Test User", "");
        var result = _profileValidator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    // ---------- ChangePasswordRequestValidator ----------

    [Fact]
    public void ChangePassword_ValidInput_ReturnsValid()
    {
        var request = new ChangePasswordRequest("OldPass1!", "NewPass2!");
        var result = _changePasswordValidator.Validate(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ChangePassword_EmptyCurrentPassword_ReturnsInvalid()
    {
        var request = new ChangePasswordRequest("", "NewPass2!");
        var result = _changePasswordValidator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "CurrentPassword");
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("nouppercase1!")]
    [InlineData("NOLOWERCASE1!")]
    [InlineData("NoDigit!")]
    [InlineData("NoSpecial1")]
    public void ChangePassword_WeakNewPassword_ReturnsInvalid(string password)
    {
        var request = new ChangePasswordRequest("OldPass1!", password);
        var result = _changePasswordValidator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "NewPassword");
    }

    [Fact]
    public void ChangePassword_SamePassword_ReturnsInvalid()
    {
        var request = new ChangePasswordRequest("SamePass1!", "SamePass1!");
        var result = _changePasswordValidator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "NewPassword");
    }

    [Fact]
    public void ChangePassword_EmptyNewPassword_ReturnsInvalid()
    {
        var request = new ChangePasswordRequest("OldPass1!", "");
        var result = _changePasswordValidator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "NewPassword");
    }

    [Fact]
    public void ChangePassword_BothEmpty_ReturnsInvalidForBoth()
    {
        var request = new ChangePasswordRequest("", "");
        var result = _changePasswordValidator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "CurrentPassword");
        result.Errors.Should().Contain(e => e.PropertyName == "NewPassword");
    }
}
