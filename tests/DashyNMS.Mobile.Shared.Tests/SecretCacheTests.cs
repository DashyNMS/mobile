using DashyNMS.Mobile.Security;
using Microsoft.Extensions.Logging.Abstractions;

namespace DashyNMS.Mobile.Tests;

public sealed class SecretCacheTests
{
    private readonly InMemoryStorage _storage = new();
    private readonly SecretCache _cache;

    public SecretCacheTests() => _cache = new SecretCache(_storage, NullLogger<SecretCache>.Instance);

    [Fact]
    public async Task Load_reads_saved_values_into_memory()
    {
        _storage.Values["a"] = "secret";

        await _cache.LoadAsync(["a", "missing"]);

        Assert.Equal("secret", _cache.Get("a"));
        Assert.Null(_cache.Get("missing"));
    }

    [Fact]
    public async Task Ensure_loaded_reads_the_store_only_once()
    {
        _storage.Values["a"] = "first";
        await _cache.EnsureLoadedAsync(["a"]);

        _storage.Values["a"] = "changed behind our back";
        await _cache.EnsureLoadedAsync(["a"]);

        Assert.Equal("first", _cache.Get("a"));
    }

    [Fact]
    public async Task A_secret_that_cannot_be_read_is_discarded()
    {
        _storage.Values["a"] = "secret";
        _storage.FailReads = true;

        await _cache.LoadAsync(["a"]);

        Assert.Null(_cache.Get("a"));
        Assert.False(_storage.Values.ContainsKey("a"));
    }

    [Fact]
    public async Task Writes_show_immediately_and_reach_storage_in_order()
    {
        _cache.Set("a", "one");
        Assert.Equal("one", _cache.Get("a"));

        _cache.Remove("a");
        _cache.Set("a", "two");
        _cache.Remove("a");
        Assert.Null(_cache.Get("a"));

        await _cache.FlushAsync();

        Assert.False(_storage.Values.ContainsKey("a"));
        Assert.Equal(["set a", "remove a", "set a", "remove a"], _storage.Log);
    }

    [Fact]
    public async Task A_failed_write_does_not_stop_later_ones()
    {
        _storage.FailNextWrite = true;

        _cache.Set("a", "one");
        _cache.Set("b", "two");
        await _cache.FlushAsync();

        Assert.Equal("two", _storage.Values["b"]);
    }

    [Fact]
    public async Task Token_protector_round_trips_through_the_cache()
    {
        var tokens = new SecureTokenProtector(_cache);

        Assert.False(tokens.HasStoredToken);
        tokens.Save("abc123");
        Assert.True(tokens.HasStoredToken);
        Assert.Equal("abc123", tokens.Load());

        await _cache.FlushAsync();
        Assert.Equal("abc123", _storage.Values[SecureTokenProtector.Key]);

        tokens.Clear();
        Assert.Null(tokens.Load());
    }

    private sealed class InMemoryStorage : ISecureStorage
    {
        public Dictionary<string, string> Values { get; } = new();

        public List<string> Log { get; } = new();

        public bool FailReads { get; set; }

        public bool FailNextWrite { get; set; }

        public Task<string?> GetAsync(string key) => FailReads
            ? throw new InvalidOperationException("keystore entry missing")
            : Task.FromResult(Values.TryGetValue(key, out var v) ? v : null);

        public async Task SetAsync(string key, string value)
        {
            // Yield so a broken queue would let a later remove overtake this set.
            await Task.Delay(5);
            if (FailNextWrite)
            {
                FailNextWrite = false;
                throw new IOException("disk full");
            }

            Log.Add("set " + key);
            Values[key] = value;
        }

        public bool Remove(string key)
        {
            Log.Add("remove " + key);
            return Values.Remove(key);
        }
    }
}
