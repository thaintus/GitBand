using System.Security.Cryptography;
using System.Text;
using GitBinder.Application.Security;
using GitBinder.Platform.Windows;

namespace GitBinder.Tests;

public sealed class WindowsDpapiSecretStoreTests
{
    [Theory]
    [InlineData("https")]
    [InlineData("ssh")]
    public async Task Set_NewAccount_CreatesNestedDirectoryAndSupportsOverwriteAndDelete(string kind)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // 只在本测试创建的临时目录中验证，绝不访问真实用户的凭据目录。
        var temporary = Directory.CreateTempSubdirectory("gitbinder-dpapi-");
        try
        {
            var storage = Path.Combine(temporary.FullName, "secrets");
            var store = new WindowsDpapiSecretStore(storage);
            var accountId = Guid.NewGuid();
            var key = SecretService.BuildSecretId(accountId, kind);
            var path = Path.Combine(storage, "gitbinder", "account", accountId.ToString(), kind + ".bin");
            const string firstValue = "fictional-dpapi-test-value";

            Assert.False(Directory.Exists(storage));
            Assert.Null(await store.GetAsync(key));
            await store.SetAsync(key, firstValue);

            Assert.True(File.Exists(path));
            Assert.Equal(firstValue, await store.GetAsync(key));
            Assert.DoesNotContain(firstValue, Encoding.UTF8.GetString(await File.ReadAllBytesAsync(path)));

            await store.SetAsync(key, "replacement-test-value");
            Assert.Equal("replacement-test-value", await store.GetAsync(key));
            await store.DeleteAsync(key);
            Assert.Null(await store.GetAsync(key));
            Assert.False(File.Exists(path));
            await store.DeleteAsync(key);
        }
        finally
        {
            temporary.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Get_ExistingCiphertext_PreservesPreviousPathAndEncryptionFormat()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var temporary = Directory.CreateTempSubdirectory("gitbinder-dpapi-legacy-");
        try
        {
            var accountId = Guid.NewGuid();
            var key = SecretService.BuildSecretId(accountId, "https");
            var parent = Path.Combine(temporary.FullName, "gitbinder", "account", accountId.ToString());
            Directory.CreateDirectory(parent);
            var ciphertext = ProtectedData.Protect(
                Encoding.UTF8.GetBytes("legacy-test-value"), null, DataProtectionScope.CurrentUser);
            await File.WriteAllBytesAsync(Path.Combine(parent, "https.bin"), ciphertext);

            var store = new WindowsDpapiSecretStore(temporary.FullName);

            Assert.Equal("legacy-test-value", await store.GetAsync(key));
        }
        finally
        {
            temporary.Delete(recursive: true);
        }
    }
}
