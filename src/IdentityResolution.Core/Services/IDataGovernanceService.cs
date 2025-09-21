using IdentityResolution.Core.Models;

namespace IdentityResolution.Core.Services;

/// <summary>
/// Service for data governance, retention policies, and access control
/// </summary>
public interface IDataGovernanceService
{
    /// <summary>
    /// Check if the current user has permission to access specific audit records
    /// </summary>
    /// <param name="userId">The user requesting access</param>
    /// <param name="recordType">Type of audit record</param>
    /// <param name="recordId">Specific record ID (optional)</param>
    /// <returns>True if access is allowed</returns>
    Task<bool> CanAccessAuditRecordAsync(string userId, AuditOperationType recordType, Guid? recordId = null);

    /// <summary>
    /// Check if the current user has permission to view raw identifiers
    /// </summary>
    /// <param name="userId">The user requesting access</param>
    /// <param name="identifierType">Type of identifier (SSN, etc.)</param>
    /// <returns>True if access to raw identifiers is allowed</returns>
    Task<bool> CanViewRawIdentifierAsync(string userId, string identifierType);

    /// <summary>
    /// Anonymize or mask sensitive data based on user permissions
    /// </summary>
    /// <param name="auditRecord">The audit record to potentially mask</param>
    /// <param name="userId">The user requesting the data</param>
    /// <returns>Audit record with appropriate masking applied</returns>
    Task<AuditRecord> ApplyDataMaskingAsync(AuditRecord auditRecord, string userId);

    /// <summary>
    /// Get records that are eligible for deletion based on retention policies
    /// </summary>
    /// <param name="recordType">Type of records to check</param>
    /// <param name="retentionPeriod">Retention period in years</param>
    /// <returns>Records eligible for deletion</returns>
    Task<IEnumerable<Guid>> GetRecordsForDeletionAsync(AuditOperationType recordType, int retentionPeriod = 7);

    /// <summary>
    /// Permanently delete records that have exceeded retention period
    /// </summary>
    /// <param name="recordIds">IDs of records to delete</param>
    /// <returns>Number of records successfully deleted</returns>
    Task<int> DeleteExpiredRecordsAsync(IEnumerable<Guid> recordIds);

    /// <summary>
    /// Archive old records to cold storage
    /// </summary>
    /// <param name="recordType">Type of records to archive</param>
    /// <param name="archiveAfterDays">Archive records older than this many days</param>
    /// <returns>Number of records archived</returns>
    Task<int> ArchiveOldRecordsAsync(AuditOperationType recordType, int archiveAfterDays = 365);

    /// <summary>
    /// Encrypt sensitive fields in place
    /// </summary>
    /// <param name="data">Data to encrypt</param>
    /// <param name="fieldName">Name of the field being encrypted</param>
    /// <returns>Encrypted data</returns>
    Task<string> EncryptSensitiveFieldAsync(string data, string fieldName);

    /// <summary>
    /// Decrypt sensitive fields (requires appropriate permissions)
    /// </summary>
    /// <param name="encryptedData">Encrypted data</param>
    /// <param name="fieldName">Name of the field being decrypted</param>
    /// <param name="userId">User requesting decryption</param>
    /// <returns>Decrypted data or masked version based on permissions</returns>
    Task<string> DecryptSensitiveFieldAsync(string encryptedData, string fieldName, string userId);
}

/// <summary>
/// Configuration for data governance policies
/// </summary>
public class DataGovernanceConfiguration
{
    /// <summary>
    /// Default retention period for audit logs (years)
    /// </summary>
    public int AuditLogRetentionYears { get; set; } = 7;

    /// <summary>
    /// Retention period for golden profiles (indefinite = -1)
    /// </summary>
    public int GoldenProfileRetentionYears { get; set; } = -1;

    /// <summary>
    /// Days after which records are archived to cold storage
    /// </summary>
    public int ArchiveAfterDays { get; set; } = 365;

    /// <summary>
    /// Enable encryption at rest for all data
    /// </summary>
    public bool EnableEncryptionAtRest { get; set; } = true;

    /// <summary>
    /// Require authorization for audit record access
    /// </summary>
    public bool RequireAuditAuthorization { get; set; } = true;

    /// <summary>
    /// Fields that require special handling for PII
    /// </summary>
    public HashSet<string> SensitiveFields { get; set; } = new()
    {
        "SSN",
        "DateOfBirth", 
        "Email",
        "Phone",
        "Address"
    };

    /// <summary>
    /// Roles that can access raw identifier data
    /// </summary>
    public HashSet<string> DataAdminRoles { get; set; } = new()
    {
        "DataAdmin",
        "ComplianceOfficer",
        "SystemAdmin"
    };
}