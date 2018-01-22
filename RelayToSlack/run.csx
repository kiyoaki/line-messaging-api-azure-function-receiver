#r "System.Runtime.Serialization"

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Runtime.Serialization;
using Utf8Json;

static readonly string ChannelSecret = Environment.GetEnvironmentVariable("ChannelSecret", EnvironmentVariableTarget.Process);
static readonly string SlackWebhookPath = Environment.GetEnvironmentVariable("SlackWebhookPath", EnvironmentVariableTarget.Process);
static readonly HttpClient HttpClient = new HttpClient
{
    BaseAddress = new Uri("https://hooks.slack.com"),
    Timeout = TimeSpan.FromSeconds(10)
};

public static async Task<string> Run(HttpRequestMessage req, TraceWriter log)
{
    IEnumerable<string> headers;
    if (!req.Headers.TryGetValues("X-Line-Signature", out headers))
    {
        return null;
    }

    var channelSignature = headers.FirstOrDefault();
    if (channelSignature == null)
    {
        return null;
    }

    if (string.IsNullOrEmpty(ChannelSecret))
    {
        log.Error("Please set ChannelSecret in App Settings");
        return null;
    }

    var secret = Encoding.UTF8.GetBytes(ChannelSecret);
    var content = await req.Content.ReadAsStringAsync();
    var body = await req.Content.ReadAsByteArrayAsync();

    using (var hmacsha256 = new HMACSHA256(secret))
    {
        var signature = Convert.ToBase64String(hmacsha256.ComputeHash(body));
        if (channelSignature != signature)
        {
            return null;
        }
    }

    if (string.IsNullOrEmpty(SlackWebhookPath))
    {
        log.Error("Please set SlackWebhookPath in App Settings");
        return null;
    }

    var slackMessage = new SlackMessage
    {
        Text = content
    };
    var serialized = JsonSerializer.ToJsonString(slackMessage);
    using (var json = new StringContent(serialized))
    {
        await HttpClient.PostAsync(SlackWebhookPath, json);
    }

    return content;
}

public class SlackMessage
{
    [DataMember(Name = "text")]
    public string Text { get; set; }
}