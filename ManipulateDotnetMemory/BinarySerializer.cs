using System.Buffers.Binary;
using System.Text;

namespace BinarySerializer;

public class BinarySerializer
{
    #region Constants and Format Definition

    /// <summary>
    /// Magic number to identify valid File entries and detect corruption
    /// Using "MAN1" as a 4-byte signature
    /// </summary>
    private const uint MAGIC_NUMBER = 0x314F414D; // "MAN1" in little-endian

    /// <summary>
    /// Current binary format version for schema evolution
    /// Version 1: Initial format with all basic fields
    /// </summary>
    private const byte FORMAT_VERSION = 1;

    /// <summary>
    /// Maximum supported string length to prevent memory exhaustion attacks
    /// </summary>
    private const int MAX_STRING_LENGTH = 4096; // 4KB max for paths

    /// <summary>
    /// Maximum supported file content size for inline storage
    /// Larger files should be stored externally with references
    /// </summary>
    private const int MAX_INLINE_CONTENT_SIZE = 10 * 1024 * 1024; // 10MB

    #endregion


    /*
        BINARY FORMAT LAYOUT:
        [4 bytes] Magic Number (0x314F414D)
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

    public static byte[] Serialize(FileEntry entry)
    {
        using MemoryStream ms = new();
        using BinaryWriter writer = new(ms, Encoding.UTF8); // Follow Little Endian 

        writer.Write(MAGIC_NUMBER);
        writer.Write(FORMAT_VERSION);

        long lengthPosition = writer.BaseStream.Position;
        writer.Write(0); // Placeholder to update later
        long contentStartPosition = writer.BaseStream.Position;

        writer.Write(entry.SequenceNumber);
        writer.Write(entry.Timestamp.ToBinary());
        writer.Write((byte)entry.OperationType);
        writer.Write(entry.OriginalFileSize);
        writer.Write(entry.IsLargeFile);

        // Write variable-length size
        WriteString(writer, entry.TargetPath);
        WriteNullableString(writer, entry.NewPath);
        WriteNullableString(writer, entry.ContentChecksum);

        // Write file content
        if (entry.FileContent != null)
        {
            if (entry.FileContent.Length > MAX_INLINE_CONTENT_SIZE)
            {
                throw new InvalidOperationException(
                        $"File content size {entry.FileContent.Length} exceeds maximum {MAX_INLINE_CONTENT_SIZE}");
            }

            writer.Write(entry.FileContent.Length);
            writer.Write(entry.FileContent);
        }
        else
        {
            writer.Write(-1);
        }

        // Update entry length
        long currentPosition = writer.BaseStream.Position;
        var entryLength = (int)(currentPosition - contentStartPosition);

        writer.BaseStream.Seek(lengthPosition, SeekOrigin.Begin);
        writer.Write(entryLength);
        writer.BaseStream.Seek(currentPosition, SeekOrigin.Begin);

        // Calculate and write CRC32
        var entryData = ms.ToArray();
        var crc32 = CalculateCRC32(entryData, 0, entryData.Length - 9);
        writer.Write(crc32);

        writer.Flush();
        return ms.ToArray();
    }

    public FileEntry Deserialize(byte[] data)
    {
        if (data == null || data.Length < 17)
            throw new ArgumentException("Invalid WAL entry data: too small");

        using MemoryStream memoryStream = new(data);
        using BinaryReader reader = new(memoryStream, Encoding.UTF8);

        //Validate Magic Number
        int magic = reader.ReadInt32();
        if (magic != MAGIC_NUMBER)
            throw new NotSupportedException($"Invalid magic number: expected ${MAGIC_NUMBER:x8}, got ${magic:x8}");

        // Check format version
        byte version = reader.ReadByte();
        if (version != FORMAT_VERSION)
            throw new NotSupportedException($"Unsupported format version: ${version}");

        // Read entry length
        int expectedLength = reader.ReadInt32();
        int lastCrc32Space = 4;
        var remainingData = data.Length - (int)reader.BaseStream.Position - lastCrc32Space;
        if (remainingData < 0)
            throw new InvalidDataException("Invalid data: truncated before entry content");
        if (remainingData != expectedLength)
            throw new InvalidDataException($"Entry length mismatch: expected {expectedLength}, available {remainingData}");

        // Read core fields
        long sequenceNumber = reader.ReadInt64();
        DateTime timestamp = DateTime.FromBinary(reader.ReadInt64());
        FileOperationType operationType = (FileOperationType)reader.ReadByte();
        long originalFileSize = reader.ReadInt64();
        bool isLargeFile = reader.ReadBoolean();

        // Read variable-length data
        string targetPath = ReadString(reader);
        string? newPath = ReadNullableString(reader);
        string? readNullableString = ReadNullableString(reader);

        // Read file content
        byte[]? fileContent = null;
        int contentLength = reader.ReadInt32();
        if (contentLength >= 0)
        {
            if (contentLength > MAX_INLINE_CONTENT_SIZE)
                throw new InvalidDataException($"File content size {contentLength} exceeds maximum {MAX_INLINE_CONTENT_SIZE}");
            fileContent = reader.ReadBytes(contentLength);
            if (fileContent.Length != contentLength)
                throw new InvalidDataException("Unexpected end of stream while reading file content");
        }

        // Validate CRC32
        var expectedCrc = reader.ReadUInt32();
        var actualCrc = CalculateCRC32(data, 9, data.Length - 13);
        if (expectedCrc != actualCrc)
            throw new InvalidDataException($"CRC mismatch: expected {expectedCrc:X8}, calculated {actualCrc:X8}");

        // Create and validate entry
        var entry = new FileEntry(
            sequenceNumber: sequenceNumber,
            operationType: operationType,
            targetPath: targetPath,
            fileContent: fileContent,
            newPath: newPath,
            originalFileSize: originalFileSize
        );

        if (!entry.IsValid())
            throw new InvalidDataException("Deserialized WAL entry failed validation");

        return entry;
    }

    private static string ReadString(BinaryReader reader)
    {
        int length = reader.ReadInt32();
        if (length < 0 || length > MAX_STRING_LENGTH)
            throw new InvalidDataException($"Invalid string length: {length}");

        byte[] bytes = reader.ReadBytes(length);
        if (bytes.Length != length)
            throw new InvalidDataException("Unexpected end of stream while reading string");

        return Encoding.UTF8.GetString(bytes);
    }

    private static string? ReadNullableString(BinaryReader reader)
    {
        int length = reader.ReadInt32();
        if (length == -1)
            return null;

        return ReadString(reader);
    }

    private static uint CalculateCRC32(byte[] data, int offset, int length)
    {
        const uint polynomial = 0xEDB88320;
        var crc = 0xFFFFFFFF;

        for (int i = offset; i < offset + length; i++)
        {
            crc ^= data[i];
            for (int j = 0; j < 8; j++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ polynomial : crc >> 1;
            }
        }

        return crc ^ 0xFFFFFFFF;
    }

    private static void WriteString(BinaryWriter writer, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentNullException(nameof(value));

        if (value.Length > MAX_STRING_LENGTH)
            throw new ArgumentException($"String length {value.Length} exceeds maximum ${MAX_STRING_LENGTH}");

        var bytes = Encoding.UTF8.GetBytes(value);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static void WriteNullableString(BinaryWriter writer, string? value)
    {
        if (value == null)
        {
            writer.Write(-1); // null
            return;
        }

        if (value.Length > MAX_STRING_LENGTH)
            throw new ArgumentException($"String length {value.Length} exceeds maximum ${MAX_STRING_LENGTH}");

        var bytes = Encoding.UTF8.GetBytes(value);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

}