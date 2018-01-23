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
using LineMessaging;
using Newtonsoft.Json;

static readonly string ChannelSecret = Environment.GetEnvironmentVariable("ChannelSecret", EnvironmentVariableTarget.Process);
static readonly string SlackWebhookPath = Environment.GetEnvironmentVariable("SlackWebhookPath", EnvironmentVariableTarget.Process);
static readonly HttpClient HttpClient = new HttpClient
{
    BaseAddress = new Uri("https://hooks.slack.com"),
    Timeout = TimeSpan.FromSeconds(10)
};

public static async Task<string> Run(HttpRequestMessage req, TraceWriter log)
{
    if (string.IsNullOrEmpty(ChannelSecret))
    {
        log.Error("Please set ChannelSecret in App Settings");
        return null;
    }

    var webhookRequest = new LineWebhookRequest(ChannelSecret, req);
    var valid = await webhookRequest.IsValid();
    var content = await webhookRequest.GetContentJson();
    if (!valid)
    {
        log.Error("request is invalid.");
        return null;
    }

    log.Info("content: " + content);

    if (string.IsNullOrEmpty(SlackWebhookPath))
    {
        log.Error("Please set SlackWebhookPath in App Settings");
        return null;
    }

    var slackMessage = new SlackMessage
    {
        Text = $"```{content}```"
    };
    var serialized = JsonConvert.SerializeObject(slackMessage);
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