using Api.Helpers;
using Api.Models;
using Api.Services;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Helpers;

public class ConfigurationHelperTests
{
    private readonly Mock<IConfigurationService> _mockConfigService;
    private readonly Mock<IConfigurationWrapper> _mockConfigWrapper;
    private readonly Mock<ILogger<ConfigurationHelper>> _mockLogger;
    private readonly ConfigurationHelper _helper;

    public ConfigurationHelperTests()
    {
        _mockConfigService = new Mock<IConfigurationService>();
        _mockConfigWrapper = new Mock<IConfigurationWrapper>();
        _mockLogger = new Mock<ILogger<ConfigurationHelper>>();
        _helper = new ConfigurationHelper(
            _mockConfigService.Object,
            _mockConfigWrapper.Object,
            _mockLogger.Object
        );
    }

    #region Database Tier Tests

    [Fact]
    public async Task GetValueAsync_DatabaseHasZero_ReturnsZero()
    {
        // Arrange
        const string key = "TestKey";
        const int dbValue = 0;
        const int defaultValue = 100;

        var dbConfig = new SystemConfigurationDto(
            Id: "test-config-1",
            Key: key,
            Value: "0",
            ValueType: "int",
            Description: null,
            Category: "Test",
            IsActive: true,
            RequiresRestart: false,
            Environment: "All",
            Version: 1,
            PreviousValue: null,
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow,
            CreatedByUserId: "test-user",
            UpdatedByUserId: null,
            LastToggledAt: null
        );

        _mockConfigService
            .Setup(x => x.GetConfigurationByKeyAsync(key, null))
            .ReturnsAsync(dbConfig);
        _mockConfigService
            .Setup(x => x.GetValueAsync<int>(key, It.IsAny<int>(), null))
            .ReturnsAsync(dbValue);

        // Act
        var result = await _helper.GetValueAsync(key, defaultValue);

        // Assert
        Assert.Equal(dbValue, result);
        _mockConfigService.Verify(x => x.GetConfigurationByKeyAsync(key, null), Times.Once);
        _mockConfigService.Verify(x => x.GetValueAsync<int>(key, It.IsAny<int>(), null), Times.Once);
    }

    [Fact]
    public async Task GetValueAsync_DatabaseHasFalse_ReturnsFalse()
    {
        // Arrange
        const string key = "TestFlag";
        const bool dbValue = false;
        const bool defaultValue = true;

        var dbConfig = new SystemConfigurationDto(
            Id: "test-config-2",
            Key: key,
            Value: "false",
            ValueType: "bool",
            Description: null,
            Category: "Test",
            IsActive: true,
            RequiresRestart: false,
            Environment: "All",
            Version: 1,
            PreviousValue: null,
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow,
            CreatedByUserId: "test-user",
            UpdatedByUserId: null,
            LastToggledAt: null
        );

        _mockConfigService
            .Setup(x => x.GetConfigurationByKeyAsync(key, null))
            .ReturnsAsync(dbConfig);
        _mockConfigService
            .Setup(x => x.GetValueAsync<bool>(key, It.IsAny<bool>(), null))
            .ReturnsAsync(dbValue);

        // Act
        var result = await _helper.GetValueAsync(key, defaultValue);

        // Assert
        Assert.False(result);
        _mockConfigService.Verify(x => x.GetConfigurationByKeyAsync(key, null), Times.Once);
        _mockConfigService.Verify(x => x.GetValueAsync<bool>(key, It.IsAny<bool>(), null), Times.Once);
    }

    [Fact]
    public async Task GetValueAsync_DatabaseHasEmptyString_ReturnsEmptyString()
    {
        // Arrange
        const string key = "TestString";
        const string dbValue = "";
        const string defaultValue = "default";

        var dbConfig = new SystemConfigurationDto(
            Id: "test-config-3",
            Key: key,
            Value: "",
            ValueType: "string",
            Description: null,
            Category: "Test",
            IsActive: true,
            RequiresRestart: false,
            Environment: "All",
            Version: 1,
            PreviousValue: null,
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow,
            CreatedByUserId: "test-user",
            UpdatedByUserId: null,
            LastToggledAt: null
        );

        _mockConfigService
            .Setup(x => x.GetConfigurationByKeyAsync(key, null))
            .ReturnsAsync(dbConfig);
        _mockConfigService
            .Setup(x => x.GetValueAsync<string>(key, It.IsAny<string>(), null))
            .ReturnsAsync(dbValue);

        // Act
        var result = await _helper.GetValueAsync(key, defaultValue);

        // Assert
        Assert.Equal(string.Empty, result);
        _mockConfigService.Verify(x => x.GetConfigurationByKeyAsync(key, null), Times.Once);
        _mockConfigService.Verify(x => x.GetValueAsync<string>(key, It.IsAny<string>(), null), Times.Once);
    }

    [Fact]
    public async Task GetValueAsync_DatabaseConfigInactive_FallsBackToAppsettings()
    {
        // Arrange
        const string key = "TestKey";
        const int appsettingsValue = 50;
        const int defaultValue = 100;

        // Database has inactive config
        var inactiveDbConfig = new SystemConfigurationDto(
            Id: "test-config-4",
            Key: key,
            Value: "25",
            ValueType: "int",
            Description: null,
            Category: "Test",
            IsActive: false, // INACTIVE
            RequiresRestart: false,
            Environment: "All",
            Version: 1,
            PreviousValue: null,
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow,
            CreatedByUserId: "test-user",
            UpdatedByUserId: null,
            LastToggledAt: null
        );

        _mockConfigService
            .Setup(x => x.GetConfigurationByKeyAsync(key, null))
            .ReturnsAsync(inactiveDbConfig);

        // Mock wrapper directly - key exists and has value
        _mockConfigWrapper.Setup(x => x.Exists(key)).Returns(true);
        _mockConfigWrapper.Setup(x => x.GetValue<int>(key)).Returns(appsettingsValue);

        // Act
        var result = await _helper.GetValueAsync(key, defaultValue);

        // Assert
        Assert.Equal(appsettingsValue, result);
        _mockConfigService.Verify(x => x.GetConfigurationByKeyAsync(key, null), Times.Once);
    }

    #endregion

    #region Appsettings Tier Tests

    [Fact]
    public async Task GetValueAsync_AppsettingsHasZero_DatabaseMissing_ReturnsZero()
    {
        // Arrange
        const string key = "TestKey";
        const int appsettingsValue = 0;
        const int defaultValue = 100;

        _mockConfigService
            .Setup(x => x.GetConfigurationByKeyAsync(key, null))
            .ReturnsAsync((SystemConfigurationDto?)null);

        // Mock wrapper directly - key exists with zero value
        _mockConfigWrapper.Setup(x => x.Exists(key)).Returns(true);
        _mockConfigWrapper.Setup(x => x.GetValue<int>(key)).Returns(appsettingsValue);

        // Act
        var result = await _helper.GetValueAsync(key, defaultValue);

        // Assert
        Assert.Equal(appsettingsValue, result);
        _mockConfigService.Verify(x => x.GetConfigurationByKeyAsync(key, null), Times.Once);
    }

    [Fact]
    public async Task GetValueAsync_AppsettingsHasFalse_DatabaseMissing_ReturnsFalse()
    {
        // Arrange
        const string key = "TestFlag";
        const bool appsettingsValue = false;
        const bool defaultValue = true;

        _mockConfigService
            .Setup(x => x.GetConfigurationByKeyAsync(key, null))
            .ReturnsAsync((SystemConfigurationDto?)null);

        // Mock wrapper directly - key exists with false value
        _mockConfigWrapper.Setup(x => x.Exists(key)).Returns(true);
        _mockConfigWrapper.Setup(x => x.GetValue<bool>(key)).Returns(appsettingsValue);

        // Act
        var result = await _helper.GetValueAsync(key, defaultValue);

        // Assert
        Assert.False(result);
        _mockConfigService.Verify(x => x.GetConfigurationByKeyAsync(key, null), Times.Once);
    }

    [Fact]
    public async Task GetValueAsync_AppsettingsHasEmptyString_DatabaseMissing_ReturnsEmptyString()
    {
        // Arrange
        const string key = "TestKey";
        const string appsettingsValue = "";
        const string defaultValue = "default";

        _mockConfigService
            .Setup(x => x.GetConfigurationByKeyAsync(key, null))
            .ReturnsAsync((SystemConfigurationDto?)null);

        // Mock wrapper directly - key exists with empty string
        _mockConfigWrapper.Setup(x => x.Exists(key)).Returns(true);
        _mockConfigWrapper.Setup(x => x.GetValue<string>(key)).Returns(appsettingsValue);

        // Act
        var result = await _helper.GetValueAsync(key, defaultValue);

        // Assert
        Assert.Equal(string.Empty, result);
        _mockConfigService.Verify(x => x.GetConfigurationByKeyAsync(key, null), Times.Once);
    }

    #endregion

    #region Default Value Tests

    [Fact]
    public async Task GetValueAsync_BothDatabaseAndAppsettingsMissing_ReturnsDefaultValue()
    {
        // Arrange
        const string key = "MissingKey";
        const int defaultValue = 999;

        _mockConfigService
            .Setup(x => x.GetConfigurationByKeyAsync(key, null))
            .ReturnsAsync((SystemConfigurationDto?)null);

        // Mock wrapper directly - key does NOT exist
        _mockConfigWrapper.Setup(x => x.Exists(key)).Returns(false);

        // Act
        var result = await _helper.GetValueAsync(key, defaultValue);

        // Assert
        Assert.Equal(defaultValue, result);
        _mockConfigService.Verify(x => x.GetConfigurationByKeyAsync(key, null), Times.Once);
    }

    [Fact]
    public async Task GetValueAsync_AppsettingsMissing_DatabaseThrows_ReturnsDefaultValue()
    {
        // Arrange
        const string key = "TestKey";
        const int defaultValue = 888;

        _mockConfigService
            .Setup(x => x.GetConfigurationByKeyAsync(key, null))
            .ThrowsAsync(new Exception("Database connection failed"));

        // Mock wrapper directly - key does NOT exist
        _mockConfigWrapper.Setup(x => x.Exists(key)).Returns(false);

        // Act
        var result = await _helper.GetValueAsync(key, defaultValue);

        // Assert
        Assert.Equal(defaultValue, result);
    }

    #endregion

    #region Fallback Order Tests

    [Fact]
    public async Task GetValueAsync_DatabaseHasValue_DoesNotCheckAppsettings()
    {
        // Arrange
        const string key = "TestKey";
        const int dbValue = 42;
        const int defaultValue = 100;

        var dbConfig = new SystemConfigurationDto(
            Id: "test-config-5",
            Key: key,
            Value: "42",
            ValueType: "int",
            Description: null,
            Category: "Test",
            IsActive: true,
            RequiresRestart: false,
            Environment: "All",
            Version: 1,
            PreviousValue: null,
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow,
            CreatedByUserId: "test-user",
            UpdatedByUserId: null,
            LastToggledAt: null
        );

        _mockConfigService
            .Setup(x => x.GetConfigurationByKeyAsync(key, null))
            .ReturnsAsync(dbConfig);
        _mockConfigService
            .Setup(x => x.GetValueAsync<int>(key, It.IsAny<int>(), null))
            .ReturnsAsync(dbValue);

        // Act
        var result = await _helper.GetValueAsync(key, defaultValue);

        // Assert
        Assert.Equal(dbValue, result);
        _mockConfigWrapper.Verify(x => x.GetSection(It.IsAny<string>()), Times.Never);
        // Removed GetValue verify - extension method cannot be verified
    }

    [Fact]
    public async Task GetValueAsync_DatabaseMissing_AppsettingsHasValue_DoesNotUseDefault()
    {
        // Arrange
        const string key = "TestKey";
        const int appsettingsValue = 0; // Zero, but still a valid value
        const int defaultValue = 100;

        _mockConfigService
            .Setup(x => x.GetConfigurationByKeyAsync(key, null))
            .ReturnsAsync((SystemConfigurationDto?)null);

        // Mock wrapper directly - key exists with zero
        _mockConfigWrapper.Setup(x => x.Exists(key)).Returns(true);
        _mockConfigWrapper.Setup(x => x.GetValue<int>(key)).Returns(appsettingsValue);

        // Act
        var result = await _helper.GetValueAsync(key, defaultValue);

        // Assert
        Assert.Equal(appsettingsValue, result);
        _mockConfigService.Verify(x => x.GetConfigurationByKeyAsync(key, null), Times.Once);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task GetValueAsync_DatabaseThrows_FallsBackToAppsettings()
    {
        // Arrange
        const string key = "TestKey";
        const int appsettingsValue = 75;
        const int defaultValue = 100;

        _mockConfigService
            .Setup(x => x.GetConfigurationByKeyAsync(key, null))
            .ThrowsAsync(new Exception("Database error"));

        // Mock wrapper directly - key exists in appsettings
        _mockConfigWrapper.Setup(x => x.Exists(key)).Returns(true);
        _mockConfigWrapper.Setup(x => x.GetValue<int>(key)).Returns(appsettingsValue);

        // Act
        var result = await _helper.GetValueAsync(key, defaultValue);

        // Assert
        Assert.Equal(appsettingsValue, result);
    }

    [Fact]
    public async Task GetValueAsync_AppsettingsThrows_FallsBackToDefault()
    {
        // Arrange
        const string key = "TestKey";
        const int defaultValue = 100;

        _mockConfigService
            .Setup(x => x.GetConfigurationByKeyAsync(key, null))
            .ReturnsAsync((SystemConfigurationDto?)null);

        _mockConfigWrapper
            .Setup(x => x.GetSection(key))
            .Throws(new Exception("Appsettings error"));

        // Act
        var result = await _helper.GetValueAsync(key, defaultValue);

        // Assert
        Assert.Equal(defaultValue, result);
    }

    #endregion

    #region Typed Convenience Methods Tests

    [Fact]
    public async Task GetIntAsync_CallsGenericMethod()
    {
        // Arrange
        const string key = "TestInt";
        const int defaultValue = 42;

        _mockConfigService
            .Setup(x => x.GetConfigurationByKeyAsync(key, null))
            .ReturnsAsync((SystemConfigurationDto?)null);

        // Mock wrapper directly - key does NOT exist
        _mockConfigWrapper.Setup(x => x.Exists(key)).Returns(false);

        // Act
        var result = await _helper.GetIntAsync(key, defaultValue);

        // Assert
        Assert.Equal(defaultValue, result);
    }

    [Fact]
    public async Task GetBoolAsync_CallsGenericMethod()
    {
        // Arrange
        const string key = "TestBool";
        const bool defaultValue = true;

        _mockConfigService
            .Setup(x => x.GetConfigurationByKeyAsync(key, null))
            .ReturnsAsync((SystemConfigurationDto?)null);

        // Mock wrapper directly - key does NOT exist
        _mockConfigWrapper.Setup(x => x.Exists(key)).Returns(false);

        // Act
        var result = await _helper.GetBoolAsync(key, defaultValue);

        // Assert
        Assert.Equal(defaultValue, result);
    }

    [Fact]
    public async Task GetStringAsync_CallsGenericMethod()
    {
        // Arrange
        const string key = "TestString";
        const string defaultValue = "default";

        _mockConfigService
            .Setup(x => x.GetConfigurationByKeyAsync(key, null))
            .ReturnsAsync((SystemConfigurationDto?)null);

        // Mock wrapper directly - key does NOT exist
        _mockConfigWrapper.Setup(x => x.Exists(key)).Returns(false);

        // Act
        var result = await _helper.GetStringAsync(key, defaultValue);

        // Assert
        Assert.Equal(defaultValue, result);
    }

    [Fact]
    public async Task GetDoubleAsync_CallsGenericMethod()
    {
        // Arrange
        const string key = "TestDouble";
        const double defaultValue = 3.14;

        _mockConfigService
            .Setup(x => x.GetConfigurationByKeyAsync(key, null))
            .ReturnsAsync((SystemConfigurationDto?)null);

        // Mock wrapper directly - key does NOT exist
        _mockConfigWrapper.Setup(x => x.Exists(key)).Returns(false);

        // Act
        var result = await _helper.GetDoubleAsync(key, defaultValue);

        // Assert
        Assert.Equal(defaultValue, result);
    }

    [Fact]
    public async Task GetLongAsync_CallsGenericMethod()
    {
        // Arrange
        const string key = "TestLong";
        const long defaultValue = 999999L;

        _mockConfigService
            .Setup(x => x.GetConfigurationByKeyAsync(key, null))
            .ReturnsAsync((SystemConfigurationDto?)null);

        // Mock wrapper directly - key does NOT exist
        _mockConfigWrapper.Setup(x => x.Exists(key)).Returns(false);

        // Act
        var result = await _helper.GetLongAsync(key, defaultValue);

        // Assert
        Assert.Equal(defaultValue, result);
    }

    #endregion

    #region Deserialization Failure Tests

    [Fact]
    public async Task GetValueAsync_DatabaseDeserializationFails_ReturnsDefaultValue()
    {
        // Arrange
        const string key = "TestKey";
        const int defaultValue = 100;

        // Database returns config but GetValueAsync fails (simulating deserialization error)
        var dbConfig = new SystemConfigurationDto(
            Id: "test-config-1",
            Key: key,
            Value: "invalid_int",
            ValueType: "int",
            Description: null,
            Category: "Test",
            IsActive: true,
            RequiresRestart: false,
            Environment: "All",
            Version: 1,
            PreviousValue: null,
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow,
            CreatedByUserId: "test-user",
            UpdatedByUserId: null,
            LastToggledAt: null
        );

        _mockConfigService
            .Setup(x => x.GetConfigurationByKeyAsync(key, null))
            .ReturnsAsync(dbConfig);

        _mockConfigService
            .Setup(x => x.GetValueAsync<int>(key, defaultValue, null))
            .ReturnsAsync(defaultValue); // Deserialization failed, returns default

        // Act
        var result = await _helper.GetValueAsync(key, defaultValue);

        // Assert
        Assert.Equal(defaultValue, result);
    }

    [Fact]
    public async Task GetValueAsync_DatabaseDeserializationFails_Bool_ReturnsDefaultValue()
    {
        // Arrange
        const string key = "Features:EnableDangerousFeature";
        const bool defaultValue = false; // Safe default

        // Database has active config but with invalid value "maybe" for bool type
        var dbConfig = new SystemConfigurationDto(
            Id: "test-config-7",
            Key: key,
            Value: "maybe", // Invalid value for bool
            ValueType: "bool",
            Description: null,
            Category: "Test",
            IsActive: true,
            RequiresRestart: false,
            Environment: "All",
            Version: 1,
            PreviousValue: null,
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow,
            CreatedByUserId: "test-user",
            UpdatedByUserId: null,
            LastToggledAt: null
        );

        _mockConfigService
            .Setup(x => x.GetConfigurationByKeyAsync(key, null))
            .ReturnsAsync(dbConfig);

        // GetValueAsync will return defaultValue when deserialization fails
        _mockConfigService
            .Setup(x => x.GetValueAsync<bool>(key, defaultValue, null))
            .ReturnsAsync(defaultValue);

        // Act
        var result = await _helper.GetValueAsync(key, defaultValue);

        // Assert
        Assert.False(result); // Should return false, not true
        _mockConfigService.Verify(x => x.GetConfigurationByKeyAsync(key, null), Times.Once);
        _mockConfigService.Verify(x => x.GetValueAsync<bool>(key, defaultValue, null), Times.Once);
    }

    #endregion

    #region Environment Parameter Tests

    [Fact]
    public async Task GetValueAsync_PassesEnvironmentToConfigService()
    {
        // Arrange
        const string key = "TestKey";
        const string environment = "Production";
        const int defaultValue = 100;

        _mockConfigService
            .Setup(x => x.GetConfigurationByKeyAsync(key, environment))
            .ReturnsAsync((SystemConfigurationDto?)null);

        // Mock wrapper directly - key does NOT exist
        _mockConfigWrapper.Setup(x => x.Exists(key)).Returns(false);

        // Act
        var result = await _helper.GetValueAsync(key, defaultValue, environment);

        // Assert
        Assert.Equal(defaultValue, result);
        _mockConfigService.Verify(x => x.GetConfigurationByKeyAsync(key, environment), Times.Once);
    }

    #endregion
}