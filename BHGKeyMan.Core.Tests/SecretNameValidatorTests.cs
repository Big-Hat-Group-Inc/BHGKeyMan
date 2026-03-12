namespace BHGKeyMan.Core.Tests;

public class SecretNameValidatorTests
{
    [Theory]
    [InlineData("openai-api-key", true)]
    [InlineData("A1", true)]
    [InlineData("a", true)]
    [InlineData("A-B-C", true)]
    [InlineData("1starts-with-number", false)]
    [InlineData("contains_underscore", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("has space", false)]
    [InlineData("-starts-with-dash", false)]
    public void ValidatesExpectedPatterns(string? input, bool expected)
    {
        var validator = new SecretNameValidator();

        var actual = validator.IsValid(input);

        Assert.Equal(expected, actual);
    }
}
