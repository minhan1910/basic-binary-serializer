namespace BinarySerializer;

/*
    BINARY FORMAT LAYOUT:
    [4 bytes] Magic Number (0x314C4157)
    [1 byte]  Format Version (1)
    [4 bytes] Entry Length (excluding header)
    [8 bytes] Sequence Number
    [8 bytes] Timestamp (DateTime.ToBinary())
    [1 byte]  Operation Type
    [8 bytes] Original File Size
    [1 byte]  Is Large File flag
    [4 bytes] Target Path Length + UTF-8 Target Path
    [4 bytes] New Path Length + UTF-8 New Path (or -1 if null)
    [4 bytes] Content Checksum Length + UTF-8 Checksum (or -1 if null)
    [4 bytes] File Content Length + Binary Content (or -1 if null)
    [4 bytes] CRC32 of entire entry (for integrity validation)
*/

public enum FileOperationType : byte
{
    CreateFile = 1,
    WriteFile = 2,
    DeleteFile = 3,
    RenameFile = 4,
    CreateDirectory = 5,
    DeleteDirectory = 6
}

public readonly record struct FileEntry
{
    public readonly long SequenceNumber;
    public readonly DateTime Timestamp;
    public readonly FileOperationType OperationType;
    public readonly string TargetPath;
    public readonly string? NewPath;
    public readonly byte[]? FileContent;
    public readonly long OriginalFileSize;
    public readonly string? ContentChecksum;
    public readonly bool IsLargeFile;

    public FileEntry(
        long sequenceNumber,
        FileOperationType operationType,
        string targetPath,
        byte[]? fileContent = null,
        string? newPath = null,
        long originalFileSize = 0)
    {
        SequenceNumber = sequenceNumber;
        Timestamp = DateTime.UtcNow;
        OperationType = operationType;
        TargetPath = targetPath ?? throw new ArgumentNullException(nameof(targetPath));
        NewPath = newPath;
        FileContent = fileContent;
        OriginalFileSize = originalFileSize;
        IsLargeFile = originalFileSize > (1024 * 1024);

        // Calculate checksum if we have content
        if (fileContent != null && fileContent.Length > 0)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hash = sha256.ComputeHash(fileContent);
            ContentChecksum = Convert.ToHexString(hash);
        }
        else
        {
            ContentChecksum = null;
        }
    }

    public bool IsValid()
    {
        if (SequenceNumber <= 0) return false;
        if (string.IsNullOrWhiteSpace(TargetPath)) return false;
        if (Timestamp == DateTime.MinValue) return false;

        switch (OperationType)
        {
            case FileOperationType.RenameFile:
                return !string.IsNullOrWhiteSpace(NewPath);
            case FileOperationType.CreateFile:
            case FileOperationType.WriteFile:
                return FileContent != null || IsLargeFile;
            default:
                return true;
        }
    }
}