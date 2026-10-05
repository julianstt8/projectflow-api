using Microsoft.Extensions.Options;
using ProjectFlow.Infrastructure.Authentication;

namespace ProjectFlow.Infrastructure.Tests.Authentication;

public class RefreshTokenServiceTests
{
    private readonly RefreshTokenService _service = new(Options.Create(new JwtOptions { RefreshTokenLifetimeDays = 7 }));

    [Fact]
    public void Generates_unique_url_safe_tokens_with_512_bits()
    {
        var tokens = Enumerable.Range(0, 100).Select(_ => _service.Generate().Value).ToList();

        Assert.Equal(100, tokens.Distinct().Count());
        Assert.All(tokens, token =>
        {
            Assert.Matches("^[A-Za-z0-9_-]+$", token);
            Assert.Equal(86, token.Length); // 64 bytes in base64url without padding
        });
    }

    [Fact]
    public void Hash_is_a_deterministic_sha256_that_differs_from_the_token()
    {
        var generated = _service.Generate();

        Assert.Equal(generated.Hash, _service.Hash(generated.Value));
        Assert.NotEqual(generated.Value, generated.Hash);
        Assert.Matches("^[0-9a-f]{64}$", generated.Hash);
        Assert.Equal("9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08", _service.Hash("test"));
    }

    [Fact]
    public void Lifetime_comes_from_the_configuration()
    {
        Assert.Equal(TimeSpan.FromDays(7), _service.Lifetime);
    }
}
