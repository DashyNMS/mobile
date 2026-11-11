using System.Security.Cryptography;
using System.Text;
using DashyNMS.Mobile.Security;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Intelligence;
using Microsoft.Extensions.Logging;

namespace DashyNMS.Mobile.Intelligence;

/// <summary>Whether saved chats can be kept right now (#222).</summary>
public enum IntelligenceChatStorageState
{
    /// <summary>Not opened since the app started or since the last wipe.</summary>
    Closed,

    /// <summary>The key is in hand: chats are read and written encrypted.</summary>
    Ready,

    /// <summary>
    /// The key couldn't be read or stored, or the folder couldn't be kept out of
    /// backups. Nothing is written - never a plain file instead - and chats
    /// already saved are left as they are for when the key is back.
    /// </summary>
    Unavailable,
}

/// <summary>
/// Saved chats can't be reached: the storage is unavailable, or was wiped after
/// this store was opened. An <see cref="IOException"/>, as Core's chat store
/// treats those as "out of reach for now" rather than damage.
/// </summary>
public sealed class ChatStorageUnavailableException(string message) : IOException(message);

/// <summary>
/// Saved Intelligence chats on the phone (#222): Core's
/// <see cref="FileIntelligenceChatStore"/> - per-chat files, written atomically,
/// a damaged one set aside while the rest load - encrypted with AES-256-GCM under
/// one random key kept in the platform's secure storage, as desktop's keyring
/// protector does on macOS and Linux (desktop #154).
/// </summary>
/// <remarks>
/// <para>
/// The key is only created when secure storage answers that there isn't one. If
/// it can't be read, nothing is created, deleted or written, so files saved under
/// the real key aren't orphaned and nothing plain is left behind; the state says
/// so instead. A key that has gone (a restored phone, a cleared keystore) means
/// the old files can never be read: a new key is made and Core sets those files
/// aside as damaged when they fail to decrypt.
/// </para>
/// <para>
/// Sign-out wipes the folder and the key (<see cref="WipeAsync"/>). Every read
/// and write goes through one lock and carries the generation it was opened in;
/// a wipe moves the generation on under that lock before deleting anything, so a
/// save that arrives late - an answer finishing as the user signs out - is
/// refused rather than writing the old conversation back. Opening and wiping also
/// take turns over the key, so a key being created can't land after the wipe
/// that should have removed it, and two screens opening at once make one key.
/// </para>
/// <para>
/// Nothing from a chat, the key or the server's scope is logged: only which
/// step failed, in fixed words. Exceptions from cryptography or the file system
/// are never logged, as their messages can carry paths or data.
/// </para>
/// <para>
/// History, autosave and the chat list are #229; single-chat privacy mode,
/// which must skip this altogether, is #230. This is only where they keep chats.
/// </para>
/// </remarks>
public sealed class IntelligenceChatStorage
{
    /// <summary>The secure-storage entry holding the key, base64.</summary>
    public const string KeyName = "intelligence-chats-key-v1";

    private const byte Version = 1;
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    /// <summary>Bound into every file's tag, so nothing encrypted for another purpose passes as a chat.</summary>
    private static readonly byte[] Purpose = Encoding.UTF8.GetBytes("DashyNMS.Intelligence.Chats.v1");

    private const string ClosedMessage = "Saved chats were closed or removed after this store was opened.";

    private readonly string _root;
    private readonly ISecureStorage _secure;
    private readonly IBackupExclusion _backup;
    private readonly ILogger<IntelligenceChatStorage> _logger;
    private readonly FileIntelligenceChatStore _files;

    /// <summary>Every read and write of the files, and every change to the key and generation.</summary>
    private readonly object _gate = new();

    /// <summary>Opening and wiping take turns, as both go to secure storage and await it.</summary>
    private readonly SemaphoreSlim _keyTurn = new(1, 1);

    private byte[]? _key;
    private long _generation;
    private IntelligenceChatStorageState _state;
    private string? _unavailableReason;

    /// <param name="root">The app-private <c>chats</c> folder, created when first opened.</param>
    public IntelligenceChatStorage(string root, ISecureStorage secure, IBackupExclusion backup, ILogger<IntelligenceChatStorage> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        _root = root;
        _secure = secure;
        _backup = backup;
        _logger = logger;
        _files = new FileIntelligenceChatStore(root, Protect, Unprotect);
    }

    /// <summary>Raised when <see cref="State"/> changes, on whichever thread changed it.</summary>
    public event EventHandler? StateChanged;

    public IntelligenceChatStorageState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    /// <summary>Why chats aren't being kept, in words for the user, while <see cref="State"/> is <see cref="IntelligenceChatStorageState.Unavailable"/>.</summary>
    public string? UnavailableReason
    {
        get
        {
            lock (_gate)
            {
                return _unavailableReason;
            }
        }
    }

    /// <summary>
    /// The store for this session: reads the key from secure storage the first
    /// time (making one if there's none), and prepares the folder. Null when
    /// chats can't be kept - see <see cref="UnavailableReason"/> - or when a wipe
    /// came while opening. Trying again later retries an unavailable key.
    /// </summary>
    /// <remarks>
    /// The store's calls block on file I/O: run them off the UI thread. After a
    /// wipe they throw <see cref="ChatStorageUnavailableException"/> and write nothing.
    /// </remarks>
    public async Task<IIntelligenceChatStore?> OpenAsync(CancellationToken cancellationToken = default)
    {
        await _keyTurn.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            long generation;
            lock (_gate)
            {
                if (_key is not null)
                {
                    return new Session(this, _generation);
                }

                generation = _generation;
            }

            if (!AesGcm.IsSupported)
            {
                return Fail(generation, Cause.Unsupported);
            }

            string? stored;
            try
            {
                stored = await _secure.GetAsync(KeyName).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Locked, declined or broken: not the same as "there's none", so
                // don't make one over it - files saved under it would be lost.
                return Fail(generation, Cause.KeyUnreadable);
            }

            byte[]? key;
            if (string.IsNullOrEmpty(stored))
            {
                key = await CreateKeyAsync().ConfigureAwait(false);
                if (key is null)
                {
                    return Fail(generation, Cause.KeyUnwritable);
                }
            }
            else if ((key = Decode(stored)) is null)
            {
                // Left in place: the user can clear it by signing out, rather
                // than this quietly replacing it.
                return Fail(generation, Cause.KeyDamaged);
            }

            bool prepared;
            var changed = false;
            lock (_gate)
            {
                if (_generation != generation)
                {
                    // Wiped while the key was being read. The wipe is waiting for
                    // this turn and removes whatever key was just stored.
                    Array.Clear(key);
                    return null;
                }

                prepared = PrepareFolder();
                if (prepared)
                {
                    _key = key;
                    changed = SetState(IntelligenceChatStorageState.Ready, null);
                }
                else
                {
                    Array.Clear(key);
                }
            }

            if (!prepared)
            {
                return Fail(generation, Cause.FolderUnavailable);
            }

            if (changed)
            {
                StateChanged?.Invoke(this, EventArgs.Empty);
            }

            return new Session(this, generation);
        }
        finally
        {
            _keyTurn.Release();
        }
    }

    /// <summary>
    /// Sign-out (#222, #139): stops every store opened so far, removes the key,
    /// then the <c>chats</c> folder. Without the key, anything left can't be
    /// read. Returns false if either couldn't be removed.
    /// </summary>
    public async Task<bool> WipeAsync()
    {
        bool changed;
        lock (_gate)
        {
            // First, before awaiting anything: from here no save is accepted.
            _generation++;
            if (_key is not null)
            {
                Array.Clear(_key);
                _key = null;
            }

            changed = SetState(IntelligenceChatStorageState.Closed, null);
        }

        if (changed)
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        await _keyTurn.WaitAsync().ConfigureAwait(false);
        try
        {
            var removed = true;
            try
            {
                _secure.Remove(KeyName);
            }
            catch (Exception)
            {
                removed = false;
            }

            lock (_gate)
            {
                try
                {
                    if (Directory.Exists(_root))
                    {
                        Directory.Delete(_root, recursive: true);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    removed = false;
                }
            }

            if (removed)
            {
                _logger.LogInformation("Removed saved chats and their key");
            }
            else
            {
                _logger.LogWarning("Saved chats or their key could not all be removed");
            }

            return removed;
        }
        finally
        {
            _keyTurn.Release();
        }
    }

    private async Task<byte[]?> CreateKeyAsync()
    {
        var key = RandomNumberGenerator.GetBytes(KeySize);
        var encoded = Convert.ToBase64String(key);
        try
        {
            await _secure.SetAsync(KeyName, encoded).ConfigureAwait(false);

            // Read back: proves it was kept, and if something else stored a key
            // at the same moment, both end up using the one that stayed.
            var kept = await _secure.GetAsync(KeyName).ConfigureAwait(false);
            if (kept == encoded)
            {
                _logger.LogInformation("Created the saved-chats key");
                return key;
            }

            Array.Clear(key);
            return string.IsNullOrEmpty(kept) ? null : Decode(kept);
        }
        catch (Exception)
        {
            Array.Clear(key);
            return null;
        }
    }

    private static byte[]? Decode(string stored)
    {
        var key = new byte[KeySize];
        if (Convert.TryFromBase64String(stored, key, out var written) && written == KeySize)
        {
            return key;
        }

        Array.Clear(key);
        return null;
    }

    /// <summary>
    /// Creates the folder and keeps it out of backups - every time it's opened,
    /// as a wipe deletes the folder and its mark with it. Also clears a write
    /// cut short (Core writes <c>.tmp</c> then renames), which is never loaded
    /// but would otherwise stay. Called holding <see cref="_gate"/>, so no write
    /// is in progress.
    /// </summary>
    private bool PrepareFolder()
    {
        try
        {
            Directory.CreateDirectory(_root);
            _backup.Exclude(_root);
        }
        catch (Exception)
        {
            return false;
        }

        try
        {
            foreach (var partial in Directory.EnumerateFiles(_root, "*.tmp", SearchOption.AllDirectories).ToList())
            {
                File.Delete(partial);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Encrypted, and the next save of that chat replaces it.
        }

        return true;
    }

    private IIntelligenceChatStore? Fail(long generation, Cause cause)
    {
        bool changed;
        lock (_gate)
        {
            if (_generation != generation)
            {
                return null;
            }

            changed = SetState(IntelligenceChatStorageState.Unavailable, Describe(cause));
        }

        if (changed)
        {
            _logger.LogWarning("Saved chats are unavailable ({Cause}); nothing is being saved", cause.ToString());
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        return null;
    }

    // Callers hold _gate.
    private bool SetState(IntelligenceChatStorageState state, string? reason)
    {
        if (_state == state && _unavailableReason == reason)
        {
            return false;
        }

        _state = state;
        _unavailableReason = reason;
        return true;
    }

    private static string Describe(Cause cause) => cause switch
    {
        Cause.Unsupported => "This phone can't encrypt saved chats, so none are kept.",
        Cause.KeyUnreadable => "Saved chats are unavailable: the phone's secure storage couldn't be read. Nothing is saved until it can be, and chats already saved are left as they are.",
        Cause.KeyUnwritable => "Saved chats are unavailable: their key couldn't be stored in the phone's secure storage, so nothing is saved.",
        Cause.KeyDamaged => "Saved chats are unavailable: their key in the phone's secure storage is damaged. Signing out clears it.",
        _ => "Saved chats are unavailable: their folder couldn't be prepared and kept out of backups, so nothing is saved.",
    };

    private T Run<T>(long generation, Func<FileIntelligenceChatStore, T> work)
    {
        lock (_gate)
        {
            if (generation != _generation || _key is null)
            {
                throw new ChatStorageUnavailableException(ClosedMessage);
            }

            return work(_files);
        }
    }

    // Protect and Unprotect run inside Run, holding _gate.
    private byte[] Protect(byte[] plain)
    {
        var key = _key ?? throw new ChatStorageUnavailableException(ClosedMessage);
        var cipher = new byte[1 + NonceSize + TagSize + plain.Length];
        cipher[0] = Version;
        var nonce = cipher.AsSpan(1, NonceSize);
        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plain, cipher.AsSpan(1 + NonceSize + TagSize), cipher.AsSpan(1 + NonceSize, TagSize), Purpose);
        return cipher;
    }

    private byte[] Unprotect(byte[] cipher)
    {
        var key = _key ?? throw new ChatStorageUnavailableException(ClosedMessage);

        // Damage, not "out of reach": Core sets the file aside.
        if (cipher.Length < 1 + NonceSize + TagSize || cipher[0] != Version)
        {
            throw new CryptographicException("Not a saved chat.");
        }

        var plain = new byte[cipher.Length - 1 - NonceSize - TagSize];
        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(cipher.AsSpan(1, NonceSize), cipher.AsSpan(1 + NonceSize + TagSize), cipher.AsSpan(1 + NonceSize, TagSize), plain, Purpose);
        return plain;
    }

    private enum Cause
    {
        Unsupported,
        KeyUnreadable,
        KeyUnwritable,
        KeyDamaged,
        FolderUnavailable,
    }

    /// <summary>One opening's view of the store; refuses everything once the storage is wiped.</summary>
    private sealed class Session(IntelligenceChatStorage owner, long generation) : IIntelligenceChatStore
    {
        public SavedChatCollection Load(string scope) => owner.Run(generation, files => files.Load(scope));

        public void Save(string scope, SavedIntelligenceChat chat, bool makeActive = true) =>
            owner.Run(generation, files =>
            {
                files.Save(scope, chat, makeActive);
                return true;
            });

        public void Delete(string scope, Guid id) =>
            owner.Run(generation, files =>
            {
                files.Delete(scope, id);
                return true;
            });
    }
}
