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

public static async Task<string> Run(HttpRequestMessage req, TraceWriter log)
{
    string content;
    if (!Line.IsValidRequest(req, out content))
    {
        return null;
    }

    await Slack.Post(content);

    return content;
}

internal static class Line
{
    internal static readonly string ChannelSecret = Environment.GetEnvironmentVariable("ChannelSecret", EnvironmentVariableTarget.Process);

    internal static bool IsValidRequest(HttpRequestMessage req, out string content)
    {
        content = null;
        IEnumerable<string> headers;
        if (!req.Headers.TryGetValues("X-Line-Signature", out headers))
        {
            return false;
        }

        var channelSignature = headers.FirstOrDefault();
        if (channelSignature == null)
        {
            return false;
        }

        if (string.IsNullOrEmpty(LineSettings.ChannelSecret))
        {
            log.Error("Please set ChannelSecret in App Settings");
            return false;
        }

        var secret = Encoding.UTF8.GetBytes(LineSettings.ChannelSecret);
        content = await req.Content.ReadAsStringAsync();
        var body = Encoding.UTF8.GetBytes(content);

        using (var hmacsha256 = new HMACSHA256(secret))
        {
            var signature = Convert.ToBase64String(hmacsha256.ComputeHash(body));
            if (channelSignature != signature)
            {
                return false;
            }
        }

        return true;
    }
}

internal static class Slack
{
    static readonly string SlackWebhookUrl = Environment.GetEnvironmentVariable("SlackWebhookPath", EnvironmentVariableTarget.Process);
    static readonly HttpClient HttpClient = new HttpClient
    {
        BaseAddress = "https://hooks.slack.com",
        Timeout = TimeSpan.FromSeconds(10)
    };

    internal static Task Post(string json)
    {
        if (string.IsNullOrEmpty(SlackWebhookUrl))
        {
            log.Error("Please set SlackWebhookUrl in App Settings");
            return Task.CompletedTask;
        }

        var slackMessage = new SlackMessage
        {
            Text = json
        };
        var serialized = JsonSerializer.ToJsonString(slackMessage);
        using (var content = new StringContent(serialized))
        {
            return HttpClient.PostAsync(SlackWebhookUrl, content);
        }
    }

    internal class SlackMessage
    {
        [DataMember(Name = "text")]
        public string Text { get; set; }
    }
}