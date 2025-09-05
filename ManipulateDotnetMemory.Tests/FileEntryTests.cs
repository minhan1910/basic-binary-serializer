using System.Text;
using BinarySerializer;

namespace ManipulateDotnetMemory.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Layer", "DataStructure")]
public class FileEntryTests
{
    [Fact]
    [Trait("Type", "Constructor")]
    [Trait("Scenario", "HappyPath")]
    public void Constructor_ValidParameter_CreatesCorrectEntry()
    {
        var fileEntry = new FileEntry(
            sequenceNumber: 42,
            FileOperationType.CreateFile,
            targetPath: "/test/file.txt",
            fileContent: Encoding.UTF8.GetBytes("Hello World")
        );

        Assert.Equal(42, fileEntry.SequenceNumber);
        Assert.Equal(FileOperationType.CreateFile, fileEntry.OperationType);
        Assert.Equal("/test/file.txt", fileEntry.TargetPath);
        Assert.Equal(Encoding.UTF8.GetBytes("Hello World"), fileEntry.FileContent);
        Assert.True(Math.Abs((DateTime.UtcNow - fileEntry.Timestamp).TotalSeconds) < 1);
        Assert.False(fileEntry.IsLargeFile);
    }

    [Fact]
    [Trait("Type", "Constructor")]
    [Trait("Scenario", "HappyPath")]
    public void Constructor_LargeFile_SetsIsLargeFileCorrectly()
    {
        const string targetLargePath = "/test/large_file.txt";
        const int originalFileSize = 2 * 1024 * 1024;

        var fileEntry = new FileEntry(
            sequenceNumber: 42,
            FileOperationType.CreateFile,
            targetPath: targetLargePath,
            originalFileSize: originalFileSize
        );

        Assert.True(fileEntry.IsLargeFile);
    }

    [Fact]
    [Trait("Type", "Validation")]
    [Trait("Scenario", "ErrorCondition")]
    public void IsValid_RenameWithoutNewPath_ReturnsFalse()
    {
        const long sequenceNumber = 1;
        const FileOperationType fileOperationType = FileOperationType.RenameFile;
        const string oldPath = "/files/old-name.txt";

        var fileEntry = new FileEntry(
            sequenceNumber: sequenceNumber,
            operationType: fileOperationType,
            targetPath: oldPath
        );

        bool isValid = fileEntry.IsValid();

        Assert.False(isValid, "Rename operations without new path should fail validation");
    }

    [Fact]
    [Trait("Type", "Validation")]
    [Trait("Scenario", "ErrorCondition")]
    public void IsValid_SequenceNumberLessThan0_ReturnsFalse()
    {
        const long sequenceNumber = -1;
        const FileOperationType fileOperationType = FileOperationType.CreateFile;
        const string oldPath = "/files/old-name.txt";

        var fileEntry = new FileEntry(
            sequenceNumber: sequenceNumber,
            operationType: fileOperationType,
            targetPath: oldPath
        );

        bool isValid = fileEntry.IsValid();

        Assert.False(isValid, "Sequence Number should be greater than or equals 0");
    }

    [Fact]
    [Trait("Type", "Validation")]
    [Trait("Scenario", "ErrorCondition")]
    public void IsValid_TargetPathIsNullOrWhitespace_ReturnsFalse()
    {
        const long sequenceNumber = 1;
        const FileOperationType fileOperationType = FileOperationType.CreateDirectory;
        const string invalidTargetPath = "";

        var fileEntry = new FileEntry(
            sequenceNumber: sequenceNumber,
            operationType: fileOperationType,
            targetPath: invalidTargetPath
        );

        bool isValid = fileEntry.IsValid();

        Assert.False(isValid, "Target Path should not null or empty");
    }

    [Fact]
    [Trait("Type", "Validation")]
    [Trait("Scenario", "ErrorCondition")]
    public void IsValid_CreateOrWriteFileWithoutFileContent_ReturnsFalse()
    {
        const long sequenceNumber = 1;
        const FileOperationType fileOperationType = FileOperationType.CreateFile;
        const string targetPath = "/file/old.txt";
        byte[]? fileContent = null;

        var fileEntry = new FileEntry(
            sequenceNumber: sequenceNumber,
            operationType: fileOperationType,
            targetPath: targetPath,
            fileContent: fileContent
        );

        bool isValid = fileEntry.IsValid();

        Assert.False(isValid, "FileContent is must-have when operation type is CreateFile or WriteFile");

        Assert.Equal(sequenceNumber, fileEntry.SequenceNumber);
        Assert.Equal(fileOperationType, fileEntry.OperationType);
        Assert.Equal(targetPath, fileEntry.TargetPath);    
    }



}









