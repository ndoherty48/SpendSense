using System.Security.Cryptography;

using SpendSense.Common.Security;
using SpendSense.Tests.TestDoubles;

namespace SpendSense.Tests.Unit;

public class RecoveryCodeTests
{
    [Fact]
    public void A_code_is_26_base32_characters_in_groups_of_four_and_reads_back()
    {
        var code = RandomNumberGenerator.GetBytes(RecoveryCode.Size);

        var text = RecoveryCode.Format(code);

        Assert.Matches("^[A-Z2-7]{4}(-[A-Z2-7]{4}){5}-[A-Z2-7]{2}$", text);
        Assert.True(RecoveryCode.TryParse(text, out var parsed));
        Assert.Equal(code, parsed);
    }

    [Fact]
    public void Case_spaces_dashes_and_lookalike_digits_are_forgiven()
    {
        var code = new byte[RecoveryCode.Size];
        code[0] = 0b0111_0010; // starts with "O", "I"…
        var text = RecoveryCode.Format(code);

        var typed = text.ToLowerInvariant().Replace("-", " ").Replace('o', '0').Replace('i', '1');

        Assert.True(RecoveryCode.TryParse(typed, out var parsed));
        Assert.Equal(code, parsed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ABCD-EFGH")]
    [InlineData("ABCD-EFGH-IJKL-MNOP-QRST-UVWX-YZ-23")]
    [InlineData("ABCD-EFGH-IJKL-MNOP-QRST-UVWX-8!")]
    public void Anything_else_is_not_a_code(string input) => Assert.False(RecoveryCode.TryParse(input, out _));
}

public class BackupKeysTests
{
    static BackupKeys Keys(ISecretStore store) => new(store) { PasswordIterations = 1_000 };

    [Fact]
    public async Task Only_derived_keys_are_stored_never_the_password()
    {
        var store = new InMemorySecretStore();

        var code = await Keys(store).SetPassword("correct horse battery");

        var stored = System.Text.Encoding.UTF8.GetString((await store.Get(BackupKeys.StoreName))!);
        Assert.DoesNotContain("correct horse battery", stored);
        Assert.DoesNotContain(code, stored);
        Assert.True(await Keys(store).IsSetUp());
    }

    [Fact]
    public async Task A_short_password_is_refused()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Keys(new InMemorySecretStore()).SetPassword("short"));
    }

    [Fact]
    public async Task Passwords_are_compared_after_unicode_normalisation()
    {
        var store = new InMemorySecretStore();
        await Keys(store).SetPassword("café au lait please");
        var backupKey = RandomNumberGenerator.GetBytes(32);
        var manifest = await Keys(store).CreateManifest(backupKey, DateTime.UtcNow);

        // The same word typed with a combining accent, as some keyboards produce it.
        var unlocked = await Keys(new InMemorySecretStore()).Unlock(manifest, UnlockMethod.Password, "café au lait please");

        Assert.Equal(backupKey, unlocked);
    }

    [Fact]
    public async Task A_slot_moved_to_another_type_does_not_unwrap()
    {
        var store = new InMemorySecretStore();
        await Keys(store).SetPassword("correct horse battery");
        var manifest = await Keys(store).CreateManifest(RandomNumberGenerator.GetBytes(32), DateTime.UtcNow);

        var password = manifest.KeySlots.Single(s => s.Type == KeySlot.Password);
        var swapped = manifest with { KeySlots = [password with { Type = KeySlot.RecoveryCode }] };

        Assert.Null(await Keys(store).UnlockWithRemembered(swapped));
    }
}

public class DevFallbackSecretStoreTests : IDisposable
{
    readonly string directory = Path.Combine(Path.GetTempPath(), "spendsense-tests", Guid.NewGuid().ToString("N"));

    sealed class BrokenStore : ISecretStore
    {
        public Task<byte[]?> Get(string name) => throw new InvalidOperationException("MissingEntitlement");
        public Task Set(string name, byte[] value) => throw new InvalidOperationException("MissingEntitlement");
        public Task Remove(string name) => throw new InvalidOperationException("MissingEntitlement");
    }

    [Fact]
    public async Task Secure_storage_is_used_while_it_works()
    {
        var primary = new InMemorySecretStore();
        var store = new DevFallbackSecretStore(primary, directory);

        await store.Set("k", [1, 2, 3]);

        Assert.Equal([1, 2, 3], await primary.Get("k"));
        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public async Task When_secure_storage_refuses_secrets_go_to_files()
    {
        var store = new DevFallbackSecretStore(new BrokenStore(), directory);

        await store.Set("k", [4, 5]);

        Assert.Equal([4, 5], await new DevFallbackSecretStore(new BrokenStore(), directory).Get("k"));
        await store.Remove("k");
        Assert.Null(await store.Get("k"));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }
}
