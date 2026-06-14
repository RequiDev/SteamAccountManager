# Steam Account Manager — Core Engine Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build and unit-test `SteamAccountManager.Core`, the UI-free library that locates Steam, reads/writes `loginusers.vdf`, controls the Steam process, switches accounts safely, and persists app metadata/groups/settings/autostart.

**Architecture:** A single `net10.0-windows` class library. Every external dependency (Windows registry, OS process, HTTP) sits behind an interface so the orchestration logic is fully unit-testable with in-memory fakes; file I/O is tested against real temp directories. The switch engine is a thin orchestrator over these seams. The WPF app (separate plan) wires the concrete implementations with DI.

**Tech Stack:** .NET 10 (LTS), C#, `ValveKeyValue` 0.70.x (KeyValues1 text), `System.Text.Json` (built-in), xUnit.

---

## Conventions (apply to every task)

- **Target framework:** `net10.0-windows` for both Core and the test project. `Nullable` = enable, `ImplicitUsings` = enable, `LangVersion` = latest (template defaults).
- **Namespaces:** root `SteamAccountManager.Core` with sub-namespaces by folder (`SteamAccountManager.Core.Models`, `.Steam`, `.Storage`, `.Avatars`, `.System`).
- **Commits:** one atomic commit per task (or per logically distinct sub-step where noted). Single-line subject in the form `Area/Component: description`. **Append this exact trailer to every commit message** (shown once here; do not repeat verbatim in each step — just apply it):

  ```
  Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
  ```
  i.e. `git commit -m "Core/AtomicFile: Add atomic file writer" -m "Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"`
- **Run tests:** `dotnet test` from the repo root. To run one test class: `dotnet test --filter "FullyQualifiedName~ClassName"`.
- **ValveKeyValue facts (verified against v0.70.0):** `KVSerializer.Create(KVSerializationFormat.KeyValues1Text)`; `Deserialize(stream)` returns a `KVDocument` whose `.Name` is `"users"` and whose `.Root` is an order-preserving collection; iterate `foreach (KeyValuePair<string, KVObject> e in doc.Root)`; the string indexer **throws** on a missing key (use `TryGetValue`); `KVObject` is **mutable in place**; assigning a string uses the implicit `string → KVObject` conversion (`account["MostRecent"] = "1"`); `Serialize(stream, doc)` writes UTF-8 **without BOM**, LF line endings, tab indentation; **key matching is ordinal/case-sensitive**, so always match the file's existing key casing when overwriting (the `VdfKeyValues` helper in Task 4 handles this).

---

### Task 1: Solution and project scaffolding

**Files:**
- Create: `SteamAccountManager.sln`
- Create: `src/SteamAccountManager.Core/SteamAccountManager.Core.csproj`
- Create: `tests/SteamAccountManager.Core.Tests/SteamAccountManager.Core.Tests.csproj`
- Create: `tests/SteamAccountManager.Core.Tests/TestPaths.cs`
- Delete: the template-generated `Class1.cs` and `UnitTest1.cs`

- [ ] **Step 1: Create the solution and projects**

Run from repo root:
```bash
dotnet new sln -n SteamAccountManager
dotnet new classlib -n SteamAccountManager.Core -o src/SteamAccountManager.Core -f net10.0
dotnet new xunit -n SteamAccountManager.Core.Tests -o tests/SteamAccountManager.Core.Tests -f net10.0
dotnet sln add src/SteamAccountManager.Core/SteamAccountManager.Core.csproj
dotnet sln add tests/SteamAccountManager.Core.Tests/SteamAccountManager.Core.Tests.csproj
dotnet add tests/SteamAccountManager.Core.Tests/SteamAccountManager.Core.Tests.csproj reference src/SteamAccountManager.Core/SteamAccountManager.Core.csproj
dotnet add src/SteamAccountManager.Core/SteamAccountManager.Core.csproj package ValveKeyValue
rm src/SteamAccountManager.Core/Class1.cs
rm tests/SteamAccountManager.Core.Tests/UnitTest1.cs
```

- [ ] **Step 2: Set both projects to `net10.0-windows` and expose internals to tests**

Replace the contents of `src/SteamAccountManager.Core/SteamAccountManager.Core.csproj` with:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="ValveKeyValue" Version="0.70.0" />
  </ItemGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="SteamAccountManager.Core.Tests" />
  </ItemGroup>

</Project>
```
In `tests/SteamAccountManager.Core.Tests/SteamAccountManager.Core.Tests.csproj`, change the `<TargetFramework>` value from `net10.0` to `net10.0-windows` (leave everything else the template generated).

- [ ] **Step 3: Add the shared test temp-directory helper**

Create `tests/SteamAccountManager.Core.Tests/TestPaths.cs`:
```csharp
using System;
using System.IO;

namespace SteamAccountManager.Core.Tests;

/// <summary>
/// Creates a unique temp directory for a test and deletes it on Dispose.
/// Use with `using var tmp = new TestPaths();` inside a test.
/// </summary>
public sealed class TestPaths : IDisposable
{
    public string Root { get; }

    public TestPaths()
    {
        Root = Path.Combine(Path.GetTempPath(), "sam-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string File(string name) => Path.Combine(Root, name);

    public string WriteFile(string name, string content)
    {
        var path = File(name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllText(path, content);
        return path;
    }

    public void Dispose()
    {
        try { Directory.Delete(Root, recursive: true); } catch { /* best effort cleanup */ }
    }
}
```

- [ ] **Step 4: Build and run the (now empty) test suite**

Run: `dotnet build`
Expected: build succeeds for both projects, targeting `net10.0-windows`.
Run: `dotnet test`
Expected: PASS (0 tests, no failures).

- [ ] **Step 5: Commit**

```bash
git add SteamAccountManager.sln src/ tests/
git commit -m "Build/Solution: Scaffold Core library and xUnit test project" -m "Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 2: Atomic file writer

**Files:**
- Create: `src/SteamAccountManager.Core/System/AtomicFile.cs`
- Test: `tests/SteamAccountManager.Core.Tests/System/AtomicFileTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/SteamAccountManager.Core.Tests/System/AtomicFileTests.cs`:
```csharp
using System.IO;
using System.Text;
using SteamAccountManager.Core.System;
using Xunit;

namespace SteamAccountManager.Core.Tests.System;

public class AtomicFileTests
{
    [Fact]
    public void Write_CreatesFileWithContent()
    {
        using var tmp = new TestPaths();
        var path = tmp.File("sub/data.txt");
        var sut = new AtomicFile();

        sut.Write(path, stream =>
        {
            var bytes = Encoding.UTF8.GetBytes("hello");
            stream.Write(bytes, 0, bytes.Length);
        });

        Assert.Equal("hello", File.ReadAllText(path));
    }

    [Fact]
    public void Write_OverwritesExistingFile_AndLeavesNoTempFiles()
    {
        using var tmp = new TestPaths();
        var path = tmp.File("data.txt");
        File.WriteAllText(path, "old-and-longer-content");
        var sut = new AtomicFile();

        sut.Write(path, stream =>
        {
            var bytes = Encoding.UTF8.GetBytes("new");
            stream.Write(bytes, 0, bytes.Length);
        });

        Assert.Equal("new", File.ReadAllText(path));
        Assert.Empty(Directory.GetFiles(tmp.Root, "*.tmp"));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~AtomicFileTests"`
Expected: FAIL — `AtomicFile` / namespace `SteamAccountManager.Core.System` does not exist (compile error).

- [ ] **Step 3: Write minimal implementation**

Create `src/SteamAccountManager.Core/System/AtomicFile.cs`:
```csharp
using System;
using System.IO;

namespace SteamAccountManager.Core.System;

/// <summary>Writes a file atomically: write to a temp file in the same directory, then replace.</summary>
public interface IAtomicFile
{
    void Write(string path, Action<Stream> writeContent);
}

public sealed class AtomicFile : IAtomicFile
{
    public void Write(string path, Action<Stream> writeContent)
    {
        var fullPath = Path.GetFullPath(path);
        var dir = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(dir);

        var temp = Path.Combine(dir, Path.GetFileName(fullPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var fs = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                writeContent(fs);
                fs.Flush(flushToDisk: true);
            }

            File.Move(temp, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
            {
                try { File.Delete(temp); } catch { /* best effort */ }
            }
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~AtomicFileTests"`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/ tests/
git commit -m "Core/AtomicFile: Add atomic temp-then-replace file writer" -m "Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 3: Domain models and exceptions

These are plain data/exception types with no behavior, so they are verified by compilation (used by later tested tasks), not by their own unit tests.

**Files:**
- Create: `src/SteamAccountManager.Core/Models/SteamAccount.cs`
- Create: `src/SteamAccountManager.Core/Models/SteamPaths.cs`
- Create: `src/SteamAccountManager.Core/Models/AccountMetadata.cs`
- Create: `src/SteamAccountManager.Core/Models/Group.cs`
- Create: `src/SteamAccountManager.Core/Models/AppSettings.cs`
- Create: `src/SteamAccountManager.Core/Models/CoreExceptions.cs`

- [ ] **Step 1: Create the model types**

`src/SteamAccountManager.Core/Models/SteamAccount.cs`:
```csharp
namespace SteamAccountManager.Core.Models;

/// <summary>An account as read from Steam's loginusers.vdf.</summary>
public sealed class SteamAccount
{
    public required string SteamId64 { get; init; }
    public string AccountName { get; init; } = "";
    public string PersonaName { get; init; } = "";
    public bool MostRecent { get; init; }
    public bool RememberPassword { get; init; }
    public bool AllowAutoLogin { get; init; }
    public long Timestamp { get; init; }
}
```

`src/SteamAccountManager.Core/Models/SteamPaths.cs`:
```csharp
namespace SteamAccountManager.Core.Models;

/// <summary>Resolved on-disk locations for a Steam installation.</summary>
public sealed record SteamPaths(
    string InstallDirectory,
    string ExecutablePath,
    string ConfigDirectory,
    string LoginUsersPath);
```

`src/SteamAccountManager.Core/Models/AccountMetadata.cs`:
```csharp
using System.Collections.Generic;

namespace SteamAccountManager.Core.Models;

/// <summary>App-owned metadata for an account, keyed by SteamId64. Persisted as JSON.</summary>
public sealed class AccountMetadata
{
    public string SteamId64 { get; set; } = "";
    public string? CustomLabel { get; set; }
    public string? Notes { get; set; }
    public List<string> GroupIds { get; set; } = new();
}
```

`src/SteamAccountManager.Core/Models/Group.cs`:
```csharp
namespace SteamAccountManager.Core.Models;

/// <summary>A user-defined category. Many accounts can belong to many groups.</summary>
public sealed class Group
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
}
```

`src/SteamAccountManager.Core/Models/AppSettings.cs`:
```csharp
namespace SteamAccountManager.Core.Models;

public sealed class AppSettings
{
    public bool AutostartEnabled { get; set; }
    public bool StartMinimized { get; set; }
}
```

`src/SteamAccountManager.Core/Models/CoreExceptions.cs`:
```csharp
using System;

namespace SteamAccountManager.Core.Models;

/// <summary>Thrown when no Steam installation can be located.</summary>
public sealed class SteamNotFoundException : Exception
{
    public SteamNotFoundException()
        : base("Could not locate a Steam installation on this machine.") { }
}

/// <summary>Thrown when a requested account is not present in loginusers.vdf.</summary>
public sealed class AccountNotFoundException : Exception
{
    public AccountNotFoundException(string steamId64)
        : base($"No account with SteamID '{steamId64}' was found.") => SteamId64 = steamId64;

    public string SteamId64 { get; }
}

/// <summary>Thrown when Steam could not be shut down within the allotted time.</summary>
public sealed class SteamShutdownException : Exception
{
    public SteamShutdownException()
        : base("Steam did not shut down within the timeout. Aborting to avoid clobbering its files.") { }
}
```

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build`
Expected: build succeeds.

- [ ] **Step 3: Commit**

```bash
git add src/
git commit -m "Core/Models: Add domain models and exceptions" -m "Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 4: VDF key helper and reading loginusers.vdf

**Files:**
- Create: `src/SteamAccountManager.Core/Steam/VdfKeyValues.cs`
- Create: `src/SteamAccountManager.Core/Steam/LoginUsersStore.cs` (read half this task)
- Test: `tests/SteamAccountManager.Core.Tests/Steam/LoginUsersStoreReadTests.cs`
- Test fixture helper: reuse `TestPaths.WriteFile`

- [ ] **Step 1: Write the failing test**

Create `tests/SteamAccountManager.Core.Tests/Steam/LoginUsersStoreReadTests.cs`:
```csharp
using System.Linq;
using SteamAccountManager.Core.Steam;
using SteamAccountManager.Core.System;
using Xunit;

namespace SteamAccountManager.Core.Tests.Steam;

public class LoginUsersStoreReadTests
{
    private const string ModernVdf = """
        "users"
        {
        	"76561198000000001"
        	{
        		"AccountName"		"alice"
        		"PersonaName"		"Alice"
        		"RememberPassword"		"1"
        		"WantsOfflineMode"		"0"
        		"SkipOfflineModeWarning"		"0"
        		"AllowAutoLogin"		"1"
        		"MostRecent"		"1"
        		"Timestamp"		"1688740727"
        	}
        	"76561198000000002"
        	{
        		"AccountName"		"bob"
        		"PersonaName"		"Bob"
        		"RememberPassword"		"1"
        		"AllowAutoLogin"		"0"
        		"MostRecent"		"0"
        		"Timestamp"		"1688740000"
        	}
        }
        """;

    // Legacy/lowercase-cased field names must still be read.
    private const string LowercaseVdf = """
        "users"
        {
        	"76561198000000003"
        	{
        		"accountname"		"carol"
        		"personaname"		"Carol"
        		"mostrecent"		"1"
        	}
        }
        """;

    [Fact]
    public void Read_ParsesAllAccountsAndFields()
    {
        using var tmp = new TestPaths();
        var path = tmp.WriteFile("config/loginusers.vdf", ModernVdf);
        var sut = new LoginUsersStore(new AtomicFile());

        var accounts = sut.Read(path);

        Assert.Equal(2, accounts.Count);
        var alice = accounts.Single(a => a.SteamId64 == "76561198000000001");
        Assert.Equal("alice", alice.AccountName);
        Assert.Equal("Alice", alice.PersonaName);
        Assert.True(alice.MostRecent);
        Assert.True(alice.RememberPassword);
        Assert.True(alice.AllowAutoLogin);
        Assert.Equal(1688740727L, alice.Timestamp);

        var bob = accounts.Single(a => a.SteamId64 == "76561198000000002");
        Assert.False(bob.MostRecent);
    }

    [Fact]
    public void Read_IsCaseInsensitiveOnFieldNames()
    {
        using var tmp = new TestPaths();
        var path = tmp.WriteFile("config/loginusers.vdf", LowercaseVdf);
        var sut = new LoginUsersStore(new AtomicFile());

        var carol = sut.Read(path).Single();

        Assert.Equal("carol", carol.AccountName);
        Assert.True(carol.MostRecent);
    }

    [Fact]
    public void Read_ReturnsEmpty_WhenFileMissing()
    {
        using var tmp = new TestPaths();
        var sut = new LoginUsersStore(new AtomicFile());

        var accounts = sut.Read(tmp.File("nope.vdf"));

        Assert.Empty(accounts);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~LoginUsersStoreReadTests"`
Expected: FAIL — `LoginUsersStore` / `VdfKeyValues` do not exist (compile error).

- [ ] **Step 3: Write the VDF helper**

Create `src/SteamAccountManager.Core/Steam/VdfKeyValues.cs`:
```csharp
using System;
using System.Collections.Generic;
using ValveKeyValue;

namespace SteamAccountManager.Core.Steam;

/// <summary>
/// Helpers around ValveKeyValue's case-sensitive (ordinal) key matching.
/// Steam treats keys case-insensitively, so reads scan case-insensitively and
/// writes overwrite the existing key using its on-disk casing (avoiding dup keys).
/// </summary>
internal static class VdfKeyValues
{
    public static bool TryGetCI(KVObject obj, string key, out KVObject value)
    {
        foreach (KeyValuePair<string, KVObject> child in obj)
        {
            if (string.Equals(child.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = child.Value;
                return true;
            }
        }

        value = null!;
        return false;
    }

    public static string? GetStringCI(KVObject obj, string key)
        => TryGetCI(obj, key, out var v) ? v.ToString() : null;

    public static bool GetBoolCI(KVObject obj, string key)
        => GetStringCI(obj, key) == "1";

    /// <summary>Sets a string value, reusing the existing key's casing if present.</summary>
    public static void SetStringPreservingCase(KVObject obj, string key, string value)
    {
        string? existingKey = null;
        foreach (KeyValuePair<string, KVObject> child in obj)
        {
            if (string.Equals(child.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                existingKey = child.Key;
                break;
            }
        }

        obj[existingKey ?? key] = value; // implicit string -> KVObject
    }
}
```

- [ ] **Step 4: Write the read half of `LoginUsersStore`**

Create `src/SteamAccountManager.Core/Steam/LoginUsersStore.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using SteamAccountManager.Core.Models;
using SteamAccountManager.Core.System;
using ValveKeyValue;

namespace SteamAccountManager.Core.Steam;

public interface ILoginUsersStore
{
    IReadOnlyList<SteamAccount> Read(string loginUsersPath);
    void SetActiveAccount(string loginUsersPath, string steamId64);
}

public sealed class LoginUsersStore : ILoginUsersStore
{
    private readonly IAtomicFile _atomicFile;

    public LoginUsersStore(IAtomicFile atomicFile) => _atomicFile = atomicFile;

    private static KVSerializer Serializer =>
        KVSerializer.Create(KVSerializationFormat.KeyValues1Text);

    public IReadOnlyList<SteamAccount> Read(string loginUsersPath)
    {
        if (!File.Exists(loginUsersPath))
        {
            return Array.Empty<SteamAccount>();
        }

        KVDocument doc;
        using (var fs = File.OpenRead(loginUsersPath))
        {
            doc = Serializer.Deserialize(fs);
        }

        var accounts = new List<SteamAccount>();
        foreach (KeyValuePair<string, KVObject> entry in doc.Root)
        {
            var acc = entry.Value;
            long.TryParse(VdfKeyValues.GetStringCI(acc, "Timestamp"), out var timestamp);

            accounts.Add(new SteamAccount
            {
                SteamId64 = entry.Key,
                AccountName = VdfKeyValues.GetStringCI(acc, "AccountName") ?? "",
                PersonaName = VdfKeyValues.GetStringCI(acc, "PersonaName") ?? "",
                MostRecent = VdfKeyValues.GetBoolCI(acc, "MostRecent"),
                RememberPassword = VdfKeyValues.GetBoolCI(acc, "RememberPassword"),
                AllowAutoLogin = VdfKeyValues.GetBoolCI(acc, "AllowAutoLogin"),
                Timestamp = timestamp,
            });
        }

        return accounts;
    }

    // SetActiveAccount is implemented in Task 5.
    public void SetActiveAccount(string loginUsersPath, string steamId64)
        => throw new NotImplementedException();
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~LoginUsersStoreReadTests"`
Expected: PASS (3 tests).

- [ ] **Step 6: Commit**

```bash
git add src/ tests/
git commit -m "Core/LoginUsersStore: Parse accounts from loginusers.vdf" -m "Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 5: Writing the active account back to loginusers.vdf

**Files:**
- Modify: `src/SteamAccountManager.Core/Steam/LoginUsersStore.cs` (replace `SetActiveAccount`)
- Test: `tests/SteamAccountManager.Core.Tests/Steam/LoginUsersStoreWriteTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/SteamAccountManager.Core.Tests/Steam/LoginUsersStoreWriteTests.cs`:
```csharp
using System.IO;
using System.Linq;
using SteamAccountManager.Core.Models;
using SteamAccountManager.Core.Steam;
using SteamAccountManager.Core.System;
using Xunit;

namespace SteamAccountManager.Core.Tests.Steam;

public class LoginUsersStoreWriteTests
{
    private const string Vdf = """
        "users"
        {
        	"76561198000000001"
        	{
        		"AccountName"		"alice"
        		"PersonaName"		"Alice"
        		"RememberPassword"		"1"
        		"WantsOfflineMode"		"0"
        		"AllowAutoLogin"		"1"
        		"MostRecent"		"1"
        		"Timestamp"		"1688740727"
        	}
        	"76561198000000002"
        	{
        		"AccountName"		"bob"
        		"PersonaName"		"Bob"
        		"RememberPassword"		"0"
        		"AllowAutoLogin"		"0"
        		"MostRecent"		"0"
        		"Timestamp"		"1688740000"
        	}
        }
        """;

    [Fact]
    public void SetActiveAccount_FlipsMostRecentAndRemember_AndPreservesOtherFields()
    {
        using var tmp = new TestPaths();
        var path = tmp.WriteFile("config/loginusers.vdf", Vdf);
        var sut = new LoginUsersStore(new AtomicFile());

        sut.SetActiveAccount(path, "76561198000000002");

        var accounts = sut.Read(path);
        var alice = accounts.Single(a => a.SteamId64 == "76561198000000001");
        var bob = accounts.Single(a => a.SteamId64 == "76561198000000002");

        Assert.False(alice.MostRecent);
        Assert.True(bob.MostRecent);
        Assert.True(bob.RememberPassword);

        // Unknown/unmodelled fields must survive the round-trip.
        var raw = File.ReadAllText(path);
        Assert.Contains("WantsOfflineMode", raw);
        Assert.Contains("Bob", raw);
        Assert.Contains("1688740000", raw);
    }

    [Fact]
    public void SetActiveAccount_Throws_WhenAccountMissing()
    {
        using var tmp = new TestPaths();
        var path = tmp.WriteFile("config/loginusers.vdf", Vdf);
        var sut = new LoginUsersStore(new AtomicFile());

        Assert.Throws<AccountNotFoundException>(
            () => sut.SetActiveAccount(path, "76561190000000000"));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~LoginUsersStoreWriteTests"`
Expected: FAIL — `SetActiveAccount` throws `NotImplementedException`.

- [ ] **Step 3: Implement `SetActiveAccount`**

In `src/SteamAccountManager.Core/Steam/LoginUsersStore.cs`, replace the placeholder `SetActiveAccount` method with:
```csharp
    public void SetActiveAccount(string loginUsersPath, string steamId64)
    {
        KVDocument doc;
        using (var fs = File.OpenRead(loginUsersPath))
        {
            doc = Serializer.Deserialize(fs);
        }

        var found = false;
        foreach (KeyValuePair<string, KVObject> entry in doc.Root)
        {
            var isTarget = string.Equals(entry.Key, steamId64, StringComparison.Ordinal);
            if (isTarget)
            {
                found = true;
            }

            VdfKeyValues.SetStringPreservingCase(entry.Value, "MostRecent", isTarget ? "1" : "0");
            if (isTarget)
            {
                VdfKeyValues.SetStringPreservingCase(entry.Value, "RememberPassword", "1");
            }
        }

        if (!found)
        {
            throw new AccountNotFoundException(steamId64);
        }

        _atomicFile.Write(loginUsersPath, stream => Serializer.Serialize(stream, doc));

        // Validate that what we wrote re-parses; throws if we produced something invalid.
        using var verify = File.OpenRead(loginUsersPath);
        _ = Serializer.Deserialize(verify);
    }
```
Add `using SteamAccountManager.Core.Models;` at the top if not already present (it is, from Task 4's read code referencing `SteamAccount`).

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~LoginUsersStoreWriteTests"`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/ tests/
git commit -m "Core/LoginUsersStore: Write active account with atomic, validated save" -m "Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 6: Windows registry abstraction, real implementation, and test fake

**Files:**
- Create: `src/SteamAccountManager.Core/System/IWindowsRegistry.cs`
- Create: `src/SteamAccountManager.Core/System/WindowsRegistry.cs`
- Create: `tests/SteamAccountManager.Core.Tests/Fakes/FakeWindowsRegistry.cs`
- Test: `tests/SteamAccountManager.Core.Tests/Fakes/FakeWindowsRegistryTests.cs`

The real `WindowsRegistry` touches the live registry and is verified manually (and exercised by the app). The fake is the seam that makes every registry-dependent class unit-testable, so we test the fake's own behavior to trust it.

- [ ] **Step 1: Write the failing test (for the fake)**

Create `tests/SteamAccountManager.Core.Tests/Fakes/FakeWindowsRegistry.cs`:
```csharp
using System.Collections.Generic;
using SteamAccountManager.Core.System;

namespace SteamAccountManager.Core.Tests.Fakes;

/// <summary>In-memory IWindowsRegistry for tests.</summary>
public sealed class FakeWindowsRegistry : IWindowsRegistry
{
    private readonly Dictionary<string, object> _values = new();

    private static string Key(RegistryHiveSelector hive, string subKey, string name)
        => $"{hive}|{subKey}|{name}";

    public string? GetString(RegistryHiveSelector hive, string subKey, string name)
        => _values.TryGetValue(Key(hive, subKey, name), out var v) ? v as string : null;

    public int? GetDword(RegistryHiveSelector hive, string subKey, string name)
        => _values.TryGetValue(Key(hive, subKey, name), out var v) && v is int i ? i : null;

    public void SetString(RegistryHiveSelector hive, string subKey, string name, string value)
        => _values[Key(hive, subKey, name)] = value;

    public void SetDword(RegistryHiveSelector hive, string subKey, string name, int value)
        => _values[Key(hive, subKey, name)] = value;

    public void DeleteValue(RegistryHiveSelector hive, string subKey, string name)
        => _values.Remove(Key(hive, subKey, name));
}
```

Create `tests/SteamAccountManager.Core.Tests/Fakes/FakeWindowsRegistryTests.cs`:
```csharp
using SteamAccountManager.Core.System;
using Xunit;

namespace SteamAccountManager.Core.Tests.Fakes;

public class FakeWindowsRegistryTests
{
    [Fact]
    public void RoundTripsStringsAndDwords_AndDeletes()
    {
        var reg = new FakeWindowsRegistry();

        Assert.Null(reg.GetString(RegistryHiveSelector.CurrentUser, @"Software\X", "S"));

        reg.SetString(RegistryHiveSelector.CurrentUser, @"Software\X", "S", "hello");
        reg.SetDword(RegistryHiveSelector.CurrentUser, @"Software\X", "D", 1);

        Assert.Equal("hello", reg.GetString(RegistryHiveSelector.CurrentUser, @"Software\X", "S"));
        Assert.Equal(1, reg.GetDword(RegistryHiveSelector.CurrentUser, @"Software\X", "D"));

        reg.DeleteValue(RegistryHiveSelector.CurrentUser, @"Software\X", "S");
        Assert.Null(reg.GetString(RegistryHiveSelector.CurrentUser, @"Software\X", "S"));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~FakeWindowsRegistryTests"`
Expected: FAIL — `IWindowsRegistry` / `RegistryHiveSelector` do not exist (compile error).

- [ ] **Step 3: Write the interface**

Create `src/SteamAccountManager.Core/System/IWindowsRegistry.cs`:
```csharp
namespace SteamAccountManager.Core.System;

public enum RegistryHiveSelector
{
    CurrentUser,
    LocalMachine,
}

/// <summary>Thin abstraction over the Windows registry so consumers are testable.</summary>
public interface IWindowsRegistry
{
    string? GetString(RegistryHiveSelector hive, string subKey, string name);
    int? GetDword(RegistryHiveSelector hive, string subKey, string name);
    void SetString(RegistryHiveSelector hive, string subKey, string name, string value);
    void SetDword(RegistryHiveSelector hive, string subKey, string name, int value);
    void DeleteValue(RegistryHiveSelector hive, string subKey, string name);
}
```

- [ ] **Step 4: Write the real implementation**

Create `src/SteamAccountManager.Core/System/WindowsRegistry.cs`:
```csharp
using System;
using Microsoft.Win32;

namespace SteamAccountManager.Core.System;

public sealed class WindowsRegistry : IWindowsRegistry
{
    public string? GetString(RegistryHiveSelector hive, string subKey, string name)
    {
        using var key = BaseKey(hive).OpenSubKey(subKey);
        return key?.GetValue(name) as string;
    }

    public int? GetDword(RegistryHiveSelector hive, string subKey, string name)
    {
        using var key = BaseKey(hive).OpenSubKey(subKey);
        var value = key?.GetValue(name);
        return value is null ? null : Convert.ToInt32(value);
    }

    public void SetString(RegistryHiveSelector hive, string subKey, string name, string value)
    {
        using var key = BaseKey(hive).CreateSubKey(subKey, writable: true);
        key.SetValue(name, value, RegistryValueKind.String);
    }

    public void SetDword(RegistryHiveSelector hive, string subKey, string name, int value)
    {
        using var key = BaseKey(hive).CreateSubKey(subKey, writable: true);
        key.SetValue(name, value, RegistryValueKind.DWord);
    }

    public void DeleteValue(RegistryHiveSelector hive, string subKey, string name)
    {
        using var key = BaseKey(hive).OpenSubKey(subKey, writable: true);
        key?.DeleteValue(name, throwOnMissingValue: false);
    }

    private static RegistryKey BaseKey(RegistryHiveSelector hive)
        => hive == RegistryHiveSelector.CurrentUser ? Registry.CurrentUser : Registry.LocalMachine;
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~FakeWindowsRegistryTests"`
Expected: PASS (1 test). Run `dotnet build` to confirm the real `WindowsRegistry` compiles.

- [ ] **Step 6: Commit**

```bash
git add src/ tests/
git commit -m "Core/Registry: Add IWindowsRegistry abstraction, impl, and fake" -m "Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 7: Steam install locator

**Files:**
- Create: `src/SteamAccountManager.Core/Steam/SteamLocator.cs`
- Test: `tests/SteamAccountManager.Core.Tests/Steam/SteamLocatorTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/SteamAccountManager.Core.Tests/Steam/SteamLocatorTests.cs`:
```csharp
using SteamAccountManager.Core.Steam;
using SteamAccountManager.Core.System;
using SteamAccountManager.Core.Tests.Fakes;
using Xunit;

namespace SteamAccountManager.Core.Tests.Steam;

public class SteamLocatorTests
{
    [Fact]
    public void Locate_UsesHkcuSteamPath_AndNormalizesSlashes()
    {
        var reg = new FakeWindowsRegistry();
        reg.SetString(RegistryHiveSelector.CurrentUser, @"Software\Valve\Steam", "SteamPath",
            "C:/Program Files (x86)/Steam");
        var sut = new SteamLocator(reg);

        var paths = sut.Locate();

        Assert.NotNull(paths);
        Assert.Equal(@"C:\Program Files (x86)\Steam", paths!.InstallDirectory);
        Assert.Equal(@"C:\Program Files (x86)\Steam\steam.exe", paths.ExecutablePath);
        Assert.Equal(@"C:\Program Files (x86)\Steam\config\loginusers.vdf", paths.LoginUsersPath);
    }

    [Fact]
    public void Locate_FallsBackToHklmWow6432Node()
    {
        var reg = new FakeWindowsRegistry();
        reg.SetString(RegistryHiveSelector.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath",
            @"D:\Steam");
        var sut = new SteamLocator(reg);

        var paths = sut.Locate();

        Assert.Equal(@"D:\Steam", paths!.InstallDirectory);
    }

    [Fact]
    public void Locate_ReturnsNull_WhenNothingRegistered()
    {
        var sut = new SteamLocator(new FakeWindowsRegistry());
        Assert.Null(sut.Locate());
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~SteamLocatorTests"`
Expected: FAIL — `SteamLocator` does not exist.

- [ ] **Step 3: Write the implementation**

Create `src/SteamAccountManager.Core/Steam/SteamLocator.cs`:
```csharp
using System.IO;
using SteamAccountManager.Core.Models;
using SteamAccountManager.Core.System;

namespace SteamAccountManager.Core.Steam;

public interface ISteamLocator
{
    SteamPaths? Locate();
}

public sealed class SteamLocator : ISteamLocator
{
    private readonly IWindowsRegistry _registry;

    public SteamLocator(IWindowsRegistry registry) => _registry = registry;

    public SteamPaths? Locate()
    {
        var install =
            _registry.GetString(RegistryHiveSelector.CurrentUser, @"Software\Valve\Steam", "SteamPath")
            ?? _registry.GetString(RegistryHiveSelector.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath")
            ?? _registry.GetString(RegistryHiveSelector.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath");

        if (string.IsNullOrWhiteSpace(install))
        {
            return null;
        }

        install = install.Replace('/', '\\').TrimEnd('\\');
        var configDir = Path.Combine(install, "config");

        return new SteamPaths(
            InstallDirectory: install,
            ExecutablePath: Path.Combine(install, "steam.exe"),
            ConfigDirectory: configDir,
            LoginUsersPath: Path.Combine(configDir, "loginusers.vdf"));
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~SteamLocatorTests"`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/ tests/
git commit -m "Core/SteamLocator: Resolve Steam paths from the registry" -m "Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 8: Steam registry selectors (AutoLoginUser / RememberPassword)

**Files:**
- Create: `src/SteamAccountManager.Core/Steam/SteamRegistry.cs`
- Test: `tests/SteamAccountManager.Core.Tests/Steam/SteamRegistryTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/SteamAccountManager.Core.Tests/Steam/SteamRegistryTests.cs`:
```csharp
using SteamAccountManager.Core.Steam;
using SteamAccountManager.Core.System;
using SteamAccountManager.Core.Tests.Fakes;
using Xunit;

namespace SteamAccountManager.Core.Tests.Steam;

public class SteamRegistryTests
{
    private const string SteamKey = @"Software\Valve\Steam";

    [Fact]
    public void SetAutoLoginUser_And_GetAutoLoginUser_RoundTrip()
    {
        var reg = new FakeWindowsRegistry();
        var sut = new SteamRegistry(reg);

        sut.SetAutoLoginUser("alice");

        Assert.Equal("alice", sut.GetAutoLoginUser());
        Assert.Equal("alice", reg.GetString(RegistryHiveSelector.CurrentUser, SteamKey, "AutoLoginUser"));
    }

    [Fact]
    public void ClearAutoLoginUser_SetsEmptyString()
    {
        var reg = new FakeWindowsRegistry();
        var sut = new SteamRegistry(reg);
        sut.SetAutoLoginUser("alice");

        sut.ClearAutoLoginUser();

        Assert.Equal("", sut.GetAutoLoginUser());
    }

    [Fact]
    public void SetRememberPassword_WritesDword()
    {
        var reg = new FakeWindowsRegistry();
        var sut = new SteamRegistry(reg);

        sut.SetRememberPassword(true);
        Assert.True(sut.GetRememberPassword());
        Assert.Equal(1, reg.GetDword(RegistryHiveSelector.CurrentUser, SteamKey, "RememberPassword"));

        sut.SetRememberPassword(false);
        Assert.False(sut.GetRememberPassword());
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~SteamRegistryTests"`
Expected: FAIL — `SteamRegistry` does not exist.

- [ ] **Step 3: Write the implementation**

Create `src/SteamAccountManager.Core/Steam/SteamRegistry.cs`:
```csharp
using SteamAccountManager.Core.System;

namespace SteamAccountManager.Core.Steam;

public interface ISteamRegistry
{
    string? GetAutoLoginUser();
    void SetAutoLoginUser(string accountName);
    void ClearAutoLoginUser();
    bool GetRememberPassword();
    void SetRememberPassword(bool value);
}

public sealed class SteamRegistry : ISteamRegistry
{
    private const string SteamKey = @"Software\Valve\Steam";
    private const string AutoLoginUserValue = "AutoLoginUser";
    private const string RememberPasswordValue = "RememberPassword";

    private readonly IWindowsRegistry _registry;

    public SteamRegistry(IWindowsRegistry registry) => _registry = registry;

    public string? GetAutoLoginUser()
        => _registry.GetString(RegistryHiveSelector.CurrentUser, SteamKey, AutoLoginUserValue);

    public void SetAutoLoginUser(string accountName)
        => _registry.SetString(RegistryHiveSelector.CurrentUser, SteamKey, AutoLoginUserValue, accountName);

    public void ClearAutoLoginUser()
        => _registry.SetString(RegistryHiveSelector.CurrentUser, SteamKey, AutoLoginUserValue, "");

    public bool GetRememberPassword()
        => _registry.GetDword(RegistryHiveSelector.CurrentUser, SteamKey, RememberPasswordValue) == 1;

    public void SetRememberPassword(bool value)
        => _registry.SetDword(RegistryHiveSelector.CurrentUser, SteamKey, RememberPasswordValue, value ? 1 : 0);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~SteamRegistryTests"`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/ tests/
git commit -m "Core/SteamRegistry: Read and write Steam auto-login selectors" -m "Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 9: Steam process controller

**Files:**
- Create: `src/SteamAccountManager.Core/Steam/SteamProcessController.cs`

This class touches real OS processes, so it is defined behind `ISteamProcessController` (faked in Task 11's tests) and verified manually here (build + the manual smoke check noted below). No unit test.

- [ ] **Step 1: Write the interface and implementation**

Create `src/SteamAccountManager.Core/Steam/SteamProcessController.cs`:
```csharp
using System;
using System.Diagnostics;
using System.Threading;
using SteamAccountManager.Core.Models;

namespace SteamAccountManager.Core.Steam;

public interface ISteamProcessController
{
    bool IsSteamRunning();

    /// <summary>Requests a graceful shutdown and waits. Returns true once Steam is gone.</summary>
    bool ShutdownAndWait(TimeSpan timeout);

    void Launch(string? arguments = null);
}

public sealed class SteamProcessController : ISteamProcessController
{
    private const string SteamProcessName = "steam"; // steam.exe -> "steam"
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

    private readonly ISteamLocator _locator;

    public SteamProcessController(ISteamLocator locator) => _locator = locator;

    public bool IsSteamRunning()
        => Process.GetProcessesByName(SteamProcessName).Length > 0;

    public bool ShutdownAndWait(TimeSpan timeout)
    {
        if (!IsSteamRunning())
        {
            return true;
        }

        var paths = _locator.Locate();
        if (paths is null)
        {
            return false;
        }

        using (Process.Start(new ProcessStartInfo(paths.ExecutablePath, "-shutdown") { UseShellExecute = false }))
        {
        }

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (!IsSteamRunning())
            {
                return true;
            }

            Thread.Sleep(PollInterval);
        }

        return !IsSteamRunning();
    }

    public void Launch(string? arguments = null)
    {
        var paths = _locator.Locate() ?? throw new SteamNotFoundException();
        using (Process.Start(new ProcessStartInfo(paths.ExecutablePath, arguments ?? "") { UseShellExecute = true }))
        {
        }
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build`
Expected: build succeeds.

- [ ] **Step 3: Manual smoke check (note for the executor; do not block the commit on a machine without Steam)**

If Steam is installed on the dev machine: in a scratch console or via the app later, confirm `IsSteamRunning()` reflects reality, `ShutdownAndWait` closes a running Steam within ~10s, and `Launch()` starts it. This concrete class has no automated test by design.

- [ ] **Step 4: Commit**

```bash
git add src/
git commit -m "Core/SteamProcess: Add graceful shutdown, running-check, and launch" -m "Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 10: Backup service

**Files:**
- Create: `src/SteamAccountManager.Core/Steam/BackupService.cs`
- Test: `tests/SteamAccountManager.Core.Tests/Steam/BackupServiceTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/SteamAccountManager.Core.Tests/Steam/BackupServiceTests.cs`:
```csharp
using System.IO;
using SteamAccountManager.Core.Steam;
using Xunit;

namespace SteamAccountManager.Core.Tests.Steam;

public class BackupServiceTests
{
    [Fact]
    public void Backup_ThenRestore_RecoversOriginalContent()
    {
        using var tmp = new TestPaths();
        var file = tmp.WriteFile("config/loginusers.vdf", "ORIGINAL");
        var sut = new BackupService(tmp.File("backups"));

        sut.Backup(file);
        Assert.True(sut.HasBackup(file));

        File.WriteAllText(file, "CORRUPTED");
        sut.Restore(file);

        Assert.Equal("ORIGINAL", File.ReadAllText(file));
    }

    [Fact]
    public void Backup_DoesNothing_WhenSourceMissing()
    {
        using var tmp = new TestPaths();
        var sut = new BackupService(tmp.File("backups"));

        sut.Backup(tmp.File("missing.vdf")); // must not throw

        Assert.False(sut.HasBackup(tmp.File("missing.vdf")));
    }

    [Fact]
    public void Restore_DoesNothing_WhenNoBackupExists()
    {
        using var tmp = new TestPaths();
        var file = tmp.WriteFile("loginusers.vdf", "LIVE");
        var sut = new BackupService(tmp.File("backups"));

        sut.Restore(file); // must not throw or change the file

        Assert.Equal("LIVE", File.ReadAllText(file));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~BackupServiceTests"`
Expected: FAIL — `BackupService` does not exist.

- [ ] **Step 3: Write the implementation**

Create `src/SteamAccountManager.Core/Steam/BackupService.cs`:
```csharp
using System.IO;

namespace SteamAccountManager.Core.Steam;

public interface IBackupService
{
    void Backup(string filePath);
    void Restore(string filePath);
    bool HasBackup(string filePath);
}

/// <summary>Keeps a single rolling ".last.bak" copy per file, restored on switch failure.</summary>
public sealed class BackupService : IBackupService
{
    private readonly string _backupDirectory;

    public BackupService(string backupDirectory) => _backupDirectory = backupDirectory;

    private string BackupPathFor(string filePath)
        => Path.Combine(_backupDirectory, Path.GetFileName(filePath) + ".last.bak");

    public void Backup(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return;
        }

        Directory.CreateDirectory(_backupDirectory);
        File.Copy(filePath, BackupPathFor(filePath), overwrite: true);
    }

    public bool HasBackup(string filePath) => File.Exists(BackupPathFor(filePath));

    public void Restore(string filePath)
    {
        var backup = BackupPathFor(filePath);
        if (File.Exists(backup))
        {
            File.Copy(backup, filePath, overwrite: true);
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~BackupServiceTests"`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/ tests/
git commit -m "Core/BackupService: Add rolling backup and restore for Steam files" -m "Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 11: Account switch orchestrator

**Files:**
- Create: `src/SteamAccountManager.Core/Steam/AccountSwitcher.cs`
- Create: `tests/SteamAccountManager.Core.Tests/Fakes/FakeSteamLocator.cs`
- Create: `tests/SteamAccountManager.Core.Tests/Fakes/FakeProcessController.cs`
- Create: `tests/SteamAccountManager.Core.Tests/Fakes/ThrowingLoginUsersStore.cs`
- Test: `tests/SteamAccountManager.Core.Tests/Steam/AccountSwitcherTests.cs`

- [ ] **Step 1: Write the test fakes**

Create `tests/SteamAccountManager.Core.Tests/Fakes/FakeSteamLocator.cs`:
```csharp
using SteamAccountManager.Core.Models;
using SteamAccountManager.Core.Steam;

namespace SteamAccountManager.Core.Tests.Fakes;

public sealed class FakeSteamLocator : ISteamLocator
{
    private readonly SteamPaths? _paths;
    public FakeSteamLocator(SteamPaths? paths) => _paths = paths;
    public SteamPaths? Locate() => _paths;
}
```

Create `tests/SteamAccountManager.Core.Tests/Fakes/FakeProcessController.cs`:
```csharp
using System;
using SteamAccountManager.Core.Steam;

namespace SteamAccountManager.Core.Tests.Fakes;

public sealed class FakeProcessController : ISteamProcessController
{
    public bool Running { get; set; }
    public bool ShutdownResult { get; set; } = true;

    public bool ShutdownCalled { get; private set; }
    public bool LaunchCalled { get; private set; }
    public string? LaunchArguments { get; private set; }

    public bool IsSteamRunning() => Running;

    public bool ShutdownAndWait(TimeSpan timeout)
    {
        ShutdownCalled = true;
        if (ShutdownResult)
        {
            Running = false;
        }

        return ShutdownResult;
    }

    public void Launch(string? arguments = null)
    {
        LaunchCalled = true;
        LaunchArguments = arguments;
    }
}
```

Create `tests/SteamAccountManager.Core.Tests/Fakes/ThrowingLoginUsersStore.cs`:
```csharp
using System;
using System.Collections.Generic;
using SteamAccountManager.Core.Models;
using SteamAccountManager.Core.Steam;

namespace SteamAccountManager.Core.Tests.Fakes;

/// <summary>Reads via a real delegate but throws on write, to exercise rollback.</summary>
public sealed class ThrowingLoginUsersStore : ILoginUsersStore
{
    private readonly ILoginUsersStore _inner;
    public ThrowingLoginUsersStore(ILoginUsersStore inner) => _inner = inner;

    public IReadOnlyList<SteamAccount> Read(string loginUsersPath) => _inner.Read(loginUsersPath);

    public void SetActiveAccount(string loginUsersPath, string steamId64)
        => throw new InvalidOperationException("simulated write failure");
}
```

- [ ] **Step 2: Write the failing test**

Create `tests/SteamAccountManager.Core.Tests/Steam/AccountSwitcherTests.cs`:
```csharp
using System;
using System.IO;
using SteamAccountManager.Core.Models;
using SteamAccountManager.Core.Steam;
using SteamAccountManager.Core.System;
using SteamAccountManager.Core.Tests.Fakes;
using Xunit;

namespace SteamAccountManager.Core.Tests.Steam;

public class AccountSwitcherTests
{
    private const string Vdf = """
        "users"
        {
        	"76561198000000001"
        	{
        		"AccountName"		"alice"
        		"MostRecent"		"1"
        	}
        	"76561198000000002"
        	{
        		"AccountName"		"bob"
        		"MostRecent"		"0"
        	}
        }
        """;

    private sealed record Harness(
        AccountSwitcher Switcher,
        FakeWindowsRegistry Registry,
        SteamRegistry SteamRegistry,
        FakeProcessController Process,
        string LoginUsersPath);

    private static Harness BuildHarness(TestPaths tmp, ILoginUsersStore? loginUsersOverride = null)
    {
        var loginUsersPath = tmp.WriteFile("config/loginusers.vdf", Vdf);
        var paths = new SteamPaths(
            tmp.Root, Path.Combine(tmp.Root, "steam.exe"),
            tmp.File("config"), loginUsersPath);

        var reg = new FakeWindowsRegistry();
        var steamReg = new SteamRegistry(reg);
        var process = new FakeProcessController();
        var atomic = new AtomicFile();
        var loginUsers = loginUsersOverride ?? new LoginUsersStore(atomic);
        var backup = new BackupService(tmp.File("backups"));

        var switcher = new AccountSwitcher(
            new FakeSteamLocator(paths), loginUsers, steamReg, process, backup);

        return new Harness(switcher, reg, steamReg, process, loginUsersPath);
    }

    [Fact]
    public void SwitchTo_SetsRegistryAndFile_AndLaunches_WhenSteamNotRunning()
    {
        using var tmp = new TestPaths();
        var h = BuildHarness(tmp);

        h.Switcher.SwitchTo("76561198000000002");

        Assert.Equal("bob", h.SteamRegistry.GetAutoLoginUser());
        Assert.True(h.SteamRegistry.GetRememberPassword());
        Assert.Contains("\"MostRecent\"\t\t\"1\"", FileForBob(h.LoginUsersPath));
        Assert.True(h.Process.LaunchCalled);
        Assert.False(h.Process.ShutdownCalled);
    }

    [Fact]
    public void SwitchTo_ShutsDownFirst_WhenSteamRunning()
    {
        using var tmp = new TestPaths();
        var h = BuildHarness(tmp);
        h.Process.Running = true;

        h.Switcher.SwitchTo("76561198000000002");

        Assert.True(h.Process.ShutdownCalled);
        Assert.True(h.Process.LaunchCalled);
    }

    [Fact]
    public void SwitchTo_Throws_AndDoesNotLaunch_WhenShutdownFails()
    {
        using var tmp = new TestPaths();
        var h = BuildHarness(tmp);
        h.Process.Running = true;
        h.Process.ShutdownResult = false;

        Assert.Throws<SteamShutdownException>(() => h.Switcher.SwitchTo("76561198000000002"));
        Assert.False(h.Process.LaunchCalled);
    }

    [Fact]
    public void SwitchTo_Throws_WhenAccountUnknown()
    {
        using var tmp = new TestPaths();
        var h = BuildHarness(tmp);

        Assert.Throws<AccountNotFoundException>(() => h.Switcher.SwitchTo("76561190000000000"));
    }

    [Fact]
    public void SwitchTo_RestoresFileAndRegistry_WhenWriteFails()
    {
        using var tmp = new TestPaths();
        var inner = new LoginUsersStore(new AtomicFile());
        var h = BuildHarness(tmp, new ThrowingLoginUsersStore(inner));
        h.SteamRegistry.SetAutoLoginUser("previous-user");
        var original = File.ReadAllText(h.LoginUsersPath);

        Assert.Throws<InvalidOperationException>(() => h.Switcher.SwitchTo("76561198000000002"));

        Assert.Equal(original, File.ReadAllText(h.LoginUsersPath));   // file restored
        Assert.Equal("previous-user", h.SteamRegistry.GetAutoLoginUser()); // registry restored
        Assert.False(h.Process.LaunchCalled);
    }

    [Fact]
    public void BeginAddAccount_ClearsAutoLogin_AndLaunches()
    {
        using var tmp = new TestPaths();
        var h = BuildHarness(tmp);
        h.SteamRegistry.SetAutoLoginUser("alice");

        h.Switcher.BeginAddAccount();

        Assert.Equal("", h.SteamRegistry.GetAutoLoginUser());
        Assert.True(h.Process.LaunchCalled);
    }

    private static string FileForBob(string loginUsersPath) => File.ReadAllText(loginUsersPath);
}
```
> Note on the `MostRecent` assertion: ValveKeyValue writes tab-separated `"key"\t\t"value"`. If the exact tab count differs on your machine, assert with `Assert.Contains("\"MostRecent\"", ...)` plus a re-read via `LoginUsersStore.Read` checking `bob.MostRecent == true` instead. Prefer the semantic re-read if brittle.

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~AccountSwitcherTests"`
Expected: FAIL — `AccountSwitcher` does not exist.

- [ ] **Step 4: Write the implementation**

Create `src/SteamAccountManager.Core/Steam/AccountSwitcher.cs`:
```csharp
using System;
using System.Linq;
using SteamAccountManager.Core.Models;

namespace SteamAccountManager.Core.Steam;

public interface IAccountSwitcher
{
    void SwitchTo(string steamId64);
    void BeginAddAccount();
}

public sealed class AccountSwitcher : IAccountSwitcher
{
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(30);

    private readonly ISteamLocator _locator;
    private readonly ILoginUsersStore _loginUsers;
    private readonly ISteamRegistry _registry;
    private readonly ISteamProcessController _process;
    private readonly IBackupService _backup;

    public AccountSwitcher(
        ISteamLocator locator,
        ILoginUsersStore loginUsers,
        ISteamRegistry registry,
        ISteamProcessController process,
        IBackupService backup)
    {
        _locator = locator;
        _loginUsers = loginUsers;
        _registry = registry;
        _process = process;
        _backup = backup;
    }

    public void SwitchTo(string steamId64)
    {
        var paths = _locator.Locate() ?? throw new SteamNotFoundException();

        var target = _loginUsers.Read(paths.LoginUsersPath)
            .FirstOrDefault(a => a.SteamId64 == steamId64)
            ?? throw new AccountNotFoundException(steamId64);

        EnsureSteamClosed();

        var previousAutoLogin = _registry.GetAutoLoginUser();
        var previousRemember = _registry.GetRememberPassword();
        _backup.Backup(paths.LoginUsersPath);

        try
        {
            _registry.SetAutoLoginUser(target.AccountName);
            _registry.SetRememberPassword(true);
            _loginUsers.SetActiveAccount(paths.LoginUsersPath, steamId64);
        }
        catch
        {
            _backup.Restore(paths.LoginUsersPath);
            if (previousAutoLogin is not null)
            {
                _registry.SetAutoLoginUser(previousAutoLogin);
            }

            _registry.SetRememberPassword(previousRemember);
            throw;
        }

        _process.Launch();
    }

    public void BeginAddAccount()
    {
        EnsureSteamClosed();
        _registry.ClearAutoLoginUser();
        _process.Launch();
    }

    private void EnsureSteamClosed()
    {
        if (_process.IsSteamRunning() && !_process.ShutdownAndWait(ShutdownTimeout))
        {
            throw new SteamShutdownException();
        }
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~AccountSwitcherTests"`
Expected: PASS (6 tests). If the `MostRecent` tab assertion is brittle, switch it to the semantic re-read described in the Step 2 note.

- [ ] **Step 6: Commit**

```bash
git add src/ tests/
git commit -m "Core/AccountSwitcher: Orchestrate safe switch and add-account flows" -m "Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 12: JSON file helper and account metadata store

**Files:**
- Create: `src/SteamAccountManager.Core/Storage/JsonFile.cs`
- Create: `src/SteamAccountManager.Core/Storage/AccountMetadataStore.cs`
- Test: `tests/SteamAccountManager.Core.Tests/Storage/AccountMetadataStoreTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/SteamAccountManager.Core.Tests/Storage/AccountMetadataStoreTests.cs`:
```csharp
using SteamAccountManager.Core.Models;
using SteamAccountManager.Core.Storage;
using SteamAccountManager.Core.System;
using Xunit;

namespace SteamAccountManager.Core.Tests.Storage;

public class AccountMetadataStoreTests
{
    [Fact]
    public void Get_ReturnsEmptyMetadata_WhenUnknown()
    {
        using var tmp = new TestPaths();
        var sut = new AccountMetadataStore(tmp.File("metadata.json"), new AtomicFile());

        var meta = sut.Get("76561198000000001");

        Assert.Equal("76561198000000001", meta.SteamId64);
        Assert.Null(meta.CustomLabel);
        Assert.Empty(meta.GroupIds);
    }

    [Fact]
    public void Upsert_Persists_AndSurvivesReload()
    {
        using var tmp = new TestPaths();
        var path = tmp.File("metadata.json");
        var atomic = new AtomicFile();

        var store = new AccountMetadataStore(path, atomic);
        store.Upsert(new AccountMetadata
        {
            SteamId64 = "76561198000000001",
            CustomLabel = "Main",
            GroupIds = { "g1", "g2" },
        });

        var reloaded = new AccountMetadataStore(path, atomic);
        var meta = reloaded.Get("76561198000000001");

        Assert.Equal("Main", meta.CustomLabel);
        Assert.Equal(new[] { "g1", "g2" }, meta.GroupIds);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~AccountMetadataStoreTests"`
Expected: FAIL — `JsonFile` / `AccountMetadataStore` do not exist.

- [ ] **Step 3: Write the JSON helper**

Create `src/SteamAccountManager.Core/Storage/JsonFile.cs`:
```csharp
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using SteamAccountManager.Core.System;

namespace SteamAccountManager.Core.Storage;

internal static class JsonFile
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static T Load<T>(string path, Func<T> defaultFactory)
    {
        if (!File.Exists(path))
        {
            return defaultFactory();
        }

        var json = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(json))
        {
            return defaultFactory();
        }

        return JsonSerializer.Deserialize<T>(json, Options) ?? defaultFactory();
    }

    public static void Save<T>(string path, T value, IAtomicFile atomicFile)
    {
        var json = JsonSerializer.Serialize(value, Options);
        var bytes = Encoding.UTF8.GetBytes(json); // GetBytes never emits a BOM
        atomicFile.Write(path, stream => stream.Write(bytes, 0, bytes.Length));
    }
}
```

- [ ] **Step 4: Write the metadata store**

Create `src/SteamAccountManager.Core/Storage/AccountMetadataStore.cs`:
```csharp
using System.Collections.Generic;
using SteamAccountManager.Core.Models;
using SteamAccountManager.Core.System;

namespace SteamAccountManager.Core.Storage;

public interface IAccountMetadataStore
{
    AccountMetadata Get(string steamId64);
    void Upsert(AccountMetadata metadata);
    IReadOnlyDictionary<string, AccountMetadata> GetAll();
}

public sealed class AccountMetadataStore : IAccountMetadataStore
{
    private readonly string _path;
    private readonly IAtomicFile _atomicFile;
    private readonly Dictionary<string, AccountMetadata> _data;

    public AccountMetadataStore(string path, IAtomicFile atomicFile)
    {
        _path = path;
        _atomicFile = atomicFile;
        _data = JsonFile.Load(path, () => new Dictionary<string, AccountMetadata>());
    }

    public AccountMetadata Get(string steamId64)
        => _data.TryGetValue(steamId64, out var meta)
            ? meta
            : new AccountMetadata { SteamId64 = steamId64 };

    public void Upsert(AccountMetadata metadata)
    {
        _data[metadata.SteamId64] = metadata;
        JsonFile.Save(_path, _data, _atomicFile);
    }

    public IReadOnlyDictionary<string, AccountMetadata> GetAll() => _data;
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~AccountMetadataStoreTests"`
Expected: PASS (2 tests).

- [ ] **Step 6: Commit**

```bash
git add src/ tests/
git commit -m "Core/Metadata: Add JSON helper and account metadata store" -m "Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 13: Group store

**Files:**
- Create: `src/SteamAccountManager.Core/Storage/GroupStore.cs`
- Test: `tests/SteamAccountManager.Core.Tests/Storage/GroupStoreTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/SteamAccountManager.Core.Tests/Storage/GroupStoreTests.cs`:
```csharp
using System.Linq;
using SteamAccountManager.Core.Storage;
using SteamAccountManager.Core.System;
using Xunit;

namespace SteamAccountManager.Core.Tests.Storage;

public class GroupStoreTests
{
    [Fact]
    public void Add_AssignsIdAndIncrementingSortOrder_AndPersists()
    {
        using var tmp = new TestPaths();
        var path = tmp.File("groups.json");
        var atomic = new AtomicFile();

        var store = new GroupStore(path, atomic);
        var main = store.Add("Main");
        var smurfs = store.Add("Smurfs");

        Assert.False(string.IsNullOrWhiteSpace(main.Id));
        Assert.NotEqual(main.Id, smurfs.Id);
        Assert.Equal(0, main.SortOrder);
        Assert.Equal(1, smurfs.SortOrder);

        var reloaded = new GroupStore(path, atomic);
        Assert.Equal(2, reloaded.GetAll().Count);
    }

    [Fact]
    public void Rename_ChangesName()
    {
        using var tmp = new TestPaths();
        var store = new GroupStore(tmp.File("groups.json"), new AtomicFile());
        var g = store.Add("Old");

        store.Rename(g.Id, "New");

        Assert.Equal("New", store.GetAll().Single().Name);
    }

    [Fact]
    public void Delete_RemovesGroup()
    {
        using var tmp = new TestPaths();
        var store = new GroupStore(tmp.File("groups.json"), new AtomicFile());
        var g = store.Add("Temp");

        store.Delete(g.Id);

        Assert.Empty(store.GetAll());
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~GroupStoreTests"`
Expected: FAIL — `GroupStore` does not exist.

- [ ] **Step 3: Write the implementation**

Create `src/SteamAccountManager.Core/Storage/GroupStore.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using SteamAccountManager.Core.Models;
using SteamAccountManager.Core.System;

namespace SteamAccountManager.Core.Storage;

public interface IGroupStore
{
    IReadOnlyList<Group> GetAll();
    Group Add(string name);
    void Rename(string id, string newName);
    void Delete(string id);
}

public sealed class GroupStore : IGroupStore
{
    private readonly string _path;
    private readonly IAtomicFile _atomicFile;
    private readonly List<Group> _groups;

    public GroupStore(string path, IAtomicFile atomicFile)
    {
        _path = path;
        _atomicFile = atomicFile;
        _groups = JsonFile.Load(path, () => new List<Group>());
    }

    public IReadOnlyList<Group> GetAll() => _groups;

    public Group Add(string name)
    {
        var group = new Group
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name,
            SortOrder = _groups.Count == 0 ? 0 : _groups.Max(g => g.SortOrder) + 1,
        };

        _groups.Add(group);
        Save();
        return group;
    }

    public void Rename(string id, string newName)
    {
        var group = _groups.FirstOrDefault(g => g.Id == id);
        if (group is null)
        {
            return;
        }

        group.Name = newName;
        Save();
    }

    public void Delete(string id)
    {
        if (_groups.RemoveAll(g => g.Id == id) > 0)
        {
            Save();
        }
    }

    private void Save() => JsonFile.Save(_path, _groups, _atomicFile);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~GroupStoreTests"`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/ tests/
git commit -m "Core/GroupStore: Add CRUD for account categories" -m "Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 14: Settings store

**Files:**
- Create: `src/SteamAccountManager.Core/Storage/SettingsStore.cs`
- Test: `tests/SteamAccountManager.Core.Tests/Storage/SettingsStoreTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/SteamAccountManager.Core.Tests/Storage/SettingsStoreTests.cs`:
```csharp
using SteamAccountManager.Core.Models;
using SteamAccountManager.Core.Storage;
using SteamAccountManager.Core.System;
using Xunit;

namespace SteamAccountManager.Core.Tests.Storage;

public class SettingsStoreTests
{
    [Fact]
    public void Load_ReturnsDefaults_WhenFileMissing()
    {
        using var tmp = new TestPaths();
        var sut = new SettingsStore(tmp.File("settings.json"), new AtomicFile());

        var settings = sut.Load();

        Assert.False(settings.AutostartEnabled);
        Assert.False(settings.StartMinimized);
    }

    [Fact]
    public void Save_ThenLoad_RoundTrips()
    {
        using var tmp = new TestPaths();
        var path = tmp.File("settings.json");
        var atomic = new AtomicFile();

        new SettingsStore(path, atomic).Save(new AppSettings
        {
            AutostartEnabled = true,
            StartMinimized = true,
        });

        var settings = new SettingsStore(path, atomic).Load();

        Assert.True(settings.AutostartEnabled);
        Assert.True(settings.StartMinimized);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~SettingsStoreTests"`
Expected: FAIL — `SettingsStore` does not exist.

- [ ] **Step 3: Write the implementation**

Create `src/SteamAccountManager.Core/Storage/SettingsStore.cs`:
```csharp
using SteamAccountManager.Core.Models;
using SteamAccountManager.Core.System;

namespace SteamAccountManager.Core.Storage;

public interface ISettingsStore
{
    AppSettings Load();
    void Save(AppSettings settings);
}

public sealed class SettingsStore : ISettingsStore
{
    private readonly string _path;
    private readonly IAtomicFile _atomicFile;

    public SettingsStore(string path, IAtomicFile atomicFile)
    {
        _path = path;
        _atomicFile = atomicFile;
    }

    public AppSettings Load() => JsonFile.Load(_path, () => new AppSettings());

    public void Save(AppSettings settings) => JsonFile.Save(_path, settings, _atomicFile);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~SettingsStoreTests"`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/ tests/
git commit -m "Core/SettingsStore: Persist app settings as JSON" -m "Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 15: Autostart service

**Files:**
- Create: `src/SteamAccountManager.Core/System/AutostartService.cs`
- Test: `tests/SteamAccountManager.Core.Tests/System/AutostartServiceTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/SteamAccountManager.Core.Tests/System/AutostartServiceTests.cs`:
```csharp
using SteamAccountManager.Core.System;
using SteamAccountManager.Core.Tests.Fakes;
using Xunit;

namespace SteamAccountManager.Core.Tests.System;

public class AutostartServiceTests
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    [Fact]
    public void Enable_WritesQuotedExePathWithMinimizedFlag()
    {
        var reg = new FakeWindowsRegistry();
        var sut = new AutostartService(reg);

        sut.Enable(@"C:\Apps\SteamAccountManager.exe");

        Assert.True(sut.IsEnabled());
        Assert.Equal(
            "\"C:\\Apps\\SteamAccountManager.exe\" --minimized",
            reg.GetString(RegistryHiveSelector.CurrentUser, RunKey, "SteamAccountManager"));
    }

    [Fact]
    public void Disable_RemovesValue()
    {
        var reg = new FakeWindowsRegistry();
        var sut = new AutostartService(reg);
        sut.Enable(@"C:\Apps\SteamAccountManager.exe");

        sut.Disable();

        Assert.False(sut.IsEnabled());
    }

    [Fact]
    public void IsEnabled_False_WhenNothingWritten()
    {
        Assert.False(new AutostartService(new FakeWindowsRegistry()).IsEnabled());
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~AutostartServiceTests"`
Expected: FAIL — `AutostartService` does not exist.

- [ ] **Step 3: Write the implementation**

Create `src/SteamAccountManager.Core/System/AutostartService.cs`:
```csharp
namespace SteamAccountManager.Core.System;

public interface IAutostartService
{
    bool IsEnabled();
    void Enable(string executablePath);
    void Disable();
}

public sealed class AutostartService : IAutostartService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "SteamAccountManager";

    private readonly IWindowsRegistry _registry;

    public AutostartService(IWindowsRegistry registry) => _registry = registry;

    public bool IsEnabled()
        => !string.IsNullOrEmpty(
            _registry.GetString(RegistryHiveSelector.CurrentUser, RunKey, ValueName));

    public void Enable(string executablePath)
        => _registry.SetString(
            RegistryHiveSelector.CurrentUser, RunKey, ValueName, $"\"{executablePath}\" --minimized");

    public void Disable()
        => _registry.DeleteValue(RegistryHiveSelector.CurrentUser, RunKey, ValueName);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~AutostartServiceTests"`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/ tests/
git commit -m "Core/Autostart: Toggle Windows run-key autostart entry" -m "Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 16: Avatar service and Steam Community fetcher

**Files:**
- Create: `src/SteamAccountManager.Core/Avatars/AvatarService.cs`
- Create: `src/SteamAccountManager.Core/Avatars/SteamCommunityAvatarFetcher.cs`
- Create: `tests/SteamAccountManager.Core.Tests/Fakes/FakeAvatarFetcher.cs`
- Test: `tests/SteamAccountManager.Core.Tests/Avatars/AvatarServiceTests.cs`
- Test: `tests/SteamAccountManager.Core.Tests/Avatars/SteamCommunityAvatarFetcherTests.cs`

- [ ] **Step 1: Write the test fake and failing tests**

Create `tests/SteamAccountManager.Core.Tests/Fakes/FakeAvatarFetcher.cs`:
```csharp
using System.Threading;
using System.Threading.Tasks;
using SteamAccountManager.Core.Avatars;

namespace SteamAccountManager.Core.Tests.Fakes;

public sealed class FakeAvatarFetcher : IAvatarFetcher
{
    private readonly byte[]? _bytes;
    public int CallCount { get; private set; }

    public FakeAvatarFetcher(byte[]? bytes) => _bytes = bytes;

    public Task<byte[]?> FetchAsync(string steamId64, CancellationToken ct = default)
    {
        CallCount++;
        return Task.FromResult(_bytes);
    }
}
```

Create `tests/SteamAccountManager.Core.Tests/Avatars/AvatarServiceTests.cs`:
```csharp
using System.IO;
using System.Threading.Tasks;
using SteamAccountManager.Core.Avatars;
using SteamAccountManager.Core.Tests.Fakes;
using Xunit;

namespace SteamAccountManager.Core.Tests.Avatars;

public class AvatarServiceTests
{
    [Fact]
    public async Task GetAvatarAsync_DownloadsCachesAndReturnsPath()
    {
        using var tmp = new TestPaths();
        var fetcher = new FakeAvatarFetcher(new byte[] { 1, 2, 3 });
        var sut = new AvatarService(tmp.File("avatars"), fetcher);

        var path = await sut.GetAvatarAsync("76561198000000001");

        Assert.NotNull(path);
        Assert.True(File.Exists(path));
        Assert.Equal(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(path!));
    }

    [Fact]
    public async Task GetAvatarAsync_UsesCache_OnSecondCall()
    {
        using var tmp = new TestPaths();
        var fetcher = new FakeAvatarFetcher(new byte[] { 9 });
        var sut = new AvatarService(tmp.File("avatars"), fetcher);

        await sut.GetAvatarAsync("76561198000000001");
        await sut.GetAvatarAsync("76561198000000001");

        Assert.Equal(1, fetcher.CallCount); // second call served from disk cache
    }

    [Fact]
    public async Task GetAvatarAsync_ReturnsNull_WhenFetchFails()
    {
        using var tmp = new TestPaths();
        var sut = new AvatarService(tmp.File("avatars"), new FakeAvatarFetcher(null));

        Assert.Null(await sut.GetAvatarAsync("76561198000000001"));
    }
}
```

Create `tests/SteamAccountManager.Core.Tests/Avatars/SteamCommunityAvatarFetcherTests.cs`:
```csharp
using SteamAccountManager.Core.Avatars;
using Xunit;

namespace SteamAccountManager.Core.Tests.Avatars;

public class SteamCommunityAvatarFetcherTests
{
    [Fact]
    public void ParseAvatarUrl_ExtractsAvatarFull()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <profile>
              <steamID64>76561198000000001</steamID64>
              <avatarFull><![CDATA[https://avatars.steamstatic.com/abc_full.jpg]]></avatarFull>
            </profile>
            """;

        Assert.Equal(
            "https://avatars.steamstatic.com/abc_full.jpg",
            SteamCommunityAvatarFetcher.ParseAvatarUrl(xml));
    }

    [Fact]
    public void ParseAvatarUrl_ReturnsNull_WhenAbsent()
    {
        Assert.Null(SteamCommunityAvatarFetcher.ParseAvatarUrl("<profile></profile>"));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~Avatars"`
Expected: FAIL — `AvatarService` / `IAvatarFetcher` / `SteamCommunityAvatarFetcher` do not exist.

- [ ] **Step 3: Write the avatar service**

Create `src/SteamAccountManager.Core/Avatars/AvatarService.cs`:
```csharp
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SteamAccountManager.Core.Avatars;

public interface IAvatarFetcher
{
    /// <summary>Returns avatar image bytes for a SteamID64, or null if unavailable.</summary>
    Task<byte[]?> FetchAsync(string steamId64, CancellationToken ct = default);
}

public interface IAvatarService
{
    /// <summary>Returns a local cached avatar file path, or null if none could be obtained.</summary>
    Task<string?> GetAvatarAsync(string steamId64, CancellationToken ct = default);
}

public sealed class AvatarService : IAvatarService
{
    private readonly string _cacheDirectory;
    private readonly IAvatarFetcher _fetcher;

    public AvatarService(string cacheDirectory, IAvatarFetcher fetcher)
    {
        _cacheDirectory = cacheDirectory;
        _fetcher = fetcher;
    }

    private string CachePath(string steamId64) => Path.Combine(_cacheDirectory, steamId64 + ".jpg");

    public async Task<string?> GetAvatarAsync(string steamId64, CancellationToken ct = default)
    {
        var cached = CachePath(steamId64);
        if (File.Exists(cached))
        {
            return cached;
        }

        var bytes = await _fetcher.FetchAsync(steamId64, ct).ConfigureAwait(false);
        if (bytes is null || bytes.Length == 0)
        {
            return null;
        }

        Directory.CreateDirectory(_cacheDirectory);
        await File.WriteAllBytesAsync(cached, bytes, ct).ConfigureAwait(false);
        return cached;
    }
}
```

- [ ] **Step 4: Write the Steam Community fetcher**

Create `src/SteamAccountManager.Core/Avatars/SteamCommunityAvatarFetcher.cs`:
```csharp
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SteamAccountManager.Core.Avatars;

/// <summary>Keyless avatar lookup via the public Steam Community profile XML endpoint.</summary>
public sealed class SteamCommunityAvatarFetcher : IAvatarFetcher
{
    private readonly HttpClient _http;

    public SteamCommunityAvatarFetcher(HttpClient http) => _http = http;

    public async Task<byte[]?> FetchAsync(string steamId64, CancellationToken ct = default)
    {
        try
        {
            var xml = await _http
                .GetStringAsync($"https://steamcommunity.com/profiles/{steamId64}?xml=1", ct)
                .ConfigureAwait(false);

            var url = ParseAvatarUrl(xml);
            if (url is null)
            {
                return null;
            }

            return await _http.GetByteArrayAsync(url, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            return null; // offline / private / transient — caller falls back to a default avatar
        }
    }

    internal static string? ParseAvatarUrl(string xml)
    {
        try
        {
            var url = XDocument.Parse(xml).Root?.Element("avatarFull")?.Value?.Trim();
            return string.IsNullOrWhiteSpace(url) ? null : url;
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~Avatars"`
Expected: PASS (5 tests).

- [ ] **Step 6: Commit**

```bash
git add src/ tests/
git commit -m "Core/Avatars: Add avatar cache service and keyless community fetcher" -m "Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 17: Full Core verification gate

**Files:** none (verification only).

- [ ] **Step 1: Run the full suite**

Run: `dotnet test`
Expected: PASS — all tests green (≈ 35+ across the suite). No skipped or failed tests.

- [ ] **Step 2: Confirm a clean build with warnings surfaced**

Run: `dotnet build -warnaserror`
Expected: build succeeds with no warnings. If nullable/analyzer warnings appear, fix them, then re-run `dotnet test`.

- [ ] **Step 3: Commit any warning fixes (if needed)**

```bash
git add -A
git commit -m "Core: Resolve build warnings and finalize Core engine" -m "Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```
(If there were no changes, skip this commit.)

---

## Self-Review

**Spec coverage** (against `2026-06-14-steam-account-manager-design.md`):
- Account list storage / parse → Tasks 4. ✅
- Steam path discovery → Task 7. ✅
- Registry selectors / auto-login / clear → Task 8. ✅
- Graceful shutdown + relaunch → Task 9. ✅
- Backups + atomic writes + validate re-parse → Tasks 2, 5, 10. ✅
- Switch sequence orchestration + rollback → Task 11. ✅
- Add-account flow (clear auto-login + launch) → Task 11 (`BeginAddAccount`). The `loginusers.vdf` watch/diff is a UI concern handled in the App plan. ✅
- Custom labels/notes + group membership → Task 12. ✅
- Groups CRUD → Task 13. ✅
- Settings (autostart, start-minimized) → Task 14. ✅
- Autostart run-key → Task 15. ✅
- Avatars (keyless community XML + local cache + fallback) → Task 16. ✅
- Never touch tokens/credentials → honored throughout (no code reads `local.vdf` or ssfn). ✅
- **Deferred to App plan (by design):** tray menu, window/close-to-tray, single-instance, start-minimized wiring, group-deletion cleanup of membership in metadata, DI registration of concrete services, `HttpClient` lifetime. Noted here so they are not lost.

**Placeholder scan:** Task 4 intentionally ships `SetActiveAccount` as `throw new NotImplementedException()` and Task 5 replaces it the very next task with the real body + its own failing test first — this is a deliberate TDD seam, not a left-behind placeholder. No other placeholders.

**Type consistency:** Interfaces and signatures referenced across tasks are consistent: `IAtomicFile.Write(string, Action<Stream>)`, `ILoginUsersStore.{Read, SetActiveAccount}`, `IWindowsRegistry`/`RegistryHiveSelector`, `ISteamLocator.Locate(): SteamPaths?`, `ISteamRegistry` members, `ISteamProcessController.{IsSteamRunning, ShutdownAndWait, Launch}`, `IBackupService.{Backup, Restore, HasBackup}`, `IAccountSwitcher.{SwitchTo, BeginAddAccount}`, `IAvatarFetcher.FetchAsync`, `IAvatarService.GetAvatarAsync`. The `AccountSwitcher` constructor parameter order matches the `BuildHarness` call in its test.
