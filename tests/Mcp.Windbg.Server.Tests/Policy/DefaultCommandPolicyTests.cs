using Mcp.Windbg.Server.Policy;
using Xunit;

namespace Mcp.Windbg.Server.Tests.Policy;

public class DefaultCommandPolicyTests
{
    [Fact]
    public void DefaultPolicy_AllowsBasicAnalysisCommands()
    {
        // Arrange
        var policy = DefaultCommandPolicy.CreateDefault();

        // Act & Assert
        Assert.True(policy.ValidateCommand("!analyze -v").IsAllowed);
        Assert.True(policy.ValidateCommand("k").IsAllowed);
        Assert.True(policy.ValidateCommand("kb").IsAllowed);
        Assert.True(policy.ValidateCommand("lm").IsAllowed);
        Assert.True(policy.ValidateCommand("~").IsAllowed);
        Assert.True(policy.ValidateCommand("r").IsAllowed);
        Assert.True(policy.ValidateCommand(".lastevent").IsAllowed);
    }

    [Fact]
    public void DefaultPolicy_DeniesShellCommands()
    {
        // Arrange
        var policy = DefaultCommandPolicy.CreateDefault();

        // Act & Assert
        Assert.False(policy.ValidateCommand(".shell cmd").IsAllowed);
        Assert.False(policy.ValidateCommand(".cmd dir").IsAllowed);
        Assert.False(policy.ValidateCommand("bp 0x12345").IsAllowed);
        Assert.False(policy.ValidateCommand("g").IsAllowed);
    }

    [Fact]
    public void DefaultPolicy_AllowsPatternMatchedCommands()
    {
        // Arrange
        var policy = DefaultCommandPolicy.CreateDefault();

        // Act & Assert
        Assert.True(policy.ValidateCommand("k 10").IsAllowed);
        Assert.True(policy.ValidateCommand("~3k").IsAllowed);
        Assert.True(policy.ValidateCommand("lmv notepad").IsAllowed);
        Assert.True(policy.ValidateCommand(".echo hello world").IsAllowed);
    }

    [Fact]
    public void DefaultPolicy_DeniesUnknownCommands()
    {
        // Arrange  
        var policy = DefaultCommandPolicy.CreateDefault();

        // Act & Assert
        var result = policy.ValidateCommand("unknown_command");
        Assert.False(result.IsAllowed);
        Assert.Equal("NotInAllowlist", result.PolicyRule);
    }

    [Fact]
    public void DefaultPolicy_DeniesEmptyCommands()
    {
        // Arrange
        var policy = DefaultCommandPolicy.CreateDefault();

        // Act & Assert
        var result = policy.ValidateCommand("");
        Assert.False(result.IsAllowed);
        Assert.Equal("EmptyCommand", result.PolicyRule);
    }
}