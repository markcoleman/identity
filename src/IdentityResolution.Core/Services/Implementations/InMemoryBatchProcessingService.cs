using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using IdentityResolution.Core.Models;
using Microsoft.Extensions.Logging;

namespace IdentityResolution.Core.Services.Implementations;

/// <summary>
/// In-memory implementation of batch processing service for development/testing
/// </summary>
public class InMemoryBatchProcessingService : IBatchProcessingService
{
    private readonly IIdentityResolutionService _resolutionService;
    private readonly IDataNormalizationService _normalizationService;
    private readonly ILogger<InMemoryBatchProcessingService> _logger;

    // In-memory storage for batch jobs
    private readonly Dictionary<Guid, BatchJobStatus> _batchJobs = new();
    private readonly Dictionary<Guid, BatchProcessingResult> _batchResults = new();

    public InMemoryBatchProcessingService(
        IIdentityResolutionService resolutionService,
        IDataNormalizationService normalizationService,
        ILogger<InMemoryBatchProcessingService> logger)
    {
        _resolutionService = resolutionService;
        _normalizationService = normalizationService;
        _logger = logger;
    }

    public async Task<BatchProcessingResult> ProcessBatchAsync(
        Stream stream,
        BatchInputFormat format,
        BatchProcessingConfiguration? configuration = null,
        CancellationToken cancellationToken = default)
    {
        configuration ??= new BatchProcessingConfiguration();
        var stopwatch = Stopwatch.StartNew();

        var result = new BatchProcessingResult
        {
            StartedAt = DateTime.UtcNow,
            Configuration = configuration
        };

        try
        {
            _logger.LogInformation("Starting batch processing with format {Format}", format);

            // Parse identities from stream
            var identities = await ParseIdentitiesFromStreamAsync(stream, format, cancellationToken);
            result.TotalRecords = identities.Count;

            _logger.LogInformation("Parsed {Count} identities from input stream", identities.Count);

            // Process identities in parallel batches
            await ProcessIdentitiesInBatchesAsync(identities, result, configuration, cancellationToken);

            result.CompletedAt = DateTime.UtcNow;
            result.TotalProcessingTime = stopwatch.Elapsed;

            // Calculate summary statistics
            CalculateSummaryStatistics(result);

            _logger.LogInformation("Batch processing completed. Processed {Successful}/{Total} records in {Duration}ms",
                result.SuccessfullyProcessed, result.TotalRecords, stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during batch processing");
            result.Errors.Add(new BatchProcessingError
            {
                ErrorCode = "BATCH_PROCESSING_ERROR",
                ErrorMessage = ex.Message,
                ExceptionDetails = ex.ToString()
            });

            result.CompletedAt = DateTime.UtcNow;
            result.TotalProcessingTime = stopwatch.Elapsed;
            return result;
        }
    }

    public Task<Guid> ScheduleBatchProcessingAsync(BatchProcessingJobRequest jobRequest)
    {
        var jobId = Guid.NewGuid();

        var jobStatus = new BatchJobStatus
        {
            JobId = jobId,
            Status = JobStatus.Queued,
            StartedAt = DateTime.UtcNow,
            CurrentOperation = "Initializing batch processing job"
        };

        _batchJobs[jobId] = jobStatus;

        // In a real implementation, this would queue the job for background processing
        _ = Task.Run(async () =>
        {
            try
            {
                jobStatus.Status = JobStatus.Running;
                jobStatus.CurrentOperation = "Processing identities";

                // For this implementation, we'll simulate processing from a file
                // In a real implementation, this would read from blob storage or other sources
                // Validate and sanitize the file path to prevent path traversal attacks
                var sanitizedPath = ValidateAndSanitizeFilePath(jobRequest.Source);
                using var fileStream = File.OpenRead(sanitizedPath);
                var result = await ProcessBatchAsync(
                    fileStream,
                    jobRequest.InputFormat,
                    jobRequest.Configuration);

                // Store the results
                _batchResults[jobId] = result;

                jobStatus.ProcessedCount = result.SuccessfullyProcessed;
                jobStatus.TotalCount = result.TotalRecords;
                jobStatus.Status = JobStatus.Completed;
                jobStatus.CurrentOperation = "Completed";

                _logger.LogInformation("Batch processing job {JobId} completed successfully", jobId);
            }
            catch (Exception ex)
            {
                jobStatus.Status = JobStatus.Failed;
                jobStatus.ErrorMessage = ex.Message;
                _logger.LogError(ex, "Batch processing job {JobId} failed", jobId);
            }
        });

        _logger.LogInformation("Scheduled batch processing job {JobId} for source {Source}",
            jobId, SanitizeForLogging(jobRequest.Source));

        return Task.FromResult(jobId);
    }

    public Task<BatchJobStatus> GetBatchJobStatusAsync(Guid jobId)
    {
        _batchJobs.TryGetValue(jobId, out var status);
        return Task.FromResult(status ?? new BatchJobStatus
        {
            JobId = jobId,
            Status = JobStatus.Failed,
            ErrorMessage = "Job not found"
        });
    }

    public async Task<Stream> GetBatchResultsAsync(Guid jobId, BatchOutputFormat format = BatchOutputFormat.Json)
    {
        if (!_batchResults.TryGetValue(jobId, out var result))
        {
            throw new ArgumentException($"No results found for job {jobId}", nameof(jobId));
        }

        var stream = new MemoryStream();

        if (format == BatchOutputFormat.Json)
        {
            await JsonSerializer.SerializeAsync(stream, result, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
        else if (format == BatchOutputFormat.Csv)
        {
            await WriteCsvResultsAsync(stream, result);
        }

        stream.Position = 0;
        return stream;
    }

    public Task<bool> CancelBatchJobAsync(Guid jobId)
    {
        if (_batchJobs.TryGetValue(jobId, out var status) &&
            (status.Status == JobStatus.Queued || status.Status == JobStatus.Running))
        {
            status.Status = JobStatus.Cancelled;
            status.CurrentOperation = "Cancelled";
            _logger.LogInformation("Cancelled batch processing job {JobId}", jobId);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    private async Task<List<Identity>> ParseIdentitiesFromStreamAsync(
        Stream stream,
        BatchInputFormat format,
        CancellationToken cancellationToken)
    {
        var identities = new List<Identity>();

        if (format == BatchInputFormat.Json)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var jsonData = await JsonSerializer.DeserializeAsync<List<Identity>>(stream, options, cancellationToken);
            if (jsonData != null)
            {
                identities.AddRange(jsonData);
            }
        }
        else if (format == BatchInputFormat.Csv)
        {
            identities.AddRange(await ParseCsvAsync(stream, cancellationToken));
        }

        return identities;
    }

    private async Task<List<Identity>> ParseCsvAsync(Stream stream, CancellationToken cancellationToken)
    {
        var identities = new List<Identity>();
        using var reader = new StreamReader(stream);

        string? headerLine = await reader.ReadLineAsync();
        if (headerLine == null) return identities;

        var headers = headerLine.Split(',').Select(h => h.Trim().ToLowerInvariant()).ToArray();

        string? line;
        int lineNumber = 2; // Start from 2 since header is line 1

        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                var identity = ParseCsvLine(line, headers, lineNumber);
                if (identity != null)
                {
                    identities.Add(identity);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse CSV line {LineNumber}: {Line}", lineNumber, SanitizeForLogging(line));
            }

            lineNumber++;
        }

        return identities;
    }

    private Identity? ParseCsvLine(string line, string[] headers, int lineNumber)
    {
        var values = line.Split(',').Select(v => v.Trim('"', ' ')).ToArray();

        if (values.Length != headers.Length)
        {
            _logger.LogWarning("CSV line {LineNumber} has {ValueCount} values but expected {HeaderCount}",
                lineNumber, values.Length, headers.Length);
            return null;
        }

        var identity = new Identity();

        for (int i = 0; i < headers.Length; i++)
        {
            var header = headers[i];
            var value = values[i];

            if (string.IsNullOrWhiteSpace(value)) continue;

            switch (header)
            {
                case "firstname":
                case "first_name":
                    identity.PersonalInfo.FirstName = value;
                    break;
                case "lastname":
                case "last_name":
                    identity.PersonalInfo.LastName = value;
                    break;
                case "middlename":
                case "middle_name":
                    identity.PersonalInfo.MiddleName = value;
                    break;
                case "dateofbirth":
                case "date_of_birth":
                case "dob":
                    if (DateTime.TryParse(value, out var dob))
                    {
                        identity.PersonalInfo.DateOfBirth = dob;
                    }
                    break;
                case "email":
                    identity.ContactInfo.Email = value;
                    break;
                case "phone":
                    identity.ContactInfo.Phone = value;
                    break;
                case "ssn":
                    identity.Identifiers.Add(new Identifier
                    {
                        Type = "SSN",
                        Value = value
                    });
                    break;
                case "address":
                    identity.PersonalInfo.Address = new Address { Street1 = value };
                    break;
                case "source":
                    identity.Source = value;
                    break;
                default:
                    // Store unknown fields as attributes
                    identity.Attributes[header] = value;
                    break;
            }
        }

        return identity;
    }

    private async Task ProcessIdentitiesInBatchesAsync(
        List<Identity> identities,
        BatchProcessingResult result,
        BatchProcessingConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var semaphore = new SemaphoreSlim(configuration.MaxParallelism);
        var tasks = new List<Task>();
        var errorCount = 0;

        for (int i = 0; i < identities.Count; i += configuration.BatchSize)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var batch = identities.Skip(i).Take(configuration.BatchSize).ToList();

            var batchTask = Task.Run(async () =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    foreach (var identity in batch)
                    {
                        if (cancellationToken.IsCancellationRequested) break;

                        var recordResult = await ProcessSingleIdentityAsync(
                            identity,
                            result.Results.Count + 1,
                            configuration,
                            cancellationToken);

                        lock (result)
                        {
                            result.Results.Add(recordResult);

                            if (recordResult.IsSuccess)
                            {
                                result.SuccessfullyProcessed++;

                                if (result.DecisionCounts.ContainsKey(recordResult.Decision))
                                    result.DecisionCounts[recordResult.Decision]++;
                                else
                                    result.DecisionCounts[recordResult.Decision] = 1;
                            }
                            else
                            {
                                result.Failed++;
                                Interlocked.Increment(ref errorCount);

                                if (!configuration.ContinueOnError || errorCount >= configuration.MaxErrorsBeforeStop)
                                {
                                    cancellationToken.ThrowIfCancellationRequested();
                                }
                            }
                        }
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken);

            tasks.Add(batchTask);
        }

        await Task.WhenAll(tasks);
    }

    private async Task<BatchRecordResult> ProcessSingleIdentityAsync(
        Identity identity,
        int recordNumber,
        BatchProcessingConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var recordResult = new BatchRecordResult
        {
            RecordNumber = recordNumber,
            InputIdentity = identity
        };

        try
        {
            // Validate the identity
            var validationResult = ValidateIdentity(identity);
            if (!validationResult.IsValid)
            {
                recordResult.IsSuccess = false;
                recordResult.ErrorCode = validationResult.ErrorCode;
                recordResult.ErrorMessage = validationResult.ErrorMessage;
                return recordResult;
            }

            // Normalize the identity
            var normalizedIdentity = _normalizationService.NormalizeIdentity(identity);

            // Resolve the identity using deterministic + probabilistic matching
            var resolutionResult = await _resolutionService.ResolveIdentityAsync(
                normalizedIdentity,
                configuration.MatchingConfiguration,
                cancellationToken);

            // Map to batch record result
            recordResult.ResolvedIdentity = resolutionResult.ResolvedIdentity;
            recordResult.EPID = resolutionResult.EPID;
            recordResult.Decision = resolutionResult.Decision;
            recordResult.Score = resolutionResult.Matches.FirstOrDefault()?.OverallScore ?? 0.0;
            recordResult.Matches = resolutionResult.Matches;
            recordResult.Explanation = resolutionResult.Explanation;
            recordResult.IsSuccess = true;

            // Extract features used in matching
            if (resolutionResult.Matches.Any())
            {
                var firstMatch = resolutionResult.Matches.First();
                recordResult.Features["overall_score"] = firstMatch.OverallScore;
                recordResult.Features["match_count"] = resolutionResult.Matches.Count;
                recordResult.Features["algorithm"] = firstMatch.Algorithm ?? "default";

                // Add field scores if available
                foreach (var fieldScore in firstMatch.FieldScores)
                {
                    recordResult.Features[$"field_score_{fieldScore.Key}"] = fieldScore.Value;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing identity record {RecordNumber}", recordNumber);
            recordResult.IsSuccess = false;
            recordResult.ErrorCode = "PROCESSING_ERROR";
            recordResult.ErrorMessage = ex.Message;
        }
        finally
        {
            recordResult.ProcessingTime = stopwatch.Elapsed;
        }

        return recordResult;
    }

    private (bool IsValid, string? ErrorCode, string? ErrorMessage) ValidateIdentity(Identity identity)
    {
        // Check for required fields
        if (string.IsNullOrWhiteSpace(identity.PersonalInfo.FirstName) &&
            string.IsNullOrWhiteSpace(identity.PersonalInfo.LastName))
        {
            return (false, BatchErrorCodes.MissingRequiredField, "Either FirstName or LastName is required");
        }

        // Validate email format if provided
        if (!string.IsNullOrWhiteSpace(identity.ContactInfo.Email) &&
            !identity.ContactInfo.Email.Contains('@'))
        {
            return (false, BatchErrorCodes.InvalidEmail, "Invalid email format");
        }

        // Validate date of birth if provided
        if (identity.PersonalInfo.DateOfBirth.HasValue)
        {
            var dob = identity.PersonalInfo.DateOfBirth.Value;
            if (dob > DateTime.Now || dob < DateTime.Now.AddYears(-150))
            {
                return (false, BatchErrorCodes.MalformedDateOfBirth, "Date of birth is invalid");
            }
        }

        return (true, null, null);
    }

    private void CalculateSummaryStatistics(BatchProcessingResult result)
    {
        // Initialize decision counts
        foreach (ResolutionDecision decision in Enum.GetValues<ResolutionDecision>())
        {
            if (!result.DecisionCounts.ContainsKey(decision))
                result.DecisionCounts[decision] = 0;
        }

        // Calculate throughput metrics
        if (result.TotalProcessingTime.TotalSeconds > 0)
        {
            var throughputPerSecond = result.TotalRecords / result.TotalProcessingTime.TotalSeconds;
            var throughputPerHour = (int)(throughputPerSecond * 3600);

            _logger.LogInformation("Batch processing throughput: {ThroughputPerHour} records/hour", throughputPerHour);
        }
    }

    private async Task WriteCsvResultsAsync(Stream stream, BatchProcessingResult result)
    {
        using var writer = new StreamWriter(stream, leaveOpen: true);

        // Write CSV header
        await writer.WriteLineAsync("RecordNumber,EPID,Decision,Score,IsSuccess,ErrorCode,ErrorMessage,ProcessingTimeMs");

        // Write data rows
        foreach (var record in result.Results)
        {
            var line = $"{record.RecordNumber}," +
                      $"\"{record.EPID ?? ""}\"," +
                      $"{record.Decision}," +
                      $"{record.Score:F4}," +
                      $"{record.IsSuccess}," +
                      $"\"{record.ErrorCode ?? ""}\"," +
                      $"\"{record.ErrorMessage ?? ""}\"," +
                      $"{record.ProcessingTime.TotalMilliseconds:F0}";

            await writer.WriteLineAsync(line);
        }
    }

    /// <summary>
    /// Validates and sanitizes file paths to prevent path traversal attacks
    /// </summary>
    /// <param name="filePath">The file path to validate</param>
    /// <returns>Sanitized file path</returns>
    /// <exception cref="ArgumentException">Thrown when path is invalid or contains traversal patterns</exception>
    private static string ValidateAndSanitizeFilePath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
        }

        // Check for path traversal patterns
        if (filePath.Contains("..") || filePath.Contains("~"))
        {
            throw new ArgumentException("File path contains invalid traversal patterns", nameof(filePath));
        }

        // Get the full path and validate it's within expected boundaries
        var fullPath = Path.GetFullPath(filePath);

        // For security, only allow files in temp directory or specific allowed directories
        var allowedDirectories = new[] { "/tmp", Path.GetTempPath(), Environment.CurrentDirectory };

        if (!allowedDirectories.Any(dir => fullPath.StartsWith(Path.GetFullPath(dir), StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException($"File path is not in an allowed directory: {fullPath}", nameof(filePath));
        }

        // Validate file exists
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"File not found: {fullPath}");
        }

        return fullPath;
    }

    /// <summary>
    /// Sanitizes user input for safe logging to prevent log injection attacks
    /// </summary>
    /// <param name="input">The input string to sanitize</param>
    /// <returns>Sanitized string safe for logging</returns>
    private static string SanitizeForLogging(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return "[empty]";
        }

        // Remove or replace potentially dangerous characters for log injection
        // Keep only alphanumeric, basic punctuation, and common safe characters
        var sanitized = Regex.Replace(input, @"[^\w\s\-\.\@\/]", "_");

        // Limit length to prevent log flooding
        if (sanitized.Length > 100)
        {
            sanitized = sanitized.Substring(0, 97) + "...";
        }

        return sanitized;
    }
}
