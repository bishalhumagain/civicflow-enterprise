using Microsoft.Extensions.Configuration;
using PublicServiceCaseManagement.Api.Infrastructure.Identity;

namespace PublicServiceCaseManagement.Tests;

public sealed class SmtpEmailSenderTests
{
    [Fact]
    public void Constructor_RequiresTls()
    {
        var configuration = CreateConfiguration(
            ("Smtp:Host", "smtp.example.test"),
            ("Smtp:Port", "587"),
            ("Smtp:FromAddress", "no-reply@example.test"),
            ("Smtp:UseSsl", "false"));

        var exception = Assert.Throws<InvalidOperationException>(() => new SmtpEmailSender(configuration));

        Assert.Equal(
            "SMTP email delivery requires TLS to be enabled.",
            exception.Message);
    }

    [Fact]
    public void Constructor_RequiresValidTransportAndSenderConfiguration()
    {
        var configuration = CreateConfiguration(
            ("Smtp:Host", "smtp.example.test"),
            ("Smtp:Port", "587"),
            ("Smtp:FromAddress", "no-reply@example.test"),
            ("Smtp:UseSsl", "true"));

        var sender = new SmtpEmailSender(configuration);

        Assert.NotNull(sender);
    }

    [Theory]
    [InlineData("smtp.example.test", "no-reply@example.test", true)]
    [InlineData("", "no-reply@example.test", false)]
    [InlineData("smtp.example.test", "", false)]
    [InlineData(" ", " ", false)]
    public void IsConfigured_RequiresHostAndSenderAddress(
        string host,
        string fromAddress,
        bool expected)
    {
        var configuration = CreateConfiguration(
            ("Smtp:Host", host),
            ("Smtp:FromAddress", fromAddress));

        Assert.Equal(expected, SmtpEmailSender.IsConfigured(configuration));
    }

    [Fact]
    public void Constructor_RejectsIncompleteCredentials()
    {
        var configuration = CreateConfiguration(
            ("Smtp:Host", "smtp.example.test"),
            ("Smtp:Port", "587"),
            ("Smtp:FromAddress", "no-reply@example.test"),
            ("Smtp:UseSsl", "true"),
            ("Smtp:Username", "smtp-user"));

        var exception = Assert.Throws<InvalidOperationException>(() => new SmtpEmailSender(configuration));

        Assert.Equal(
            "SMTP username and password must either both be configured or both be omitted.",
            exception.Message);
    }

    private static IConfiguration CreateConfiguration(
        params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                values.ToDictionary(value => value.Key, value => (string?)value.Value))
            .Build();
}