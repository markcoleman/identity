using System.Security.Cryptography;
using System.Text;
using IdentityResolution.Core.Models;
using IdentityResolution.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IdentityResolution.Core.Services.Implementations;

/// <summary>
/// In-memory implementation of data governance service for development/testing
/// </summary>
public class InMemoryDataGovernanceService : IDataGovernanceService
{
    private readonly DataGovernanceConfiguration _configuration;
    private readonly ILogger<InMemoryDataGovernanceService> _logger;
    private readonly string _encryptionKey = "IdentityResolution-DataGovernance-Key-2024"; // In production, use proper key management

    public InMemoryDataGovernanceService(
        IOptions<DataGovernanceConfiguration> configuration,
        ILogger<InMemoryDataGovernanceService> logger)
    {
        _configuration = configuration.Value ?? new DataGovernanceConfiguration();
        _logger = logger;
    }

    public Task<bool> CanAccessAuditRecordAsync(string userId, AuditOperationType recordType, Guid? recordId = null)
    {
        if (!_configuration.RequireAuditAuthorization)
            return Task.FromResult(true);

        // In production, this would check against a real authorization system
        // For demo, simulate role-based access
        if (IsDataAdmin(userId))
            return Task.FromResult(true);

        // Regular users can only access their own records for certain types
        if (recordType == AuditOperationType.Match || recordType == AuditOperationType.Resolve)
            return Task.FromResult(true);

        return Task.FromResult(false);
    }

    public Task<bool> CanViewRawIdentifierAsync(string userId, string identifierType)
    {
        if (!_configuration.SensitiveFields.Contains(identifierType))
            return Task.FromResult(true);

        // Only data admins can view raw sensitive identifiers
        return Task.FromResult(IsDataAdmin(userId));
    }

    public Task<AuditRecord> ApplyDataMaskingAsync(AuditRecord auditRecord, string userId)
    {
        var canViewRaw = IsDataAdmin(userId);

        if (canViewRaw)
            return Task.FromResult(auditRecord);

        // Create a masked copy of the audit record
        var maskedInputs = new Dictionary<string, object>();
        foreach (var input in auditRecord.Inputs)
        {
            if (_configuration.SensitiveFields.Contains(input.Key))
            {
                maskedInputs[input.Key] = MaskSensitiveData(input.Value?.ToString());
            }
            else
            {
                maskedInputs[input.Key] = input.Value;
            }
        }

        var maskedRecord = auditRecord with
        {
            Inputs = maskedInputs
        };

        return Task.FromResult(maskedRecord);
    }

    public Task<IEnumerable<Guid>> GetRecordsForDeletionAsync(AuditOperationType recordType, int retentionPeriod = 7)
    {
        var cutoffDate = DateTime.UtcNow.AddYears(-retentionPeriod);

        // In production, this would query the actual database
        // For demo, return empty list
        var expiredRecords = new List<Guid>();

        _logger.LogInformation("Found {Count} {RecordType} records eligible for deletion (older than {CutoffDate})",
            expiredRecords.Count, recordType, cutoffDate);

        return Task.FromResult(expiredRecords.AsEnumerable());
    }

    public Task<int> DeleteExpiredRecordsAsync(IEnumerable<Guid> recordIds)
    {
        var count = recordIds.Count();

        // In production, this would perform the actual deletion
        foreach (var recordId in recordIds)
        {
            _logger.LogInformation("Deleted expired record {RecordId}", recordId);
        }

        return Task.FromResult(count);
    }

    public Task<int> ArchiveOldRecordsAsync(AuditOperationType recordType, int archiveAfterDays = 365)
    {
        var archiveDate = DateTime.UtcNow.AddDays(-archiveAfterDays);

        // In production, this would move records to cold storage
        var archivedCount = 0;

        _logger.LogInformation("Archived {Count} {RecordType} records (older than {ArchiveDate})",
            archivedCount, recordType, archiveDate);

        return Task.FromResult(archivedCount);
    }

    public Task<string> EncryptSensitiveFieldAsync(string data, string fieldName)
    {
        if (string.IsNullOrEmpty(data))
            return Task.FromResult(string.Empty);

        if (!_configuration.EnableEncryptionAtRest)
            return Task.FromResult(data);

        // Simple encryption for demo - in production use proper encryption libraries
        var encrypted = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_encryptionKey}:{data}"));

        _logger.LogDebug("Encrypted field {FieldName}", fieldName);
        return Task.FromResult(encrypted);
    }

    public Task<string> DecryptSensitiveFieldAsync(string encryptedData, string fieldName, string userId)
    {
        if (string.IsNullOrEmpty(encryptedData))
            return Task.FromResult(string.Empty);

        if (!_configuration.EnableEncryptionAtRest)
            return Task.FromResult(encryptedData);

        try
        {
            // Check permissions first
            var canViewRaw = IsDataAdmin(userId);
            if (!canViewRaw && _configuration.SensitiveFields.Contains(fieldName))
            {
                return Task.FromResult(MaskSensitiveData(encryptedData));
            }

            // Simple decryption for demo
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encryptedData));
            var parts = decoded.Split(':', 2);

            if (parts.Length == 2 && parts[0] == _encryptionKey)
            {
                return Task.FromResult(parts[1]);
            }

            return Task.FromResult(encryptedData); // Return as-is if can't decrypt
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to decrypt field {FieldName}", fieldName);
            return Task.FromResult("***ENCRYPTED***");
        }
    }

    private bool IsDataAdmin(string userId)
    {
        // In production, this would check against a real user management system
        // For demo, simulate some users as admins
        var adminUsers = new[] { "admin", "system", "dataadmin", "compliance" };
        return adminUsers.Contains(userId?.ToLower() ?? "");
    }

    private string MaskSensitiveData(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        // Different masking strategies based on data type
        if (value.Contains("@")) // Email
        {
            var parts = value.Split('@');
            if (parts.Length == 2)
                return $"{parts[0][0]}***@{parts[1]}";
        }

        if (value.Length >= 4 && (value.Contains("-") || value.All(char.IsDigit))) // SSN or Phone
        {
            return $"***-**-{value.Substring(Math.Max(0, value.Length - 4))}";
        }

        // Default masking
        if (value.Length <= 2)
            return "***";

        return $"{value[0]}***{value[^1]}";
    }
}
