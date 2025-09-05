using System.Text;


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

using var ms = new MemoryStream();

WriteRecord(ms, new(1, "Minh An 20002", "23"));
ms.Position = 0; // reset position to read 

string result = ReadRecord(ms);

Console.WriteLine(result);

void WriteRecord(Stream stream, Person person)
{
    var (id, name, age) = person;

    // process id
    Span<byte> idPrefixLength = stackalloc byte[sizeof(int)];
    BitConverter.TryWriteBytes(idPrefixLength, id);

    // process Name
    byte[] nameBytes = Encoding.UTF8.GetBytes(name);
    Span<byte> namePrefixLength = stackalloc byte[sizeof(int)];
    BitConverter.TryWriteBytes(namePrefixLength, name.Length);

    // process Age
    byte[] ageBytes = Encoding.UTF8.GetBytes(age);
    Span<byte> agePrefixLength = stackalloc byte[sizeof(int)];
    BitConverter.TryWriteBytes(agePrefixLength, age.Length);

    stream.Write(idPrefixLength);
    stream.Write(namePrefixLength);
    stream.Write(nameBytes, 0, nameBytes.Length);
    stream.Write(agePrefixLength);
    stream.Write(ageBytes, 0, ageBytes.Length);
}

string ReadRecord(Stream stream)
{
    // Process Id
    Span<byte> lengthBytes = stackalloc byte[sizeof(int)];
    stream.ReadExactly(lengthBytes);
    int id = BitConverter.ToInt32(lengthBytes);

    // Process Name
    stream.ReadExactly(lengthBytes);
    int nameLength = BitConverter.ToInt32(lengthBytes);

    byte[] nameBytes = new byte[nameLength];
    stream.ReadExactly(nameBytes, 0, nameLength);
    string name = Encoding.UTF8.GetString(nameBytes);

    // Process Age
    stream.ReadExactly(lengthBytes);
    int ageLength = BitConverter.ToInt32(lengthBytes);

    byte[] ageBytes = new byte[ageLength];
    stream.ReadExactly(ageBytes, 0, ageLength);
    string age = Encoding.UTF8.GetString(ageBytes);

    return new Person(id, name, age).ToString();
}

record Person(int Id, string Name, string Age);


